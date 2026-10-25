namespace WslcDesktop.Services;

/// <summary>
///     A rectangle on the virtual screen, in physical pixels.
/// </summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>
///     Where saved window bounds go when the window is restored on the current displays.
/// </summary>
public static class WindowBounds
{
    /// <summary>
    ///     Fits saved bounds inside the work area of the display nearest to them, or returns null when too little of them is on that display.
    /// </summary>
    public static ScreenRect? Fit(ScreenRect saved, ScreenRect workArea, int minimumVisible)
    {
        if (saved.Width <= 0 || saved.Height <= 0)
        {
            return null;
        }

        int visibleWidth = Math.Min(saved.Right, workArea.Right) - Math.Max(saved.X, workArea.X);
        int visibleHeight = Math.Min(saved.Bottom, workArea.Bottom) - Math.Max(saved.Y, workArea.Y);
        if (visibleWidth < Math.Min(minimumVisible, saved.Width) || visibleHeight < Math.Min(minimumVisible, saved.Height))
        {
            return null;
        }

        int width = Math.Min(saved.Width, workArea.Width);
        int height = Math.Min(saved.Height, workArea.Height);
        return new ScreenRect(
            Math.Clamp(saved.X, workArea.X, workArea.Right - width),
            Math.Clamp(saved.Y, workArea.Y, workArea.Bottom - height),
            width,
            height);
    }
}
