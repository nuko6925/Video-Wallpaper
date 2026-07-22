using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VideoWallpaper;

internal sealed class DesktopWallpaperHost
{
    private const uint WorkerWMessage = 0x052C;

    private const uint SmtoNormal = 0x0000;

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;

    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;
    private const int WsPopup = unchecked((int)0x80000000);

    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;
    
    private IntPtr _shellViewWindow;

    private delegate bool EnumWindowsProc(
        IntPtr window,
        IntPtr parameter);

    public IntPtr ParentWindow { get; private set; }

    public bool IsAttached =>
        ParentWindow != IntPtr.Zero &&
        IsWindow(ParentWindow);

    public void Attach(Form form, Screen screen)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(screen);

        _ = form.Handle;

        IntPtr desktopHost = FindWallpaperHost();

        if (desktopHost == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "デスクトップのWorkerWまたはProgmanを取得できませんでした。");
        }

        IntPtr window = form.Handle;

        ApplyWallpaperStyles(window);

        Marshal.SetLastPInvokeError(0);

        IntPtr oldParent = SetParent(window, desktopHost);

        if (oldParent == IntPtr.Zero)
        {
            int error = Marshal.GetLastPInvokeError();

            if (error != 0)
            {
                throw new Win32Exception(
                    error,
                    "壁紙ウィンドウのSetParentに失敗しました。");
            }
        }

        ParentWindow = desktopHost;

        PositionOnScreen(window, desktopHost, screen);
    }

    public void ReattachIfRequired(Form form, Screen screen)
    {
        if (!IsAttached)
        {
            Attach(form, screen);
            return;
        }

        PositionOnScreen(form.Handle, ParentWindow, screen);
    }

    private static void ApplyWallpaperStyles(IntPtr window)
    {
        var style = GetWindowLongPtr(window, GwlStyle);

        style &= ~(nint)WsPopup;
        style |= WsChild | WsVisible;

        SetWindowLongPtr(window, GwlStyle, style);

        var extendedStyle = GetWindowLongPtr(window, GwlExStyle);

        extendedStyle |= WsExToolWindow;
        extendedStyle |= WsExNoActivate;

        SetWindowLongPtr(
            window,
            GwlExStyle,
            extendedStyle);
    }

    private void PositionOnScreen(
        IntPtr window,
        IntPtr parent,
        Screen screen)
    {
        Rectangle bounds = screen.Bounds;

        
        var origin = new NativePoint
        {
            X = bounds.Left,
            Y = bounds.Top
        };

        if (!ScreenToClient(parent, ref origin))
        {
            origin.X = bounds.Left;
            origin.Y = bounds.Top;
        }
        
        if (!SetWindowPos(
                window,
                _shellViewWindow,
                origin.X,
                origin.Y,
                bounds.Width,
                bounds.Height,
                SwpNoActivate |
                SwpShowWindow |
                SwpFrameChanged))
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "壁紙ウィンドウの位置設定に失敗しました。");
        }
    }
    
    private IntPtr FindWallpaperHost()
    {
        IntPtr progman = FindWindow("Progman", null);

        if (progman == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Progmanウィンドウを取得できませんでした。");
        }

        SendMessageTimeout(
            progman,
            WorkerWMessage,
            new IntPtr(0xD),
            new IntPtr(1),
            SmtoNormal,
            1000,
            out _);

        _shellViewWindow = FindWindowEx(
            progman,
            IntPtr.Zero,
            "SHELLDLL_DefView",
            null);

        if (_shellViewWindow == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Progman配下のSHELLDLL_DefViewを取得できませんでした。");
        }

        return progman;
    }

    private static nint GetWindowLongPtr(
        IntPtr window,
        int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(window, index)
            : GetWindowLong32(window, index);
    }

    private static nint SetWindowLongPtr(
        IntPtr window,
        int index,
        nint value)
    {
        Marshal.SetLastPInvokeError(0);

        var result = IntPtr.Size == 8
            ? SetWindowLongPtr64(window, index, value)
            : SetWindowLong32(
                window,
                index,
                unchecked((int)value));

        if (result == 0)
        {
            int error = Marshal.GetLastPInvokeError();

            if (error != 0)
                throw new Win32Exception(error);
        }

        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr FindWindow(
        string? className,
        string? windowName);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr FindWindowEx(
        IntPtr parent,
        IntPtr childAfter,
        string? className,
        string? windowName);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(
        EnumWindowsProc callback,
        IntPtr parameter);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        uint flags,
        uint timeout,
        out IntPtr result);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern IntPtr SetParent(
        IntPtr child,
        IntPtr newParent);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongW",
        SetLastError = true)]
    private static extern int GetWindowLong32(
        IntPtr window,
        int index);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongPtrW",
        SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(
        IntPtr window,
        int index);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongW",
        SetLastError = true)]
    private static extern int SetWindowLong32(
        IntPtr window,
        int index,
        int value);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongPtrW",
        SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr window,
        int index,
        IntPtr value);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(
        IntPtr window);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(
        IntPtr window,
        ref NativePoint point);
}