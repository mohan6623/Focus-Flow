using System.Runtime.InteropServices;
using System.Text;

namespace ScreenTimeTracker.SystemUtils;

/// <summary>
/// Provides information about windows using native Windows APIs.
/// </summary>
public class WindowInfoProvider
{
    #region Native Methods

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hWnd);

    #endregion

    /// <summary>
    /// Gets information about the currently focused foreground window.
    /// </summary>
    public WindowInfo? GetForegroundWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        
        if (hwnd == IntPtr.Zero)
            return null;

        return GetWindowInfo(hwnd);
    }

    /// <summary>
    /// Gets information about a specific window by its handle.
    /// </summary>
    public WindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindow(hwnd))
            return null;

        GetWindowThreadProcessId(hwnd, out int processId);
        
        if (processId <= 0)
            return null;

        var title = GetWindowTitle(hwnd);

        return new WindowInfo(hwnd, processId, title);
    }

    /// <summary>
    /// Gets the title of a window.
    /// </summary>
    private static string? GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        
        if (length == 0)
            return null;

        var sb = new StringBuilder(length + 1);
        GetWindowText(hwnd, sb, sb.Capacity);
        
        return sb.ToString();
    }
}

/// <summary>
/// Information about a window.
/// </summary>
public sealed record WindowInfo(IntPtr Handle, int ProcessId, string? Title)
{
    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}
