using System;
using System.Windows.Forms;

namespace TalkKeys.Services.Hotkey
{
    /// <summary>
    /// Event args containing information about which hotkey was pressed
    /// </summary>
    public class HotkeyPressedEventArgs : EventArgs
    {
        public string HotkeyId { get; }

        public HotkeyPressedEventArgs(string hotkeyId)
        {
            HotkeyId = hotkeyId ?? throw new ArgumentNullException(nameof(hotkeyId));
        }
    }

    /// <summary>
    /// Service for registering and managing global hotkeys
    /// </summary>
    public interface IHotkeyService
    {
        /// <summary>
        /// Fired when any registered hotkey is pressed
        /// </summary>
        event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;

        /// <summary>
        /// Sets the window handle that will receive hotkey messages
        /// </summary>
        void SetWindowHandle(IntPtr handle);

        /// <summary>
        /// Registers a hotkey with a specific ID and key combination
        /// </summary>
        /// <param name="id">Unique identifier for this hotkey</param>
        /// <param name="modifiers">Modifier keys (Ctrl, Shift, Alt, Win)</param>
        /// <param name="key">The main key</param>
        void RegisterHotkey(string id, Keys modifiers, Keys key);

        /// <summary>
        /// Unregisters a specific hotkey by ID
        /// </summary>
        void UnregisterHotkey(string id);

        /// <summary>
        /// Unregisters all hotkeys
        /// </summary>
        void UnregisterAllHotkeys();
    }
}
