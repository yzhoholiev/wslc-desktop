using Microsoft.UI.Windowing;
using Windows.Graphics;
using WslcDesktop.Interop;
using WslcDesktop.Models;
using WslcDesktop.Services;

namespace WslcDesktop;

public sealed partial class MainWindow
{
    private const int DefaultWidth = 1360;
    private const int DefaultHeight = 880;
    private const double MaxShareOfWorkArea = 0.9;

    /// <summary>
    ///     How much of the saved window, in device-independent pixels, must still be on a display to restore it there.
    /// </summary>
    private const int MinimumVisible = 120;

    private bool _maximizeOnFirstShow;

    /// <summary>
    ///     Applies the saved bounds, moved inside the nearest display, or centers the window on the primary display when there are none or they are off-screen.
    /// </summary>
    private void RestorePlacement()
    {
        if (Services.Settings.Current.Window is { } saved && FitOnDisplay(saved) is { } bounds)
        {
            AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));

            // Maximizing shows the window, so it waits until the window is meant to appear (never on a tray start).
            _maximizeOnFirstShow = saved.IsMaximized;
        }
        else
        {
            CenterOnPrimaryDisplay();
        }
    }

    private ScreenRect? FitOnDisplay(WindowPlacement saved)
    {
        RectInt32 workArea = DisplayArea.GetFromRect(new RectInt32(saved.X, saved.Y, saved.Width, saved.Height), DisplayAreaFallback.Nearest).WorkArea;
        int minimumVisible = (int)(MinimumVisible * WindowGeometry.GetScale(_hwnd));
        return WindowBounds.Fit(
            new ScreenRect(saved.X, saved.Y, saved.Width, saved.Height),
            new ScreenRect(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            minimumVisible);
    }

    private void CenterOnPrimaryDisplay()
    {
        RectInt32 workArea = DisplayArea.Primary.WorkArea;
        double scale = WindowGeometry.GetScale(_hwnd);
        int width = Math.Min((int)(DefaultWidth * scale), (int)(workArea.Width * MaxShareOfWorkArea));
        int height = Math.Min((int)(DefaultHeight * scale), (int)(workArea.Height * MaxShareOfWorkArea));

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));
    }

    /// <summary>
    ///     Saves the restored (normal) bounds even while the window is maximized, minimized or hidden in the tray.
    /// </summary>
    private void SavePlacement()
    {
        if (WindowGeometry.GetNormalPlacement(_hwnd) is not { } normal)
        {
            return;
        }

        // The normal bounds are in workspace coordinates, which are offset from screen coordinates by any taskbar docked top or left.
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        int offsetX = display.WorkArea.X - display.OuterBounds.X;
        int offsetY = display.WorkArea.Y - display.OuterBounds.Y;

        Services.Settings.Current.Window = new WindowPlacement
        {
            X = normal.Left + offsetX,
            Y = normal.Top + offsetY,
            Width = normal.Width,
            Height = normal.Height,
            IsMaximized = _maximizeOnFirstShow
                || _presenter.State == OverlappedPresenterState.Maximized
                || (_presenter.State == OverlappedPresenterState.Minimized
                    && normal.RestoresMaximized)
        };

        Services.Settings.Save();
    }
}
