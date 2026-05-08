using System.Collections.Concurrent;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== BLE 心率设备扫描测试工具 ===\n");

var heartRateServiceUuid = new Guid("0000180d-0000-1000-8000-00805f9b34fb");
var heartRateMeasurementUuid = new Guid("00002a37-0000-1000-8000-00805f9b34fb");

var allDevices = new ConcurrentDictionary<string, (string Name, ulong Address, int Rssi, bool Connectable, DateTimeOffset Timestamp, bool HasHrService)>();

Console.WriteLine("[步骤1] 检查蓝牙适配器...");
var adapter = await BluetoothAdapter.GetDefaultAsync();
if (adapter == null)
{
    Console.WriteLine("✗ 未找到蓝牙适配器！请确认电脑有蓝牙功能且已开启。");
    return;
}
Console.WriteLine($"✓ 蓝牙适配器就绪: {adapter.DeviceId}");
Console.WriteLine($"  经典蓝牙: {adapter.IsClassicSupported}");
Console.WriteLine($"  低功耗蓝牙: {adapter.IsLowEnergySupported}");
Console.WriteLine($"  广播支持: {adapter.IsAdvertisementOffloadSupported}");
Console.WriteLine();

Console.WriteLine("[步骤2] 启动无过滤扫描（扫描所有BLE设备，持续45秒）...");
Console.WriteLine("───────────────────────────────────────────────");

int totalCount = 0;
int withNameCount = 0;
int withHrServiceCount = 0;

var watcher = new BluetoothLEAdvertisementWatcher
{
    ScanningMode = BluetoothLEScanningMode.Active
};

watcher.Received += (sender, args) =>
{
    totalCount++;
    var localName = args.Advertisement.LocalName;
    var hasName = !string.IsNullOrEmpty(localName);
    if (hasName) withNameCount++;

    var serviceUuids = args.Advertisement.ServiceUuids;
    var hasHrService = serviceUuids.Contains(heartRateServiceUuid);
    if (hasHrService) withHrServiceCount++;

    var key = args.BluetoothAddress.ToString();
    var newVal = (hasName ? localName! : "(未知)", args.BluetoothAddress, args.RawSignalStrengthInDBm, args.IsConnectable, DateTimeOffset.Now, hasHrService);
    allDevices.AddOrUpdate(key, newVal, (_, existing) =>
    {
        var name = existing.Name != "(未知)" ? existing.Name : (hasName ? localName! : "(未知)");
        return (name, args.BluetoothAddress, Math.Max(existing.Rssi, args.RawSignalStrengthInDBm), args.IsConnectable, DateTimeOffset.Now, existing.HasHrService || hasHrService);
    });

    if (hasName)
    {
        var hrTag = hasHrService ? " [含心率服务UUID]" : "";
        Console.WriteLine($"  #{totalCount,-4} 发现: {localName} | 地址: {args.BluetoothAddress} | 信号: {args.RawSignalStrengthInDBm} dBm | 可连接: {args.IsConnectable}{hrTag}");
    }
};

watcher.Stopped += (sender, args) =>
{
    Console.WriteLine($"\n  扫描停止，原因: {args.Error}");
};

watcher.Start();
Console.WriteLine("  扫描中... 请确保手环在电脑附近且蓝牙已开启\n");

await Task.Delay(45000);

watcher.Stop();

Console.WriteLine("\n───────────────────────────────────────────────");
Console.WriteLine($"[扫描结果汇总]");
Console.WriteLine($"  总广播接收次数: {totalCount}");
Console.WriteLine($"  有名称的设备广播: {withNameCount}");
Console.WriteLine($"  含心率服务UUID的广播: {withHrServiceCount}");
Console.WriteLine($"  唯一设备数量: {allDevices.Count}");
Console.WriteLine();

Console.WriteLine("[所有发现的设备列表]");
Console.WriteLine("───────────────────────────────────────────────");

var sorted = allDevices.Values.OrderByDescending(d => d.Timestamp).ToList();
int index = 1;
foreach (var d in sorted)
{
    var hrTag = d.HasHrService ? " ★心率★" : "";
    Console.WriteLine($"  {index,3}. {d.Name,-30} | 地址: {d.Address,-20} | 信号: {d.Rssi,4} dBm | 可连接: {d.Connectable}{hrTag}");
    index++;
}

Console.WriteLine("\n───────────────────────────────────────────────");
Console.WriteLine("[步骤3] 查找可连接的心率设备并尝试连接...");

var hrDevices = sorted.Where(d => d.HasHrService).ToList();
if (hrDevices.Count == 0)
{
    Console.WriteLine("  ⚠ 未发现包含心率服务UUID的设备！");

    var connectableDevices = sorted.Where(d => d.Connectable && d.Name != "(未知)").ToList();
    if (connectableDevices.Count > 0)
    {
        Console.WriteLine($"\n  但发现了 {connectableDevices.Count} 个有名称的可连接设备，尝试逐一连接并查询GATT服务...\n");

        foreach (var device in connectableDevices)
        {
            Console.WriteLine($"  → 尝试连接: {device.Name} (地址: {device.Address})...");
            try
            {
                var bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(device.Address);
                if (bleDevice == null)
                {
                    Console.WriteLine($"    ✗ 无法连接（返回null）");
                    continue;
                }

                Console.WriteLine($"    ✓ 已连接，正在查询GATT服务...");
                var servicesResult = await bleDevice.GetGattServicesAsync();
                if (servicesResult.Status == GattCommunicationStatus.Success)
                {
                    Console.WriteLine($"    发现 {servicesResult.Services.Count} 个服务:");
                    foreach (var svc in servicesResult.Services)
                    {
                        var isHr = svc.Uuid == heartRateServiceUuid;
                        var tag = isHr ? " ★心率服务★" : "";
                        Console.WriteLine($"      - {svc.Uuid}{tag}");

                        if (isHr)
                        {
                            Console.WriteLine($"\n    ✅ 找到心率服务！正在读取特征值...");
                            var chars = await svc.GetCharacteristicsAsync();
                            foreach (var ch in chars.Characteristics)
                            {
                                Console.WriteLine($"      特征值: {ch.Uuid} | 属性: {ch.CharacteristicProperties}");

                                if (ch.Uuid == heartRateMeasurementUuid)
                                {
                                    Console.WriteLine($"      ✓ 这是心率测量特征值！尝试启用通知...");
                                    var status = await ch.WriteClientCharacteristicConfigurationDescriptorAsync(
                                        GattClientCharacteristicConfigurationDescriptorValue.Notify);
                                    Console.WriteLine($"      通知启用结果: {status}");

                                    if (status == GattCommunicationStatus.Success)
                                    {
                                        var received = false;
                                        ch.ValueChanged += (s, e) =>
                                        {
                                            received = true;
                                            var bytes = new byte[e.CharacteristicValue.Length];
                                            using var reader = DataReader.FromBuffer(e.CharacteristicValue);
                                            reader.ReadBytes(bytes);

                                            var flags = bytes[0];
                                            var hr = (flags & 0x01) == 0 ? bytes[1] : BitConverter.ToUInt16(bytes, 1);
                                            Console.WriteLine($"\n      ❤ 收到心率数据: {hr} BPM (原始: {BitConverter.ToString(bytes)})");
                                        };

                                        Console.WriteLine("      等待心率数据 (10秒)...");
                                        await Task.Delay(10000);

                                        if (!received)
                                        {
                                            Console.WriteLine("      ⚠ 未收到心率数据（可能需要在手环上启动心率监测）");
                                        }

                                        await ch.WriteClientCharacteristicConfigurationDescriptorAsync(
                                            GattClientCharacteristicConfigurationDescriptorValue.None);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"    ✗ GATT服务查询失败: {servicesResult.Status}");
                }

                bleDevice.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ 连接失败: {ex.Message}");
            }

            Console.WriteLine();
        }
    }
    else
    {
        Console.WriteLine("  ⚠ 也没有有名称的可连接设备。");
        Console.WriteLine("  可能原因:");
        Console.WriteLine("    1. 手环蓝牙已关闭");
        Console.WriteLine("    2. 手环不在范围内");
        Console.WriteLine("    3. 手环已与其他设备配对（需要先取消配对）");
        Console.WriteLine("    4. Windows蓝牙驱动问题");
    }
}
else
{
    Console.WriteLine($"  发现 {hrDevices.Count} 个心率设备，尝试连接第一个...");
    var target = hrDevices[0];
    Console.WriteLine($"  → 目标: {target.Name} (地址: {target.Address})");

    try
    {
        var bleDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(target.Address);
        if (bleDevice != null)
        {
            Console.WriteLine($"  ✓ 连接成功: {bleDevice.Name}");
            var servicesResult = await bleDevice.GetGattServicesAsync();
            if (servicesResult.Status == GattCommunicationStatus.Success)
            {
                Console.WriteLine($"  发现 {servicesResult.Services.Count} 个GATT服务");
                foreach (var svc in servicesResult.Services)
                {
                    Console.WriteLine($"    - {svc.Uuid}" + (svc.Uuid == heartRateServiceUuid ? " ★心率★" : ""));
                }
            }
            bleDevice.Dispose();
        }
        else
        {
            Console.WriteLine("  ✗ 连接返回null");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ 连接失败: {ex.Message}");
    }
}

Console.WriteLine("\n=== 测试完成 ===");
Console.WriteLine("按任意键退出...");
Console.ReadKey();
