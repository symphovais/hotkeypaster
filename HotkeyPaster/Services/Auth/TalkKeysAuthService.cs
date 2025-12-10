using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TalkKeys.Logging;
using TalkKeys.Services.Settings;

namespace TalkKeys.Services.Auth
{
    /// <summary>
    /// Response from the TalkKeys API containing auth tokens
    /// </summary>
    public class AuthTokenResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? Email { get; set; }
        public string? Name { get; set; }
    }

    /// <summary>
    /// Service for handling TalkKeys account authentication via Google OAuth
    /// </summary>
    public class TalkKeysAuthService : IDisposable
    {
        private const string ApiBaseUrl = "https://talkkeys.symphonytek.dk";
        private const string AuthPath = "/auth/google";

        private readonly ILogger? _logger;
        private readonly SettingsService _settingsService;
        private HttpListener? _callbackListener;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<AuthTokenResponse>? AuthenticationCompleted;
        public event EventHandler<string>? AuthenticationFailed;

        public TalkKeysAuthService(SettingsService settingsService, ILogger? logger = null)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        /// <summary>
        /// Check if user is currently logged in with a TalkKeys account
        /// </summary>
        public bool IsLoggedIn
        {
            get
            {
                var settings = _settingsService.LoadSettings();
                return settings.AuthMode == AuthMode.TalkKeysAccount &&
                       !string.IsNullOrEmpty(settings.TalkKeysAccessToken);
            }
        }

        /// <summary>
        /// Check if user has configured any authentication method
        /// </summary>
        public bool IsConfigured
        {
            get
            {
                var settings = _settingsService.LoadSettings();
                return (settings.AuthMode == AuthMode.TalkKeysAccount && !string.IsNullOrEmpty(settings.TalkKeysAccessToken)) ||
                       (settings.AuthMode == AuthMode.OwnApiKey && !string.IsNullOrEmpty(settings.GroqApiKey));
            }
        }

        /// <summary>
        /// Start the OAuth login flow - opens browser and waits for callback
        /// </summary>
        public async Task<AuthTokenResponse?> LoginAsync(CancellationToken cancellationToken = default)
        {
            _logger?.Log("[Auth] Starting OAuth login flow");

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // Start local HTTP listener for callback
                var callbackPort = GetAvailablePort();
                var callbackUrl = $"http://localhost:{callbackPort}/callback/";

                _callbackListener = new HttpListener();
                _callbackListener.Prefixes.Add(callbackUrl);
                _callbackListener.Start();

                _logger?.Log($"[Auth] Listening for callback on {callbackUrl}");

                // Open browser to OAuth URL with our callback URL
                var encodedCallback = Uri.EscapeDataString(callbackUrl);
                var authUrl = $"{ApiBaseUrl}{AuthPath}?callback_url={encodedCallback}";
                _logger?.Log($"[Auth] Opening browser to {authUrl}");

                Process.Start(new ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                });

                // Wait for callback with token
                var result = await WaitForCallbackAsync(_cancellationTokenSource.Token);

                if (result != null)
                {
                    // Save tokens to settings
                    var settings = _settingsService.LoadSettings();
                    settings.AuthMode = AuthMode.TalkKeysAccount;
                    settings.TalkKeysAccessToken = result.AccessToken;
                    settings.TalkKeysRefreshToken = result.RefreshToken;
                    settings.TalkKeysUserEmail = result.Email;
                    settings.TalkKeysUserName = result.Name;
                    _settingsService.SaveSettings(settings);

                    _logger?.Log("[Auth] Login successful");
                    AuthenticationCompleted?.Invoke(this, result);
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                _logger?.Log("[Auth] Login cancelled");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Auth] Login failed: {ex.Message}");
                AuthenticationFailed?.Invoke(this, ex.Message);
                return null;
            }
            finally
            {
                StopListener();
            }
        }

        /// <summary>
        /// Wait for the OAuth callback with the access token
        /// </summary>
        private async Task<AuthTokenResponse?> WaitForCallbackAsync(CancellationToken cancellationToken)
        {
            if (_callbackListener == null) return null;

            // Set a timeout of 5 minutes for the user to complete login
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            try
            {
                while (!linkedCts.Token.IsCancellationRequested)
                {
                    var contextTask = _callbackListener.GetContextAsync();

                    // Wait for either the request or cancellation
                    var completedTask = await Task.WhenAny(
                        contextTask,
                        Task.Delay(Timeout.Infinite, linkedCts.Token)
                    );

                    if (completedTask == contextTask)
                    {
                        var context = await contextTask;
                        var request = context.Request;
                        var response = context.Response;

                        _logger?.Log($"[Auth] Received callback: {request.Url}");

                        // Parse the access token from query parameters
                        var query = request.QueryString;
                        var accessToken = query["access_token"];
                        var refreshToken = query["refresh_token"];
                        var expiresIn = query["expires_in"];

                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            // Get user info from token (decode JWT payload)
                            var (email, name) = DecodeJwtUserInfo(accessToken);

                            // Send success response to browser
                            var successHtml = GetSuccessHtml();
                            var buffer = Encoding.UTF8.GetBytes(successHtml);
                            response.ContentType = "text/html";
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, linkedCts.Token);
                            response.Close();

                            return new AuthTokenResponse
                            {
                                AccessToken = accessToken,
                                RefreshToken = refreshToken,
                                ExpiresIn = int.TryParse(expiresIn, out var exp) ? exp : 3600,
                                Email = email,
                                Name = name
                            };
                        }
                        else
                        {
                            // Send error response
                            var error = query["error"] ?? "Unknown error";
                            var errorHtml = GetErrorHtml(error);
                            var buffer = Encoding.UTF8.GetBytes(errorHtml);
                            response.ContentType = "text/html";
                            response.ContentLength64 = buffer.Length;
                            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, linkedCts.Token);
                            response.Close();

                            throw new Exception($"Authentication failed: {error}");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            return null;
        }

        /// <summary>
        /// Decode user info from JWT token (without validation - server already validated)
        /// </summary>
        private (string? email, string? name) DecodeJwtUserInfo(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3) return (null, null);

                var payload = parts[1];
                // Add padding if needed
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;
                var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

                return (email, name);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// Logout - clear stored tokens
        /// </summary>
        public void Logout()
        {
            var settings = _settingsService.LoadSettings();
            settings.TalkKeysAccessToken = null;
            settings.TalkKeysRefreshToken = null;
            settings.TalkKeysUserEmail = null;
            settings.TalkKeysUserName = null;
            _settingsService.SaveSettings(settings);

            _logger?.Log("[Auth] Logged out");
        }

        /// <summary>
        /// Get current access token (for API calls)
        /// </summary>
        public string? GetAccessToken()
        {
            var settings = _settingsService.LoadSettings();
            return settings.TalkKeysAccessToken;
        }

        /// <summary>
        /// Cancel any pending login attempt
        /// </summary>
        public void CancelLogin()
        {
            _cancellationTokenSource?.Cancel();
            StopListener();
        }

        private void StopListener()
        {
            try
            {
                _callbackListener?.Stop();
                _callbackListener?.Close();
            }
            catch { }
            _callbackListener = null;
        }

        private int GetAvailablePort()
        {
            // Find an available port
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private string GetSuccessHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <title>TalkKeys - Login Successful</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }
        .container {
            text-align: center;
            padding: 40px;
            background: rgba(255,255,255,0.1);
            border-radius: 16px;
            backdrop-filter: blur(10px);
        }
        h1 { color: #10b981; margin-bottom: 16px; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Login Successful!</h1>
        <p>You can close this window and return to TalkKeys.</p>
    </div>
</body>
</html>";
        }

        private string GetErrorHtml(string error)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <title>TalkKeys - Login Failed</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }}
        .container {{
            text-align: center;
            padding: 40px;
            background: rgba(255,255,255,0.1);
            border-radius: 16px;
            backdrop-filter: blur(10px);
        }}
        h1 {{ color: #ef4444; margin-bottom: 16px; }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>Login Failed</h1>
        <p>{error}</p>
        <p>Please close this window and try again.</p>
    </div>
</body>
</html>";
        }

        public void Dispose()
        {
            StopListener();
            _cancellationTokenSource?.Dispose();
        }
    }
}
