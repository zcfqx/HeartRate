using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;
using HeartRateMonitor.App.ViewModels;
using HeartRateMonitor.App.Views;
using HeartRateMonitor.App.Views.Settings;
using HeartRateMonitor.Core.Interfaces;
using HeartRateMonitor.Core.Models;
using HeartRateMonitor.Extensibility;
using Microsoft.Extensions.DependencyInjection;

namespace HeartRateMonitor.UiSmoke;

/// <summary>
/// 离线 UI 冒烟测试 + 截图渲染器。
/// <para>
/// 直接复用真实应用启动流程（单实例、数据库、设置、插件宿主、托盘），在 Dispatcher 内
/// 逐项检查资源字典、主题切换、插件启停、组件增删、极简模式与 0% 透明背景，并用
/// <see cref="RenderTargetBitmap"/> 把窗口渲染为 PNG（无需系统截图，不受桌面遮挡影响）。
/// </para>
/// <para>
/// 用法：<c>dotnet run --project tools/HeartRateMonitor.UiSmoke -- [输出目录]</c>。
/// 默认输出到仓库的 <c>docs/screenshots</c>；运行前请先退出正在运行的 HeartRateMonitor
/// （应用是单实例的，第二次启动会唤醒旧进程并让本进程退出）。
/// </para>
/// <para>
/// 本工具会临时修改主题、透明度、极简模式与看板布局以渲染不同状态，结束前会恢复原值。
/// </para>
/// </summary>
internal static class Program
{
    private static readonly List<string> Failures = [];
    private static string _shots = string.Empty;
    private static SettingsSnapshot? _original;

    [STAThread]
    private static int Main(string[] args)
    {
        _shots = ResolveShotsDirectory(args);
        Directory.CreateDirectory(_shots);
        Console.WriteLine($"截图输出目录：{_shots}");

        var app = new HeartRateMonitor.App.App();
        app.InitializeComponent();

        // 必须走 Application.Run()：WPF 自己的启动回调会执行 App.OnStartup，
        // 手动调用 OnStartup 会被再执行一次，单实例逻辑会把当前进程当成第二个实例。
        var result = 1;
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                result = RunChecks(app);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SMOKE CRASH：{ex}");
                result = 1;
            }
            finally
            {
                app.Shutdown(result);
            }
        };
        timer.Start();
        app.Run();
        return result;
    }

    private static int RunChecks(Application app)
    {
        Check("主题资源已合并", () =>
        {
            var theme = FindThemeDictionary(app) ?? throw new InvalidOperationException("未找到主题字典");
            var background = theme["Brush.Background"] as Brush
                ?? throw new InvalidOperationException("主题缺少 Brush.Background");
            Console.WriteLine($"  背景色：{background}");
        });

        Check("转换器资源", () =>
        {
            foreach (var key in new[] { "BoolToVisibility", "NullToVisibility", "InverseBool", "HeartRateToBrush", "HexToBrush", "SignalToOpacity", "IconKeyToGeometry" })
            {
                if (app.Resources[key] is null)
                {
                    throw new InvalidOperationException($"缺少转换器 {key}");
                }
            }
        });

        Check("强调色资源", () =>
        {
            foreach (var key in new[] { "Brush.Accent", "Brush.AccentSoft", "Brush.AccentDeep", "Brush.OnAccent", "Color.Accent" })
            {
                if (app.Resources[key] is null)
                {
                    throw new InvalidOperationException($"缺少强调色资源 {key}");
                }
            }
        });

        Check("深色与浅色主题可切换", () =>
        {
            var theme = FindThemeDictionary(app) ?? throw new InvalidOperationException("未找到主题字典");
            var isLight = theme.Source!.OriginalString.Contains("Light", StringComparison.OrdinalIgnoreCase);
            var target = isLight ? "Dark" : "Light";
            var before = ((SolidColorBrush)theme["Brush.Background"]).Color;
            EnsureTheme(app, target);
            var after = ((SolidColorBrush)theme["Brush.Background"]).Color;
            if (before == after)
            {
                throw new InvalidOperationException($"切换到 {target} 后背景色未变化");
            }

            Console.WriteLine($"  背景色：{before} -> {after}（{target}）");
            EnsureTheme(app, isLight ? "Light" : "Dark");
        });

        Check("Sparkline 自绘控件", () =>
        {
            var sparkline = new HeartRateMonitor.App.Controls.Sparkline { Width = 200, Height = 60 };
            sparkline.Measure(new Size(200, 60));
            sparkline.Arrange(new Rect(0, 0, 200, 60));
        });

        Check("DI 容器可构建", () =>
        {
            var services = new ServiceCollection();
            var configure = typeof(HeartRateMonitor.App.App)
                .GetMethod("ConfigureServices", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("未找到 ConfigureServices");
            configure.Invoke(null, [services]);
            using var validationProvider = services.BuildServiceProvider();
            if (validationProvider.GetService<IPluginHost>() is null)
            {
                throw new InvalidOperationException("DI 容器缺少 IPluginHost");
            }
        });

        // 复用真实启动创建的容器：与界面共用同一套单例（设置、插件宿主、主窗口等）。
        var provider = HeartRateMonitor.App.App.Services;
        if (provider is null)
        {
            Console.WriteLine("FAIL App.Services 为空（真实启动未完成，是否有另一个实例在运行？）");
            Failures.Add("App.Services 为空");
            return Finish();
        }

        Check("数据库初始化", () =>
        {
            var dataService = provider.GetRequiredService<IDataService>();
            Task.Run(() => dataService.InitializeAsync()).GetAwaiter().GetResult();
        });

        Check("设置加载", () =>
        {
            var settings = provider.GetRequiredService<ISettingsService>();
            Task.Run(() => settings.LoadAsync()).GetAwaiter().GetResult();
        });

        Check("插件宿主初始化", () =>
        {
            var host = provider.GetRequiredService<IPluginHost>();
            Task.Run(() => host.InitializeAsync()).GetAwaiter().GetResult();
            Console.WriteLine($"  插件：{string.Join(", ", host.Plugins.Select(p => $"{p.Id}({p.Source}, enabled={p.IsEnabled})"))}");
            Console.WriteLine($"  组件 {host.Widgets.Count} 个，设置页 {host.SettingsSections.Count} 个，命令 {host.Commands.Count} 个");
            if (host.Widgets.Count == 0)
            {
                throw new InvalidOperationException("没有发现任何组件");
            }
        });

        _original = SettingsSnapshot.Capture(provider);

        Check("主窗口与 MainViewModel", () =>
        {
            var window = provider.GetRequiredService<MainWindow>();
            if (window.DataContext is not MainViewModel)
            {
                throw new InvalidOperationException("主窗口 DataContext 不是 MainViewModel");
            }

            Console.WriteLine($"  主窗口宽度：{window.Width}");
        });

        Check("设置窗口与各设置页 XAML", () =>
        {
            var viewModel = provider.GetRequiredService<SettingsViewModel>();
            _ = new SettingsWindow(viewModel);
            _ = new AppearancePage();
            _ = new ConnectionPage();
            _ = new NotificationPage();
            _ = new DataPage();
            _ = new PluginsPage();
            _ = new AboutPage();
            Console.WriteLine($"  设置页数量：{viewModel.Sections.Count}");
        });

        Check("组件增删", () =>
        {
            var window = provider.GetRequiredService<MainWindow>();
            var viewModel = (MainViewModel)window.DataContext;
            var before = viewModel.Tiles.Count;
            var descriptor = viewModel.AvailableWidgets.FirstOrDefault()
                ?? throw new InvalidOperationException("组件库为空，无法添加组件");
            viewModel.AddWidgetCommand.Execute(descriptor);
            if (viewModel.Tiles.Count != before + 1)
            {
                throw new InvalidOperationException($"添加组件后数量应为 {before + 1}，实际 {viewModel.Tiles.Count}");
            }

            Console.WriteLine($"  添加组件：{descriptor.Id} -> {viewModel.Tiles.Count} 个卡片");
            viewModel.RemoveWidgetCommand.Execute(viewModel.Tiles.Last());
            if (viewModel.Tiles.Count != before)
            {
                throw new InvalidOperationException($"移除组件后数量应为 {before}，实际 {viewModel.Tiles.Count}");
            }
        });

        Check("插件禁用与启用", () =>
        {
            var host = provider.GetRequiredService<IPluginHost>();
            var breathing = host.Plugins.FirstOrDefault(p => p.Id == "breathing");
            if (breathing is null)
            {
                Console.WriteLine("  跳过：未安装外部插件 breathing（samples/Breathing）");
                return;
            }

            var before = host.Widgets.Count;
            Task.Run(() => host.DisableAsync(breathing.Id)).GetAwaiter().GetResult();
            if (host.Widgets.Count >= before)
            {
                throw new InvalidOperationException("禁用插件后其组件应被移除");
            }

            Task.Run(() => host.EnableAsync(breathing.Id)).GetAwaiter().GetResult();
            if (host.Widgets.Count != before)
            {
                throw new InvalidOperationException("重新启用插件后组件应恢复");
            }

            Console.WriteLine($"  插件启停正常：组件数 {before}");
        });

        Check("渲染界面截图", () => RenderScreenshots(provider));

        Check("恢复用户设置", () => RestoreSettings(provider));

        return Finish();
    }

    /// <summary>把看板布局、主题、极简模式调到固定状态后逐张渲染截图。</summary>
    private static void RenderScreenshots(IServiceProvider provider)
    {
        var host = provider.GetRequiredService<IPluginHost>();
        var settings = provider.GetRequiredService<ISettingsService>();
        var window = provider.GetRequiredService<MainWindow>();
        var viewModel = (MainViewModel)window.DataContext;
        var content = (FrameworkElement)window.Content;

        // 走真实的 ThemeManager.Apply 而不是只替换资源字典，这样 ThemeChanged 才会触发，
        // 窗口壳背景、边框阴影等代码里计算出的画刷才会跟着主题刷新。
        ApplyTheme(provider, "Dark");
        window.Show(); // 让 Loaded、样式与模板真正生效，避免无头未 Show 时延迟资源引用为 null
        Pump(window.Dispatcher);

        // 固定为标准三卡布局（默认布局），保证截图可复现；结束时恢复用户布局。
        SetLayout(host, viewModel, ["bpm", "chart", "device"]);
        Pump(window.Dispatcher);

        // 用户可能把应用留在了极简模式或全透明状态；渲染前统一回到看板视图与 92% 不透明度。
        settings.OverlayOpacity = 0.92;
        settings.NotifySettingsChanged();
        if (viewModel.IsMinimalMode)
        {
            viewModel.ToggleMinimalModeCommand.Execute(null);
        }

        Pump(window.Dispatcher);

        Render(content, 320, 620, "main-dark.png");

        viewModel.IsAddPanelOpen = true;
        Pump(window.Dispatcher);
        Render(content, 320, 620, "main-add-panel.png");
        viewModel.IsAddPanelOpen = false;
        Pump(window.Dispatcher);

        var settingsViewModel = provider.GetRequiredService<SettingsViewModel>();
        var settingsWindow = new SettingsWindow(settingsViewModel);
        var settingsContent = (FrameworkElement)settingsWindow.Content;
        var darkTitleBrightness = 0;
        Render(settingsContent, 780, 600, "settings-dark.png");
        var darkBitmap = RenderBitmap(settingsContent, 780, 600);
        darkTitleBrightness = MaxBrightness(darkBitmap, new Int32Rect(40, 16, 100, 32));
        Console.WriteLine($"  深色设置页标题最大亮度：{darkTitleBrightness}");
        if (darkTitleBrightness < 150)
        {
            throw new InvalidOperationException($"深色主题下标题过暗（最大亮度 {darkTitleBrightness}），疑似黑字回归");
        }

        ApplyTheme(provider, "Light");
        Pump(window.Dispatcher);
        Render(content, 320, 620, "main-light.png");
        Render(settingsContent, 780, 600, "settings-light.png");
        ApplyTheme(provider, "Dark");
        Pump(window.Dispatcher);
        // 极简模式：进入后普通视图应隐藏、极简视图可见。
        var normalView = (Grid)window.FindName("NormalView");
        var minimalView = (Grid)window.FindName("MinimalView");
        var shell = (Border)window.FindName("Shell");

        settings.OverlayOpacity = 0.92;
        settings.NotifySettingsChanged();
        if (!viewModel.IsMinimalMode)
        {
            viewModel.ToggleMinimalModeCommand.Execute(null);
        }

        Pump(window.Dispatcher);
        content.UpdateLayout();
        Pump(window.Dispatcher);

        if (normalView.Visibility != Visibility.Collapsed || minimalView.Visibility != Visibility.Visible)
        {
            throw new InvalidOperationException(
                $"极简模式视图状态异常：Normal={normalView.Visibility}, Minimal={minimalView.Visibility}");
        }

        // 极简模式视觉规范：红色渐变实心心形 + 双层心跳光环，只显示图标与数字。
        var heart = (System.Windows.Shapes.Path)minimalView.FindName("MinimalHeart");
        if (heart.Fill is not LinearGradientBrush heartFill)
        {
            throw new InvalidOperationException("极简模式心跳图标应为红色渐变填充");
        }

        if (heartFill.GradientStops.Count < 2 ||
            heartFill.GradientStops.All(s => s.Color.R < 180 || s.Color.R <= s.Color.B))
        {
            throw new InvalidOperationException("极简模式心跳图标渐变不是红色系");
        }

        if (heart.Effect is not DropShadowEffect glow || glow.Color.R < 180 || glow.BlurRadius < 10)
        {
            throw new InvalidOperationException("极简模式心跳图标缺少红色发光渲染效果");
        }

        var ripple = (Ellipse)minimalView.FindName("MinimalRipple");
        var rippleOuter = (Ellipse)minimalView.FindName("MinimalRippleOuter");
        if (ripple.Opacity > 0.001 || rippleOuter.Opacity > 0.001)
        {
            throw new InvalidOperationException("极简模式光环初始应完全隐藏，仅由心跳节拍动画驱动");
        }

        var bpmText = (TextBlock)minimalView.FindName("MinimalBpm");
        if (bpmText.Text != "--")
        {
            throw new InvalidOperationException($"未连接时心率数字应显示 --，实际为 {bpmText.Text}");
        }

        if (FindText(minimalView, "BPM") is not null)
        {
            throw new InvalidOperationException("极简模式只显示心跳图标与数字，不应出现 BPM 单位文字");
        }

        Render(content, 180, 300, "main-minimal.png");

        viewModel.MinimalShowChart = false;
        Pump(window.Dispatcher);
        Render(content, 180, 260, "main-minimal-nochart.png");
        viewModel.MinimalShowChart = true;
        Pump(window.Dispatcher);

        // 0% 透明：Shell 背景 Alpha 必须为 0（命中测试用 1/255 保留拖动与右键），同时文字清晰可见。
        settings.OverlayOpacity = 0;
        settings.NotifySettingsChanged();
        Pump(window.Dispatcher);
        var background = shell.Background as SolidColorBrush;
        if (background is null || background.Color.A > 1)
        {
            throw new InvalidOperationException($"不透明度 0 时背景 Alpha 应接近 0，实际 {background?.Color.A.ToString() ?? "null"}");
        }

        if (shell.Effect is not null)
        {
            throw new InvalidOperationException("背景完全透明时不应保留窗口阴影效果");
        }

        var transparent = RenderBitmap(content, 180, 300);
        var textBrightness = MaxBrightness(transparent, new Int32Rect(20, 90, 140, 70));
        Console.WriteLine($"  0% 透明时文字最大亮度：{textBrightness}");
        if (textBrightness < 150)
        {
            throw new InvalidOperationException($"背景完全透明时心率数字不可见（最大亮度 {textBrightness}）");
        }

        SavePng(transparent, Path.Combine(_shots, "main-minimal-transparent.png"));

        settings.OverlayOpacity = 0.92;
        settings.NotifySettingsChanged();
        Pump(window.Dispatcher);
        if (viewModel.IsMinimalMode)
        {
            viewModel.ToggleMinimalModeCommand.Execute(null);
        }

        Pump(window.Dispatcher);
    }

    /// <summary>把截图渲染期间改动的设置恢复到进入工具前的值。</summary>
    private static void RestoreSettings(IServiceProvider provider)
    {
        if (_original is null)
        {
            return;
        }

        var host = provider.GetRequiredService<IPluginHost>();
        var settings = provider.GetRequiredService<ISettingsService>();
        var window = provider.GetRequiredService<MainWindow>();
        var viewModel = (MainViewModel)window.DataContext;

        var ids = WidgetLayoutCodec.Parse(
            _original.Widgets,
            host.Widgets.Where(w => w.DefaultEnabled).Select(w => w.Id));
        SetLayout(host, viewModel, ids);

        settings.Theme = _original.Theme;
        settings.AccentColor = _original.AccentColor;
        settings.OverlayOpacity = _original.OverlayOpacity;
        settings.WindowWidth = _original.WindowWidth;
        settings.MinimalMode = _original.MinimalMode;
        settings.MinimalShowChart = _original.MinimalShowChart;
        settings.Widgets = WidgetLayoutCodec.Serialize(ids);
        settings.DisabledPlugins = _original.DisabledPlugins;
        ApplyTheme(provider, _original.Theme);
        settings.NotifySettingsChanged();
        Pump(window.Dispatcher);
        Task.Run(() => settings.SaveAsync()).GetAwaiter().GetResult();
        Console.WriteLine("  主题、透明度、极简模式与看板布局已恢复");
    }

    private static TextBlock? FindText(DependencyObject root, string text)
    {
        if (root is TextBlock block && block.Text == text)
        {
            return block;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var found = FindText(VisualTreeHelper.GetChild(root, i), text);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static void SetLayout(IPluginHost host, MainViewModel viewModel, IReadOnlyList<string> ids)
    {
        while (viewModel.Tiles.Count > 0)
        {
            viewModel.RemoveWidgetCommand.Execute(viewModel.Tiles[^1]);
        }

        foreach (var id in ids)
        {
            var descriptor = host.Widgets.FirstOrDefault(
                widget => string.Equals(widget.Id, id, StringComparison.OrdinalIgnoreCase));
            if (descriptor is not null)
            {
                viewModel.AddWidgetCommand.Execute(descriptor);
            }
        }
    }

    private static void Check(string name, Action action)
    {
        try
        {
            action();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception ex)
        {
            var actual = ex is TargetInvocationException { InnerException: not null } tie ? tie.InnerException! : ex;
            Failures.Add($"{name}：{actual.GetType().Name}：{actual.Message}");
            Console.WriteLine($"FAIL {name}：{actual.GetType().Name}：{actual.Message}");
            Console.WriteLine(actual.StackTrace);
        }
    }

    private static string ResolveShotsDirectory(string[] args)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            return Path.GetFullPath(args[0]);
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "docs", "screenshots");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return Path.Combine(Path.GetTempPath(), "hr-ui-shots");
    }

    private static ResourceDictionary? FindThemeDictionary(Application app)
        => app.Resources.MergedDictionaries.FirstOrDefault(
            dictionary => dictionary.Source?.OriginalString.Contains("Themes/Theme.", StringComparison.Ordinal) == true);

    private static void EnsureTheme(Application app, string theme)
        => FindThemeDictionary(app)!.Source = new Uri(
            $"pack://application:,,,/HeartRateMonitor.App;component/Themes/Theme.{theme}.xaml",
            UriKind.Absolute);

    /// <summary>
    /// 调用应用内部的 <c>ThemeManager.Apply</c>（internal，只能反射），它与设置页、启动流程
    /// 走同一条主题切换路径，会同时替换资源字典并触发 ThemeChanged。
    /// </summary>
    private static void ApplyTheme(IServiceProvider provider, string theme)
    {
        var settings = provider.GetRequiredService<ISettingsService>();
        var manager = typeof(MainWindow).Assembly.GetType("HeartRateMonitor.App.Infrastructure.ThemeManager", throwOnError: true)!;
        var apply = manager.GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("未找到 ThemeManager.Apply");
        apply.Invoke(null, [theme, settings.AccentColor]);
    }

    private static void Pump(Dispatcher dispatcher) => dispatcher.Invoke(() => { }, DispatcherPriority.Background);

    private static RenderTargetBitmap RenderBitmap(FrameworkElement element, int width, int height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        return bitmap;
    }

    private static void Render(FrameworkElement element, int width, int height, string fileName)
        => SavePng(RenderBitmap(element, width, height), Path.Combine(_shots, fileName));

    private static void SavePng(RenderTargetBitmap bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static int MaxBrightness(RenderTargetBitmap bitmap, Int32Rect region)
    {
        var stride = region.Width * 4;
        var pixels = new byte[stride * region.Height];
        bitmap.CopyPixels(region, pixels, stride, 0);

        var max = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            // Pbgra32：B, G, R, A
            var brightness = Math.Max(pixels[i + 2], Math.Max(pixels[i + 1], pixels[i]));
            if (brightness > max)
            {
                max = brightness;
            }
        }

        return max;
    }

    private static int Finish()
    {
        Console.WriteLine();
        Console.WriteLine(Failures.Count == 0 ? "ALL UI SMOKE CHECKS PASSED" : $"FAILURES：{Failures.Count}");
        foreach (var failure in Failures)
        {
            Console.WriteLine("  - " + failure);
        }

        return Failures.Count == 0 ? 0 : 1;
    }

    private sealed record SettingsSnapshot(
        string Theme,
        string AccentColor,
        double OverlayOpacity,
        double WindowWidth,
        bool MinimalMode,
        bool MinimalShowChart,
        string Widgets,
        string DisabledPlugins)
    {
        public static SettingsSnapshot Capture(IServiceProvider provider)
        {
            var settings = provider.GetRequiredService<ISettingsService>();
            return new SettingsSnapshot(
                settings.Theme,
                settings.AccentColor,
                settings.OverlayOpacity,
                settings.WindowWidth,
                settings.MinimalMode,
                settings.MinimalShowChart,
                settings.Widgets,
                settings.DisabledPlugins);
        }
    }
}
