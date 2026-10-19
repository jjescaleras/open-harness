using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenHarness.UI.Avalonia.Services;
using OpenHarness.UI.Avalonia.ViewModels;
using OpenHarness.UI.Avalonia.Views;

namespace OpenHarness.UI.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(new ChatWindowService()),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
