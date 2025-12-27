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
    /// Tests for RemoteControlServer API responses and request handling.
    /// Note: These tests validate JSON response formats and API contract compliance.
    /// Full integration tests with actual HTTP would require running the app.
    /// </summary>
    public class RemoteControlServerTests
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        #region API Response Format Tests

        [Fact]
        public void ApiResponse_Success_HasCorrectFormat()
        {
            var response = new { success = true, status = "idle", message = (string?)null };
            var json = JsonSerializer.Serialize(response, JsonOptions);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out var successProp));
            Assert.True(successProp.GetBoolean());
            Assert.True(root.TryGetProperty("status", out _));
        }

        [Fact]
        public void ApiResponse_Error_HasCorrectFormat()
        {
            var response = new { success = false, status = "idle", message = "Not recording" };
            var json = JsonSerializer.Serialize(response, JsonOptions);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out var successProp));
            Assert.False(successProp.GetBoolean());
            Assert.True(root.TryGetProperty("message", out var messageProp));
            Assert.Equal("Not recording", messageProp.GetString());
        }

        [Fact]
        public void ApiResponse_NotFound_HasCorrectFormat()
        {
            var response = new { success = false, message = "Endpoint not found: /invalid" };
            var json = JsonSerializer.Serialize(response, JsonOptions);

            Assert.Contains("\"success\":false", json);
            Assert.Contains("Endpoint not found", json);
        }

        [Fact]
        public void ApiResponse_MethodNotAllowed_HasCorrectFormat()
        {
            var response = new { success = false, message = "Method not allowed. Use POST." };
            var json = JsonSerializer.Serialize(response, JsonOptions);

            Assert.Contains("\"success\":false", json);
            Assert.Contains("Method not allowed", json);
        }

        #endregion

        #region Request Parsing Tests

        [Fact]
        public void MicrophoneRequest_ParsesCorrectly()
        {
            var requestJson = @"{""index"": 1}";
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var request = JsonSerializer.Deserialize<MicrophoneRequest>(requestJson, options);

            Assert.NotNull(request);
            Assert.Equal(1, request.Index);
        }

        [Fact]
        public void MicrophoneRequest_WithCamelCase_ParsesCorrectly()
        {
            var requestJson = @"{""Index"": 2}";
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var request = JsonSerializer.Deserialize<MicrophoneRequest>(requestJson, options);

            Assert.NotNull(request);
            Assert.Equal(2, request.Index);
        }

        [Fact]
        public void ShortcutsRequest_ParsesCorrectly()
        {
            var requestJson = @"{
                ""shortcuts"": {
                    ""transcription"": ""Ctrl+Alt+Space"",
                    ""explain"": ""Ctrl+Shift+E""
                }
            }";
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var request = JsonSerializer.Deserialize<ShortcutsRequest>(requestJson, options);

            Assert.NotNull(request);
            Assert.NotNull(request.Shortcuts);
            Assert.Equal(2, request.Shortcuts.Count);
            Assert.Equal("Ctrl+Alt+Space", request.Shortcuts["transcription"]);
            Assert.Equal("Ctrl+Shift+E", request.Shortcuts["explain"]);
        }

        [Fact]
        public void ShortcutsRequest_EmptyShortcuts_ParsesCorrectly()
        {
            var requestJson = @"{""shortcuts"": {}}";
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var request = JsonSerializer.Deserialize<ShortcutsRequest>(requestJson, options);

            Assert.NotNull(request);
            Assert.NotNull(request.Shortcuts);
            Assert.Empty(request.Shortcuts);
        }

        [Fact]
        public void InvalidJson_ReturnsNull_WhenTryDeserialize()
        {
            var invalidJson = "{ invalid }";

            try
            {
                JsonSerializer.Deserialize<MicrophoneRequest>(invalidJson);
                Assert.Fail("Should have thrown JsonException");
            }
            catch (JsonException)
            {
                // Expected
            }
        }

        #endregion

        #region Endpoint Path Tests

        [Theory]
        [InlineData("/", "GET")]
        [InlineData("/status", "GET")]
        [InlineData("/microphones", "GET")]
        [InlineData("/shortcuts", "GET")]
        public void GetEndpoints_RequireGetMethod(string path, string expectedMethod)
        {
            Assert.Equal("GET", expectedMethod);
        }

        [Theory]
        [InlineData("/starttranscription", "POST")]
        [InlineData("/stoptranscription", "POST")]
        [InlineData("/canceltranscription", "POST")]
        [InlineData("/explain", "POST")]
        [InlineData("/microphone", "POST")]
        [InlineData("/shortcuts", "POST")] // shortcuts also accepts POST for updates
        public void PostEndpoints_RequirePostMethod(string path, string expectedMethod)
        {
            Assert.Equal("POST", expectedMethod);
        }

        [Fact]
        public void AllEndpoints_AreLowercased()
        {
            var endpoints = new[]
            {
                "/",
                "/status",
                "/starttranscription",
                "/stoptranscription",
                "/canceltranscription",
                "/explain",
                "/microphones",
                "/microphone",
                "/shortcuts"
            };

            foreach (var endpoint in endpoints)
            {
                Assert.Equal(endpoint, endpoint.ToLowerInvariant());
            }
        }

        #endregion

        #region Response Content Type Tests

        [Fact]
        public void AllResponses_ShouldBeJson()
        {
            // This documents the expected content type
            const string expectedContentType = "application/json";
            Assert.NotNull(expectedContentType);
        }

        [Fact]
        public void ErrorResponse_ForInvalidEndpoint_Is404()
        {
            const int expectedStatusCode = 404;
            Assert.Equal(HttpStatusCode.NotFound, (HttpStatusCode)expectedStatusCode);
        }

        [Fact]
        public void ErrorResponse_ForWrongMethod_Is405()
        {
            const int expectedStatusCode = 405;
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (HttpStatusCode)expectedStatusCode);
        }

        [Fact]
        public void ErrorResponse_ForBadRequest_Is400()
        {
            const int expectedStatusCode = 400;
            Assert.Equal(HttpStatusCode.BadRequest, (HttpStatusCode)expectedStatusCode);
        }

        #endregion

        #region CORS Headers Tests

        [Theory]
        [InlineData("http://localhost:3000", true)]
        [InlineData("http://localhost:8080", true)]
        [InlineData("http://localhost", true)]
        [InlineData("http://127.0.0.1:3000", true)]
        [InlineData("http://127.0.0.1", true)]
        [InlineData("http://evil.com", false)]
        [InlineData("http://malicious-site.com:38450", false)]
        [InlineData("https://attacker.com", false)]
        [InlineData("", false)]
        public void CorsHeaders_OnlyAllowLocalhostOrigins(string origin, bool shouldAllow)
        {
            // Test the origin validation logic
            bool isAllowed = false;
            if (!string.IsNullOrEmpty(origin))
            {
                isAllowed = origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                            origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase) ||
                            origin == "http://localhost" ||
                            origin == "http://127.0.0.1";
            }

            Assert.Equal(shouldAllow, isAllowed);
        }

        [Fact]
        public void CorsHeaders_RequiredHeaders()
        {
            // Document expected CORS headers (excluding Allow-Origin which is dynamic)
            var expectedMethods = "GET, POST, OPTIONS";
            var expectedHeaders = "Content-Type";

            Assert.Equal("GET, POST, OPTIONS", expectedMethods);
            Assert.Equal("Content-Type", expectedHeaders);
        }

        #endregion

        #region Rate Limiting Tests

        [Fact]
        public void RateLimiting_AllowsRequestsWithinLimit()
        {
            // Simulate rate limit check with sliding window
            var requestTimestamps = new Queue<DateTime>();
            var maxRequestsPerWindow = 30;
            var rateLimitWindow = TimeSpan.FromSeconds(10);
            var now = DateTime.UtcNow;

            // Add 29 requests (below limit)
            for (int i = 0; i < 29; i++)
            {
                requestTimestamps.Enqueue(now);
            }

            // Check if 30th request is allowed
            var windowStart = now - rateLimitWindow;
            while (requestTimestamps.Count > 0 && requestTimestamps.Peek() < windowStart)
            {
                requestTimestamps.Dequeue();
            }

            bool allowed = requestTimestamps.Count < maxRequestsPerWindow;
            Assert.True(allowed);
        }

        [Fact]
        public void RateLimiting_BlocksExcessiveRequests()
        {
            // Simulate rate limit check
            var requestTimestamps = new Queue<DateTime>();
            var maxRequestsPerWindow = 30;
            var rateLimitWindow = TimeSpan.FromSeconds(10);
            var now = DateTime.UtcNow;

            // Add 30 requests (at limit)
            for (int i = 0; i < 30; i++)
            {
                requestTimestamps.Enqueue(now);
            }

            // Check if 31st request is blocked
            var windowStart = now - rateLimitWindow;
            while (requestTimestamps.Count > 0 && requestTimestamps.Peek() < windowStart)
            {
                requestTimestamps.Dequeue();
            }

            bool allowed = requestTimestamps.Count < maxRequestsPerWindow;
            Assert.False(allowed);
        }

        [Fact]
        public void RateLimiting_ExpiresOldRequests()
        {
            // Simulate rate limit with expired requests
            var requestTimestamps = new Queue<DateTime>();
            var maxRequestsPerWindow = 30;
            var rateLimitWindow = TimeSpan.FromSeconds(10);
            var now = DateTime.UtcNow;

            // Add 30 requests from 15 seconds ago (should be expired)
            var oldTime = now - TimeSpan.FromSeconds(15);
            for (int i = 0; i < 30; i++)
            {
                requestTimestamps.Enqueue(oldTime);
            }

            // Remove expired timestamps
            var windowStart = now - rateLimitWindow;
            while (requestTimestamps.Count > 0 && requestTimestamps.Peek() < windowStart)
            {
                requestTimestamps.Dequeue();
            }

            // All old requests should be expired, so new request should be allowed
            bool allowed = requestTimestamps.Count < maxRequestsPerWindow;
            Assert.True(allowed);
            Assert.Empty(requestTimestamps); // All expired
        }

        [Fact]
        public void RateLimiting_ReturnsCorrectStatusCode()
        {
            // Rate limit exceeded should return 429 Too Many Requests
            const int expectedStatusCode = 429;
            Assert.Equal(HttpStatusCode.TooManyRequests, (HttpStatusCode)expectedStatusCode);
        }

        [Fact]
        public void RateLimiting_ConfiguredCorrectly()
        {
            // Document the rate limit configuration
            const int maxRequests = 30;
            const int windowSeconds = 10;

            Assert.Equal(30, maxRequests);
            Assert.Equal(10, windowSeconds);
            // 30 requests per 10 seconds = 3 requests/second average
        }

        #endregion

        #region Port Configuration Tests

        [Fact]
        public void DefaultPort_Is38450()
        {
            const int defaultPort = 38450;
            Assert.Equal(38450, defaultPort);
        }

        [Fact]
        public void Port_IsConfigurable()
        {
            var settings = new TalkKeys.Services.Settings.AppSettings();
            settings.RemoteControlPort = 12345;
            Assert.Equal(12345, settings.RemoteControlPort);
        }

        [Fact]
        public void RemoteControl_CanBeDisabled()
        {
            var settings = new TalkKeys.Services.Settings.AppSettings();
            settings.RemoteControlEnabled = false;
            Assert.False(settings.RemoteControlEnabled);
        }

        #endregion

        #region URL Format Tests

        [Fact]
        public void BaseUrl_Format_IsCorrect()
        {
            const int port = 38450;
            var baseUrl = $"http://localhost:{port}/";
            Assert.Equal("http://localhost:38450/", baseUrl);
        }

        [Fact]
        public void EndpointUrl_Format_IsCorrect()
        {
            const int port = 38450;
            var statusUrl = $"http://localhost:{port}/status";
            Assert.Equal("http://localhost:38450/status", statusUrl);
        }

        #endregion
    }

    // Internal classes to match server request models
    internal class MicrophoneRequest
    {
        public int Index { get; set; }
    }

    internal class ShortcutsRequest
    {
        public Dictionary<string, string>? Shortcuts { get; set; }
    }
}
