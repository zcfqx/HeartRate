using System.Runtime.CompilerServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;
using HeartRateMonitor.Core.Enums;
using HeartRateMonitor.Core.Events;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;

namespace HeartRateMonitor.Services.BLE;

public class BleService : IBleService, IDisposable
{
    private static readonly Guid HeartRateServiceUuid = new("0000180d-0000-1000-8000-00805f9b34fb");
    private static readonly Guid HeartRateMeasurementUuid = new("00002a37-0000-1000-8000-00805f9b34fb");
    private const int ScanTimeoutSeconds = 60;

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
    public event EventHandler<HeartRateAlertEventArgs>? HeartRateAlert;
    public event EventHandler<BleDevice>? DeviceDiscovered;

    public BleService(IHeartRateParser parser, ILogger logger)
    {
        _parser = parser;
        _logger = logger;
    }

    public ConnectionState State => _state;
    public BleDevice? ConnectedDevice => _connectedDevice;
    public bool IsScanning => _isScanning;

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            if (adapter == null)
            {
                _logger.Warning("未找到蓝牙适配器");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("请求蓝牙权限失败", ex);
            return false;
        }
    }

    public async Task StartScanningAsync()
    {
        // 如果正在扫描，先停止
        if (_isScanning)
        {
            await StopScanningAsync();
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
            _advertisementWatcher.Start();

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
        catch (Exception ex)
        {
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
        var hasName = !string.IsNullOrEmpty(localName);
        var isConnectable = args.IsConnectable;

        // 记录所有广播用于调试
        if (hasName)
        {
            _logger.Debug($"收到广播: {localName} | 可连接: {isConnectable} | 信号: {args.RawSignalStrengthInDBm} dBm");
        }

        if (!hasName || !isConnectable) return;

        var deviceId = args.BluetoothAddress.ToString();
        var device = new BleDevice
        {
            DeviceId = deviceId,
            DeviceName = localName,
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
                _logger.Debug($"[NEW] 设备首次发现: {localName} (ID: {deviceId})");
            }
            else
            {
                _discoveredDevices[deviceId].SignalStrength = device.SignalStrength;
                _logger.Debug($"[UPDATE] 设备已存在: {localName} (ID: {deviceId})");
            }
        }

        if (isNew)
        {
            _logger.Info($"发现心率设备: {device.DeviceName} (信号: {device.SignalStrength} dBm) - 触发DeviceDiscovered事件");
            DeviceDiscovered?.Invoke(this, device);
            _logger.Debug($"DeviceDiscovered事件已触发");
        }
    }

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
            HeartRateReceived?.Invoke(this, new HeartRateChangedEventArgs(heartRateData));
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
