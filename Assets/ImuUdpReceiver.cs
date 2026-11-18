using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

// Listens for pose packets over UDP and feeds them to LaryngoscopePoseDriver.
// Works with imu_simulator.py now, and with a real sensor board later, as long as it sends:
//     x,y,z,pitch,yaw,roll      (metres, degrees; one packet per sample)
//
// Put this on any GameObject (e.g. an empty called "ImuReceiver") and drag in the driver.
// On Quest: Project Settings > Player > Android > Other Settings > Internet Access = Require.
public class ImuUdpReceiver : MonoBehaviour
{
    public LaryngoscopePoseDriver driver;
    public int port = 5005;
    [Tooltip("Seconds without packets before showing 'no data'.")]
    public float timeoutSeconds = 1f;
    [Tooltip("Optional: a world-space TextMeshPro to show connection status in the headset.")]
    public TMP_Text statusText;

    [Header("Status (read only)")]
    public bool receiving;
    public int packetsPerSecond;
    public string lastPacket;

    UdpClient client;
    Thread thread;
    volatile bool running;

    readonly object lockObj = new object();
    Vector3 latestPos, latestEuler;
    string latestRaw;
    bool hasNew;
    int packetCount;

    float lastPacketTime = -999f;
    float rateTimer;

    void Start()
    {
        if (!driver) { Debug.LogError("ImuUdpReceiver: drag the laryngoscope (with LaryngoscopePoseDriver) into 'Driver'."); return; }

        try
        {
            client = new UdpClient(port);
        }
        catch (Exception e)
        {
            Debug.LogError($"ImuUdpReceiver: could not open UDP port {port}: {e.Message}");
            return;
        }

        running = true;
        thread = new Thread(Listen) { IsBackground = true };
        thread.Start();
        Debug.Log($"ImuUdpReceiver: listening on UDP port {port}");
    }

    void Listen()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        var values = new float[6];

        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref remote);
                string s = Encoding.ASCII.GetString(data).Trim();
                string[] parts = s.Split(',');
                if (parts.Length < 6) continue;

                bool ok = true;
                for (int i = 0; i < 6; i++)
                    ok &= float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);
                if (!ok) continue;

                lock (lockObj)
                {
                    latestPos = new Vector3(values[0], values[1], values[2]);
                    latestEuler = new Vector3(values[3], values[4], values[5]);
                    latestRaw = s;
                    hasNew = true;
                    packetCount++;
                }
            }
            catch (ObjectDisposedException) { break; }   // socket closed on shutdown
            catch (SocketException) { if (!running) break; }
        }
    }

    void Update()
    {
        bool got;
        Vector3 pos, euler;
        string raw;
        lock (lockObj)
        {
            got = hasNew; hasNew = false;
            pos = latestPos; euler = latestEuler; raw = latestRaw;
        }

        if (got && driver)
        {
            driver.SetFromSensor(pos, euler);
            lastPacketTime = Time.realtimeSinceStartup;
            lastPacket = raw;
        }

        receiving = Time.realtimeSinceStartup - lastPacketTime < timeoutSeconds;

        rateTimer += Time.unscaledDeltaTime;
        if (rateTimer >= 1f)
        {
            lock (lockObj) { packetsPerSecond = packetCount; packetCount = 0; }
            rateTimer = 0f;
        }

        if (statusText)
            statusText.text = receiving
                ? $"IMU: {packetsPerSecond} Hz\n{lastPacket}"
                : $"IMU: no data (port {port})";
    }

    void OnDestroy() => Shutdown();
    void OnApplicationQuit() => Shutdown();

    void Shutdown()
    {
        running = false;
        client?.Close();
        client = null;
    }
}
