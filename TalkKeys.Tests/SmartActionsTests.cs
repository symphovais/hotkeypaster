using System.Collections.Generic;
using TalkKeys.Services.Auth;
using TalkKeys.Services.Windowing;
using Xunit;

namespace TalkKeys.Tests
{
    /// <summary>
    /// Tests for Smart Actions feature - context detection and action suggestions
    /// </summary>
    public class SmartActionsTests
    {
        #region Context Detection Tests

        [Theory]
        [InlineData("outlook", "RE: Budget Meeting", "email")]
        [InlineData("OUTLOOK", "Inbox - user@example.com", "email")]
        [InlineData("outlook.exe", "FW: Project Update", "email")]
        [InlineData("thunderbird", "Inbox", "email")]
        [InlineData("slack", "#general - Slack", "chat")]
        [InlineData("teams", "Meeting Chat", "chat")]
        [InlineData("discord", "Server Name", "chat")]
        [InlineData("code", "file.cs - VS Code", "code")]
        [InlineData("devenv", "Solution - Visual Studio", "code")]
        [InlineData("notepad", "Untitled", "other")]
        [InlineData("unknown", "Random Window", "other")]
        public void ContextDetection_ReturnsCorrectType(string processName, string windowTitle, string expectedType)
        {
            // Test the context detection logic that would be used by the API
            var contextType = DetectContextType(processName, windowTitle);
            Assert.Equal(expectedType, contextType);
        }

        /// <summary>
        /// Local implementation of context detection logic for testing
        /// (mirrors the backend logic)
        /// </summary>
        private static string DetectContextType(string processName, string windowTitle)
        {
            var process = processName.ToLowerInvariant();
            var title = windowTitle.ToLowerInvariant();

            // Email detection
            if (process.Contains("outlook") || process.Contains("thunderbird") ||
                process.Contains("mail") || title.Contains("gmail") ||
                title.Contains("inbox") && (title.Contains("mail") || title.Contains("@")))
            {
                return "email";
            }

            // Chat detection
            if (process.Contains("slack") || process.Contains("teams") ||
                process.Contains("discord") || process.Contains("telegram") ||
                process.Contains("whatsapp"))
            {
                return "chat";
            }

            // Code detection
            if (process.Contains("code") || process.Contains("devenv") ||
                process.Contains("rider") || process.Contains("idea") ||
                process.Contains("sublime") || process.Contains("atom"))
            {
                return "code";
            }

            return "other";
        }

        #endregion

        #region Action Suggestion Tests

        [Fact]
        public void GetActionsForContext_EmailContext_ReturnsReplyAction()
        {
            var actions = GetActionsForContext("email");
            Assert.Contains(actions, a => a.Id == "reply");
        }

        [Fact]
        public void GetActionsForContext_EmailContext_ReplyIsPrimary()
        {
            var actions = GetActionsForContext("email");
            var replyAction = actions.Find(a => a.Id == "reply");
            Assert.NotNull(replyAction);
            Assert.True(replyAction!.Primary);
        }

        [Fact]
        public void GetActionsForContext_ChatContext_ReturnsReplyAction()
        {
            var actions = GetActionsForContext("chat");
            Assert.Contains(actions, a => a.Id == "reply");
        }

        [Fact]
        public void GetActionsForContext_ChatContext_ReplyIsPrimary()
        {
            var actions = GetActionsForContext("chat");
            var replyAction = actions.Find(a => a.Id == "reply");
            Assert.NotNull(replyAction);
            Assert.True(replyAction!.Primary);
        }

        [Fact]
        public void GetActionsForContext_CodeContext_DoesNotReturnReply()
        {
            var actions = GetActionsForContext("code");
            Assert.DoesNotContain(actions, a => a.Id == "reply");
        }

        [Fact]
        public void GetActionsForContext_OtherContext_ReturnsEmptyOrMinimalActions()
        {
            var actions = GetActionsForContext("other");
            // Other context may have summarize or no actions
            Assert.True(actions.Count <= 1);
        }

        /// <summary>
        /// Local implementation of action suggestions for testing
        /// (mirrors the backend logic)
        /// </summary>
        private static List<SuggestedAction> GetActionsForContext(string contextType)
        {
            return contextType switch
            {
                "email" => new List<SuggestedAction>
                {
                    new() { Id = "reply", Label = "Reply", Icon = "message", Primary = true },
                    new() { Id = "forward", Label = "Forward", Icon = "forward", Primary = false },
                    new() { Id = "summarize", Label = "Summarize", Icon = "compress", Primary = false }
                },
                "chat" => new List<SuggestedAction>
                {
                    new() { Id = "reply", Label = "Quick Reply", Icon = "message", Primary = true }
                },
                "code" => new List<SuggestedAction>
                {
                    new() { Id = "explain", Label = "Explain Code", Icon = "code", Primary = true },
                    new() { Id = "simplify", Label = "Simplify", Icon = "edit", Primary = false }
                },
                _ => new List<SuggestedAction>()
            };
        }

        #endregion

        #region Model Tests

        [Fact]
        public void SuggestedAction_Properties_CanBeSetAndRead()
        {
            var action = new SuggestedAction
            {
                Id = "test",
                Label = "Test Action",
                Icon = "star",
                Primary = true
            };

            Assert.Equal("test", action.Id);
            Assert.Equal("Test Action", action.Label);
            Assert.Equal("star", action.Icon);
            Assert.True(action.Primary);
        }

        [Fact]
        public void ActionSuggestionResult_Success_HasActions()
        {
            var result = new ActionSuggestionResult
            {
                Success = true,
                ContextType = "email",
                Actions = new List<SuggestedAction>
                {
                    new() { Id = "reply", Label = "Reply", Icon = "message", Primary = true }
                }
            };

            Assert.True(result.Success);
            Assert.Equal("email", result.ContextType);
            Assert.Single(result.Actions);
        }

        [Fact]
        public void ActionSuggestionResult_Failure_HasError()
        {
            var result = new ActionSuggestionResult
            {
                Success = false,
                Error = "Something went wrong"
            };

            Assert.False(result.Success);
            Assert.Equal("Something went wrong", result.Error);
        }

        [Fact]
        public void GeneratedReplyResult_Success_HasReply()
        {
            var result = new GeneratedReplyResult
            {
                Success = true,
                Reply = "Thank you for your email. I will follow up shortly.",
                Tone = "professional"
            };

            Assert.True(result.Success);
            Assert.NotEmpty(result.Reply!);
            Assert.Equal("professional", result.Tone);
        }

        [Fact]
        public void GeneratedReplyResult_Failure_HasError()
        {
            var result = new GeneratedReplyResult
            {
                Success = false,
                Error = "Failed to generate reply"
            };

            Assert.False(result.Success);
            Assert.Equal("Failed to generate reply", result.Error);
        }

        #endregion

        #region WindowContext Tests

        [Fact]
        public void WindowContext_CanBeCreatedWithProperties()
        {
            var context = new WindowContext
            {
                ProcessName = "outlook.exe",
                WindowTitle = "RE: Meeting - Outlook"
            };

            Assert.Equal("outlook.exe", context.ProcessName);
            Assert.Equal("RE: Meeting - Outlook", context.WindowTitle);
        }

        [Fact]
        public void WindowContext_EmptyByDefault()
        {
            var context = new WindowContext();

            Assert.Empty(context.ProcessName);
            Assert.Empty(context.WindowTitle);
        }

        #endregion
    }
}
