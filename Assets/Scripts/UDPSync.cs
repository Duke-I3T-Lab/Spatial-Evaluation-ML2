using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

public class UDPSync : MonoBehaviour
{
    public string serverIP = "192.168.1.1";
    public int serverPort = 12345;
    public int receivePort = 54321;

    private UdpClient sendClient;
    private UdpClient receiveClient;
    private Thread receiveThread;
    private bool isListening;
    private List<long> differencesList = new List<long>();
    private long timestampOffset;

    void Start()
    {
        string localIP = GetLocalIPAddress();
        if (string.IsNullOrEmpty(localIP))
        {
            Debug.LogError("Could not find local IPv4 address");
            return;
        }

        // Send initial IP address
        SendIPAddress(localIP);

        // Start listening thread
        isListening = true;
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return string.Empty;
    }

    void SendIPAddress(string localIP)
    {
        try
        {
            sendClient = new UdpClient();
            IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);
            byte[] sendBytes = Encoding.ASCII.GetBytes(localIP);
            sendClient.Send(sendBytes, sendBytes.Length, serverEndPoint);
            sendClient.Close();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error sending IP: {e}");
        }
    }

    void ReceiveData()
    {
        receiveClient = new UdpClient(receivePort);

        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            while (isListening)
            {
                byte[] data = receiveClient.Receive(ref remoteEP);

                if (data.Length == 8) // Timestamp message
                {
                    // Handle endianness
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(data);
                    }

                    long serverTimestamp = BitConverter.ToInt64(data, 0);
                    long localTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long difference = localTimestamp - serverTimestamp;

                    lock (differencesList)
                    {
                        differencesList.Add(difference);
                    }
                }
                else // Text message
                {
                    string message = Encoding.ASCII.GetString(data);
                    if (message == "Sync Over")
                    {
                        isListening = false;
                        ComputeAverageOffset();
                    }
                }
            }
        }
        catch (SocketException ex) when (ex.ErrorCode == 10004)
        {
            // Expected exception when closing
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Receive error: {e}");
        }
        finally
        {
            receiveClient.Close();
        }
    }

    void ComputeAverageOffset()
    {
        lock (differencesList)
        {
            if (differencesList.Count > 0)
            {
                SharedVariables.Instance.timestampOffset = (long)differencesList.Average();
                SharedVariables.Instance.timestampOffsetComputed = true;
                Debug.Log($"Average offset calculated: {timestampOffset} ticks");
            }
            else
            {
                Debug.LogWarning("No timestamps received for offset calculation");
                SharedVariables.Instance.timestampOffset = 0;
            }
        }
    }

    void OnDestroy()
    {
        isListening = false;

        if (receiveClient != null)
            receiveClient.Close();

        if (receiveThread != null && receiveThread.IsAlive)
            receiveThread.Join();

        if (sendClient != null)
            sendClient.Close();
    }
}