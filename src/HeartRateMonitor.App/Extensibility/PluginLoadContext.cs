using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace HeartRateMonitor.App.Extensibility;

/// <summary>
/// 每个外部插件一个可回收的加载上下文：
/// 宿主程序集（HeartRateMonitor.*）统一交给默认上下文解析，插件私有依赖从插件目录解析，
/// 这样卸载插件时可以整体回收，避免把第三方程序集泄漏进宿主。
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private const string HostAssemblyPrefix = "HeartRateMonitor.";

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base($"Plugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name is null) return null;

        // 插件契约与核心程序集必须与宿主共享同一份类型标识
        if (name.StartsWith(HostAssemblyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
