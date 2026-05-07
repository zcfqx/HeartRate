using System.Windows;
using HeartRateMonitor.App.ViewModels;

namespace HeartRateMonitor.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
