using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using HeartRateMonitor.App.Infrastructure;
using HeartRateMonitor.App.ViewModels;
using HeartRateMonitor.Core.Interfaces;

namespace HeartRateMonitor.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly ILogger _log;
    private bool _positionRestored;

    public MainWindow(MainViewModel viewModel, ISettingsService settings, ILogger log)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        _log = log;

        Icon = AppIcon.Image;
        DataContext = viewModel;

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;

        _settings.SettingsChanged += OnSettingsChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyPreferences();
        UpdateMinimalHeart(_viewModel.CurrentBpm);

        if (!_positionRestored)
        {
            _positionRestored = true;
            RestoreWindowPosition();
        }
    }

    /// <summary>把设置里的尺寸与不透明度应用到窗口；极简模式使用更小的窗口。</summary>
    private void ApplyPreferences()
    {
        try
        {
            if (_viewModel.IsMinimalMode)
            {
                // 极简模式固定为紧凑宽度；同时收紧 Min/Max，避免窗口被拉伸或还原成看板宽度。
                MinWidth = 176;
                MaxWidth = 180;
                MaxHeight = 320;
                Width = 180;
            }
            else
            {
                MinWidth = 280;
                MaxWidth = 560;
                MaxHeight = 780;
                Width = Math.Clamp(_settings.WindowWidth, 280, 520);
            }

            ApplyShellTransparency();
        }
        catch (Exception ex)
        {
            _log.Error("应用窗口外观失败", ex);
        }
    }

    /// <summary>
    /// 背景不透明度支持 0（完全透明）：直接改 Shell 背景画刷的 Alpha，
    /// 而不是 Window.Opacity，这样文字与曲线始终保持清晰。
    /// </summary>
    private void ApplyShellTransparency()
    {
        var alpha = (byte)Math.Round(Math.Clamp(_settings.OverlayOpacity, 0.0, 1.0) * 255);
        var surface = TryGetColor("Color.Surface", Color.FromRgb(0x12, 0x1A, 0x2E));
        var border = TryGetColor("Color.Border", Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

        // 分层窗口（AllowsTransparency）的命中测试按 Alpha 进行：Alpha=0 的像素会把鼠标
        // 透传给桌面，用户就抓不到窗口。所以 0% 时仍保留 1/255 的不可见底色，
        // 视觉上完全透明，但窗体仍然可拖动、可右键呼出菜单。
        var hitTestAlpha = alpha == 0 ? (byte)1 : alpha;

        Shell.Background = new SolidColorBrush(Color.FromArgb(hitTestAlpha, surface.R, surface.G, surface.B));
        Shell.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(border.A * hitTestAlpha / 255), border.R, border.G, border.B));

        // 完全透明时去掉阴影，否则会在桌面上留下一圈灰色雾。
        Shell.Effect = alpha == 0 ? null : TryFindResource("Shadow.Shell") as Effect;
    }

    private Color TryGetColor(string key, Color fallback)
        => TryFindResource(key) is Color color ? color : fallback;

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ApplyPreferences);
            return;
        }

        ApplyPreferences();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(ApplyShellTransparency);
            return;
        }

        ApplyShellTransparency();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsMinimalMode))
        {
            Dispatcher.BeginInvoke(() =>
            {
                ApplyPreferences();
                UpdateMinimalHeart(_viewModel.CurrentBpm);
            });
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentBpm))
        {
            Dispatcher.BeginInvoke(() => UpdateMinimalHeart(_viewModel.CurrentBpm));
        }
    }

    /// <summary>当前心跳动画模式：-1 未启动、0 未连接时的呼吸动画、>0 正在按该 BPM 跳动。</summary>
    private int _heartbeatMode = -1;

    /// <summary>
    /// 红心按当前心率做“咚—哒”双拍：心形放大回弹，同时向外扩散两圈光环。
    /// 未连接（BPM 为 0）时改为缓慢呼吸动画，避免界面像“死掉了”。
    /// </summary>
    private void UpdateMinimalHeart(int bpm)
    {
        if (!_viewModel.IsMinimalMode)
        {
            StopHeartbeat();
            return;
        }

        if (bpm <= 0)
        {
            if (_heartbeatMode != 0)
            {
                StartIdleBreath();
            }

            return;
        }

        if (_heartbeatMode == bpm)
        {
            return;
        }

        StartHeartbeat(bpm);
    }

    private void StopHeartbeat()
    {
        if (_heartbeatMode == -1)
        {
            return;
        }

        _heartbeatMode = -1;
        StopScale(MinimalHeartScale);
        StopScale(MinimalRippleScale);
        StopScale(MinimalRippleOuterScale);
        MinimalRipple.BeginAnimation(UIElement.OpacityProperty, null);
        MinimalRippleOuter.BeginAnimation(UIElement.OpacityProperty, null);
        MinimalRipple.Opacity = 0;
        MinimalRippleOuter.Opacity = 0;
    }

    private static void StopScale(ScaleTransform transform)
    {
        transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        transform.ScaleX = 1;
        transform.ScaleY = 1;
    }

    private void StartIdleBreath()
    {
        StopHeartbeat();
        _heartbeatMode = 0;

        var breath = new DoubleAnimation(1.0, 1.06, TimeSpan.FromSeconds(1.6))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleXProperty, breath);
        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleYProperty, breath.Clone());
    }

    private void StartHeartbeat(int bpm)
    {
        StopHeartbeat();
        _heartbeatMode = bpm;

        var beat = TimeSpan.FromSeconds(Math.Clamp(60.0 / bpm, 0.28, 2.0));

        // 1.0 → 1.20（第一拍）→ 0.97 → 1.10（第二拍）→ 回到 1.0
        var pulse = new DoubleAnimationUsingKeyFrames
        {
            Duration = beat,
            RepeatBehavior = RepeatBehavior.Forever
        };
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(0.00)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1.20, KeyTime.FromPercent(0.10)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(0.97, KeyTime.FromPercent(0.22)));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1.10, KeyTime.FromPercent(0.32)));
        pulse.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromPercent(0.62),
            new SineEase { EasingMode = EasingMode.EaseOut }));
        pulse.KeyFrames.Add(new LinearDoubleKeyFrame(1.00, KeyTime.FromPercent(1.00)));

        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse.Clone());

        // 两圈光环：紧随心形扩散并淡出，第二圈晚半个节拍，形成余韵。
        AnimateRipple(MinimalRipple, MinimalRippleScale, beat, TimeSpan.Zero, 0.55, 1.9);
        AnimateRipple(MinimalRippleOuter, MinimalRippleOuterScale, beat,
            TimeSpan.FromSeconds(beat.TotalSeconds * 0.5), 0.35, 2.2);
    }

    private static void AnimateRipple(UIElement ring, ScaleTransform scale, TimeSpan beat,
        TimeSpan delay, double fromOpacity, double toScale)
    {
        StopScale(scale);
        ring.BeginAnimation(UIElement.OpacityProperty, null);

        var duration = TimeSpan.FromSeconds(Math.Max(beat.TotalSeconds * 0.85, 0.35));
        var ease = new SineEase { EasingMode = EasingMode.EaseOut };

        var grow = new DoubleAnimation(0.85, toScale, duration)
        {
            BeginTime = delay,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow.Clone());

        var fade = new DoubleAnimation(fromOpacity, 0, duration)
        {
            BeginTime = delay,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        ring.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    // === 顶栏 ===

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove 在鼠标已释放时会抛异常，忽略即可
        }

        SaveWindowPosition();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    // === 极简模式 ===

    private void OnMinimalMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        // 双击退出极简模式
        if (e.ClickCount == 2)
        {
            e.Handled = true;
            _viewModel.ToggleMinimalModeCommand.Execute(null);
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // 同上
        }

        SaveWindowPosition();
    }

    // === 生命周期 ===

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowPosition();

        if (App.IsExiting)
        {
            return;
        }

        if (_settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        e.Cancel = true;
        App.ExitApp();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ThemeManager.ThemeChanged -= OnThemeChanged;
    }

    private void SaveWindowPosition()
    {
        if (WindowState == WindowState.Minimized || double.IsNaN(Left) || double.IsNaN(Top))
        {
            return;
        }

        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
    }

    private void RestoreWindowPosition()
    {
        if (_settings.WindowLeft <= 0 || _settings.WindowTop <= 0)
        {
            // 默认放到工作区右上角
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 12;
            Top = area.Top + 8;
            return;
        }

        var screen = SystemParameters.WorkArea;
        Left = Math.Clamp(_settings.WindowLeft, screen.Left, Math.Max(screen.Left, screen.Right - Width));
        Top = Math.Clamp(_settings.WindowTop, screen.Top, Math.Max(screen.Top, screen.Bottom - 120));
    }
}
