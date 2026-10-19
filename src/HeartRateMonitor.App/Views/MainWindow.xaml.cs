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
                MinWidth = 150;
                MaxWidth = 320;
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

        Shell.Background = new SolidColorBrush(Color.FromArgb(alpha, surface.R, surface.G, surface.B));
        Shell.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(border.A * alpha / 255), border.R, border.G, border.B));

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

    /// <summary>极简模式心形图标按当前心率跳动（周期 = 60 / BPM）。</summary>
    private void UpdateMinimalHeart(int bpm)
    {
        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        if (!_viewModel.IsMinimalMode || bpm <= 0)
        {
            return;
        }

        var duration = TimeSpan.FromSeconds(Math.Max(60.0 / bpm, 0.4));
        var animation = new DoubleAnimation(1.0, 1.18, new Duration(duration))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        MinimalHeartScale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
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
