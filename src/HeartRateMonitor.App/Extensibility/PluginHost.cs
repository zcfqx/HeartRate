using System.IO;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Extensibility;

namespace HeartRateMonitor.App.Extensibility;

/// <summary>
/// 插件宿主实现：内置插件 + 用户插件目录中的外部插件。
/// <para>
/// 插件生命周期：扫描 →（启用）Register → 贡献组件/命令/设置页 →（禁用）Shutdown + 卸载上下文。
/// 启用状态持久化在设置的 <c>DisabledPlugins</c> 中（分号分隔），因此重启后仍保持用户选择。
/// </para>
/// </summary>
internal sealed class PluginHost : IPluginHost
{
    private const char Separator = ';';

    private readonly IEnumerable<IAppPlugin> _builtins;
    private readonly PluginContextFactory _contextFactory;
    private readonly ISettingsService _settings;
    private readonly ILogger _log;

    private readonly List<LoadedPlugin> _loaded = [];
    private readonly List<PluginInfo> _infos = [];
    private readonly List<WidgetDescriptor> _widgets = [];
    private readonly List<CommandDescriptor> _commands = [];
    private readonly List<SettingsSectionDescriptor> _sections = [];
    private readonly HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;

    public PluginHost(
        IEnumerable<IAppPlugin> builtins,
        PluginContextFactory contextFactory,
        ISettingsService settings,
        ILogger log)
    {
        _builtins = builtins;
        _contextFactory = contextFactory;
        _settings = settings;
        _log = log;

        PluginsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HeartRateMonitor", "plugins");
    }

    public string PluginsDirectory { get; }

    public IReadOnlyList<PluginInfo> Plugins => _infos;

    public IReadOnlyList<WidgetDescriptor> Widgets => _widgets;

    public IReadOnlyList<CommandDescriptor> Commands => _commands;

    public IReadOnlyList<SettingsSectionDescriptor> SettingsSections => _sections;

    public event EventHandler? PluginsChanged;

    public Task InitializeAsync()
    {
        if (_initialized) return Task.CompletedTask;
        _initialized = true;

        try
        {
            Directory.CreateDirectory(PluginsDirectory);
        }
        catch (Exception ex)
        {
            _log.Warning($"无法创建插件目录: {ex.Message}");
        }

        LoadDisabledPlugins();
        DiscoverBuiltins();
        ScanExternalPlugins();
        Rebuild();
        RaiseChanged();

        _log.Info($"插件宿主初始化完成，共 {_infos.Count} 个插件，{_widgets.Count} 个组件");
        return Task.CompletedTask;
    }

    public async Task EnableAsync(string pluginId)
    {
        var plugin = Find(pluginId);
        if (plugin is null) return;

        _disabled.Remove(plugin.Id);

        if (plugin.Source == PluginSource.External)
        {
            LoadExternalPlugin(plugin);
        }
        else if (!plugin.IsEnabled)
        {
            Activate(plugin);
        }

        await PersistAsync();
        Rebuild();
        RaiseChanged();
    }

    public async Task DisableAsync(string pluginId)
    {
        var plugin = Find(pluginId);
        if (plugin is null) return;

        Deactivate(plugin);
        _disabled.Add(plugin.Id);

        await PersistAsync();
        Rebuild();
        RaiseChanged();
    }

    public async Task RescanAsync()
    {
        LoadDisabledPlugins();
        DiscoverBuiltins();
        ScanExternalPlugins();
        Rebuild();

        await PersistAsync();
        RaiseChanged();
    }

    public async Task ReloadAsync(string pluginId)
    {
        var plugin = Find(pluginId);
        if (plugin is null) return;

        if (plugin.Source == PluginSource.External)
        {
            Deactivate(plugin);
            _disabled.Remove(plugin.Id);
            LoadExternalPlugin(plugin);
        }
        else
        {
            Deactivate(plugin);
            Activate(plugin);
        }

        await PersistAsync();
        Rebuild();
        RaiseChanged();
    }

    public async Task<bool> UninstallAsync(string pluginId)
    {
        var plugin = Find(pluginId);
        if (plugin is null || plugin.Source != PluginSource.External) return false;

        var directory = Path.GetFullPath(plugin.Location);
        var root = Path.GetFullPath(PluginsDirectory) + Path.DirectorySeparatorChar;
        if (!directory.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return false;

        Deactivate(plugin);
        _loaded.Remove(plugin);
        _disabled.Remove(plugin.Id);

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex)
        {
            _log.Error($"删除插件目录失败: {directory}", ex);
            return false;
        }

        await PersistAsync();
        Rebuild();
        RaiseChanged();
        return true;
    }

    // ================================ 内置 / 外部发现 ================================

    private void LoadDisabledPlugins()
    {
        _disabled.Clear();
        foreach (var id in (_settings.DisabledPlugins ?? string.Empty)
                     .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _disabled.Add(id);
        }
    }

    private void DiscoverBuiltins()
    {
        foreach (var plugin in _builtins)
        {
            var metadata = plugin.Metadata;
            if (string.IsNullOrWhiteSpace(metadata.Id)) continue;
            if (_loaded.Any(p => Matches(p.Id, metadata.Id))) continue;

            var loaded = new LoadedPlugin
            {
                Id = metadata.Id,
                Name = metadata.Name,
                Description = metadata.Description,
                Version = metadata.Version,
                Author = metadata.Author,
                Source = PluginSource.BuiltIn,
                Location = "内置",
                Instance = plugin
            };

            _loaded.Add(loaded);

            if (!_disabled.Contains(loaded.Id))
            {
                Activate(loaded);
            }
        }
    }

    private void ScanExternalPlugins()
    {
        // 重建外部插件列表：先整体卸载，再重新扫描目录（内置插件不受影响）
        foreach (var plugin in _loaded.Where(p => p.Source == PluginSource.External).ToList())
        {
            Deactivate(plugin);
            _loaded.Remove(plugin);
        }

        if (!Directory.Exists(PluginsDirectory)) return;

        foreach (var directory in Directory.EnumerateDirectories(PluginsDirectory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var manifest = PluginManifest.Load(directory, out var error);
            if (manifest is null)
            {
                var folderName = Path.GetFileName(directory);
                _loaded.Add(new LoadedPlugin
                {
                    Id = folderName,
                    Name = folderName,
                    Source = PluginSource.External,
                    Location = directory,
                    Error = error,
                    IsEnabled = false
                });
                _log.Warning($"跳过插件的目录 {folderName}: {error}");
                continue;
            }

            if (_loaded.Any(p => Matches(p.Id, manifest.Id))) continue;

            var plugin = new LoadedPlugin
            {
                Id = manifest.Id,
                Name = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name,
                Description = manifest.Description,
                Version = manifest.Version,
                Author = manifest.Author,
                Source = PluginSource.External,
                Location = directory,
                Manifest = manifest
            };

            _loaded.Add(plugin);

            if (!_disabled.Contains(plugin.Id))
            {
                LoadExternalPlugin(plugin);
            }
        }
    }

    private bool LoadExternalPlugin(LoadedPlugin plugin)
    {
        if (plugin.IsEnabled) return true;
        if (plugin.Manifest is null)
        {
            plugin.Error = "缺少插件清单";
            return false;
        }

        PluginLoadContext? loadContext = null;
        try
        {
            var assemblyPath = Path.Combine(plugin.Manifest.Directory, plugin.Manifest.EntryAssembly);
            if (!File.Exists(assemblyPath))
            {
                plugin.Error = $"入口程序集不存在: {plugin.Manifest.EntryAssembly}";
                return false;
            }

            loadContext = new PluginLoadContext(assemblyPath);
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(plugin.Manifest.EntryType, throwOnError: false)
                       ?? assembly.GetTypes().FirstOrDefault(t =>
                           typeof(IAppPlugin).IsAssignableFrom(t) && !t.IsAbstract);

            if (type is null || Activator.CreateInstance(type) is not IAppPlugin instance)
            {
                plugin.Error = $"无法创建插件入口: {plugin.Manifest.EntryType}";
                loadContext.Unload();
                return false;
            }

            plugin.LoadContext = loadContext;
            plugin.Instance = instance;

            var metadata = instance.Metadata;
            if (!string.IsNullOrWhiteSpace(metadata.Name)) plugin.Name = metadata.Name;
            if (!string.IsNullOrWhiteSpace(metadata.Description)) plugin.Description = metadata.Description;
            if (!string.IsNullOrWhiteSpace(metadata.Version)) plugin.Version = metadata.Version;
            if (!string.IsNullOrWhiteSpace(metadata.Author)) plugin.Author = metadata.Author;

            Activate(plugin);
            return plugin.IsEnabled;
        }
        catch (Exception ex)
        {
            plugin.Error = ex.Message;
            plugin.IsEnabled = false;
            _log.Error($"加载插件 {plugin.Id} 失败", ex);

            try
            {
                loadContext?.Unload();
            }
            catch
            {
                // 忽略卸载异常
            }

            return false;
        }
    }

    // ================================ 生命周期 ================================

    private void Activate(LoadedPlugin plugin)
    {
        if (plugin.Instance is null)
        {
            plugin.Error = "插件实例不可用";
            return;
        }

        try
        {
            ClearContributions(plugin);
            var context = _contextFactory.Create(plugin.Id);
            plugin.Context = context;
            plugin.Instance.Register(context, new Registrar(plugin));
            plugin.IsEnabled = true;
            plugin.Error = null;
            _log.Info($"插件已启用: {plugin.Name} ({plugin.Id})");
        }
        catch (Exception ex)
        {
            plugin.IsEnabled = false;
            plugin.Error = ex.Message;
            ClearContributions(plugin);
            _log.Error($"启用插件 {plugin.Id} 失败", ex);
        }
    }

    private void Deactivate(LoadedPlugin plugin)
    {
        try
        {
            plugin.Instance?.Shutdown();
        }
        catch (Exception ex)
        {
            _log.Warning($"插件 {plugin.Id} 关闭时出错: {ex.Message}");
        }

        ClearContributions(plugin);
        plugin.IsEnabled = false;

        if (plugin.Source == PluginSource.External)
        {
            plugin.Instance = null;
            if (plugin.LoadContext is { } context)
            {
                plugin.LoadContext = null;
                try
                {
                    context.Unload();
                }
                catch (Exception ex)
                {
                    _log.Warning($"卸载插件上下文失败: {ex.Message}");
                }
            }
        }
    }

    private static void ClearContributions(LoadedPlugin plugin)
    {
        plugin.Widgets.Clear();
        plugin.Commands.Clear();
        plugin.Sections.Clear();
    }

    // ================================ 聚合 / 持久化 ================================

    private void Rebuild()
    {
        _widgets.Clear();
        _commands.Clear();
        _sections.Clear();

        foreach (var plugin in _loaded.Where(p => p.IsEnabled))
        {
            foreach (var widget in plugin.Widgets)
            {
                if (_widgets.All(w => !string.Equals(w.Id, widget.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _widgets.Add(widget);
                }
            }

            foreach (var command in plugin.Commands)
            {
                if (_commands.All(c => !string.Equals(c.Id, command.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _commands.Add(command);
                }
            }

            foreach (var section in plugin.Sections)
            {
                if (_sections.All(s => !string.Equals(s.Id, section.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _sections.Add(section);
                }
            }
        }

        _widgets.Sort((a, b) =>
        {
            var order = a.Order.CompareTo(b.Order);
            return order != 0 ? order : string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase);
        });

        _sections.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

        _infos.Clear();
        _infos.AddRange(_loaded
            .Select(p => p.ToInfo())
            .OrderBy(p => p.Source)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase));
    }

    private async Task PersistAsync()
    {
        _settings.DisabledPlugins = string.Join(Separator, _disabled.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));

        try
        {
            await _settings.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warning($"保存插件启用状态失败: {ex.Message}");
        }
    }

    private LoadedPlugin? Find(string pluginId)
        => _loaded.FirstOrDefault(p => Matches(p.Id, pluginId));

    private static bool Matches(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private void RaiseChanged() => PluginsChanged?.Invoke(this, EventArgs.Empty);

    // ================================ 内部类型 ================================

    private sealed class LoadedPlugin
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Description = string.Empty;
        public string Version = "1.0.0";
        public string Author = string.Empty;
        public PluginSource Source;
        public string Location = string.Empty;
        public bool IsEnabled;
        public string? Error;
        public IAppPlugin? Instance;
        public PluginLoadContext? LoadContext;
        public PluginManifest? Manifest;
        public IPluginContext? Context;

        public readonly List<WidgetDescriptor> Widgets = [];
        public readonly List<CommandDescriptor> Commands = [];
        public readonly List<SettingsSectionDescriptor> Sections = [];

        public PluginInfo ToInfo() => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Version = Version,
            Author = Author,
            Source = Source,
            Location = Location,
            IsEnabled = IsEnabled,
            Error = Error
        };
    }

    private sealed class Registrar : IPluginRegistrar
    {
        private readonly LoadedPlugin _plugin;

        public Registrar(LoadedPlugin plugin)
        {
            _plugin = plugin;
        }

        public void AddWidget(WidgetDescriptor widget)
        {
            widget.PluginId = _plugin.Id;
            _plugin.Widgets.Add(widget);
        }

        public void AddCommand(CommandDescriptor command)
        {
            command.PluginId = _plugin.Id;
            _plugin.Commands.Add(command);
        }

        public void AddSettingsSection(SettingsSectionDescriptor section)
        {
            section.PluginId = _plugin.Id;
            _plugin.Sections.Add(section);
        }
    }
}
