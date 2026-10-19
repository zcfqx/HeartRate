using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;

namespace HeartRateMonitor.App.Widgets;

/// <summary>设备连接组件：内嵌扫描、连接、断开，不再需要独立的选择设备窗口。</summary>
internal sealed class DeviceWidget : WidgetViewBase
{
    private readonly Border _dot;
    private readonly TextBlock _status = WidgetUi.Text("未连接", "Text.Caption", "Brush.TextSecondary");
    private readonly TextBlock _deviceName = WidgetUi.Text(string.Empty, "Text.Caption", "Brush.TextMuted");
    private readonly Button _scanButton;
    private readonly StackPanel _deviceList = new();
    private readonly Border _deviceListHost;
    private IDialogService? _dialogs;

    public DeviceWidget()
    {
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

        var statusStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        statusStack.Children.Add(_dot);
        statusStack.Children.Add(_status);
        statusRow.Children.Add(statusStack);

        _scanButton = new Button
        {
            Content = "扫描",
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 12
        };
        _scanButton.SetResourceReference(FrameworkElement.StyleProperty, "Button.Outline");
        _scanButton.Click += OnScanClick;

        Grid.SetColumn(_scanButton, 1);
        statusRow.Children.Add(_scanButton);

        Children.Add(statusRow);

        _deviceName.Margin = new Thickness(16, 6, 0, 0);
        Grid.SetRow(_deviceName, 1);
        Children.Add(_deviceName);

        _deviceList.Visibility = Visibility.Collapsed;
        _deviceListHost = new Border
        {
            Child = _deviceList,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(4),
            CornerRadius = new CornerRadius(8)
        };
        _deviceListHost.SetResourceReference(Border.BackgroundProperty, "Brush.SurfaceAlt");
        _deviceListHost.Visibility = Visibility.Collapsed;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_deviceListHost, 2);
        Children.Add(_deviceListHost);
    }

    protected override void OnAttached()
    {
        _dialogs = Context.Services.GetService(typeof(IDialogService)) as IDialogService;

        Ble.ConnectionStateChanged += OnConnectionStateChanged;
        Ble.DeviceDiscovered += OnDeviceDiscovered;
        Ble.ScanFailed += OnScanFailed;

        UpdateStatus();
    }

    protected override void OnDetached()
    {
        Ble.ConnectionStateChanged -= OnConnectionStateChanged;
        Ble.DeviceDiscovered -= OnDeviceDiscovered;
        Ble.ScanFailed -= OnScanFailed;
    }

    private void OnConnectionStateChanged(object? sender, ConnectionStateChangedEventArgs e) => RunOnUi(UpdateStatus);

    private void OnDeviceDiscovered(object? sender, BleDevice device) => RunOnUi(() => AddDevice(device));

    private void OnScanFailed(object? sender, BleScanFailedEventArgs e) => RunOnUi(() =>
    {
        _scanButton.Content = "扫描";
        UpdateStatus();
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
                _deviceListHost.Visibility = Visibility.Collapsed;
                UpdateStatus();
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

            _deviceList.Children.Clear();
            _deviceListHost.Visibility = Visibility.Visible;
            await Ble.StartScanningAsync();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Log.Warning($"设备操作失败: {ex.Message}");
            _dialogs?.ShowMessage($"蓝牙操作失败：{ex.Message}", "操作失败");
        }
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

            _deviceList.Children.Clear();
            _deviceListHost.Visibility = Visibility.Collapsed;
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
        }
    }

    private void AddDevice(BleDevice device)
    {
        foreach (var child in _deviceList.Children)
        {
            if (child is Button existing && existing.Tag is BleDevice known &&
                string.Equals(known.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var label = new StackPanel { Orientation = Orientation.Horizontal };
        label.Children.Add(WidgetUi.Icon("device", 14, "Brush.TextSecondary", 1.2));

        var text = WidgetUi.Text(device.DeviceName, "Text.Caption", "Brush.Text");
        text.Margin = new Thickness(10, 0, 8, 0);
        label.Children.Add(text);

        var signal = WidgetUi.Text($"{device.SignalStrength} dBm", "Text.Caption", "Brush.TextMuted");
        label.Children.Add(signal);

        var button = new Button
        {
            Tag = device,
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 2)
        };
        button.SetResourceReference(FrameworkElement.StyleProperty, "Button.Ghost");
        button.Click += OnDeviceClick;

        _deviceList.Children.Add(button);
        _deviceListHost.Visibility = Visibility.Visible;
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

        _deviceName.Text = state == ConnectionState.Connected && !string.IsNullOrWhiteSpace(name)
            ? name!
            : "点击扫描查找心率设备";

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
    }
}
