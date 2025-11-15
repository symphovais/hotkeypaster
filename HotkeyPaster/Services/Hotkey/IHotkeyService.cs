using System;

namespace TalkKeys.Services.Hotkey
{
    public interface IHotkeyService
    {
        event EventHandler? HotkeyPressed;
        void RegisterHotkey();
        void UnregisterHotkey();
        void SetWindowHandle(IntPtr handle);
    }
}
