using System;
using System.Collections.Generic;
using System.Text.Json;
using TalkKeys.Services.Controller;
using TalkKeys.Services.Settings;
using Xunit;

namespace TalkKeys.Tests
{
    /// <summary>
    /// Tests for TalkKeysController and related models.
    /// These are unit tests for the controller logic and model serialization.
    /// </summary>
    public class TalkKeysControllerTests
    {
        #region TalkKeysStatus Tests

        [Fact]
        public void TalkKeysStatus_DefaultValues_AreCorrect()
        {
            var status = new TalkKeysStatus();

            Assert.True(status.Success);
            Assert.Equal("idle", status.Status);
            Assert.False(status.Recording);
            Assert.False(status.Processing);
            Assert.Equal("1.0.0", status.Version);
            Assert.False(status.Authenticated);
            Assert.Null(status.Message);
        }

        [Fact]
        public void TalkKeysStatus_CanSetAllProperties()
        {
            var status = new TalkKeysStatus
            {
                Success = false,
                Status = "recording",
                Recording = true,
                Processing = false,
                Version = "1.0.8",
                Authenticated = true,
                Message = "Test message"
            };

            Assert.False(status.Success);
            Assert.Equal("recording", status.Status);
            Assert.True(status.Recording);
            Assert.False(status.Processing);
            Assert.Equal("1.0.8", status.Version);
            Assert.True(status.Authenticated);
            Assert.Equal("Test message", status.Message);
        }

        [Fact]
        public void TalkKeysStatus_SerializesToJson_WithCamelCase()
        {
            var status = new TalkKeysStatus
            {
                Success = true,
                Status = "idle",
                Recording = false,
                Processing = false,
                Version = "1.0.8",
                Authenticated = true
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(status, options);

            Assert.Contains("\"success\":true", json);
            Assert.Contains("\"status\":\"idle\"", json);
            Assert.Contains("\"recording\":false", json);
            Assert.Contains("\"processing\":false", json);
            Assert.Contains("\"version\":\"1.0.8\"", json);
            Assert.Contains("\"authenticated\":true", json);
        }

        #endregion

        #region ControllerActionResult Tests

        [Fact]
        public void ControllerActionResult_Ok_CreatesSuccessResult()
        {
            var result = ControllerActionResult.Ok("recording", "Recording started");

            Assert.True(result.Success);
            Assert.Equal("recording", result.Status);
            Assert.Equal("Recording started", result.Message);
        }

        [Fact]
        public void ControllerActionResult_Ok_WithoutMessage_CreatesSuccessResult()
        {
            var result = ControllerActionResult.Ok("idle");

            Assert.True(result.Success);
            Assert.Equal("idle", result.Status);
            Assert.Null(result.Message);
        }

        [Fact]
        public void ControllerActionResult_Fail_CreatesFailureResult()
        {
            var result = ControllerActionResult.Fail("idle", "Not recording");

            Assert.False(result.Success);
            Assert.Equal("idle", result.Status);
            Assert.Equal("Not recording", result.Message);
        }

        [Fact]
        public void ControllerActionResult_SerializesToJson_Correctly()
        {
            var result = ControllerActionResult.Ok("processing", "Transcribing...");

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(result, options);

            Assert.Contains("\"success\":true", json);
            Assert.Contains("\"status\":\"processing\"", json);
            Assert.Contains("\"message\":\"Transcribing...\"", json);
        }

        #endregion

        #region MicrophoneInfo Tests

        [Fact]
        public void MicrophoneInfo_DefaultValues_AreCorrect()
        {
            var info = new MicrophoneInfo();

            Assert.Equal(0, info.Index);
            Assert.Equal(string.Empty, info.Name);
            Assert.False(info.Current);
        }

        [Fact]
        public void MicrophoneInfo_CanSetAllProperties()
        {
            var info = new MicrophoneInfo
            {
                Index = 1,
                Name = "Jabra Engage 50 II",
                Current = true
            };

            Assert.Equal(1, info.Index);
            Assert.Equal("Jabra Engage 50 II", info.Name);
            Assert.True(info.Current);
        }

        [Fact]
        public void MicrophoneInfo_SerializesToJson_Correctly()
        {
            var info = new MicrophoneInfo
            {
                Index = 0,
                Name = "Default Microphone",
                Current = true
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(info, options);

            Assert.Contains("\"index\":0", json);
            Assert.Contains("\"name\":\"Default Microphone\"", json);
            Assert.Contains("\"current\":true", json);
        }

        [Fact]
        public void MicrophoneInfo_List_SerializesToJson_Correctly()
        {
            var microphones = new List<MicrophoneInfo>
            {
                new() { Index = 0, Name = "Microphone 1", Current = true },
                new() { Index = 1, Name = "Microphone 2", Current = false }
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(microphones, options);

            Assert.Contains("\"index\":0", json);
            Assert.Contains("\"index\":1", json);
            Assert.Contains("\"name\":\"Microphone 1\"", json);
            Assert.Contains("\"name\":\"Microphone 2\"", json);
        }

        #endregion

        #region TalkKeysCapabilities Tests

        [Fact]
        public void TalkKeysCapabilities_DefaultValues_AreCorrect()
        {
            var capabilities = new TalkKeysCapabilities();

            Assert.Equal("TalkKeys", capabilities.Name);
            Assert.Equal("1.0.0", capabilities.Version);
            Assert.NotNull(capabilities.Capabilities);
            Assert.Empty(capabilities.Capabilities);
            Assert.NotNull(capabilities.Endpoints);
            Assert.Empty(capabilities.Endpoints);
        }

        [Fact]
        public void TalkKeysCapabilities_CanAddCapabilities()
        {
            var capabilities = new TalkKeysCapabilities
            {
                Name = "TalkKeys",
                Version = "1.0.8"
            };

            capabilities.Capabilities.Add(new Capability
            {
                Id = "transcription",
                Name = "Voice Transcription",
                Description = "Record voice and transcribe to text",
                Shortcut = "Ctrl+Shift+Space",
                Actions = new List<string> { "starttranscription", "stoptranscription" }
            });

            Assert.Single(capabilities.Capabilities);
            Assert.Equal("transcription", capabilities.Capabilities[0].Id);
            Assert.Equal(2, capabilities.Capabilities[0].Actions.Count);
        }

        [Fact]
        public void TalkKeysCapabilities_CanAddEndpoints()
        {
            var capabilities = new TalkKeysCapabilities();

            capabilities.Endpoints.Add(new EndpointInfo
            {
                Method = "GET",
                Path = "/status",
                Description = "Get current status"
            });

            capabilities.Endpoints.Add(new EndpointInfo
            {
                Method = "POST",
                Path = "/starttranscription",
                Description = "Start voice recording"
            });

            Assert.Equal(2, capabilities.Endpoints.Count);
            Assert.Equal("GET", capabilities.Endpoints[0].Method);
            Assert.Equal("POST", capabilities.Endpoints[1].Method);
        }

        [Fact]
        public void TalkKeysCapabilities_SerializesToJson_WithFullStructure()
        {
            var capabilities = new TalkKeysCapabilities
            {
                Name = "TalkKeys",
                Version = "1.0.8",
                Capabilities = new List<Capability>
                {
                    new()
                    {
                        Id = "transcription",
                        Name = "Voice Transcription",
                        Description = "Record voice and transcribe",
                        Shortcut = "Ctrl+Shift+Space",
                        Actions = new List<string> { "starttranscription", "stoptranscription", "canceltranscription" }
                    }
                },
                Endpoints = new List<EndpointInfo>
                {
                    new() { Method = "GET", Path = "/", Description = "Get capabilities" },
                    new() { Method = "GET", Path = "/status", Description = "Get status" }
                }
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(capabilities, options);

            // Parse JSON to validate structure properly
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("TalkKeys", root.GetProperty("name").GetString());
            Assert.Equal("1.0.8", root.GetProperty("version").GetString());

            // Validate capabilities
            var caps = root.GetProperty("capabilities");
            Assert.Equal(1, caps.GetArrayLength());
            var firstCap = caps[0];
            Assert.Equal("transcription", firstCap.GetProperty("id").GetString());
            Assert.Equal("Ctrl+Shift+Space", firstCap.GetProperty("shortcut").GetString());
            Assert.Equal(3, firstCap.GetProperty("actions").GetArrayLength());

            // Validate endpoints
            var endpoints = root.GetProperty("endpoints");
            Assert.Equal(2, endpoints.GetArrayLength());
        }

        #endregion

        #region Capability Tests

        [Fact]
        public void Capability_DefaultValues_AreCorrect()
        {
            var capability = new Capability();

            Assert.Equal(string.Empty, capability.Id);
            Assert.Equal(string.Empty, capability.Name);
            Assert.Equal(string.Empty, capability.Description);
            Assert.Null(capability.Shortcut);
            Assert.NotNull(capability.Actions);
            Assert.Empty(capability.Actions);
        }

        [Fact]
        public void Capability_CanSetAllProperties()
        {
            var capability = new Capability
            {
                Id = "explain",
                Name = "Plain English Explainer",
                Description = "Explain selected text in plain English",
                Shortcut = "Ctrl+Win+E",
                Actions = new List<string> { "explain" }
            };

            Assert.Equal("explain", capability.Id);
            Assert.Equal("Plain English Explainer", capability.Name);
            Assert.Equal("Explain selected text in plain English", capability.Description);
            Assert.Equal("Ctrl+Win+E", capability.Shortcut);
            Assert.Single(capability.Actions);
            Assert.Equal("explain", capability.Actions[0]);
        }

        #endregion

        #region EndpointInfo Tests

        [Fact]
        public void EndpointInfo_DefaultValues_AreCorrect()
        {
            var endpoint = new EndpointInfo();

            Assert.Equal("GET", endpoint.Method);
            Assert.Equal(string.Empty, endpoint.Path);
            Assert.Equal(string.Empty, endpoint.Description);
        }

        [Fact]
        public void EndpointInfo_CanSetAllProperties()
        {
            var endpoint = new EndpointInfo
            {
                Method = "POST",
                Path = "/microphone",
                Description = "Set active microphone"
            };

            Assert.Equal("POST", endpoint.Method);
            Assert.Equal("/microphone", endpoint.Path);
            Assert.Equal("Set active microphone", endpoint.Description);
        }

        #endregion

        #region Settings Integration Tests

        [Fact]
        public void AppSettings_RemoteControl_DefaultValues_AreCorrect()
        {
            var settings = new AppSettings();

            Assert.True(settings.RemoteControlEnabled);
            Assert.Equal(38450, settings.RemoteControlPort);
        }

        [Fact]
        public void AppSettings_RemoteControl_CanSerializeAndDeserialize()
        {
            var original = new AppSettings
            {
                RemoteControlEnabled = false,
                RemoteControlPort = 12345
            };

            var json = JsonSerializer.Serialize(original);
            var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

            Assert.NotNull(deserialized);
            Assert.False(deserialized.RemoteControlEnabled);
            Assert.Equal(12345, deserialized.RemoteControlPort);
        }

        [Fact]
        public void AppSettings_LegacySettingsWithoutRemoteControl_UsesDefaults()
        {
            // Simulate loading settings from before RemoteControl was added
            var legacyJson = @"{ ""GroqApiKey"": ""legacy-key"" }";

            var loaded = JsonSerializer.Deserialize<AppSettings>(legacyJson);

            Assert.NotNull(loaded);
            Assert.True(loaded.RemoteControlEnabled); // Default is true
            Assert.Equal(38450, loaded.RemoteControlPort); // Default port
        }

        #endregion

        #region JSON Response Format Tests

        [Fact]
        public void CapabilitiesResponse_MatchesExpectedApiFormat()
        {
            // This test validates that the capabilities response matches the documented API format
            var capabilities = new TalkKeysCapabilities
            {
                Name = "TalkKeys",
                Version = "1.0.8",
                Capabilities = new List<Capability>
                {
                    new()
                    {
                        Id = "transcription",
                        Name = "Voice Transcription",
                        Description = "Record voice and transcribe to text, paste to active application",
                        Shortcut = "Ctrl+Shift+Space",
                        Actions = new List<string> { "starttranscription", "stoptranscription", "canceltranscription" }
                    },
                    new()
                    {
                        Id = "explain",
                        Name = "Plain English Explainer",
                        Description = "Explain selected text in plain English",
                        Shortcut = "Ctrl+Win+E",
                        Actions = new List<string> { "explain" }
                    }
                },
                Endpoints = new List<EndpointInfo>
                {
                    new() { Method = "GET", Path = "/", Description = "Get capabilities and API info" },
                    new() { Method = "GET", Path = "/status", Description = "Get current status" },
                    new() { Method = "POST", Path = "/starttranscription", Description = "Start voice recording" },
                    new() { Method = "POST", Path = "/stoptranscription", Description = "Stop and transcribe" },
                    new() { Method = "POST", Path = "/canceltranscription", Description = "Cancel recording" },
                    new() { Method = "POST", Path = "/explain", Description = "Explain selected text" },
                    new() { Method = "GET", Path = "/microphones", Description = "List microphones" },
                    new() { Method = "POST", Path = "/microphone", Description = "Set active microphone" },
                    new() { Method = "GET", Path = "/shortcuts", Description = "Get shortcuts" },
                    new() { Method = "POST", Path = "/shortcuts", Description = "Update shortcuts" }
                }
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(capabilities, options);

            // Verify structure
            Assert.Contains("\"name\":\"TalkKeys\"", json);
            Assert.Contains("\"version\":\"1.0.8\"", json);
            Assert.Contains("\"capabilities\":", json);
            Assert.Contains("\"endpoints\":", json);

            // Verify capabilities
            Assert.Equal(2, capabilities.Capabilities.Count);
            Assert.Equal("transcription", capabilities.Capabilities[0].Id);
            Assert.Equal("explain", capabilities.Capabilities[1].Id);

            // Verify endpoints
            Assert.Equal(10, capabilities.Endpoints.Count);
        }

        [Fact]
        public void StatusResponse_MatchesExpectedApiFormat()
        {
            var status = new TalkKeysStatus
            {
                Success = true,
                Status = "idle",
                Recording = false,
                Processing = false,
                Version = "1.0.8",
                Authenticated = true
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(status, options);

            // Parse back to verify structure
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out var successProp));
            Assert.True(successProp.GetBoolean());

            Assert.True(root.TryGetProperty("status", out var statusProp));
            Assert.Equal("idle", statusProp.GetString());

            Assert.True(root.TryGetProperty("recording", out var recordingProp));
            Assert.False(recordingProp.GetBoolean());

            Assert.True(root.TryGetProperty("processing", out var processingProp));
            Assert.False(processingProp.GetBoolean());

            Assert.True(root.TryGetProperty("version", out var versionProp));
            Assert.Equal("1.0.8", versionProp.GetString());

            Assert.True(root.TryGetProperty("authenticated", out var authProp));
            Assert.True(authProp.GetBoolean());
        }

        [Fact]
        public void ActionResponse_MatchesExpectedApiFormat()
        {
            var result = ControllerActionResult.Ok("recording", "Recording started");

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(result, options);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out var successProp));
            Assert.True(successProp.GetBoolean());

            Assert.True(root.TryGetProperty("status", out var statusProp));
            Assert.Equal("recording", statusProp.GetString());

            Assert.True(root.TryGetProperty("message", out var messageProp));
            Assert.Equal("Recording started", messageProp.GetString());
        }

        [Fact]
        public void MicrophonesResponse_MatchesExpectedApiFormat()
        {
            var response = new
            {
                success = true,
                microphones = new List<MicrophoneInfo>
                {
                    new() { Index = 0, Name = "Jabra Engage 50 II", Current = true },
                    new() { Index = 1, Name = "Realtek Audio", Current = false }
                }
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out var successProp));
            Assert.True(successProp.GetBoolean());

            Assert.True(root.TryGetProperty("microphones", out var micsProp));
            Assert.Equal(JsonValueKind.Array, micsProp.ValueKind);
            Assert.Equal(2, micsProp.GetArrayLength());

            var firstMic = micsProp[0];
            Assert.Equal(0, firstMic.GetProperty("index").GetInt32());
            Assert.Equal("Jabra Engage 50 II", firstMic.GetProperty("name").GetString());
            Assert.True(firstMic.GetProperty("current").GetBoolean());
        }

        [Fact]
        public void ShortcutsResponse_MatchesExpectedApiFormat()
        {
            var response = new
            {
                success = true,
                shortcuts = new Dictionary<string, string>
                {
                    { "transcription", "Ctrl+Shift+Space" },
                    { "explain", "Ctrl+Win+E" }
                }
            };

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(response, options);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("success", out _));
            Assert.True(root.TryGetProperty("shortcuts", out var shortcutsProp));
            Assert.Equal("Ctrl+Shift+Space", shortcutsProp.GetProperty("transcription").GetString());
            Assert.Equal("Ctrl+Win+E", shortcutsProp.GetProperty("explain").GetString());
        }

        #endregion
    }
}
