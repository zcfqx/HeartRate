using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Exceptions;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Services.BLE;

public class BleService : IBleService, IDisposable
{
    private static readonly Guid HeartRateServiceUuid = new("0000180d-0000-1000-8000-00805f9b34fb");
    private static readonly Guid HeartRateMeasurementUuid = new("00002a37-0000-1000-8000-00805f9b34fb");
    private const int ScanTimeoutSeconds = 60;

    /// <summary>ERROR_DEVICE_NOT_AVAILABLE：蓝牙无线电被关闭时 Start() 抛出的 HRESULT。</summary>
    private const int ErrorDeviceNotAvailable = unchecked((int)0x800710DF);

    /// <summary>E_ACCESSDENIED：蓝牙访问被系统拒绝。</summary>
    private const int ErrorAccessDenied = unchecked((int)0x80070005);

    private readonly IHeartRateParser _parser;
    private readonly ILogger _logger;

    private BluetoothLEDevice? _bluetoothDevice;
    private GattCharacteristic? _heartRateCharacteristic;
    private BluetoothLEAdvertisementWatcher? _advertisementWatcher;
    private CancellationTokenSource? _scanCts;

    private ConnectionState _state = ConnectionState.Disconnected;
    private BleDevice? _connectedDevice;
    private bool _isScanning;
    private bool _disposed;

    private readonly Dictionary<string, BleDevice> _discoveredDevices = new();
    private readonly object _lock = new();

    public event EventHandler<HeartRateChangedEventArgs>? HeartRateReceived;
    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    // 预留：高/低心率提醒事件（契约 IBleService 的一部分），由提醒类插件/订阅者使用，当前内置实现尚未触发
#pragma warning disable CS0067
    public event EventHandler<HeartRateAlertEventArgs>? HeartRateAlert;
#pragma warning restore CS0067
    public event EventHandler<BleDevice>? DeviceDiscovered;
    public event EventHandler<BleScanFailedEventArgs>? ScanFailed;

    public BleService(IHeartRateParser parser, ILogger logger)
    {
        _parser = parser;
        _logger = logger;
    }

    public ConnectionState State => _state;
    public BleDevice? ConnectedDevice => _connectedDevice;
    public bool IsScanning => _isScanning;

    /// <summary>
    /// 检查蓝牙适配器与无线电状态。蓝牙开关关闭时返回 <see cref="BleAvailability.RadioOff"/>，
    /// 而不是像以前那样只判断适配器是否存在（导致关闭蓝牙后依然报告“权限已授予”）。
    /// </summary>
    public async Task<BleAvailability> CheckAvailabilityAsync()
    {
        try
        {
            // 注意：这里必须 ConfigureAwait(false)。本方法可能被 UI 线程以
            // .GetAwaiter().GetResult() 的方式同步等待（见 App.OnStartup），
            // 若不脱离同步上下文，await 的续体会被排到已被阻塞的 Dispatcher 上，造成死锁（界面卡死）。
            var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask().ConfigureAwait(false);
            if (adapter == null)
            {
                _logger.Warning("未找到蓝牙适配器");
                return BleAvailability.NoAdapter;
            }

            if (!adapter.IsLowEnergySupported)
            {
                _logger.Warning("蓝牙适配器不支持低功耗蓝牙（BLE）");
                return BleAvailability.NoAdapter;
            }

            try
            {
                var radio = await adapter.GetRadioAsync().AsTask().ConfigureAwait(false);
                if (radio != null && radio.State != RadioState.On)
                {
                    _logger.Warning($"蓝牙无线电未开启（当前: {radio.State}）");
                    return BleAvailability.RadioOff;
                }
            }
            catch (Exception radioEx)
            {
                // 部分环境无法读取无线电状态，此时不阻断扫描，交由 Start() 的真实结果决定
                _logger.Warning($"读取蓝牙无线电状态失败: {radioEx.Message}");
            }

            return BleAvailability.Ready;
        }
        catch (Exception ex) when (IsAccessDenied(ex))
        {
            _logger.Warning($"蓝牙访问被拒绝: {ex.Message}");
            return BleAvailability.AccessDenied;
        }
        catch (Exception ex)
        {
            _logger.Error("检查蓝牙可用性失败", ex);
            return BleAvailability.Unknown;
        }
    }

    public async Task<bool> RequestPermissionAsync()
    {
        // 本方法可能被 UI 线程同步等待（App.OnStartup 的旧代码），因此不捕获同步上下文。
        var availability = await CheckAvailabilityAsync().ConfigureAwait(false);
        if (availability == BleAvailability.Ready)
        {
            return true;
        }

        _logger.Warning($"蓝牙当前不可用（{availability}）: {availability.ToUserMessage()}");
        return false;
    }

    public async Task StartScanningAsync()
    {
        // 如果正在扫描，先停止
        if (_isScanning)
        {
            await StopScanningAsync();
        }

        // 蓝牙开关关闭时 Start() 会抛出信息为空的 COMException，
        // 这里先给出明确原因，避免界面只显示“扫描失败:”。
        var availability = await CheckAvailabilityAsync();
        if (availability != BleAvailability.Ready)
        {
            _logger.Warning($"蓝牙不可用（{availability}），无法开始扫描");
            throw new BleUnavailableException(availability, availability.ToUserMessage());
        }

        try
        {
            _scanCts = new CancellationTokenSource();
            _isScanning = true;
            UpdateState(ConnectionState.Scanning);

            lock (_lock)
            {
                _discoveredDevices.Clear();
            }

            _advertisementWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            _advertisementWatcher.Received += OnAdvertisementReceived;
            _advertisementWatcher.Stopped += OnAdvertisementStopped;
            try
            {
                _advertisementWatcher.Start();
            }
            catch (Exception startEx) when (IsDeviceUnavailable(startEx) || IsAccessDenied(startEx))
            {
                var reason = IsAccessDenied(startEx) ? BleAvailability.AccessDenied : BleAvailability.RadioOff;
                throw new BleUnavailableException(reason, reason.ToUserMessage(), startEx);
            }

            _logger.Info("BLE扫描已启动");

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ScanTimeoutSeconds * 1000, _scanCts.Token);
                    if (_isScanning)
                    {
                        _logger.Info($"扫描超时（{ScanTimeoutSeconds}秒），自动停止");
                        await StopScanningAsync();
                    }
                }
                catch (TaskCanceledException) { }
            }, _scanCts.Token);
        }
        catch (BleUnavailableException ex)
        {
            CleanupWatcher();
            _scanCts?.Dispose();
            _scanCts = null;
            _isScanning = false;
            UpdateState(ConnectionState.Disconnected);
            _logger.Warning($"启动BLE扫描失败: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            CleanupWatcher();
            _scanCts?.Dispose();
            _scanCts = null;
            _isScanning = false;
            UpdateState(ConnectionState.Disconnected);
            _logger.Error("启动BLE扫描失败", ex);
            throw;
        }
    }

    public Task StopScanningAsync()
    {
        if (!_isScanning) return Task.CompletedTask;

        try
        {
            if (_advertisementWatcher != null)
            {
                _advertisementWatcher.Received -= OnAdvertisementReceived;
                _advertisementWatcher.Stopped -= OnAdvertisementStopped;
                try { _advertisementWatcher.Stop(); } catch { }
                _advertisementWatcher = null;
            }

            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = null;
            _isScanning = false;

            if (_state == ConnectionState.Scanning)
            {
                UpdateState(ConnectionState.Disconnected);
            }

            _logger.Info("BLE扫描已停止");
        }
        catch (Exception ex)
        {
            _logger.Error("停止BLE扫描出错", ex);
        }

        return Task.CompletedTask;
    }

    public async Task ConnectAsync(BleDevice device)
    {
        try
        {
            UpdateState(ConnectionState.Connecting);
            await StopScanningAsync();

            _logger.Info($"正在连接设备: {device.DeviceName} (地址: {device.BluetoothAddress})");

            _bluetoothDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(device.BluetoothAddress);

            if (_bluetoothDevice == null)
            {
                throw new InvalidOperationException($"无法连接设备: {device.DeviceName}（蓝牙地址无效或设备不可达）");
            }

            _bluetoothDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

            _logger.Info("正在发现心率服务...");
            var serviceResult = await _bluetoothDevice.GetGattServicesForUuidAsync(HeartRateServiceUuid);
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0)
            {
                throw new InvalidOperationException($"设备 {device.DeviceName} 上未找到心率服务（状态: {serviceResult.Status}）");
            }

            var service = serviceResult.Services[0];
            _logger.Info("正在发现心率测量特征值...");
            var charResult = await service.GetCharacteristicsForUuidAsync(HeartRateMeasurementUuid);

            if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
            {
                throw new InvalidOperationException("未找到心率测量特征值");
            }

            _heartRateCharacteristic = charResult.Characteristics[0];
            _heartRateCharacteristic.ValueChanged += OnHeartRateValueChanged;

            _logger.Info("正在启用心率数据通知...");
            var notifyStatus = await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (notifyStatus != GattCommunicationStatus.Success)
            {
                _logger.Warning("启用通知失败，尝试指示模式...");
                notifyStatus = await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Indicate);

                if (notifyStatus != GattCommunicationStatus.Success)
                {
                    throw new InvalidOperationException($"启用心率数据通知失败（状态: {notifyStatus}）");
                }
            }

            _connectedDevice = device;
            UpdateState(ConnectionState.Connected);

            _logger.Info($"已连接到 {device.DeviceName}");
        }
        catch (Exception ex)
        {
            _connectedDevice = null;
            UpdateState(ConnectionState.Disconnected);
            _logger.Error($"连接设备失败: {device.DeviceName}", ex);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_heartRateCharacteristic != null)
            {
                _heartRateCharacteristic.ValueChanged -= OnHeartRateValueChanged;
                try
                {
                    await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                        GattClientCharacteristicConfigurationDescriptorValue.None);
                }
                catch { }
                _heartRateCharacteristic = null;
            }

            if (_bluetoothDevice != null)
            {
                _bluetoothDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _bluetoothDevice.Dispose();
                _bluetoothDevice = null;
            }

            _connectedDevice = null;
            UpdateState(ConnectionState.Disconnected);

            _logger.Info("已断开设备连接");
        }
        catch (Exception ex)
        {
            _logger.Error("断开连接出错", ex);
        }
    }

    public async Task<bool> AutoReconnectAsync()
    {
        if (_connectedDevice == null) return false;

        try
        {
            UpdateState(ConnectionState.Reconnecting);
            _logger.Info("尝试自动重连...");

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await ConnectAsync(_connectedDevice);
                    return true;
                }
                catch
                {
                    _logger.Info($"重连尝试 {i + 1}/3 失败，等待重试...");
                    await Task.Delay(2000 * (i + 1));
                }
            }

            UpdateState(ConnectionState.Disconnected);
            _logger.Warning("自动重连失败，已放弃");
            return false;
        }
        catch (Exception ex)
        {
            _logger.Error("自动重连失败", ex);
            UpdateState(ConnectionState.Disconnected);
            return false;
        }
    }

#pragma warning disable CS1998
    public async IAsyncEnumerable<BleDevice> DiscoverDevicesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
#pragma warning restore CS1998
    {
        List<BleDevice> devices;
        lock (_lock)
        {
            devices = _discoveredDevices.Values.ToList();
        }

        foreach (var device in devices)
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            yield return device;
        }
    }

    public List<BleDevice> GetDiscoveredDevices()
    {
        lock (_lock)
        {
            return _discoveredDevices.Values.ToList();
        }
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        var localName = args.Advertisement.LocalName;
        var hasName = !string.IsNullOrWhiteSpace(localName);
        var isConnectable = args.IsConnectable;

        // 心率带有时不广播名称，但会广播心率服务 UUID（0x180D），这类设备同样需要收录，
        // 否则用户点击扫描后列表可能一直是空的。
        var advertisesHeartRate = false;
        var serviceUuids = args.Advertisement.ServiceUuids;
        if (serviceUuids is not null)
        {
            for (var i = 0; i < serviceUuids.Count; i++)
            {
                if (serviceUuids[i] == HeartRateServiceUuid)
                {
                    advertisesHeartRate = true;
                    break;
                }
            }
        }

        // 记录所有广播用于调试
        _logger.Debug(
            $"收到广播: {(hasName ? localName : "(无名称)")} | 心率服务: {advertisesHeartRate} | 可连接: {isConnectable} | 信号: {args.RawSignalStrengthInDBm} dBm");

        if (!isConnectable) return;
        if (!hasName && !advertisesHeartRate) return;

        var deviceId = args.BluetoothAddress.ToString();
        var device = new BleDevice
        {
            DeviceId = deviceId,
            DeviceName = hasName ? localName! : "心率传感器",
            SignalStrength = args.RawSignalStrengthInDBm,
            IsConnectable = args.IsConnectable,
            BluetoothAddress = args.BluetoothAddress
        };

        bool isNew = false;
        lock (_lock)
        {
            if (!_discoveredDevices.ContainsKey(deviceId))
            {
                _discoveredDevices[deviceId] = device;
                isNew = true;
                _logger.Debug($"[NEW] 设备首次发现: {device.DeviceName} (ID: {deviceId})");
            }
            else
            {
                _discoveredDevices[deviceId].SignalStrength = device.SignalStrength;
                _logger.Debug($"[UPDATE] 设备已存在: {device.DeviceName} (ID: {deviceId})");
            }
        }

        if (!isNew)
        {
            return;
        }

        try
        {
            _logger.Info($"发现设备: {device.DeviceName} (信号: {device.SignalStrength} dBm) - 触发DeviceDiscovered事件");
            DeviceDiscovered?.Invoke(this, device);
        }
        catch (Exception ex)
        {
            // 订阅方异常不能中断扫描回调，否则后续设备都收不到。
            _logger.Error("处理设备发现事件时出错", ex);
        }
    }

    /// <summary>解绑并释放当前扫描器。</summary>
    private void CleanupWatcher()
    {
        var watcher = _advertisementWatcher;
        _advertisementWatcher = null;
        if (watcher == null) return;

        try
        {
            watcher.Received -= OnAdvertisementReceived;
            watcher.Stopped -= OnAdvertisementStopped;
            try { watcher.Stop(); } catch { }
        }
        catch (Exception ex)
        {
            _logger.Debug($"释放扫描器时出错: {ex.Message}");
        }
    }

    /// <summary>判断异常是否为蓝牙设备不可用（ERROR_DEVICE_NOT_AVAILABLE，通常是蓝牙开关关闭）。</summary>
    private static bool IsDeviceUnavailable(Exception ex)
        => ex is COMException com && (com.HResult == ErrorDeviceNotAvailable || com.HResult == ErrorAccessDenied);

    /// <summary>判断异常是否为访问被拒绝。</summary>
    private static bool IsAccessDenied(Exception ex)
        => (ex is COMException com && com.HResult == ErrorAccessDenied)
           || ex is UnauthorizedAccessException;

    private void OnAdvertisementStopped(BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        _logger.Info($"广播监听已停止（错误: {args.Error}）");
        if (_isScanning)
        {
            _isScanning = false;
            if (_state == ConnectionState.Scanning)
            {
                UpdateState(ConnectionState.Disconnected);
            }
        }

        if (args.Error == BluetoothError.Success) return;

        var message = args.Error switch
        {
            BluetoothError.RadioNotAvailable => BleAvailability.RadioOff.ToUserMessage(),
            BluetoothError.ResourceInUse => "蓝牙正被其它应用占用，请关闭其它使用蓝牙的程序后重试。",
            BluetoothError.DisabledByPolicy => "蓝牙已被系统策略禁用，请联系管理员。",
            BluetoothError.NotSupported => BleAvailability.NoAdapter.ToUserMessage(),
            _ => $"蓝牙扫描意外中断（{args.Error}），请重试。"
        };

        _logger.Warning($"扫描中断: {message}");
        try
        {
            ScanFailed?.Invoke(this, new BleScanFailedEventArgs(message));
        }
        catch (Exception ex)
        {
            _logger.Error("处理扫描失败事件时出错", ex);
        }
    }

    private void OnHeartRateValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            var data = new byte[args.CharacteristicValue.Length];
            using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                reader.ReadBytes(data);
            }

            var heartRateData = _parser.Parse(data);
            try
            {
                HeartRateReceived?.Invoke(this, new HeartRateChangedEventArgs(heartRateData));
            }
            catch (Exception ex)
            {
                // 订阅方异常不能中断 GATT 通知回调。
                _logger.Error("处理心率数据事件时出错", ex);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("解析心率数据出错", ex);
        }
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            _logger.Warning("设备断开连接，尝试重连...");
            UpdateState(ConnectionState.Reconnecting);
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000);
                await AutoReconnectAsync();
            });
        }
    }

    private void UpdateState(ConnectionState newState)
    {
        if (_state == newState) return;
        _state = newState;
        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(newState, _connectedDevice?.DeviceName));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_advertisementWatcher != null)
        {
            _advertisementWatcher.Received -= OnAdvertisementReceived;
            _advertisementWatcher.Stopped -= OnAdvertisementStopped;
            try { _advertisementWatcher.Stop(); } catch { }
            _advertisementWatcher = null;
        }

        _scanCts?.Cancel();
        _scanCts?.Dispose();

        if (_heartRateCharacteristic != null)
        {
            _heartRateCharacteristic.ValueChanged -= OnHeartRateValueChanged;
            _heartRateCharacteristic = null;
        }

        if (_bluetoothDevice != null)
        {
            _bluetoothDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
            _bluetoothDevice.Dispose();
            _bluetoothDevice = null;
        }
    }
}
