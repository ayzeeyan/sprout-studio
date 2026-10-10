using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SproutStudio.Editor;
using SproutStudio.Models;
using SproutStudio.Services;
using SproutStudio.ViewModels;
using SproutStudio.Views.Academy;
using SproutStudio.Views.Documentation;
using SproutStudio.Views.Modes;
using SproutStudio.Views.Packages;
using SproutStudio.Views.Search;
using SproutStudio.Views.SourceControl;
using SproutStudio.Views.Toolchains;

namespace SproutStudio.Views;

public sealed partial class MainWindow : Window
{
    private const double ActivityRailWidth = 56;
    private readonly IReadOnlyList<string> _arguments;
    private readonly MainWindowViewModel _viewModel;
    private readonly Dictionary<ActivityDestination, Control> _modeViews = [];
    private CancellationTokenSource? _modeActivationCancellation;
    private bool _allowClose;
    private bool _responsiveInspectorCollapsed;
    private double _lastExplorerWidth = 280;
    private double _lastInspectorWidth = 360;
    private double _lastBottomHeight = 220;

    public MainWindow()
        : this([])
    {
    }

    public MainWindow(IReadOnlyList<string> arguments)
    {
        _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        InitializeComponent();
        UpdateMaximizeButton();
        _viewModel = new MainWindowViewModel();
        _viewModel.AttachDialogs(new AvaloniaStudioDialogService(this));
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.DocumentationRequested += ViewModel_DocumentationRequested;
        DataContext = _viewModel;
        AddHandler(KeyDownEvent, MainWindow_KeyDown, RoutingStrategies.Tunnel);
        ApplyInitialLayout(_viewModel.InitialLayout);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            UpdateMaximizeButton();
        }
    }

    private async void MainWindow_Opened(object? sender, EventArgs args)
    {
        FitToWorkingArea();
        await _viewModel.InitializeAsync(_arguments);
        await ActivateStudioModeAsync();
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            SaveLayout();
            _modeActivationCancellation?.Cancel();
            _modeActivationCancellation?.Dispose();
            _viewModel.DocumentationRequested -= ViewModel_DocumentationRequested;
            _viewModel.Dispose();
            return;
        }

        args.Cancel = true;
        if (await _viewModel.ConfirmCanCloseAsync())
        {
            _allowClose = true;
            SaveLayout();
            Close();
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainWindowViewModel.ActivityVisible)
            or nameof(MainWindowViewModel.ExplorerVisible)
            or nameof(MainWindowViewModel.InspectorVisible)
            or nameof(MainWindowViewModel.BottomPanelVisible))
        {
            RecalculateResponsivePaneVisibility(ClientSize.Width);
            ApplyPaneVisibility();
        }
        else if (args.PropertyName == nameof(MainWindowViewModel.SelectedDocument))
        {
            RecalculateResponsivePaneVisibility(ClientSize.Width);
            ApplyPaneVisibility();
        }
        else if (args.PropertyName == nameof(MainWindowViewModel.SelectedActivity))
        {
            _ = ActivateStudioModeAsync();
        }
        else if (args.PropertyName is nameof(MainWindowViewModel.AcademyHubVisible)
            or nameof(MainWindowViewModel.SelectedAcademySection)
            or nameof(MainWindowViewModel.SelectedAcademyTrackId))
        {
            _ = ActivateStudioModeAsync();
        }
        else if (args.PropertyName == nameof(MainWindowViewModel.CurrentProject)
            && _viewModel.IsIntegratedMode)
        {
            _ = ActivateStudioModeAsync();
        }
    }

    private async void ViewModel_DocumentationRequested(
        object? sender,
        DocumentationNavigationRequest request)
    {
        await ActivateStudioModeAsync();
        if (this.FindControl<ContentControl>("StudioModeHost")?.Content is DocumentationModeView docs)
        {
            await docs.OpenTopicAsync(request.TopicId, request.Anchor);
        }
    }

    private async Task ActivateStudioModeAsync()
    {
        ContentControl host = this.FindControl<ContentControl>("StudioModeHost")!;
        if (_viewModel.IsWorkspaceMode || !_viewModel.IsIntegratedMode)
        {
            _modeActivationCancellation?.Cancel();
            host.Content = null;
            return;
        }

        _modeActivationCancellation?.Cancel();
        _modeActivationCancellation?.Dispose();
        _modeActivationCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken = _modeActivationCancellation.Token;
        ActivityDestination destination = _viewModel.SelectedActivity;
        if (!_modeViews.TryGetValue(destination, out Control? control))
        {
            control = CreateModeView(destination);
            _modeViews[destination] = control;
        }
        host.Content = control;
        if (control is IStudioModeView mode)
        {
            try
            {
                await mode.ActivateAsync(_viewModel, cancellationToken);
                if (control is AcademyModeView academy)
                {
                    academy.SelectSection(_viewModel.SelectedAcademySection);
                    if (_viewModel.SelectedAcademyTrackId is { Length: > 0 } trackId)
                    {
                        academy.SelectTrack(trackId);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A newer mode selection superseded this activation.
            }
            catch (Exception exception)
            {
                StudioLog.Error($"Could not activate {destination} mode.", exception);
            }
        }
    }

    private Control CreateModeView(ActivityDestination destination) => destination switch
    {
        ActivityDestination.Search => new SearchModeView(),
        ActivityDestination.SourceControl => new SourceControlModeView(_viewModel.GitRepository),
        ActivityDestination.Academy => new AcademyModeView(_viewModel.Toolchains, _viewModel.Academy),
        ActivityDestination.Documentation => new DocumentationModeView(),
        ActivityDestination.Packages => new PackagesModeView(),
        ActivityDestination.Toolchains => new ToolchainsModeView(_viewModel.Toolchains),
        _ => throw new ArgumentOutOfRangeException(nameof(destination), destination, "This destination is not an integrated Studio mode."),
    };

    private void ApplyInitialLayout(StudioLayoutState state)
    {
        _lastExplorerWidth = state.ExplorerWidth;
        _lastInspectorWidth = state.InspectorWidth;
        _lastBottomHeight = state.BottomPanelHeight;
        if (state.WindowWidth.HasValue && state.WindowHeight.HasValue)
        {
            Width = state.WindowWidth.Value;
            Height = state.WindowHeight.Value;
        }
        if (state.WindowX.HasValue && state.WindowY.HasValue)
        {
            Position = new PixelPoint(state.WindowX.Value, state.WindowY.Value);
        }
        ApplyPaneVisibility();
    }

    private void ApplyPaneVisibility()
    {
        Grid shellBody = this.FindControl<Grid>("ShellBodyGrid")!;
        Grid workspace = this.FindControl<Grid>("WorkspaceGrid")!;
        Grid editorTools = this.FindControl<Grid>("EditorToolGrid")!;
        ColumnDefinition activity = shellBody.ColumnDefinitions[0];
        ColumnDefinition explorer = workspace.ColumnDefinitions[0];
        ColumnDefinition explorerSplitter = workspace.ColumnDefinitions[1];
        ColumnDefinition inspectorSplitter = workspace.ColumnDefinitions[3];
        ColumnDefinition inspector = workspace.ColumnDefinitions[4];
        RowDefinition bottomSplitter = editorTools.RowDefinitions[1];
        RowDefinition bottom = editorTools.RowDefinitions[2];

        if (explorer.ActualWidth > 0)
        {
            _lastExplorerWidth = explorer.ActualWidth;
        }
        if (inspector.ActualWidth > 0)
        {
            _lastInspectorWidth = inspector.ActualWidth;
        }
        if (bottom.ActualHeight > 0)
        {
            _lastBottomHeight = bottom.ActualHeight;
        }

        activity.Width = new GridLength(_viewModel.ActivityVisible ? ActivityRailWidth : 0);
        explorer.Width = new GridLength(_viewModel.ExplorerVisible ? _lastExplorerWidth : 0);
        explorerSplitter.Width = new GridLength(_viewModel.ExplorerVisible ? 4 : 0);
        bool showInspector = _viewModel.InspectorVisible && !_responsiveInspectorCollapsed;
        inspector.Width = new GridLength(showInspector ? _lastInspectorWidth : 0);
        inspectorSplitter.Width = new GridLength(showInspector ? 4 : 0);
        bottom.Height = new GridLength(_viewModel.BottomPanelVisible ? _lastBottomHeight : 0);
        bottomSplitter.Height = new GridLength(_viewModel.BottomPanelVisible ? 4 : 0);

        this.FindControl<GridSplitter>("ExplorerSplitter")!.IsVisible = _viewModel.ExplorerVisible;
        this.FindControl<GridSplitter>("InspectorSplitter")!.IsVisible = showInspector;
        this.FindControl<GridSplitter>("BottomSplitter")!.IsVisible = _viewModel.BottomPanelVisible;
    }

    private void SaveLayout()
    {
        Grid workspace = this.FindControl<Grid>("WorkspaceGrid")!;
        Grid editorTools = this.FindControl<Grid>("EditorToolGrid")!;
        ColumnDefinition explorer = workspace.ColumnDefinitions[0];
        ColumnDefinition inspector = workspace.ColumnDefinitions[4];
        RowDefinition bottom = editorTools.RowDefinitions[2];
        _viewModel.SaveLayout(
            explorer.ActualWidth > 0 ? explorer.ActualWidth : _lastExplorerWidth,
            inspector.ActualWidth > 0 ? inspector.ActualWidth : _lastInspectorWidth,
            bottom.ActualHeight > 0 ? bottom.ActualHeight : _lastBottomHeight,
            Position.X,
            Position.Y,
            (int)Math.Round(ClientSize.Width),
            (int)Math.Round(ClientSize.Height));
    }

    private void FitToWorkingArea()
    {
        Screen? screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is null)
        {
            return;
        }
        double maximumWidth = Math.Max(MinWidth, (screen.WorkingArea.Width / screen.Scaling) - 20);
        double maximumHeight = Math.Max(MinHeight, (screen.WorkingArea.Height / screen.Scaling) - 20);
        Width = Math.Min(Width, maximumWidth);
        Height = Math.Min(Height, maximumHeight);
        _viewModel.CompactToolbar = Width < 1600;
        UpdateCommandCenterWidth(Width);
    }

    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs args)
    {
        UpdateCommandCenterWidth(args.NewSize.Width);
        _viewModel.CompactToolbar = args.NewSize.Width < 1600;
        if (RecalculateResponsivePaneVisibility(args.NewSize.Width))
        {
            ApplyPaneVisibility();
        }
    }

    // The command center sits in the middle column of the title bar; shrink it on narrow windows
    // so it never covers the product name and the file context on the left.
    private void UpdateCommandCenterWidth(double windowWidth)
    {
        if (this.FindControl<Button>("CommandCenter") is { } commandCenter)
        {
            commandCenter.Width = Math.Clamp(windowWidth - 900, 220, 440);
            if (this.FindControl<Border>("CommandCenterHint") is { } hint)
            {
                hint.IsVisible = commandCenter.Width >= 340;
            }
        }
    }

    private bool RecalculateResponsivePaneVisibility(double windowWidth)
    {
        double activityWidth = _viewModel.ActivityVisible ? ActivityRailWidth : 0;
        double explorerWidth = _viewModel.ExplorerVisible ? _lastExplorerWidth + 4 : 0;
        double inspectorWidth = _viewModel.InspectorVisible ? _lastInspectorWidth + 4 : 0;
        double centerWidthWithInspector = windowWidth - activityWidth - explorerWidth - inspectorWidth;
        double requiredCenterWidth = _viewModel.SelectedDocument?.IsAcademyCoding == true ? 920 : 620;
        bool shouldCollapse = _viewModel.InspectorVisible && centerWidthWithInspector < requiredCenterWidth;
        if (_responsiveInspectorCollapsed == shouldCollapse)
        {
            return false;
        }

        _responsiveInspectorCollapsed = shouldCollapse;
        return true;
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs args)
    {
        bool control = args.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = args.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (args.Key == Key.F11)
        {
            Execute(_viewModel.ToggleZenModeCommand);
        }
        else if (args.Key == Key.F1)
        {
            Execute(_viewModel.ShowDocumentationCommand);
        }
        else if (control && !shift && args.Key is Key.OemPlus or Key.Add)
        {
            Execute(_viewModel.ZoomInCommand);
        }
        else if (control && !shift && args.Key is Key.OemMinus or Key.Subtract)
        {
            Execute(_viewModel.ZoomOutCommand);
        }
        else if (control && !shift && args.Key is Key.D0 or Key.NumPad0)
        {
            Execute(_viewModel.ResetZoomCommand);
        }
        else if (control && !shift && args.Key == Key.OemComma)
        {
            Execute(_viewModel.ShowSettingsCommand);
        }
        else if (control && !shift && args.Key == Key.G)
        {
            Execute(_viewModel.GoToLineCommand);
        }
        else if (control && shift && args.Key == Key.E)
        {
            Execute(_viewModel.ShowExplorerCommand);
        }
        else if (control && shift && args.Key == Key.F)
        {
            Execute(_viewModel.ShowSearchCommand);
        }
        else if (control && shift && args.Key == Key.G)
        {
            Execute(_viewModel.ShowSourceControlCommand);
        }
        else if (control && shift && args.Key == Key.M)
        {
            Execute(_viewModel.ShowProblemsCommand);
        }
        else if (control && alt && args.Key == Key.B)
        {
            Execute(_viewModel.ToggleInspectorCommand);
        }
        else if (control && shift && args.Key == Key.P)
        {
            Execute(_viewModel.CommandPaletteCommand);
        }
        else if (control && args.Key == Key.P)
        {
            Execute(_viewModel.CommandPaletteCommand);
        }
        else if (control && args.Key == Key.N)
        {
            Execute(_viewModel.NewDocumentCommand);
        }
        else if (control && args.Key == Key.O)
        {
            Execute(_viewModel.OpenDocumentCommand);
        }
        else if (control && args.Key == Key.S)
        {
            Execute(_viewModel.SaveDocumentCommand);
        }
        else if (control && args.Key == Key.W && !shift)
        {
            if (_viewModel.SelectedDocument is { } document
                && _viewModel.CloseDocumentCommand.CanExecute(document))
            {
                _viewModel.CloseDocumentCommand.Execute(document);
            }
        }
        else if (control && args.Key == Key.B && !shift)
        {
            Execute(_viewModel.ToggleExplorerCommand);
        }
        else if (control && args.Key == Key.J)
        {
            Execute(_viewModel.ToggleBottomPanelCommand);
        }
        else if (control && shift && args.Key == Key.B)
        {
            Execute(_viewModel.BuildCommand);
        }
        else if (shift && args.KeyModifiers.HasFlag(KeyModifiers.Alt) && args.Key == Key.F)
        {
            Execute(_viewModel.FormatDocumentCommand);
        }
        else if (control && args.Key == Key.F5)
        {
            Execute(_viewModel.CheckCommand);
        }
        else if (args.Key == Key.F5)
        {
            Execute(_viewModel.RunCommand);
        }
        else if (args.Key == Key.F6)
        {
            Execute(_viewModel.DebugCommand);
        }
        else if (control && args.Key == Key.Tab)
        {
            CycleDocument(shift ? -1 : 1);
        }
        else
        {
            return;
        }
        args.Handled = true;
    }

    private static void Execute(System.Windows.Input.ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private void CycleDocument(int direction)
    {
        if (_viewModel.Documents.Count < 2 || _viewModel.SelectedDocument is null)
        {
            return;
        }
        int current = _viewModel.Documents.IndexOf(_viewModel.SelectedDocument);
        int next = (current + direction + _viewModel.Documents.Count) % _viewModel.Documents.Count;
        _viewModel.SelectedDocument = _viewModel.Documents[next];
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            && !IsButtonSource(args.Source))
        {
            BeginMoveDrag(args);
        }
    }

    private void TitleBar_DoubleTapped(object? sender, TappedEventArgs args)
    {
        if (IsButtonSource(args.Source))
        {
            return;
        }
        ToggleMaximize();
        args.Handled = true;
    }

    private void Minimize_Click(object? sender, RoutedEventArgs args) =>
        WindowState = WindowState.Minimized;

    private void Maximize_Click(object? sender, RoutedEventArgs args)
    {
        ToggleMaximize();
        args.Handled = true;
    }

    private void Close_Click(object? sender, RoutedEventArgs args) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void UpdateMaximizeButton()
    {
        Button? button = this.FindControl<Button>("MaximizeButton");
        TextBlock? icon = this.FindControl<TextBlock>("MaximizeIcon");
        if (button is null || icon is null)
        {
            return;
        }
        bool maximized = WindowState == WindowState.Maximized;
        icon.Text = maximized ? "\uE923" : "\uE922";
        ToolTip.SetTip(button, maximized ? "Restore" : "Maximize");
    }

    private static bool IsButtonSource(object? source) =>
        source is Button
        || source is Visual visual && visual.FindAncestorOfType<Button>() is not null;
}
