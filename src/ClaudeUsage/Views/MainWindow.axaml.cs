using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClaudeUsage.ViewModels;

namespace ClaudeUsage.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private int _fittedCardCount = -1;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is null)
        {
            return;
        }

        // The cards arrive after the window is shown, and the window system can
        // switch SizeToContent off once the window is on screen. Fit the height
        // again whenever the number of cards changes or the settings panel
        // opens or closes. A refresh with the same number of cards leaves a
        // height the user has set by hand alone.
        _viewModel.Limits.CollectionChanged += (_, _) => Dispatcher.UIThread.Post(FitHeightIfCardCountChanged, DispatcherPriority.Loaded);
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.IsSettingsOpen))
            {
                Dispatcher.UIThread.Post(FitHeight, DispatcherPriority.Loaded);
            }
        };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Never grow taller than the screen; the card list scrolls instead.
        if (Screens.ScreenFromWindow(this) is { } screen)
        {
            MaxHeight = screen.WorkingArea.Height / screen.Scaling;
        }

        FitHeight();
    }

    private void FitHeightIfCardCountChanged()
    {
        if (_viewModel is not null && _viewModel.Limits.Count != _fittedCardCount)
        {
            FitHeight();
        }
    }

    private void FitHeight()
    {
        _fittedCardCount = _viewModel?.Limits.Count ?? 0;

        // Assigning the same value does nothing, so switch it off first.
        SizeToContent = SizeToContent.Manual;
        SizeToContent = SizeToContent.Height;
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.RefreshAsync();
        }
    }
}
