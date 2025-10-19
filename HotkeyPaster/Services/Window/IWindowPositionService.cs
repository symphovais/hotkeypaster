using System.Windows;

namespace HotkeyPaster.Services.Windowing
{
    public interface IWindowPositionService
    {
        void PositionBottomCenter(Window window, double bottomMargin = 20);
    }
}
