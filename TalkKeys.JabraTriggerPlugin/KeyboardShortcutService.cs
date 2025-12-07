using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace TalkKeys.JabraTriggerPlugin
{
    /// <summary>
    /// Service for sending keyboard shortcuts programmatically
    /// </summary>
    public class KeyboardShortcutService
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // Virtual key codes
        private static readonly Dictionary<string, byte> VirtualKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            // Modifier keys
            { "Ctrl", 0x11 },
            { "Control", 0x11 },
            { "Alt", 0x12 },
            { "Shift", 0x10 },
            { "Win", 0x5B },
            { "Windows", 0x5B },

            // Function keys
            { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
            { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
            { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },

            // Special keys
            { "Enter", 0x0D }, { "Return", 0x0D },
            { "Escape", 0x1B }, { "Esc", 0x1B },
            { "Tab", 0x09 },
            { "Space", 0x20 },
            { "Backspace", 0x08 },
            { "Delete", 0x2E }, { "Del", 0x2E },
            { "Insert", 0x2D }, { "Ins", 0x2D },
            { "Home", 0x24 },
            { "End", 0x23 },
            { "PageUp", 0x21 }, { "PgUp", 0x21 },
            { "PageDown", 0x22 }, { "PgDn", 0x22 },

            // Arrow keys
            { "Up", 0x26 }, { "Down", 0x28 }, { "Left", 0x25 }, { "Right", 0x27 },

            // Number keys (top row)
            { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 }, { "4", 0x34 },
            { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 }, { "8", 0x38 }, { "9", 0x39 },

            // Letter keys
            { "A", 0x41 }, { "B", 0x42 }, { "C", 0x43 }, { "D", 0x44 }, { "E", 0x45 },
            { "F", 0x46 }, { "G", 0x47 }, { "H", 0x48 }, { "I", 0x49 }, { "J", 0x4A },
            { "K", 0x4B }, { "L", 0x4C }, { "M", 0x4D }, { "N", 0x4E }, { "O", 0x4F },
            { "P", 0x50 }, { "Q", 0x51 }, { "R", 0x52 }, { "S", 0x53 }, { "T", 0x54 },
            { "U", 0x55 }, { "V", 0x56 }, { "W", 0x57 }, { "X", 0x58 }, { "Y", 0x59 },
            { "Z", 0x5A },

            // Numpad
            { "Num0", 0x60 }, { "Num1", 0x61 }, { "Num2", 0x62 }, { "Num3", 0x63 },
            { "Num4", 0x64 }, { "Num5", 0x65 }, { "Num6", 0x66 }, { "Num7", 0x67 },
            { "Num8", 0x68 }, { "Num9", 0x69 },

            // Other
            { "PrintScreen", 0x2C }, { "PrtSc", 0x2C },
            { "Pause", 0x13 },
            { "NumLock", 0x90 },
            { "ScrollLock", 0x91 },
            { "CapsLock", 0x14 },
        };

        /// <summary>
        /// Sends a keyboard shortcut (e.g., "Ctrl+Shift+M", "Alt+F4")
        /// </summary>
        /// <param name="shortcut">The shortcut string in format "Modifier+Modifier+Key"</param>
        /// <returns>True if successful, false if parsing failed</returns>
        public bool SendShortcut(string? shortcut)
        {
            if (string.IsNullOrWhiteSpace(shortcut))
                return false;

            var parts = shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return false;

            var keysToPress = new List<byte>();

            foreach (var part in parts)
            {
                if (VirtualKeys.TryGetValue(part, out byte vk))
                {
                    keysToPress.Add(vk);
                }
                else
                {
                    // Unknown key
                    return false;
                }
            }

            if (keysToPress.Count == 0)
                return false;

            // Press all keys down (modifiers first, then the main key)
            foreach (var vk in keysToPress)
            {
                keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            }

            // Small delay
            System.Threading.Thread.Sleep(10);

            // Release all keys in reverse order
            for (int i = keysToPress.Count - 1; i >= 0; i--)
            {
                keybd_event(keysToPress[i], 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }

            return true;
        }

        /// <summary>
        /// Converts a WPF Key and ModifierKeys to a shortcut string
        /// </summary>
        public static string KeyToShortcutString(Key key, ModifierKeys modifiers)
        {
            var parts = new List<string>();

            if (modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");

            // Convert key to string
            var keyStr = KeyToString(key);
            if (!string.IsNullOrEmpty(keyStr))
                parts.Add(keyStr);

            return string.Join("+", parts);
        }

        private static string? KeyToString(Key key)
        {
            return key switch
            {
                // Letters
                >= Key.A and <= Key.Z => key.ToString(),

                // Numbers
                >= Key.D0 and <= Key.D9 => ((int)key - (int)Key.D0).ToString(),
                >= Key.NumPad0 and <= Key.NumPad9 => "Num" + ((int)key - (int)Key.NumPad0),

                // Function keys
                >= Key.F1 and <= Key.F12 => key.ToString(),

                // Special keys
                Key.Enter => "Enter",
                Key.Escape => "Esc",
                Key.Tab => "Tab",
                Key.Space => "Space",
                Key.Back => "Backspace",
                Key.Delete => "Delete",
                Key.Insert => "Insert",
                Key.Home => "Home",
                Key.End => "End",
                Key.PageUp => "PageUp",
                Key.PageDown => "PageDown",
                Key.Up => "Up",
                Key.Down => "Down",
                Key.Left => "Left",
                Key.Right => "Right",
                Key.PrintScreen => "PrintScreen",
                Key.Pause => "Pause",

                // Ignore modifier-only keys
                Key.LeftCtrl or Key.RightCtrl => null,
                Key.LeftAlt or Key.RightAlt => null,
                Key.LeftShift or Key.RightShift => null,
                Key.LWin or Key.RWin => null,
                Key.System => null, // Alt key

                _ => null
            };
        }

        /// <summary>
        /// Checks if a key is a valid main key (not just a modifier)
        /// </summary>
        public static bool IsValidMainKey(Key key)
        {
            return KeyToString(key) != null;
        }
    }
}
