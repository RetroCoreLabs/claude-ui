using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClaudeUsage.ViewModels;

namespace ClaudeUsage.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    // Unscaled sizes; they match the card width, the gap between cards and
    // the content margin in MainWindow.axaml.
    private const double CardWidth = 424;
    private const double CardGap = 14;
    private const double ContentMargin = 18;

    private int _fittedCardCount = -1;
    private double _scale = 1;
    private bool _opened;

    public MainWindow()
    {
        InitializeComponent();

        // "0.2.0+<commit>" becomes "Claude Usage v0.2.0".
        var version = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0];
        if (!string.IsNullOrEmpty(version))
        {
            Title = $"Claude Usage v{version}";
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null)
        {
            return;
        }

        // The cards arrive after the window is shown. Fit the window again
        // whenever the number of cards changes, the settings panel opens or
        // closes, or the layout is switched. A refresh with the same number
        // of cards leaves a size the user has set by hand alone.
        _viewModel.Limits.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(FitIfCardCountChanged, DispatcherPriority.Loaded);
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.IsSettingsOpen) or nameof(MainWindowViewModel.IsHorizontal))
            {
                Dispatcher.UIThread.Post(Fit, DispatcherPriority.Loaded);
            }
        };
    }

    // On Windows a minimised window leaves the taskbar and lives on as the
    // tray icon; clicking the tray icon brings it back. Other systems keep
    // the normal minimise, because a tray is not always present there.
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty &&
            WindowState == WindowState.Minimized &&
            OperatingSystem.IsWindows())
        {
            Hide();
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Fit();
        _opened = true;
        Resized += (_, _) => RememberScale();
    }

    // Keeps the scale the content is drawn at after the window has been
    // resized, so a later fit keeps that scale.
    private void RememberScale()
    {
        var natural = Root.DesiredSize;
        if (_opened && natural.Width > 0 && natural.Height > 0)
        {
            _scale = Math.Min(ClientSize.Width / natural.Width, ClientSize.Height / natural.Height);
        }
    }

    private void FitIfCardCountChanged()
    {
        if (_viewModel is not null && _viewModel.Limits.Count != _fittedCardCount)
        {
            Fit();
        }
    }

    // Sets the unscaled content width for the chosen layout (one card wide
    // when the cards are stacked, all cards plus the gaps between them when
    // they lie side by side), then sizes the window to the content at the
    // current scale. If that is larger than the screen, the scale is lowered.
    private void Fit()
    {
        _fittedCardCount = _viewModel?.Limits.Count ?? 0;

        var cards = _viewModel is { IsHorizontal: true } ? Math.Max(1, _fittedCardCount) : 1;
        Root.Width = cards * CardWidth + (cards - 1) * CardGap;

        // The same measure the Viewbox does; the result includes the margin.
        Root.Measure(Size.Infinity);
        var natural = Root.DesiredSize;
        if (natural.Width <= 0 || natural.Height <= 0)
        {
            return;
        }

        var scale = _scale;
        if (Screens.ScreenFromWindow(this) is { } screen)
        {
            scale = Math.Min(scale, screen.WorkingArea.Width / screen.Scaling / natural.Width);
            scale = Math.Min(scale, screen.WorkingArea.Height / screen.Scaling / natural.Height);
        }

        _scale = scale;
        Width = Math.Max(MinWidth, natural.Width * scale);
        Height = Math.Max(MinHeight, natural.Height * scale);
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.RefreshAsync();
        }
    }
}
