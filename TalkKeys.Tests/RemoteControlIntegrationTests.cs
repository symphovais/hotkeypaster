using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace TalkKeys.Tests
{
    /// <summary>
    /// End-to-end integration tests for the Remote Control API.
    /// These tests hit the real running TalkKeys app - no mocks!
    ///
    /// Prerequisites: TalkKeys must be running on localhost:38450
    /// Run: dotnet test --filter "Category=Integration"
    /// </summary>
    [Trait("Category", "Integration")]
    public class RemoteControlIntegrationTests : IAsyncLifetime
    {
        private readonly HttpClient _client;
        private const string BaseUrl = "http://localhost:38450";
        private bool _appRunning;

        public RemoteControlIntegrationTests()
        {
            _client = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };
        }

        public async Task InitializeAsync()
        {
            // Check if TalkKeys is running
            try
            {
                var response = await _client.GetAsync("/status");
                _appRunning = response.IsSuccessStatusCode;
            }
            catch
            {
                _appRunning = false;
            }
        }

        public Task DisposeAsync()
        {
            _client.Dispose();
            return Task.CompletedTask;
        }

        private void SkipIfNotRunning()
        {
            Skip.If(!_appRunning, $"TalkKeys is not running on {BaseUrl}. Start the app first.");
        }

        #region GET / (Capabilities)

        [SkippableFact]
        public async Task Capabilities_ReturnsFullApiDescription()
        {
            SkipIfNotRunning();

            var response = await _client.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Verify structure
            Assert.Equal("TalkKeys", root.GetProperty("name").GetString());
            Assert.True(root.TryGetProperty("version", out _));
            Assert.True(root.TryGetProperty("capabilities", out var caps));
            Assert.True(root.TryGetProperty("endpoints", out var endpoints));

            // Should have transcription capability
            var capsArray = caps.EnumerateArray();
            Assert.Contains(capsArray, c => c.GetProperty("id").GetString() == "transcription");

            // Should list all endpoints
            var endpointsArray = endpoints.EnumerateArray();
            Assert.True(endpoints.GetArrayLength() >= 10);
        }

        #endregion

        #region GET /status

        [SkippableFact]
        public async Task Status_ReturnsCurrentState()
        {
            SkipIfNotRunning();

            var response = await _client.GetAsync("/status");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.True(root.TryGetProperty("status", out var status));
            Assert.Contains(status.GetString(), new[] { "idle", "recording", "processing" });
            Assert.True(root.TryGetProperty("recording", out _));
            Assert.True(root.TryGetProperty("processing", out _));
            Assert.True(root.TryGetProperty("version", out _));
            Assert.True(root.TryGetProperty("authenticated", out _));
        }

        #endregion

        #region GET /microphones

        [SkippableFact]
        public async Task Microphones_ReturnsDeviceList()
        {
            SkipIfNotRunning();

            var response = await _client.GetAsync("/microphones");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.True(root.TryGetProperty("microphones", out var mics));
            Assert.Equal(JsonValueKind.Array, mics.ValueKind);

            // Should have at least one microphone
            if (mics.GetArrayLength() > 0)
            {
                var firstMic = mics[0];
                Assert.True(firstMic.TryGetProperty("index", out _));
                Assert.True(firstMic.TryGetProperty("name", out _));
                Assert.True(firstMic.TryGetProperty("current", out _));
            }
        }

        #endregion

        #region GET /shortcuts

        [SkippableFact]
        public async Task Shortcuts_ReturnsConfiguredHotkeys()
        {
            SkipIfNotRunning();

            var response = await _client.GetAsync("/shortcuts");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.GetProperty("success").GetBoolean());
            Assert.True(root.TryGetProperty("shortcuts", out var shortcuts));
            Assert.True(shortcuts.TryGetProperty("transcription", out _));
        }

        #endregion

        #region Full Recording Flow (E2E)

        [SkippableFact]
        public async Task RecordingFlow_StartCheckStop()
        {
            SkipIfNotRunning();

            // 1. Check initial status
            var statusResponse = await _client.GetAsync("/status");
            var statusJson = await statusResponse.Content.ReadAsStringAsync();
            using var statusDoc = JsonDocument.Parse(statusJson);

            // If already recording, cancel first
            if (statusDoc.RootElement.GetProperty("recording").GetBoolean())
            {
                await _client.PostAsync("/canceltranscription", null);
                await Task.Delay(500);
            }

            // 2. Start recording
            var startResponse = await _client.PostAsync("/starttranscription", null);
            var startJson = await startResponse.Content.ReadAsStringAsync();
            using var startDoc = JsonDocument.Parse(startJson);

            // Check if authenticated (might fail if no API key)
            if (!startDoc.RootElement.GetProperty("success").GetBoolean())
            {
                var message = startDoc.RootElement.GetProperty("message").GetString();
                Skip.If(message?.Contains("authenticated") == true, "App not authenticated - skipping recording test");
            }

            Assert.True(startDoc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("recording", startDoc.RootElement.GetProperty("status").GetString());

            // 3. Verify status shows recording
            await Task.Delay(200);
            var recordingStatus = await _client.GetAsync("/status");
            var recordingJson = await recordingStatus.Content.ReadAsStringAsync();
            using var recordingDoc = JsonDocument.Parse(recordingJson);

            Assert.True(recordingDoc.RootElement.GetProperty("recording").GetBoolean());

            // 4. Cancel recording (don't actually transcribe in tests)
            var cancelResponse = await _client.PostAsync("/canceltranscription", null);
            Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

            // 5. Verify back to idle
            await Task.Delay(200);
            var finalStatus = await _client.GetAsync("/status");
            var finalJson = await finalStatus.Content.ReadAsStringAsync();
            using var finalDoc = JsonDocument.Parse(finalJson);

            Assert.False(finalDoc.RootElement.GetProperty("recording").GetBoolean());
        }

        #endregion

        #region Error Cases

        [SkippableFact]
        public async Task InvalidEndpoint_Returns404()
        {
            SkipIfNotRunning();

            var response = await _client.GetAsync("/nonexistent");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        }

        [SkippableFact]
        public async Task WrongMethod_Returns405()
        {
            SkipIfNotRunning();

            // GET instead of POST
            var response = await _client.GetAsync("/starttranscription");

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [SkippableFact]
        public async Task InvalidMicrophoneJson_Returns400()
        {
            SkipIfNotRunning();

            var content = new StringContent("invalid", Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("/microphone", content);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region CORS

        [SkippableFact]
        public async Task CorsHeaders_ArePresent()
        {
            SkipIfNotRunning();

            var response = await _client.GetAsync("/status");

            Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        }

        [SkippableFact]
        public async Task Options_ReturnsNoContent()
        {
            SkipIfNotRunning();

            var request = new HttpRequestMessage(HttpMethod.Options, "/status");
            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        #endregion

        #region Concurrent Requests

        [SkippableFact]
        public async Task ConcurrentStatusRequests_AllSucceed()
        {
            SkipIfNotRunning();

            var tasks = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(_client.GetAsync("/status"));
            }

            var responses = await Task.WhenAll(tasks);

            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        #endregion
    }
}
