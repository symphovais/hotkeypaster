using System;
using System.IO;
using System.Linq;
using HidSharp;

namespace JabraButtonTest
{
    class Program
    {
        // Jabra Engage 50 II HID identifiers
        private const int VendorId = 0x0B0E;  // Jabra
        private const int ProductId = 0x4054; // Engage 50 II (16468)

        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "jabra_button_log.txt");

        static void Log(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Console.WriteLine(line);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }

        static void Main(string[] args)
        {
            // Clear previous log
            if (File.Exists(LogPath))
                File.Delete(LogPath);

            Console.WriteLine("Jabra Engage 50 II Button Test");
            Console.WriteLine("==============================");
            Console.WriteLine($"Logging to: {LogPath}");
            Console.WriteLine("Press buttons on your headset to see raw HID data.");
            Console.WriteLine("Press Ctrl+C to exit.\n");

            Log("=== Jabra Button Test Started ===");

            // First, list ALL Jabra devices to help diagnose
            Log("Scanning for all Jabra devices (VendorID=0x0B0E)...");
            var allJabraDevices = DeviceList.Local.GetHidDevices().Where(d => d.VendorID == VendorId).ToList();
            Log($"Found {allJabraDevices.Count} Jabra HID device(s):");
            foreach (var d in allJabraDevices)
            {
                Log($"  - ProductID=0x{d.ProductID:X4} ({d.ProductID}), Path: {d.DevicePath}");
            }
            Log("");

            // Now look for our specific device
            var devices = DeviceList.Local.GetHidDevices(VendorId, ProductId).ToList();
            Log($"Looking for Engage 50 II (ProductID=0x{ProductId:X4})... Found {devices.Count} interface(s)");
            HidDevice? targetDevice = null;

            foreach (var device in devices)
            {
                var reportLen = device.GetMaxInputReportLength();
                var path = device.DevicePath;
                var isCol03 = path.Contains("col03", StringComparison.OrdinalIgnoreCase);

                Log($"  Interface: ReportLen={reportLen}, col03={isCol03}");
                Log($"    Path: {path}");

                // Look for col03 interface with 64-byte reports (vendor-specific buttons)
                if (isCol03 && reportLen == 64)
                {
                    targetDevice = device;
                    Log($"    >>> This is the button interface!");
                }
            }

            // Fallback: try any 64-byte interface
            if (targetDevice == null)
            {
                targetDevice = devices.FirstOrDefault(d => d.GetMaxInputReportLength() == 64);
                if (targetDevice != null)
                {
                    Log($"Using fallback: 64-byte interface");
                }
            }

            if (targetDevice == null)
            {
                Log("No Jabra Engage 50 II device found!");
                Log("Make sure the headset is connected.");
                return;
            }

            Log($"Listening on: {targetDevice.GetProductName()}");
            Log("Waiting for button events...");

            try
            {
                using var stream = targetDevice.Open();
                stream.ReadTimeout = System.Threading.Timeout.Infinite;

                var buffer = new byte[64];

                int eventCount = 0;
                while (true)
                {
                    var bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        eventCount++;
                        var hexData = BitConverter.ToString(buffer, 0, bytesRead);

                        // Log EVERY event - no filtering
                        Log($"EVENT #{eventCount}: {bytesRead} bytes");
                        Log($"  HEX: {hexData}");

                        // Show individual bytes for analysis
                        var byteDetails = "";
                        for (int i = 0; i < bytesRead; i++)
                        {
                            byteDetails += $"[{i}]=0x{buffer[i]:X2} ";
                        }
                        Log($"  RAW: {byteDetails}");
                        Log(""); // Empty line for readability
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }

            Log("=== Test Ended ===");
        }
    }
}
