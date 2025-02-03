using System;
using System.IO;

using UnityEngine;
using UnityEngine.InputSystem;

public class HeadTracking : MonoBehaviour
{
    private InputAction headPositionAction;
    private InputAction headRotationAction;



    private long timestamp = 0;

    private string csvFilePath;

    private StreamWriter writer;

    void Start()
    {
        // Initialize input actions for head tracking
        headPositionAction = new InputAction(binding: "<OpenXRHmd>/devicePosition");
        headRotationAction = new InputAction(binding: "<OpenXRHmd>/deviceRotation");
        headPositionAction.Enable();
        headRotationAction.Enable();



        // Set up CSV file
        csvFilePath = Path.Combine(Application.persistentDataPath, "HeadTrackingData.csv");
        // if (!File.Exists(csvFilePath))
        // {
        //     using (var writer = new StreamWriter(csvFilePath, false))
        //     {
        //         writer.WriteLine("timestamp,pos_x,pos_y,pos_z,qua_1,qua_2,qua_3,qua_4");
        //     }
        // }
        writer = new StreamWriter(csvFilePath);
        writer.WriteLine("timestamp,pos_x,pos_y,pos_z,qua_1,qua_2,qua_3,qua_4");

    }

    void Update()
    {
        // Continuously read head position and rotation in Update() if needed
        SaveDataToCSV();
    }



    private void SaveDataToCSV()
    {
        Vector3 headPosition = headPositionAction.ReadValue<Vector3>();
        Quaternion headRotation = headRotationAction.ReadValue<Quaternion>();
        long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();


        writer.WriteLine($"{currentTime},{headPosition.x},{headPosition.y},{headPosition.z}," +
                            $"{headRotation.x},{headRotation.y},{headRotation.z},{headRotation.w}");
    }


    void OnDestroy()
    {
        headPositionAction.Disable();
        headRotationAction.Disable();
        writer.Close();
    }
}
