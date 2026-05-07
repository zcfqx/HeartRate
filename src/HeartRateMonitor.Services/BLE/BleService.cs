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

    private readonly IHeartRateParser _parser;
    private readonly ILogger _logger;

    private BluetoothLEDevice? _bluetoothDevice;
    private GattCharacteristic? _heartRateCharacteristic;
    private BluetoothLEAdvertisementWatcher? _advertisementWatcher;
    private DeviceWatcher? _deviceWatcher;
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
            var accessStatus = await BluetoothAdapter.GetDefaultAsync();
            return accessStatus != null;
        }
        catch (Exception ex)
        {
            _logger.Error("请求蓝牙权限失败", ex);
            return false;
        }
    }

    public async Task StartScanningAsync()
    {
        if (_isScanning) return;

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
            _advertisementWatcher.Start();

            _logger.Info("BLE扫描已启动");
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
            _advertisementWatcher?.Stop();
            _advertisementWatcher = null;
            _deviceWatcher?.Stop();
            _deviceWatcher = null;
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

            _bluetoothDevice = await BluetoothLEDevice.FromIdAsync(device.DeviceId);

            if (_bluetoothDevice == null)
            {
                throw new InvalidOperationException($"连接设备失败: {device.DeviceName}");
            }

            _bluetoothDevice.ConnectionStatusChanged += OnConnectionStatusChanged;

            var serviceResult = await _bluetoothDevice.GetGattServicesForUuidAsync(HeartRateServiceUuid);
            if (serviceResult.Status != GattCommunicationStatus.Success || serviceResult.Services.Count == 0)
            {
                throw new InvalidOperationException("设备上未找到心率服务");
            }

            var service = serviceResult.Services[0];
            var charResult = await service.GetCharacteristicsForUuidAsync(HeartRateMeasurementUuid);

            if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
            {
                throw new InvalidOperationException("未找到心率测量特征值");
            }

            _heartRateCharacteristic = charResult.Characteristics[0];

            _heartRateCharacteristic.ValueChanged += OnHeartRateValueChanged;

            var notifyStatus = await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (notifyStatus != GattCommunicationStatus.Success)
            {
                _logger.Warning("启用通知失败，尝试指示模式...");
                notifyStatus = await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Indicate);
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
                await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
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
                    await Task.Delay(2000 * (i + 1));
                }
            }

            UpdateState(ConnectionState.Disconnected);
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
        lock (_lock)
        {
            foreach (var device in _discoveredDevices.Values)
            {
                yield return device;
            }
        }
    }

    private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.Advertisement.LocalName)) return;

        var device = new BleDevice
        {
            DeviceId = args.BluetoothAddress.ToString(),
            DeviceName = args.Advertisement.LocalName,
            SignalStrength = args.RawSignalStrengthInDBm,
            IsConnectable = args.IsConnectable
        };

        bool isNew = false;
        lock (_lock)
        {
            if (!_discoveredDevices.ContainsKey(device.DeviceId))
            {
                _discoveredDevices[device.DeviceId] = device;
                isNew = true;
            }
            else
            {
                _discoveredDevices[device.DeviceId].SignalStrength = device.SignalStrength;
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

        _advertisementWatcher?.Stop();
        _advertisementWatcher = null;

        _scanCts?.Cancel();
        _scanCts?.Dispose();

        _heartRateCharacteristic = null;

        _bluetoothDevice?.Dispose();
        _bluetoothDevice = null;
    }
}
