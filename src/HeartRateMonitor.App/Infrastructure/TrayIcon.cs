using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace HeartRateMonitor.App.Infrastructure;

/// <summary>
/// 极简托盘图标：直接调用 shell32 / user32，替代 Hardcodet.NotifyIcon 依赖。
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const int WmTray = 0x0400 + 101;
    private const uint NimAdd = 0;
    private const uint NimDelete = 2;
    private const uint NifMessage = 0x01;
    private const uint NifIcon = 0x02;
    private const uint NifTip = 0x04;
    private const int ImageIcon = 1;
    private const uint LrLoadFromFile = 0x10;
    private const uint LrDefaultSize = 0x40;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;

    private readonly HwndSource _source;
    private readonly IntPtr _icon;
    private readonly uint _iconId;
    private ContextMenu? _menu;
    private bool _disposed;

    public TrayIcon(string tooltip)
    {
        _iconId = (uint)Environment.ProcessId;
        _icon = LoadIcon();

        var parameters = new HwndSourceParameters("HeartRateMonitorTray")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0x00000080 // WS_EX_TOOLWINDOW
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        var data = CreateData();
        data.uFlags = NifMessage | NifIcon | NifTip;
        data.uCallbackMessage = WmTray;
        data.hIcon = _icon;
        data.szTip = Truncate(tooltip, 127);
        Shell_NotifyIcon(NimAdd, ref data);
    }

    /// <summary>右键菜单工厂：每次弹出时新建，避免持有已释放的 UI 元素。</summary>
    public Func<ContextMenu>? MenuFactory { get; set; }

    public event EventHandler? Activated;

    public event EventHandler? DoubleClicked;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmTray) return IntPtr.Zero;

        switch (lParam.ToInt32())
        {
            case WmLButtonUp:
                Activated?.Invoke(this, EventArgs.Empty);
                break;
            case WmLButtonDblClk:
                DoubleClicked?.Invoke(this, EventArgs.Empty);
                break;
            case WmRButtonUp:
                ShowMenu();
                break;
        }

        handled = true;
        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        if (MenuFactory?.Invoke() is not { } menu) return;

        _menu = menu;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.StaysOpen = false;
        menu.IsOpen = true;
    }

    private NOTIFYICONDATA CreateData() => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _source.Handle,
        uID = _iconId
    };

    private static IntPtr LoadIcon()
    {
        var path = AppIcon.EnsureTrayIconFile();
        if (path is null) return IntPtr.Zero;

        return LoadImage(IntPtr.Zero, path, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize);
    }

    private static string Truncate(string text, int max)
        => text.Length <= max ? text : text[..max];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _menu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);
        _menu = null;

        var data = CreateData();
        Shell_NotifyIcon(NimDelete, ref data);

        _source.RemoveHook(WndProc);
        _source.Dispose();

        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, int type, int cx, int cy, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
