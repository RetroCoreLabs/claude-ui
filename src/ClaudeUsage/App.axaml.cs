using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using ClaudeUsage.Services;
using ClaudeUsage.ViewModels;
using ClaudeUsage.Views;

namespace ClaudeUsage;

public partial class App : Application
{
    // Value of the trayLimit setting that selects the limit with the least remaining.
    private const string LowestRemaining = "lowest";

    private static readonly IBrush NoDataBrush = new SolidColorBrush(Color.Parse("#9AA3C0"));

    private MainWindowViewModel? _viewModel;
    private MainWindow? _window;
    private TrayIcon? _trayIcon;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _viewModel = new MainWindowViewModel();
            _window = new MainWindow { DataContext = _viewModel };
            desktop.MainWindow = _window;

            _trayIcon = new TrayIcon
            {
                ToolTipText = "Claude Usage",
                Icon = TrayIconRenderer.Render("--", NoDataBrush),
                Menu = BuildMenu(desktop),
            };
            _trayIcon.Clicked += (_, _) => ShowWindow();
            TrayIcon.SetIcons(this, [_trayIcon]);

            _viewModel.Updated += UpdateTrayIcon;
            _viewModel.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private NativeMenu BuildMenu(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Open");
        open.Click += (_, _) => ShowWindow();

        var refresh = new NativeMenuItem("Refresh");
        refresh.Click += async (_, _) =>
        {
            if (_viewModel is not null)
            {
                await _viewModel.RefreshAsync();
            }
        };

        var exit = new NativeMenuItem("Exit");
        exit.Click += (_, _) => desktop.Shutdown();

        var menu = new NativeMenu();
        menu.Items.Add(open);
        menu.Items.Add(refresh);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);
        return menu;
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            return;
        }

        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
        {
            _window.WindowState = WindowState.Normal;
        }

        _window.Activate();
    }

    private void UpdateTrayIcon()
    {
        if (_trayIcon is null || _viewModel is null)
        {
            return;
        }

        var lines = _viewModel.Limits.Select(card => card.ShortText).ToList();
        _trayIcon.ToolTipText = lines.Count > 0 ? string.Join("\n", lines) : _viewModel.StatusText;

        var trayLimit = _viewModel.Settings.TrayLimit;
        var shown = trayLimit == LowestRemaining
            ? _viewModel.Limits.MinBy(card => card.RemainingPercent)
            : _viewModel.Limits.FirstOrDefault(card => card.Kind == trayLimit);
        _trayIcon.Icon = shown is null
            ? TrayIconRenderer.Render("--", NoDataBrush)
            : TrayIconRenderer.Render($"{shown.PrimaryPercent:0}", shown.AccentBrush);
    }
}
