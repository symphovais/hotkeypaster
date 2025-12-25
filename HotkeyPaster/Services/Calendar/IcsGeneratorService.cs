using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using TalkKeys.Services.Auth;

namespace TalkKeys.Services.Calendar
{
    /// <summary>
    /// Generates ICS (iCalendar) files from calendar events.
    /// Follows RFC 5545 specification with Outlook compatibility.
    /// </summary>
    public class IcsGeneratorService
    {
        // RFC 5545 requires CRLF line endings
        private const string CRLF = "\r\n";

        /// <summary>
        /// Generate ICS content for a single event
        /// </summary>
        public string GenerateIcs(CalendarEvent calendarEvent)
        {
            return GenerateIcs(new[] { calendarEvent });
        }

        /// <summary>
        /// Generate ICS content for multiple events
        /// </summary>
        public string GenerateIcs(IEnumerable<CalendarEvent> events)
        {
            var sb = new StringBuilder();

            // Calendar header
            AppendLine(sb, "BEGIN:VCALENDAR");
            AppendLine(sb, "VERSION:2.0");
            AppendLine(sb, "PRODID:-//TalkKeys//Reminders//EN");
            AppendLine(sb, "CALSCALE:GREGORIAN");
            AppendLine(sb, "METHOD:PUBLISH");
            // Outlook compatibility
            AppendLine(sb, "X-WR-CALNAME:TalkKeys Reminders");

            foreach (var evt in events)
            {
                AppendEvent(sb, evt);
            }

            AppendLine(sb, "END:VCALENDAR");

            return sb.ToString();
        }

        private void AppendEvent(StringBuilder sb, CalendarEvent evt)
        {
            AppendLine(sb, "BEGIN:VEVENT");

            // UID - unique identifier (required)
            var uid = $"{Guid.NewGuid():N}@talkkeys.app";
            AppendLine(sb, $"UID:{uid}");

            // DTSTAMP - timestamp when this was created (required, must be UTC)
            AppendLine(sb, $"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmss}Z");

            // SEQUENCE - version number (helps Outlook)
            AppendLine(sb, "SEQUENCE:0");

            // STATUS - confirmed event
            AppendLine(sb, "STATUS:CONFIRMED");

            // TRANSP - show as busy
            AppendLine(sb, "TRANSP:OPAQUE");

            // DTSTART/DTEND
            if (evt.AllDay)
            {
                // All-day events use DATE format (no time component)
                AppendLine(sb, $"DTSTART;VALUE=DATE:{evt.Start:yyyyMMdd}");
                var endDate = evt.Start.AddDays(1);
                AppendLine(sb, $"DTEND;VALUE=DATE:{endDate:yyyyMMdd}");
            }
            else
            {
                // Convert to UTC for maximum compatibility
                var startUtc = evt.Start.Kind == DateTimeKind.Utc
                    ? evt.Start
                    : evt.Start.ToUniversalTime();
                var endUtc = evt.End.HasValue
                    ? (evt.End.Value.Kind == DateTimeKind.Utc ? evt.End.Value : evt.End.Value.ToUniversalTime())
                    : startUtc.AddMinutes(evt.DurationMinutes);

                AppendLine(sb, $"DTSTART:{startUtc:yyyyMMddTHHmmss}Z");
                AppendLine(sb, $"DTEND:{endUtc:yyyyMMddTHHmmss}Z");
            }

            // SUMMARY (title) - required
            AppendLine(sb, $"SUMMARY:{EscapeIcsText(evt.Title)}");

            // LOCATION (optional)
            if (!string.IsNullOrEmpty(evt.Location))
            {
                AppendLine(sb, $"LOCATION:{EscapeIcsText(evt.Location)}");
            }

            // DESCRIPTION (optional)
            if (!string.IsNullOrEmpty(evt.Description))
            {
                AppendLine(sb, $"DESCRIPTION:{EscapeIcsText(evt.Description)}");
            }

            // ORGANIZER (helps some clients)
            AppendLine(sb, "ORGANIZER:mailto:noreply@talkkeys.app");

            // ATTENDEE (optional)
            if (evt.Attendees != null && evt.Attendees.Count > 0)
            {
                foreach (var attendee in evt.Attendees)
                {
                    AppendLine(sb, $"ATTENDEE;CN={EscapeIcsText(attendee)};ROLE=REQ-PARTICIPANT:mailto:noreply@talkkeys.app");
                }
            }

            // Add a default reminder (15 min before)
            AppendLine(sb, "BEGIN:VALARM");
            AppendLine(sb, "ACTION:DISPLAY");
            AppendLine(sb, "DESCRIPTION:Reminder");
            AppendLine(sb, "TRIGGER:-PT15M");
            AppendLine(sb, "END:VALARM");

            AppendLine(sb, "END:VEVENT");
        }

        private void AppendLine(StringBuilder sb, string line)
        {
            sb.Append(line);
            sb.Append(CRLF);
        }

        /// <summary>
        /// Escape text for ICS format per RFC 5545
        /// </summary>
        private string EscapeIcsText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Escape backslashes first, then other special characters
            return text
                .Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "");
        }

        /// <summary>
        /// Save ICS content to a temp file and return the path
        /// </summary>
        public string SaveToTempFile(string icsContent, string? eventTitle = null)
        {
            // Create a meaningful filename
            var safeName = eventTitle ?? "reminder";
            safeName = string.Join("_", safeName.Split(Path.GetInvalidFileNameChars()));
            if (safeName.Length > 50) safeName = safeName.Substring(0, 50);

            var fileName = $"TalkKeys_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.ics";
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            // Write without BOM for maximum compatibility
            File.WriteAllText(tempPath, icsContent, new UTF8Encoding(false));

            return tempPath;
        }

        /// <summary>
        /// Open ICS file with the system's default handler (usually a calendar app)
        /// </summary>
        public bool OpenIcsFile(string filePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Open the folder containing the ICS file and select it
        /// </summary>
        public void OpenFolderAndSelect(string filePath)
        {
            try
            {
                // Use explorer.exe /select to open folder and highlight the file
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch
            {
                // Fallback: just open the temp folder
                try
                {
                    Process.Start("explorer.exe", Path.GetDirectoryName(filePath) ?? Path.GetTempPath());
                }
                catch { }
            }
        }

        /// <summary>
        /// Copy file path to clipboard
        /// </summary>
        public void CopyPathToClipboard(string filePath)
        {
            try
            {
                System.Windows.Clipboard.SetText(filePath);
            }
            catch { }
        }

        /// <summary>
        /// Generate ICS file from events and open it
        /// Returns the file path for further actions
        /// </summary>
        public string GenerateAndOpen(IEnumerable<CalendarEvent> events, string? title = null)
        {
            var icsContent = GenerateIcs(events);
            var filePath = SaveToTempFile(icsContent, title);

            // Try to open the file directly
            if (!OpenIcsFile(filePath))
            {
                // If that fails, open folder and select the file
                OpenFolderAndSelect(filePath);
            }

            return filePath;
        }

        /// <summary>
        /// Generate ICS file and open folder (for new Outlook)
        /// </summary>
        public string GenerateAndShowInFolder(IEnumerable<CalendarEvent> events, string? title = null)
        {
            var icsContent = GenerateIcs(events);
            var filePath = SaveToTempFile(icsContent, title);
            OpenFolderAndSelect(filePath);
            return filePath;
        }
    }
}
