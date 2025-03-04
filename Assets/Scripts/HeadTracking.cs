using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

public class HeadTracking : MonoBehaviour
{
    private InputAction headPositionAction;
    private InputAction headRotationAction;

    private string csvFilePath;
    private StreamWriter writer;
    private bool bufferWritten = false;
    private bool isSaving = false;

    // Thread-safe collections
    private ConcurrentQueue<string> dataQueue = new ConcurrentQueue<string>();
    private ConcurrentQueue<(long, string)> bufferedQueue = new ConcurrentQueue<(long, string)>();

    // Batch processing parameters
    private const int WRITE_BATCH_SIZE = 50;
    private const int WRITE_INTERVAL_MS = 100;

    async void Start()
    {
        InitializeInputActions();
        InitializeCSVFile();
        await StartFileWriter();
    }

    void InitializeInputActions()
    {
        headPositionAction = new InputAction(binding: "<OpenXRHmd>/devicePosition");
        headRotationAction = new InputAction(binding: "<OpenXRHmd>/deviceRotation");
        headPositionAction.Enable();
        headRotationAction.Enable();
    }

    void InitializeCSVFile()
    {
        csvFilePath = Path.Combine(Application.persistentDataPath, "HeadTrackingData.csv");
        writer = new StreamWriter(csvFilePath);
        writer.WriteLine("timestamp,pos_x,pos_y,pos_z,qua_1,qua_2,qua_3,qua_4");
    }

    async Task StartFileWriter()
    {
        isSaving = true;
        while (isSaving)
        {
            await WriteBatchToFile();
            await Task.Delay(WRITE_INTERVAL_MS);
        }
    }

    void Update()
    {
        CaptureFrameData();
    }

    void CaptureFrameData()
    {
        var headPosition = headPositionAction.ReadValue<Vector3>();
        var headRotation = headRotationAction.ReadValue<Quaternion>();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (!SharedVariables.Instance.timestampOffsetComputed)
        {
            BufferData(currentTime, headPosition, headRotation);
        }
        else
        {
            ProcessCurrentData(currentTime, headPosition, headRotation);
        }
    }

    void BufferData(long currentTime, Vector3 position, Quaternion rotation)
    {
        var data = $"{position.x},{position.y},{position.z}," +
                   $"{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
        bufferedQueue.Enqueue((currentTime, data));
    }

    void ProcessCurrentData(long currentTime, Vector3 position, Quaternion rotation)
    {
        if (!bufferWritten)
        {
            ProcessBufferedData();
            bufferWritten = true;
        }

        var adjustedTime = currentTime + SharedVariables.Instance.timestampOffset;
        var data = $"{adjustedTime},{position.x},{position.y},{position.z}," +
                   $"{rotation.x},{rotation.y},{rotation.z},{rotation.w}";
        dataQueue.Enqueue(data);
    }

    void ProcessBufferedData()
    {
        while (bufferedQueue.TryDequeue(out var item))
        {
            var adjustedTime = item.Item1 + SharedVariables.Instance.timestampOffset;
            dataQueue.Enqueue($"{adjustedTime},{item.Item2}");
        }
    }

    async Task WriteBatchToFile()
    {
        var batch = new List<string>();
        while (batch.Count < WRITE_BATCH_SIZE && dataQueue.TryDequeue(out var data))
        {
            batch.Add(data);
        }

        if (batch.Count > 0)
        {
            await writer.WriteLineAsync(string.Join(Environment.NewLine, batch));
            await writer.FlushAsync();
        }
    }

    void OnDestroy()
    {
        isSaving = false;
        headPositionAction.Disable();
        headRotationAction.Disable();

        // Write remaining data
        WriteBatchToFile().Wait();
        writer?.Close();
        writer?.Dispose();
    }
}