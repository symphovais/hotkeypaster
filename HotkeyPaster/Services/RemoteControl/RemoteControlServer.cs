using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TalkKeys.Logging;
using TalkKeys.Services.Controller;

namespace TalkKeys.Services.RemoteControl
{
    /// <summary>
    /// HTTP server for remote control of TalkKeys
    /// </summary>
    public class RemoteControlServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly ILogger _logger;
        private readonly ITalkKeysController _controller;
        private readonly int _port;
        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private bool _isRunning;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public bool IsRunning => _isRunning;
        public int Port => _port;

        public RemoteControlServer(ITalkKeysController controller, int port, ILogger logger)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _port = port;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _listener = new HttpListener();
        }

        /// <summary>
        /// Starts the HTTP server
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                _logger.Log($"[RemoteControl] Server already running on port {_port}");
                return;
            }

            try
            {
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                _isRunning = true;

                _cts = new CancellationTokenSource();
                _listenerTask = ListenAsync(_cts.Token);

                _logger.Log($"[RemoteControl] Server started on http://localhost:{_port}/");
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 183 || ex.ErrorCode == 32)
            {
                // Port already in use
                _logger.Log($"[RemoteControl] Failed to start server - port {_port} is already in use: {ex.Message}");
                _isRunning = false;
            }
            catch (Exception ex)
            {
                _logger.Log($"[RemoteControl] Failed to start server: {ex.Message}");
                _isRunning = false;
            }
        }

        /// <summary>
        /// Stops the HTTP server
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _logger.Log("[RemoteControl] Stopping server...");

            try
            {
                _cts?.Cancel();
                _listener.Stop();
                _isRunning = false;

                _listenerTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                _logger.Log($"[RemoteControl] Error stopping server: {ex.Message}");
            }

            _logger.Log("[RemoteControl] Server stopped");
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            _logger.Log("[RemoteControl] Listener loop started");

            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected when stopping
                    break;
                }
                catch (ObjectDisposedException)
                {
                    // Listener was disposed
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Log($"[RemoteControl] Error accepting request: {ex.Message}");
                }
            }

            _logger.Log("[RemoteControl] Listener loop ended");
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var path = request.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";
                var method = request.HttpMethod.ToUpperInvariant();

                _logger.Log($"[RemoteControl] {method} {path}");

                // Add CORS headers for local development
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                // Handle preflight requests
                if (method == "OPTIONS")
                {
                    response.StatusCode = 204;
                    response.Close();
                    return;
                }

                object? result = null;
                int statusCode = 200;

                switch (path)
                {
                    case "/":
                        if (method == "GET")
                        {
                            result = _controller.GetCapabilities();
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use GET." };
                        }
                        break;

                    case "/status":
                        if (method == "GET")
                        {
                            result = _controller.GetStatus();
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use GET." };
                        }
                        break;

                    case "/starttranscription":
                        if (method == "POST")
                        {
                            result = await _controller.StartTranscriptionAsync();
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use POST." };
                        }
                        break;

                    case "/stoptranscription":
                        if (method == "POST")
                        {
                            result = await _controller.StopTranscriptionAsync();
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use POST." };
                        }
                        break;

                    case "/canceltranscription":
                        if (method == "POST")
                        {
                            result = await _controller.CancelTranscriptionAsync();
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use POST." };
                        }
                        break;

                    case "/explain":
                        if (method == "POST")
                        {
                            result = await _controller.ExplainSelectedTextAsync();
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use POST." };
                        }
                        break;

                    case "/microphones":
                        if (method == "GET")
                        {
                            var mics = _controller.GetMicrophones();
                            result = new { success = true, microphones = mics };
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use GET." };
                        }
                        break;

                    case "/microphone":
                        if (method == "POST")
                        {
                            var body = await ReadRequestBodyAsync(request);
                            var micRequest = TryDeserialize<MicrophoneRequest>(body);

                            if (micRequest != null)
                            {
                                result = _controller.SetMicrophone(micRequest.Index);
                            }
                            else
                            {
                                statusCode = 400;
                                result = new { success = false, message = "Invalid request body. Expected: { \"index\": 0 }" };
                            }
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use POST." };
                        }
                        break;

                    case "/shortcuts":
                        if (method == "GET")
                        {
                            var shortcuts = _controller.GetShortcuts();
                            result = new { success = true, shortcuts };
                        }
                        else if (method == "POST")
                        {
                            var body = await ReadRequestBodyAsync(request);
                            var shortcutsRequest = TryDeserialize<ShortcutsRequest>(body);

                            if (shortcutsRequest?.Shortcuts != null)
                            {
                                result = _controller.SetShortcuts(shortcutsRequest.Shortcuts);
                            }
                            else
                            {
                                statusCode = 400;
                                result = new { success = false, message = "Invalid request body. Expected: { \"shortcuts\": { \"transcription\": \"Ctrl+Shift+Space\" } }" };
                            }
                        }
                        else
                        {
                            statusCode = 405;
                            result = new { success = false, message = "Method not allowed. Use GET or POST." };
                        }
                        break;

                    default:
                        statusCode = 404;
                        result = new { success = false, message = $"Endpoint not found: {path}" };
                        break;
                }

                await SendJsonResponseAsync(response, result, statusCode);
            }
            catch (Exception ex)
            {
                _logger.Log($"[RemoteControl] Error processing request: {ex.Message}");

                try
                {
                    await SendJsonResponseAsync(response, new { success = false, message = "Internal server error" }, 500);
                }
                catch
                {
                    // Response may already be closed
                }
            }
        }

        private async Task<string> ReadRequestBodyAsync(HttpListenerRequest request)
        {
            if (request.ContentLength64 <= 0)
            {
                return string.Empty;
            }

            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            return await reader.ReadToEndAsync();
        }

        private T? TryDeserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private async Task SendJsonResponseAsync(HttpListenerResponse response, object? data, int statusCode = 200)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Stop();
            _cts?.Dispose();
            _listener.Close();
        }
    }

    // Request models
    internal class MicrophoneRequest
    {
        public int Index { get; set; }
    }

    internal class ShortcutsRequest
    {
        public System.Collections.Generic.Dictionary<string, string>? Shortcuts { get; set; }
    }
}
