# 心率监控软件BLE通信设计文档

## 1. BLE通信概述

### 1.1 技术背景
蓝牙低功耗（Bluetooth Low Energy, BLE）是蓝牙4.0引入的低功耗通信标准，专为物联网和可穿戴设备设计。华为手环8系列开启心率广播后，会作为标准BLE心率监测器向外广播数据。

### 1.2 核心概念

| 概念 | 说明 |
|------|------|
| GATT | Generic Attribute Profile，通用属性配置文件 |
| Service | 服务，包含一组相关特征值 |
| Characteristic | 特征值，包含实际数据和操作 |
| UUID | 服务和特征值的唯一标识符 |
| Notification | 通知，设备主动推送数据 |
| Central | 中心设备（本软件） |
| Peripheral | 外围设备（手环） |

## 2. 华为手环心率协议

### 2.1 服务与特征值

| 服务/特征 | UUID | 16位UUID | 说明 |
|-----------|------|----------|------|
| Heart Rate Service | 0000180d-0000-1000-8000-00805f9b34fb | 0x180D | 心率服务 |
| Heart Rate Measurement | 00002a37-0000-1000-8000-00805f9b34fb | 0x2A37 | 心率测量值 |
| Body Sensor Location | 00002a38-0000-1000-8000-00805f9b34fb | 0x2A38 | 传感器位置 |
| Heart Rate Control Point | 00002a39-0000-1000-8000-00805f9b34fb | 0x2A39 | 控制点 |
| Device Information Service | 0000180a-0000-1000-8000-00805f9b34fb | 0x180A | 设备信息服务 |
| Battery Service | 0000180f-0000-1000-8000-00805f9b34fb | 0x180F | 电池服务 |
| Battery Level | 00002a19-0000-1000-8000-00805f9b34fb | 0x2A19 | 电池电量 |

### 2.2 心率测量数据格式

Heart Rate Measurement (0x2A37) 数据结构：

```
Byte 0: Flags
┌─────┬─────┬─────┬─────┬─────┬─────┬─────┬─────┐
│  7  │  6  │  5  │  4  │  3  │  2  │  1  │  0  │
├─────┴─────┴─────┼─────┼─────┼─────┼─────┼─────┤
│    Reserved      │ RR  │Energy│Contact│Contact│ HR  │
│                  │ Int │ Exp │ Supp │ Status│Format│
└─────────────────┴─────┴─────┴─────┴─────┴─────┘

Bit 0: Heart Rate Value Format
  0 = UINT8 (心率值在Byte 1)
  1 = UINT16 (心率值在Byte 1-2)

Bit 1: Sensor Contact Status
  0 = 传感器未接触皮肤
  1 = 传感器已接触皮肤

Bit 2: Sensor Contact Supported
  0 = 不支持接触检测
  1 = 支持接触检测

Bit 3: Energy Expended Status
  0 = Energy Expended字段不存在
  1 = Energy Expended字段存在

Bit 4: RR-Interval Status
  0 = RR-Interval字段不存在
  1 = RR-Interval字段存在
```

#### 数据解析示例

```
示例1: [0x00, 0x48]
  Flags = 0x00 (UINT8格式)
  Heart Rate = 0x48 = 72 BPM

示例2: [0x06, 0x48, 0x00]
  Flags = 0x06 (UINT16格式, 支持接触检测, 未接触)
  Heart Rate = 0x0048 = 72 BPM

示例3: [0x14, 0x50, 0x01, 0x00, 0x34, 0x01]
  Flags = 0x14 (UINT8格式, Energy Expended, RR-Interval)
  Heart Rate = 0x50 = 80 BPM
  Energy Expended = 0x0100 = 256 kJ
  RR-Interval = 0x0134 = 308 ms
```

## 3. 连接流程设计

### 3.1 完整连接流程

```
┌─────────────────────────────────────────────────────────────────┐
│                      BLE连接流程                                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────┐     ┌──────────┐     ┌──────────┐               │
│  │  开始    │────>│ 检查蓝牙 │────>│ 扫描设备 │               │
│  └──────────┘     └──────────┘     └──────────┘               │
│                        │                    │                   │
│                        │ 蓝牙不可用         │ 发现设备          │
│                        ▼                    ▼                   │
│                 ┌──────────┐     ┌──────────┐               │
│                 │ 显示错误 │     │ 选择设备 │               │
│                 └──────────┘     └──────────┘               │
│                                        │                       │
│                                        ▼                       │
│                              ┌──────────┐               │
│                              │ 建立连接 │               │
│                              └──────────┘               │
│                                        │                       │
│                                        ▼                       │
│                              ┌──────────┐               │
│                              │ 发现服务 │               │
│                              └──────────┘               │
│                                        │                       │
│                                        ▼                       │
│                              ┌──────────┐               │
│                              │ 订阅通知 │               │
│                              └──────────┘               │
│                                        │                       │
│                                        ▼                       │
│                              ┌──────────┐               │
│                              │ 接收数据 │               │
│                              └──────────┘               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 状态机设计

```csharp
public enum BleState
{
    Idle,               // 空闲
    Scanning,           // 扫描中
    ScanComplete,       // 扫描完成
    Connecting,         // 连接中
    Connected,          // 已连接
    DiscoveringServices,// 发现服务中
    Subscribing,        // 订阅中
    Receiving,          // 接收数据中
    Disconnecting,      // 断开中
    Error               // 错误
}

public class BleStateMachine
{
    private BleState _currentState = BleState.Idle;
    
    public BleState CurrentState => _currentState;
    
    public event EventHandler<BleStateChangedEventArgs> StateChanged;
    
    public bool CanTransitionTo(BleState newState)
    {
        return (_currentState, newState) switch
        {
            (BleState.Idle, BleState.Scanning) => true,
            (BleState.Scanning, BleState.ScanComplete) => true,
            (BleState.Scanning, BleState.Idle) => true,
            (BleState.ScanComplete, BleState.Connecting) => true,
            (BleState.ScanComplete, BleState.Idle) => true,
            (BleState.Connecting, BleState.Connected) => true,
            (BleState.Connecting, BleState.Error) => true,
            (BleState.Connected, BleState.DiscoveringServices) => true,
            (BleState.Connected, BleState.Disconnecting) => true,
            (BleState.DiscoveringServices, BleState.Subscribing) => true,
            (BleState.DiscoveringServices, BleState.Error) => true,
            (BleState.Subscribing, BleState.Receiving) => true,
            (BleState.Subscribing, BleState.Error) => true,
            (BleState.Receiving, BleState.Disconnecting) => true,
            (BleState.Receiving, BleState.Error) => true,
            (BleState.Disconnecting, BleState.Idle) => true,
            (BleState.Error, BleState.Idle) => true,
            _ => false
        };
    }
    
    public void TransitionTo(BleState newState)
    {
        if (!CanTransitionTo(newState))
            throw new InvalidOperationException(
                $"Cannot transition from {_currentState} to {newState}");
        
        var oldState = _currentState;
        _currentState = newState;
        StateChanged?.Invoke(this, new BleStateChangedEventArgs(oldState, newState));
    }
}
```

## 4. 核心类设计

### 4.1 BleService 实现

```csharp
public class BleService : IBleService
{
    private readonly ILogger<BleService> _logger;
    private readonly IHeartRateParser _parser;
    private readonly BleStateMachine _stateMachine;
    
    private BluetoothLEDevice _bluetoothDevice;
    private GattCharacteristic _heartRateCharacteristic;
    private CancellationTokenSource _scanCts;
    
    // 心率服务UUID
    private static readonly Guid HeartRateServiceUuid = 
        BluetoothUuidHelper.FromShortId(0x180D);
    
    // 心率测量特征UUID
    private static readonly Guid HeartRateMeasurementUuid = 
        BluetoothUuidHelper.FromShortId(0x2A37);
    
    // 电池服务UUID
    private static readonly Guid BatteryServiceUuid = 
        BluetoothUuidHelper.FromShortId(0x180F);
    
    // 电池电量特征UUID
    private static readonly Guid BatteryLevelUuid = 
        BluetoothUuidHelper.FromShortId(0x2A19);
    
    public bool IsScanning => _stateMachine.CurrentState == BleState.Scanning;
    public ConnectionState State => MapState(_stateMachine.CurrentState);
    public BleDevice ConnectedDevice { get; private set; }
    
    public event EventHandler<BleDevice> DeviceDiscovered;
    public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
    public event EventHandler<HeartRateData> HeartRateReceived;
    
    public BleService(ILogger<BleService> logger, IHeartRateParser parser)
    {
        _logger = logger;
        _parser = parser;
        _stateMachine = new BleStateMachine();
        _stateMachine.StateChanged += OnStateChanged;
    }
    
    public async Task StartScanAsync(int timeout = 30, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting BLE scan with timeout {Timeout}s", timeout);
        
        _scanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _stateMachine.TransitionTo(BleState.Scanning);
        
        try
        {
            // 使用BLE Advertisement Watcher扫描
            var watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            
            watcher.Received += OnAdvertisementReceived;
            watcher.Start();
            
            // 等待超时或取消
            await Task.Delay(TimeSpan.FromSeconds(timeout), _scanCts.Token);
            
            watcher.Stop();
            watcher.Received -= OnAdvertisementReceived;
            
            _stateMachine.TransitionTo(BleState.ScanComplete);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scan cancelled");
            _stateMachine.TransitionTo(BleState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            _stateMachine.TransitionTo(BleState.Error);
            throw;
        }
    }
    
    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        // 过滤华为设备
        var device = new BleDevice
        {
            Id = args.BluetoothAddress.ToString(),
            Name = args.Advertisement.LocalName,
            SignalStrength = args.RawSignalStrengthInDBm,
            ServiceUuids = args.Advertisement.ServiceUuids,
            LastSeen = DateTime.Now
        };
        
        // 只通知支持心率服务的设备
        if (device.SupportsHeartRate)
        {
            _logger.LogDebug("Found HR device: {Name} ({Id})", device.Name, device.Id);
            DeviceDiscovered?.Invoke(this, device);
        }
    }
    
    public async Task ConnectAsync(BleDevice device, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to device: {Name} ({Id})", device.Name, device.Id);
        
        _stateMachine.TransitionTo(BleState.Connecting);
        
        try
        {
            // 获取BLE设备
            _bluetoothDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(
                Convert.ToUInt64(device.Id));
            
            if (_bluetoothDevice == null)
            {
                throw new DeviceNotFoundException(device.Id);
            }
            
            // 监听连接状态变化
            _bluetoothDevice.ConnectionStatusChanged += OnConnectionStatusChanged;
            
            ConnectedDevice = device;
            _stateMachine.TransitionTo(BleState.Connected);
            
            // 发现服务并订阅
            await DiscoverAndSubscribeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection failed");
            _stateMachine.TransitionTo(BleState.Error);
            throw;
        }
    }
    
    private async Task DiscoverAndSubscribeAsync()
    {
        _stateMachine.TransitionTo(BleState.DiscoveringServices);
        
        // 获取GATT服务
        var serviceResult = await _bluetoothDevice.GetGattServicesAsync();
        
        if (serviceResult.Status != GattCommunicationStatus.Success)
        {
            throw new BleConnectionException(
                ConnectedDevice.Id, 
                "Failed to discover services");
        }
        
        // 查找心率服务
        var heartRateService = serviceResult.Services
            .FirstOrDefault(s => s.Uuid == HeartRateServiceUuid);
        
        if (heartRateService == null)
        {
            throw new BleConnectionException(
                ConnectedDevice.Id, 
                "Heart Rate Service not found");
        }
        
        // 获取心率测量特征
        var characteristicResult = await heartRateService.GetCharacteristicsAsync();
        
        _heartRateCharacteristic = characteristicResult.Characteristics
            .FirstOrDefault(c => c.Uuid == HeartRateMeasurementUuid);
        
        if (_heartRateCharacteristic == null)
        {
            throw new BleConnectionException(
                ConnectedDevice.Id, 
                "Heart Rate Measurement characteristic not found");
        }
        
        // 订阅通知
        _stateMachine.TransitionTo(BleState.Subscribing);
        
        _heartRateCharacteristic.ValueChanged += OnHeartRateValueChanged;
        
        var status = await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);
        
        if (status != GattCommunicationStatus.Success)
        {
            throw new BleConnectionException(
                ConnectedDevice.Id, 
                "Failed to subscribe to heart rate notifications");
        }
        
        // 读取电池电量
        await ReadBatteryLevelAsync();
        
        _stateMachine.TransitionTo(BleState.Receiving);
        _logger.LogInformation("Successfully connected and subscribed");
    }
    
    private void OnHeartRateValueChanged(
        GattCharacteristic sender,
        GattValueChangedEventArgs args)
    {
        try
        {
            var data = args.CharacteristicValue.ToArray();
            var heartRateData = _parser.Parse(data);
            heartRateData.DeviceId = ConnectedDevice.Id;
            heartRateData.Timestamp = DateTime.Now;
            
            _logger.LogDebug("Received heart rate: {HeartRate} BPM", heartRateData.HeartRate);
            HeartRateReceived?.Invoke(this, heartRateData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse heart rate data");
        }
    }
    
    private async Task ReadBatteryLevelAsync()
    {
        try
        {
            var serviceResult = await _bluetoothDevice.GetGattServicesAsync();
            var batteryService = serviceResult.Services
                .FirstOrDefault(s => s.Uuid == BatteryServiceUuid);
            
            if (batteryService != null)
            {
                var charResult = await batteryService.GetCharacteristicsAsync();
                var batteryChar = charResult.Characteristics
                    .FirstOrDefault(c => c.Uuid == BatteryLevelUuid);
                
                if (batteryChar != null)
                {
                    var valueResult = await batteryChar.ReadValueAsync();
                    if (valueResult.Status == GattCommunicationStatus.Success)
                    {
                        var data = valueResult.Value.ToArray();
                        ConnectedDevice.BatteryLevel = data[0];
                        _logger.LogInformation("Battery level: {Level}%", data[0]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read battery level");
        }
    }
    
    public async Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting from device");
        
        _stateMachine.TransitionTo(BleState.Disconnecting);
        
        try
        {
            if (_heartRateCharacteristic != null)
            {
                _heartRateCharacteristic.ValueChanged -= OnHeartRateValueChanged;
                await _heartRateCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            
            if (_bluetoothDevice != null)
            {
                _bluetoothDevice.ConnectionStatusChanged -= OnConnectionStatusChanged;
                _bluetoothDevice.Dispose();
                _bluetoothDevice = null;
            }
            
            ConnectedDevice = null;
            _stateMachine.TransitionTo(BleState.Idle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disconnect failed");
            _stateMachine.TransitionTo(BleState.Error);
        }
    }
    
    private void OnConnectionStatusChanged(
        BluetoothLEDevice sender,
        object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
        {
            _logger.LogWarning("Device disconnected unexpectedly");
            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                OldState = ConnectionState.Connected,
                NewState = ConnectionState.Disconnected,
                DeviceId = ConnectedDevice?.Id
            });
        }
    }
}
```

### 4.2 HeartRateParser 实现

```csharp
public class HeartRateParser : IHeartRateParser
{
    public HeartRateData Parse(byte[] data)
    {
        if (data == null || data.Length < 2)
        {
            throw new ArgumentException("Invalid heart rate data");
        }
        
        var result = new HeartRateData();
        var offset = 0;
        
        // 解析Flags
        var flags = data[offset++];
        
        bool isUint16 = (flags & 0x01) != 0;
        bool hasSensorContact = (flags & 0x02) != 0;
        bool sensorContactSupported = (flags & 0x04) != 0;
        bool hasEnergyExpended = (flags & 0x08) != 0;
        bool hasRRInterval = (flags & 0x10) != 0;
        
        // 解析心率值
        if (isUint16)
        {
            result.HeartRate = BitConverter.ToUInt16(data, offset);
            offset += 2;
        }
        else
        {
            result.HeartRate = data[offset++];
        }
        
        // 解析传感器接触状态
        if (sensorContactSupported)
        {
            result.SensorContact = hasSensorContact;
        }
        
        // 解析能量消耗
        if (hasEnergyExpended)
        {
            result.EnergyExpended = BitConverter.ToUInt16(data, offset);
            offset += 2;
        }
        
        // 解析RR间隔
        if (hasRRInterval)
        {
            var rrIntervals = new List<double>();
            while (offset + 1 < data.Length)
            {
                var rrRaw = BitConverter.ToUInt16(data, offset);
                var rrMs = rrRaw / 1024.0 * 1000.0; // 转换为毫秒
                rrIntervals.Add(rrMs);
                offset += 2;
            }
            result.RRIntervals = rrIntervals.AsReadOnly();
        }
        
        return result;
    }
    
    public bool IsValidFormat(byte[] data)
    {
        if (data == null || data.Length < 2)
            return false;
        
        var flags = data[0];
        var isUint16 = (flags & 0x01) != 0;
        
        // 检查最小长度
        var minLength = isUint16 ? 3 : 2;
        if (data.Length < minLength)
            return false;
        
        // 检查心率值是否合理
        int heartRate;
        if (isUint16)
        {
            heartRate = BitConverter.ToUInt16(data, 1);
        }
        else
        {
            heartRate = data[1];
        }
        
        return heartRate >= 0 && heartRate <= 255;
    }
}
```

## 5. 自动重连机制

### 5.1 重连策略

```csharp
public class AutoReconnectManager
{
    private readonly IBleService _bleService;
    private readonly ILogger<AutoReconnectManager> _logger;
    private readonly ISettingsService _settingsService;
    
    private CancellationTokenSource _reconnectCts;
    private int _reconnectAttempts;
    
    public bool IsEnabled { get; set; } = true;
    
    public AutoReconnectManager(
        IBleService bleService,
        ILogger<AutoReconnectManager> logger,
        ISettingsService settingsService)
    {
        _bleService = bleService;
        _logger = logger;
        _settingsService = settingsService;
        
        _bleService.ConnectionStateChanged += OnConnectionStateChanged;
    }
    
    private async void OnConnectionStateChanged(
        object sender, 
        ConnectionStateChangedEventArgs e)
    {
        if (!IsEnabled)
            return;
        
        if (e.NewState == ConnectionState.Disconnected)
        {
            await StartReconnectAsync(e.DeviceId);
        }
        else if (e.NewState == ConnectionState.Connected)
        {
            StopReconnect();
        }
    }
    
    private async Task StartReconnectAsync(string deviceId)
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        _reconnectAttempts = 0;
        
        var maxAttempts = _settingsService.GetValue<int>("Ble.MaxReconnectAttempts", 3);
        var interval = _settingsService.GetValue<int>("Ble.ReconnectInterval", 5);
        
        _logger.LogInformation(
            "Starting auto-reconnect for device {DeviceId}", deviceId);
        
        try
        {
            while (_reconnectAttempts < maxAttempts && 
                   !_reconnectCts.Token.IsCancellationRequested)
            {
                _reconnectAttempts++;
                
                _logger.LogInformation(
                    "Reconnect attempt {Attempt}/{MaxAttempts}", 
                    _reconnectAttempts, maxAttempts);
                
                try
                {
                    var device = new BleDevice { Id = deviceId };
                    await _bleService.ConnectAsync(device, _reconnectCts.Token);
                    
                    _logger.LogInformation("Reconnected successfully");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex, 
                        "Reconnect attempt {Attempt} failed", 
                        _reconnectAttempts);
                    
                    if (_reconnectAttempts < maxAttempts)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(interval), 
                            _reconnectCts.Token);
                    }
                }
            }
            
            _logger.LogError(
                "Failed to reconnect after {Attempts} attempts", 
                maxAttempts);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Reconnect cancelled");
        }
    }
    
    private void StopReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectAttempts = 0;
    }
    
    public void Dispose()
    {
        _reconnectCts?.Cancel();
        _bleService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
```

## 6. 设备管理

### 6.1 设备过滤器

```csharp
public class DeviceFilterBuilder
{
    private string _nameFilter;
    private List<Guid> _serviceUuids = new();
    private int? _minSignalStrength;
    
    public DeviceFilterBuilder WithNameFilter(string name)
    {
        _nameFilter = name;
        return this;
    }
    
    public DeviceFilterBuilder WithHeartRateService()
    {
        _serviceUuids.Add(BluetoothUuidHelper.FromShortId(0x180D));
        return this;
    }
    
    public DeviceFilterBuilder WithMinSignalStrength(int rssi)
    {
        _minSignalStrength = rssi;
        return this;
    }
    
    public DeviceFilter Build()
    {
        return new DeviceFilter
        {
            NameFilter = _nameFilter,
            ServiceUuids = _serviceUuids,
            MinSignalStrength = _minSignalStrength
        };
    }
}

// 使用示例
var filter = new DeviceFilterBuilder()
    .WithHeartRateService()
    .WithMinSignalStrength(-80)
    .Build();
```

### 6.2 设备缓存

```csharp
public class DeviceCache
{
    private readonly Dictionary<string, BleDevice> _devices = new();
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(5);
    
    public void Update(BleDevice device)
    {
        _devices[device.Id] = device;
        CleanupExpired();
    }
    
    public BleDevice Get(string deviceId)
    {
        if (_devices.TryGetValue(deviceId, out var device))
        {
            if (DateTime.Now - device.LastSeen < _cacheTimeout)
            {
                return device;
            }
            _devices.Remove(deviceId);
        }
        return null;
    }
    
    public IReadOnlyList<BleDevice> GetAll()
    {
        CleanupExpired();
        return _devices.Values.ToList().AsReadOnly();
    }
    
    private void CleanupExpired()
    {
        var expired = _devices
            .Where(d => DateTime.Now - d.Value.LastSeen >= _cacheTimeout)
            .Select(d => d.Key)
            .ToList();
        
        foreach (var key in expired)
        {
            _devices.Remove(key);
        }
    }
}
```

## 7. 错误处理

### 7.1 BLE异常定义

```csharp
public class BleConnectionException : AppException
{
    public string DeviceId { get; }
    
    public BleConnectionException(string deviceId, string message)
        : base(message)
    {
        DeviceId = deviceId;
    }
    
    public BleConnectionException(string deviceId, string message, Exception inner)
        : base(message, inner)
    {
        DeviceId = deviceId;
    }
}

public class DeviceNotFoundException : AppException
{
    public DeviceNotFoundException(string deviceId)
        : base($"Device not found: {deviceId}") { }
}

public class ServiceDiscoveryException : AppException
{
    public ServiceDiscoveryException(string deviceId, string message)
        : base(message) { }
}

public class SubscriptionException : AppException
{
    public SubscriptionException(string deviceId, string message)
        : base(message) { }
}
```

### 7.2 错误恢复策略

```csharp
public class BleErrorRecovery
{
    private readonly ILogger _logger;
    
    public async Task<bool> TryRecoverAsync(Exception ex, BleDevice device)
    {
        _logger.LogInformation("Attempting error recovery for: {Error}", ex.Message);
        
        return ex switch
        {
            BleConnectionException => await TryReconnectAsync(device),
            DeviceNotFoundException => await TryRediscoverAsync(device),
            ServiceDiscoveryException => await RetryServiceDiscoveryAsync(device),
            SubscriptionException => await RetrySubscriptionAsync(device),
            _ => false
        };
    }
    
    private async Task<bool> TryReconnectAsync(BleDevice device)
    {
        // 尝试重新连接
        await Task.Delay(1000);
        return true;
    }
    
    private async Task<bool> TryRediscoverAsync(BleDevice device)
    {
        // 尝试重新发现设备
        await Task.Delay(2000);
        return true;
    }
    
    private async Task<bool> RetryServiceDiscoveryAsync(BleDevice device)
    {
        // 重试服务发现
        await Task.Delay(1000);
        return true;
    }
    
    private async Task<bool> RetrySubscriptionAsync(BleDevice device)
    {
        // 重试订阅
        await Task.Delay(500);
        return true;
    }
}
```

## 8. 性能优化

### 8.1 数据缓冲

```csharp
public class HeartRateDataBuffer
{
    private readonly ConcurrentQueue<HeartRateData> _buffer = new();
    private readonly int _maxSize;
    
    public HeartRateDataBuffer(int maxSize = 300)
    {
        _maxSize = maxSize;
    }
    
    public void Add(HeartRateData data)
    {
        _buffer.Enqueue(data);
        
        while (_buffer.Count > _maxSize)
        {
            _buffer.TryDequeue(out _);
        }
    }
    
    public IReadOnlyList<HeartRateData> GetAll()
    {
        return _buffer.ToArray().AsReadOnly();
    }
    
    public void Clear()
    {
        _buffer.Clear();
    }
}
```

### 8.2 批量处理

```csharp
public class BatchProcessor<T>
{
    private readonly List<T> _batch = new();
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;
    private readonly Func<List<T>, Task> _processFunc;
    private Timer _timer;
    
    public BatchProcessor(
        int batchSize,
        TimeSpan flushInterval,
        Func<List<T>, Task> processFunc)
    {
        _batchSize = batchSize;
        _flushInterval = flushInterval;
        _processFunc = processFunc;
        
        _timer = new Timer(OnTimerTick, null, _flushInterval, _flushInterval);
    }
    
    public void Add(T item)
    {
        lock (_batch)
        {
            _batch.Add(item);
            
            if (_batch.Count >= _batchSize)
            {
                FlushAsync().ConfigureAwait(false);
            }
        }
    }
    
    private async Task FlushAsync()
    {
        List<T> toProcess;
        
        lock (_batch)
        {
            if (_batch.Count == 0) return;
            toProcess = new List<T>(_batch);
            _batch.Clear();
        }
        
        await _processFunc(toProcess);
    }
    
    private async void OnTimerTick(object state)
    {
        await FlushAsync();
    }
}
```

## 9. 日志与诊断

### 9.1 BLE诊断信息

```csharp
public class BleDiagnostics
{
    public static string GetDiagnosticInfo(BluetoothLEDevice device)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Device ID: {device.BluetoothAddress}");
        sb.AppendLine($"Device Name: {device.Name}");
        sb.AppendLine($"Connection Status: {device.ConnectionStatus}");
        sb.AppendLine($"Device ID: {device.DeviceId}");
        
        return sb.ToString();
    }
    
    public static string GetServiceInfo(GattDeviceService service)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Service UUID: {service.Uuid}");
        sb.AppendLine($"Attribute Handle: {service.AttributeHandle}");
        
        return sb.ToString();
    }
    
    public static string GetCharacteristicInfo(GattCharacteristic characteristic)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"Characteristic UUID: {characteristic.Uuid}");
        sb.AppendLine($"Characteristic Properties: {characteristic.CharacteristicProperties}");
        sb.AppendLine($"Attribute Handle: {characteristic.AttributeHandle}");
        
        return sb.ToString();
    }
}
```
