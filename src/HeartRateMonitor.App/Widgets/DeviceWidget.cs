using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;

namespace HeartRateMonitor.App.Widgets;

/// <summary>
/// 设备连接组件：内嵌扫描、连接、断开，不再需要独立的选择设备窗口。
/// 列表默认只展示“广播了心率服务”的设备（手环/心率带），避免耳机、鼠标等
/// 蓝牙设备把真正的心率设备淹没；找不到设备时可一键切换为“全部设备”。
/// </summary>
internal sealed class DeviceWidget : WidgetViewBase
{
    private const int MaxListHeight = 172;

    private readonly Border _dot;
    private readonly TextBlock _status = WidgetUi.Text("未连接", "Text.Caption", "Brush.TextSecondary");
    private readonly TextBlock _hint = WidgetUi.Text(string.Empty, "Text.Caption", "Brush.TextMuted");
    private readonly Button _scanButton;
    private readonly ToggleButton _onlyHeartRateToggle;
    private readonly TextBlock _countText = WidgetUi.Text(string.Empty, "Text.Caption", "Brush.TextMuted");
    private readonly TextBlock _emptyText = WidgetUi.Text(string.Empty, "Text.Caption", "Brush.TextMuted");
    private readonly StackPanel _rowsPanel = new();
    private readonly ScrollViewer _listScroll;
    private readonly Border _listHost;
    private readonly Dictionary<string, DeviceRow> _rows = new(StringComparer.OrdinalIgnoreCase);

    private IDialogService? _dialogs;
    private bool _onlyHeartRate = true;
    private bool _wasScanning;
    private bool _hasScanned;

    public DeviceWidget()
    {
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _dot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };

        var statusRow = new Grid();
        statusRow.ColumnDefinitions.Add(new ColumnDefinition());
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        statusStack.Children.Add(_dot);
        statusStack.Children.Add(_status);
        statusRow.Children.Add(statusStack);

        _scanButton = new Button
        {
            Content = "扫描",
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 12,
            ToolTip = "扫描附近的蓝牙心率设备（手环需开启心率广播）"
        };
        _scanButton.SetResourceReference(FrameworkElement.StyleProperty, "Button.Outline");
        _scanButton.Click += OnScanClick;

        Grid.SetColumn(_scanButton, 1);
        statusRow.Children.Add(_scanButton);
        Children.Add(statusRow);

        _hint.Margin = new Thickness(16, 6, 0, 0);
        _hint.TextWrapping = TextWrapping.Wrap;
        Grid.SetRow(_hint, 1);
        Children.Add(_hint);

        // ----- 设备列表 -----
        _onlyHeartRateToggle = new ToggleButton
        {
            Content = "仅心率设备",
            IsChecked = true,
            FontSize = 10.5,
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "只显示广播了心率服务的设备；关闭后可看到附近全部蓝牙设备"
        };
        _onlyHeartRateToggle.SetResourceReference(FrameworkElement.StyleProperty, "Chip.Toggle");
        _onlyHeartRateToggle.Checked += OnFilterChanged;
        _onlyHeartRateToggle.Unchecked += OnFilterChanged;

        var header = new Grid { Margin = new Thickness(2, 0, 0, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _countText.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(_countText);
        Grid.SetColumn(_onlyHeartRateToggle, 1);
        header.Children.Add(_onlyHeartRateToggle);

        _emptyText.TextWrapping = TextWrapping.Wrap;
        _emptyText.Margin = new Thickness(2, 2, 2, 4);
        _emptyText.LineHeight = 16;

        _listScroll = new ScrollViewer
        {
            Content = _rowsPanel,
            MaxHeight = MaxListHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var hostContent = new StackPanel();
        hostContent.Children.Add(header);
        hostContent.Children.Add(_emptyText);
        hostContent.Children.Add(_listScroll);

        _listHost = new Border
        {
            Child = hostContent,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(8, 6, 6, 6),
            CornerRadius = new CornerRadius(8),
            Visibility = Visibility.Collapsed
        };
        _listHost.SetResourceReference(Border.BackgroundProperty, "Brush.SurfaceAlt");
        Grid.SetRow(_listHost, 2);
        Children.Add(_listHost);
    }

    protected override void OnAttached()
    {
        _dialogs = Context.Services.GetService(typeof(IDialogService)) as IDialogService;

        Ble.ConnectionStateChanged += OnConnectionStateChanged;
        Ble.DeviceDiscovered += OnDeviceDiscovered;
        Ble.DeviceUpdated += OnDeviceUpdated;
        Ble.ScanFailed += OnScanFailed;

        UpdateStatus();
        RefreshList();
    }

    protected override void OnDetached()
    {
        Ble.ConnectionStateChanged -= OnConnectionStateChanged;
        Ble.DeviceDiscovered -= OnDeviceDiscovered;
        Ble.DeviceUpdated -= OnDeviceUpdated;
        Ble.ScanFailed -= OnScanFailed;
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e) =>
        RunOnUi(() =>
        {
            UpdateStatus();
            RefreshList();
        });

    private void OnDeviceDiscovered(object? sender, BleDevice device) =>
        RunOnUi(() =>
        {
            if (!_rows.ContainsKey(device.DeviceId))
            {
                _rows[device.DeviceId] = CreateRow(device);
            }

            RefreshList();
        });

    private void OnDeviceUpdated(object? sender, BleDevice device) =>
        RunOnUi(() =>
        {
            if (!_rows.TryGetValue(device.DeviceId, out var row))
            {
                _rows[device.DeviceId] = CreateRow(device);
                RefreshList();
                return;
            }

            row.Update(device);
            RefreshList();
        });

    private void OnScanFailed(object? sender, BleScanFailedEventArgs e) => RunOnUi(() =>
    {
        _wasScanning = false;
        UpdateStatus();
        RefreshList();
        _dialogs?.ShowMessage(e.Message, "扫描失败");
    });

    private async void OnScanClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Ble.State == ConnectionState.Connected)
            {
                await Ble.DisconnectAsync();
                UpdateStatus();
                return;
            }

            if (Ble.IsScanning)
            {
                await Ble.StopScanningAsync();
                _wasScanning = false;
                UpdateStatus();
                RefreshList();
                return;
            }

            var availability = await Ble.CheckAvailabilityAsync();
            if (availability != BleAvailability.Ready)
            {
                _dialogs?.ShowMessage(availability.ToUserMessage(), "蓝牙不可用");
                return;
            }

            var granted = await Ble.RequestPermissionAsync();
            if (!granted)
            {
                _dialogs?.ShowMessage("未获得蓝牙权限，请在系统设置中允许本应用使用蓝牙。", "权限不足");
                return;
            }

            _rows.Clear();
            _listHost.Visibility = Visibility.Visible;
            _wasScanning = true;
            _hasScanned = true;
            await Ble.StartScanningAsync();
            UpdateStatus();
            RefreshList();
        }
        catch (Exception ex)
        {
            Log.Warning($"设备操作失败: {ex.Message}");
            _wasScanning = false;
            UpdateStatus();
            RefreshList();
            _dialogs?.ShowMessage($"蓝牙操作失败：{ex.Message}", "操作失败");
        }
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        _onlyHeartRate = _onlyHeartRateToggle.IsChecked == true;
        RefreshList();
    }

    private void OnDeviceClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: BleDevice device })
        {
            _ = ConnectAsync(device);
        }
    }

    private async Task ConnectAsync(BleDevice device)
    {
        try
        {
            _scanButton.Content = "连接中…";
            _scanButton.IsEnabled = false;

            await Ble.StopScanningAsync();
            await Ble.ConnectAsync(device);

            Settings.LastDeviceId = device.DeviceId;
            Settings.LastDeviceName = device.DeviceName;
            await Settings.SaveAsync();

            _rows.Clear();
            _listHost.Visibility = Visibility.Collapsed;
            _wasScanning = false;
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Log.Error($"连接设备失败: {device.DeviceName}", ex);
            _dialogs?.ShowMessage($"连接设备失败：{ex.Message}", "连接失败");
        }
        finally
        {
            _scanButton.IsEnabled = true;
            UpdateStatus();
            RefreshList();
        }
    }

    private DeviceRow CreateRow(BleDevice device)
    {
        var row = new DeviceRow(device);
        row.Button.Click += OnDeviceClick;
        return row;
    }

    /// <summary>按筛选条件重建列表（行控件本身复用，只调整可见性与顺序）。</summary>
    private void RefreshList()
    {
        var devices = BleAdvertisementRules.SortForDisplay(
            _rows.Values.Select(row => row.Device));

        var visible = _onlyHeartRate
            ? devices.Where(device => device.AdvertisesHeartRate).ToList()
            : devices.ToList();

        _rowsPanel.Children.Clear();
        foreach (var device in visible)
        {
            if (_rows.TryGetValue(device.DeviceId, out var row))
            {
                _rowsPanel.Children.Add(row.Button);
            }
        }

        var total = _rows.Count;
        var connected = Ble.State == ConnectionState.Connected;
        var scanning = Ble.IsScanning;

        _countText.Text = scanning
            ? total == 0 ? "正在搜索…" : $"已发现 {total} 台 · 搜索中"
            : total == 0 ? "尚未发现设备" : $"已发现 {total} 台";

        _emptyText.Text = visible.Count > 0
            ? string.Empty
            : BuildEmptyText(total, scanning);

        _emptyText.Visibility = visible.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        _listScroll.Visibility = visible.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _onlyHeartRateToggle.Visibility = total > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 只要本次运行扫描过，就保留列表区域，便于显示“没找到设备”的排障提示。
        _listHost.Visibility = !connected && (_hasScanned || total > 0)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private string BuildEmptyText(int total, bool scanning)
    {
        if (scanning)
        {
            return "正在搜索附近的心率设备…\n请让手环/心率带保持开机，并开启“心率广播”模式。";
        }

        if (total == 0)
        {
            return "没有发现设备。请确认设备处于“心率广播/运动”模式，且系统蓝牙已开启。";
        }

        return "已发现的设备没有广播心率服务，可关闭“仅心率设备”查看全部。";
    }

    private void UpdateStatus()
    {
        var state = Ble.State;
        var name = Ble.ConnectedDevice?.DeviceName ?? Settings.LastDeviceName;

        _status.Text = state switch
        {
            ConnectionState.Connected => "已连接",
            ConnectionState.Scanning => "扫描中…",
            ConnectionState.Connecting => "连接中…",
            ConnectionState.Reconnecting => "重连中…",
            _ => "未连接"
        };

        _hint.Text = state == ConnectionState.Connected && !string.IsNullOrWhiteSpace(name)
            ? name!
            : state == ConnectionState.Scanning
                ? "点击下方设备即可连接"
                : "点击扫描，查找附近的心率设备";

        _scanButton.Content = state switch
        {
            ConnectionState.Connected => "断开",
            ConnectionState.Scanning => "停止",
            ConnectionState.Connecting => "连接中…",
            _ => "扫描"
        };

        _scanButton.IsEnabled = state is not (ConnectionState.Connecting or ConnectionState.Reconnecting);

        var colorKey = state switch
        {
            ConnectionState.Connected => "Brush.Success",
            ConnectionState.Scanning => "Brush.Warning",
            ConnectionState.Connecting or ConnectionState.Reconnecting => "Brush.Warning",
            _ => "Brush.TextMuted"
        };

        if (Application.Current?.TryFindResource(colorKey) is SolidColorBrush brush)
        {
            _dot.Background = new SolidColorBrush(brush.Color);
        }

        if (state != ConnectionState.Scanning && !Ble.IsScanning && _wasScanning)
        {
            // 扫描自然结束（超时/外部停止）后，把“搜索中”的状态收尾。
            _wasScanning = false;
        }

        if (state == ConnectionState.Connected)
        {
            _listHost.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>列表中的一行：图标 + 名称 + 信号强度，点击即连接。</summary>
    private sealed class DeviceRow
    {
        private readonly TextBlock _name;
        private readonly TextBlock _signal;
        private readonly Path _icon;

        public DeviceRow(BleDevice device)
        {
            Device = Snapshot(device);

            _icon = WidgetUi.Icon("heart", 13, device.AdvertisesHeartRate ? "Brush.Heart" : "Brush.TextSecondary", 1.3);
            _icon.VerticalAlignment = VerticalAlignment.Center;

            _name = WidgetUi.Text(Device.DeviceName, "Text.Caption", "Brush.Text");
            _name.FontSize = 11.5;
            _name.TextTrimming = TextTrimming.CharacterEllipsis;
            _name.VerticalAlignment = VerticalAlignment.Center;

            _signal = WidgetUi.Text($"{Device.SignalStrength} dBm", "Text.Caption", "Brush.TextMuted");
            _signal.FontSize = 10;
            _signal.VerticalAlignment = VerticalAlignment.Center;
            _signal.Margin = new Thickness(6, 0, 2, 0);

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(_icon);
            Grid.SetColumn(_name, 1);
            _name.Margin = new Thickness(8, 0, 4, 0);
            content.Children.Add(_name);
            Grid.SetColumn(_signal, 2);
            content.Children.Add(_signal);

            Button = new Button
            {
                Tag = Device,
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(8, 5, 6, 5),
                Margin = new Thickness(0, 0, 0, 2)
            };
            Button.SetResourceReference(FrameworkElement.StyleProperty, "Button.Ghost");
        }

        public BleDevice Device { get; private set; }
        public Button Button { get; }

        public void Update(BleDevice device)
        {
            Device = Snapshot(device);
            Button.Tag = Device;
            _name.Text = Device.DeviceName;
            _signal.Text = $"{Device.SignalStrength} dBm";
            _icon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,
                Device.AdvertisesHeartRate ? "Brush.Heart" : "Brush.TextSecondary");
        }

        private static BleDevice Snapshot(BleDevice device) => new()
        {
            DeviceId = device.DeviceId,
            DeviceName = device.DeviceName,
            SignalStrength = device.SignalStrength,
            IsConnectable = device.IsConnectable,
            AdvertisesHeartRate = device.AdvertisesHeartRate,
            BluetoothAddress = device.BluetoothAddress
        };
    }
}
