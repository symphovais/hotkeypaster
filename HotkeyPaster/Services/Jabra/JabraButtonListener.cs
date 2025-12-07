using HidSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TalkKeys.Services.Jabra
{
    /// <summary>
    /// Button event arguments
    /// </summary>
    public class JabraButtonEventArgs : EventArgs
    {
        public string ButtonName { get; }
        public byte ButtonId { get; }
        public bool IsPressed { get; }
        public byte[] RawData { get; }
        public DateTime Timestamp { get; }

        public JabraButtonEventArgs(string buttonName, byte buttonId, bool isPressed, byte[] rawData)
        {
            ButtonName = buttonName;
            ButtonId = buttonId;
            IsPressed = isPressed;
            RawData = rawData;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Listens for button presses on Jabra Engage 50 II headset
    /// </summary>
    public class JabraButtonListener : IDisposable
    {
        // Jabra Engage 50 II identifiers
        private const int JABRA_VENDOR_ID = 0x0B0E;        // 2830
        private const int ENGAGE_50_II_PRODUCT_ID = 0x4054; // 16468

        // Known button IDs
        public static class ButtonIds
        {
            public const byte ThreeDot = 0x12;
            public const byte HookIcon = 0x13;
            public const byte Mute = 0x08;
        }

        // Event markers in HID data (byte[4])
        private const byte EVENT_MARKER_PRESS = 0x09;
        private const byte EVENT_MARKER_RELEASE = 0x08;

        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private bool _disposed;

        /// <summary>
        /// Fired when a programmable button is pressed
        /// </summary>
        public event EventHandler<JabraButtonEventArgs>? ButtonPressed;

        /// <summary>
        /// Fired when a programmable button is released
        /// </summary>
        public event EventHandler<JabraButtonEventArgs>? ButtonReleased;

        /// <summary>
        /// Fired when any HID data is received (for debugging)
        /// </summary>
        public event EventHandler<byte[]>? RawDataReceived;

        /// <summary>
        /// Fired when an error occurs
        /// </summary>
        public event EventHandler<Exception>? Error;

        /// <summary>
        /// Whether the listener is currently running
        /// </summary>
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;

        /// <summary>
        /// Start listening for button presses
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _listenerTask = Task.Run(() => ListenLoop(_cts.Token));
        }

        /// <summary>
        /// Stop listening for button presses
        /// </summary>
        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _listenerTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException) { }
            _cts?.Dispose();
            _cts = null;
            _listenerTask = null;
        }

        /// <summary>
        /// Find all connected Jabra Engage 50 II devices
        /// </summary>
        public static HidDevice[] FindDevices()
        {
            return DeviceList.Local.GetHidDevices()
                .Where(d => d.VendorID == JABRA_VENDOR_ID && d.ProductID == ENGAGE_50_II_PRODUCT_ID)
                .ToArray();
        }

        /// <summary>
        /// Check if a Jabra Engage 50 II is connected with a usable button interface
        /// </summary>
        public static bool IsDeviceConnected()
        {
            return FindButtonInterface() != null;
        }

        private void ListenLoop(CancellationToken ct)
        {
            try
            {
                // Find the col03 interface (vendor-specific, 64-byte reports)
                var device = FindButtonInterface();
                if (device == null)
                {
                    Error?.Invoke(this, new Exception("Jabra Engage 50 II not found or col03 interface unavailable"));
                    return;
                }

                using var stream = device.Open();
                stream.ReadTimeout = 1000; // 1 second timeout for periodic cancellation checks

                var buffer = new byte[device.GetMaxInputReportLength()];

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            var data = buffer.Take(bytesRead).ToArray();
                            RawDataReceived?.Invoke(this, data);

                            // Button event patterns:
                            // Press: byte[4]=0x09, byte[8]=button ID
                            // Release: byte[4]=0x08, byte[8]=button ID
                            if (bytesRead >= 9)
                            {
                                var eventMarker = buffer[4];
                                var buttonId = buffer[8];

                                if (eventMarker == EVENT_MARKER_PRESS)
                                {
                                    var buttonName = GetButtonName(buttonId);
                                    ButtonPressed?.Invoke(this, new JabraButtonEventArgs(buttonName, buttonId, true, data));
                                }
                                else if (eventMarker == EVENT_MARKER_RELEASE)
                                {
                                    var buttonName = GetButtonName(buttonId);
                                    ButtonReleased?.Invoke(this, new JabraButtonEventArgs(buttonName, buttonId, false, data));
                                }
                            }
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Normal - no data available, continue polling
                    }
                    catch (System.IO.IOException ex) when (ex.Message.Contains("Operation failed"))
                    {
                        Error?.Invoke(this, new Exception("Device disconnected or stream error", ex));
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(this, ex);
            }
        }

        private static HidDevice? FindButtonInterface()
        {
            var devices = FindDevices();

            // Look for col03 interface with 64-byte reports
            // This is the vendor-specific interface that receives button events
            var col03Device = devices.FirstOrDefault(d =>
                d.DevicePath.Contains("col03", StringComparison.OrdinalIgnoreCase) &&
                d.GetMaxInputReportLength() == 64);

            if (col03Device != null)
                return col03Device;

            // Fallback: try any interface with 64-byte reports
            return devices.FirstOrDefault(d => d.GetMaxInputReportLength() == 64);
        }

        private static string GetButtonName(byte buttonId) => buttonId switch
        {
            ButtonIds.ThreeDot => "threeDot",
            ButtonIds.HookIcon => "hookIcon",
            ButtonIds.Mute => "mute",
            _ => $"unknown-0x{buttonId:X2}"
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }
    }
}
