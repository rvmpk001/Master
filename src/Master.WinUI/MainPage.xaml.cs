using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Master_WinUI.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Master_WinUI;

public sealed partial class MainPage : Page
{
    private static readonly string ThemeSettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Master.WinUI",
        "theme.txt");
    private const double DefaultPlantToolsWidth = 240;
    private const double MinimumPlantToolsWidth = 180;
    private const double MinimumWorkspaceWidth = 500;

    private static readonly HashSet<string> SupportedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp"
    };

    private readonly DispatcherQueueTimer _clockTimer;
    private readonly DispatcherQueueTimer _plcStatusTimer;
    private readonly DispatcherQueueTimer _canvasLayoutSaveTimer;
    private readonly SemaphoreSlim _accessSessionEndLock = new(1, 1);
    private readonly ThemeBrushSet _themeBrushes = new();
    private readonly AuthenticationService _authenticationService = new();
    private readonly PlcCommunicationService _plcCommunicationService = new();
    private readonly AccessConfigurationService _accessConfigurationService = new();
    private readonly InventoryService _inventoryService = new();
    private readonly FormulaService _formulaService = new();
    private readonly SupplierService _supplierService = new();
    private readonly ClientService _clientService = new();
    private readonly TruckService _truckService = new();
    private readonly OrderService _orderService = new();
    private readonly OrderScheduleConfigurationService _orderScheduleConfigurationService = new();
    private readonly ReportCatalogService _reportCatalogService = new();
    private readonly ReportExportService _reportExportService = new();
    private readonly CompanyConfigurationService _companyConfigurationService = new();
    private readonly MachineConfigurationService _machineConfigurationService = new();
    private readonly MessageConfigurationService _messageConfigurationService = new();
    private readonly AdditionalConfigurationService _additionalConfigurationService = new();
    private readonly ImageConfigurationService _imageConfigurationService = new();
    private readonly DesignerCanvasStore _designerCanvasStore = new();
    private readonly DesignerCanvasState _designerCanvasState = new();
    private readonly Dictionary<string, TextBlock> _canvasValueCaptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _canvasValueTexts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _simulatedCanvasImageStates = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, string> _dashboardMachineConfiguration =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, string>? _dashboardFormulaValues;
    private bool _hasDashboardMaterialData;
    private readonly Dictionary<string, decimal> _simulatedCanvasReadings =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, AdditionalImageConfigurationRecord> _additionalImageConfigurations =
        new Dictionary<string, AdditionalImageConfigurationRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, AdditionalImageConfigurationRecord> _pumpImageConfigurations =
        new Dictionary<string, AdditionalImageConfigurationRecord>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, AdditionalImageConfigurationRecord> _valveImageConfigurations =
        new Dictionary<string, AdditionalImageConfigurationRecord>(StringComparer.OrdinalIgnoreCase);
    private bool _showPlantTools = true;
    private bool _isDashboardResizeActive;
    private double _dashboardResizeStartY;
    private double _dashboardPanelsStartHeight;
    private bool _isPlantToolsResizeActive;
    private double _plantToolsResizeStartX;
    private double _plantToolsResizeStartWidth;
    private double _plantToolsWidth = DefaultPlantToolsWidth;
    private bool _isCompanyConfigurationOpen;
    private bool _isRemissionConfigurationOpen;
    private bool _isImagesOnConfigurationOpen;
    private bool _isImagesOffConfigurationOpen;
    private bool _isImageSizesConfigurationOpen;
    private bool _isMessagesConfigurationOpen;
    private bool _isAdditionalConfigurationOpen;
    private bool _isOrdersConfigurationOpen;
    private bool _isAccessConfigurationOpen;
    private bool _isSecurityFormOpen;
    private int? _authenticatedUserId;
    private string _authenticatedUsername = string.Empty;
    private string? _authenticatedName;
    private int? _accessLogRecordId;
    private IReadOnlyList<AccessPermissionEntry> _currentUserPermissions = Array.Empty<AccessPermissionEntry>();
    private FormulaRow? _selectedFormula;
    private SupplierRecord? _selectedSupplier;
    private ClientRecord? _selectedClient;
    private TruckRecord? _selectedTruck;
    private Grid? _selectedTruckRow;
    private OperatorRecord? _selectedOperator;
    private Grid? _selectedOperatorRow;
    private OrderRecord? _selectedOrder;
    private Grid? _selectedOrderRow;
    private bool _editingOrder;
    private bool _showingActiveOrders;
    private bool _showingCancelledOrders;
    private bool _syncingManualLoadOrderSelection;
    private int _manualLoadOrderRequestId;
    private readonly HashSet<string> _manualLoadInvalidMaterialFields = new(StringComparer.Ordinal);
    private bool _editingSupplier;
    private bool _editingClient;
    private bool _editingTruck;
    private bool _editingOperator;
    private Border? _selectedFormulaBorder;
    private bool _showingCancelledFormulas;
    private bool _editingFormula;
    private bool _viewingFormula;
    private readonly Dictionary<string, Control> _formulaEditorInputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _formulaMaterialOptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBlock> _formulaMaterialStates = new(StringComparer.Ordinal);
    private Border? _movingCanvasControl;
    private Windows.Foundation.Point _canvasControlPointerOffset;
    private Border? _resizingCanvasControl;
    private Border? _selectedCanvasMessage;
    private readonly List<Border> _canvasTransformHandles = new();
    private CanvasResizeHandleInfo? _activeCanvasResizeHandle;
    private Windows.Foundation.Point _canvasControlResizeStart;
    private Windows.Foundation.Point _canvasControlResizeStartPosition;
    private double _canvasControlResizeStartWidth;
    private double _canvasControlResizeStartHeight;
    private bool _isMessagePaletteLoading;
    private bool _isAdditionalImagePaletteLoading;
    private bool _isConfiguredAdditionalPaletteLoading;
    private bool _configuredAdditionalPaletteRefreshPending;
    private bool _isAggregatePaletteLoading;
    private bool _aggregatePaletteRefreshPending;
    private bool _isCementPaletteLoading;
    private bool _cementPaletteRefreshPending;
    private bool _isAdditivePaletteLoading;
    private bool _additivePaletteRefreshPending;
    private bool _isWaterPaletteLoading;
    private bool _waterPaletteRefreshPending;
    private bool _isFiberPaletteLoading;
    private bool _fiberPaletteRefreshPending;
    private bool _isCanvasLocked;
    private bool _isDesignerCanvasLoaded;
    private bool _isPlcStatusCheckRunning;
    private PlcConnectionState? _lastPlcConnectionState;
    private string? _lastPlcStatusDetail;
    private ReportCatalogResult? _currentReportResult;
    private SimulatedRemission? _simulatedRemission;
    private string[]? _remissionBeforeSimulation;
    private SimulatedProcessState _simulatedProcessState;
    private SimulatedProcessState _stateBeforePause;

    public ObservableCollection<MaterialRow> MaterialRows { get; } = new();
    public ObservableCollection<MaterialRowPair> MaterialRowPairs { get; } = new();

    public ObservableCollection<RemissionField> RemissionRows { get; } = new()
    {
        new("No Remisión", "0", false),
        new("Código de receta", string.Empty, true),
        new("Descripción", string.Empty, true),
        new("Cantidad", "0", true),
        new("Cliente", string.Empty, true),
        new("Obra", string.Empty, true),
        new("Camión", string.Empty, true),
        new("N. Pedido", string.Empty, true)
    };
    public ObservableCollection<RemissionFieldPair> RemissionRowPairs { get; } = new();

    public ObservableCollection<PlantValue> HumidityRows { get; } = CreateAdjustmentRows("H");
    public ObservableCollection<PlantValue> AbsorptionRows { get; } = CreateAdjustmentRows("O");
    public ObservableCollection<PlantValuePair> HumidityRowPairs { get; } = new();
    public ObservableCollection<PlantValuePair> AbsorptionRowPairs { get; } = new();
    public ObservableCollection<InventoryMaterial> AggregateInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> AdditiveInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> WaterInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> CementInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> FiberInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> SelectedInventoryMaterials { get; } = new();
    public ObservableCollection<string> InventorySuppliers { get; } = new();
    public ObservableCollection<ReportCatalogDefinition> ReportCatalogItems { get; } = new();
    public ObservableCollection<FormulaColumn> FormulaColumns { get; } = new();
    public ObservableCollection<FormulaRow> ActiveFormulaRows { get; } = new();
    public ObservableCollection<FormulaRow> CancelledFormulaRows { get; } = new();
    public ObservableCollection<FormulaRow> VisibleFormulaRows { get; } = new();
    public ObservableCollection<SupplierRecord> SupplierRecords { get; } = new();
    public ObservableCollection<SupplierCell> SupplierColumns { get; } = new();
    public ObservableCollection<ClientRecord> ClientRecords { get; } = new();
    public ObservableCollection<ClientCell> ClientColumns { get; } = new();
    public ObservableCollection<TruckRecord> TruckRecords { get; } = new();
    public ObservableCollection<OperatorRecord> OperatorRecords { get; } = new();
    public ObservableCollection<OrderRecord> ActiveOrders { get; } = new();
    public ObservableCollection<OrderRecord> CompletedOrders { get; } = new();
    public ObservableCollection<OrderRecord> CancelledOrders { get; } = new();
    public ObservableCollection<OrderRecord> VisibleOrders { get; } = new();
    public ObservableCollection<ClientRecord> OrderClients { get; } = new();
    public ObservableCollection<OrderRecipe> OrderRecipes { get; } = new();
    public ObservableCollection<OrderRecipe> ManualLoadRecipes { get; } = new();
    public ObservableCollection<string> ManualLoadOrderNumbers { get; } = new();
    public ObservableCollection<string> ManualLoadConstructionOptions { get; } = new();
    public ObservableCollection<OperatorRecord> ManualLoadOperators { get; } = new();
    public ObservableCollection<TruckRecord> ManualLoadTrucks { get; } = new();
    public ObservableCollection<string> ManualLoadShifts { get; } = new();
    public ObservableCollection<string> OrderDischargeTypes { get; } = new();
    public ObservableCollection<string> OrderProducts { get; } = new();
    public ObservableCollection<string> OrderPaymentConditions { get; } = new();

    public MainPage()
    {
        foreach (var row in CreateAdjustmentPairs(HumidityRows))
        {
            HumidityRowPairs.Add(row);
        }

        foreach (var row in CreateAdjustmentPairs(AbsorptionRows))
        {
            AbsorptionRowPairs.Add(row);
        }

        foreach (var row in CreateRemissionPairs())
        {
            RemissionRowPairs.Add(row);
        }

        InitializeComponent();
        foreach (var (column, _, input) in GetManualLoadMaterialInputs())
        {
            input.TextChanged += (_, _) => ClearManualLoadMaterialHighlightWhenValid(column, input);
        }

        UpdateManualLoadMaterialInputs(null);
        AddCanvasDeleteAccelerator(Windows.System.VirtualKey.Delete);
        AddCanvasDeleteAccelerator(Windows.System.VirtualKey.Back);
        ApplyTheme(LoadSavedTheme(), persist: false);
        ConfigurationView.CanSaveConfigurationTab = CanSaveConfigurationTab;
        ConfigurationView.CanEditDesignerCanvas = () => !_isCanvasLocked;
        ConfigurationView.HasProfileModuleAccess = HasModulePermission;
        ConfigurationView.HasProfileAction = HasActionPermission;
        MachineConfigurationView.HasPermission = action => HasActionPermission("Maquina", action);
        MachineConfigurationView.IsDesignerCanvasLocked = () => _isCanvasLocked;
        MachineConfigurationView.CloseRequested += (_, _) => MachineConfigurationOverlay.Visibility = Visibility.Collapsed;
        MachineConfigurationView.LoadMachineImageToDesignCanvasAsync = path => LoadMachineImageToDesignCanvasAsync(path);
        MachineConfigurationView.DashboardConfigurationChanged += ApplyMachineAdjustmentValues;
        MachineConfigurationView.MachineNameSaved += async (_, machineName) =>
        {
            MachineNameTextBlock.Text = string.IsNullOrWhiteSpace(machineName) ? "Máquina no configurada" : machineName;
            await RefreshMaterialControlPalettesAsync();
        };
        MachineConfigurationView.DashboardOptionsChanged += ApplyMachineDashboardOptions;
        ConfigurationView.PlantNameSaved += (_, plantName) =>
        {
            PlantNameTextBlock.Text = string.IsNullOrWhiteSpace(plantName) ? "Planta no configurada" : plantName;
        };
        ConfigurationView.AdditionalConfigurationSaved += async (_, _) =>
            await RefreshConfiguredAdditionalPaletteAsync();
        ConfigurationView.MessagesSaved += async updates =>
            await ApplySavedMessagesToCanvasAsync(updates);
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
        ConfigurationView.CloseRequested += (_, _) =>
        {
            ConfigurationFormOverlay.Visibility = Visibility.Collapsed;
            _isCompanyConfigurationOpen = false;
            _isRemissionConfigurationOpen = false;
            _isImagesOnConfigurationOpen = false;
            _isImagesOffConfigurationOpen = false;
            _isImageSizesConfigurationOpen = false;
            _isMessagesConfigurationOpen = false;
            _isAdditionalConfigurationOpen = false;
            _isOrdersConfigurationOpen = false;
            _isAccessConfigurationOpen = false;
            ConfigurationFormBorder.ClearValue(FrameworkElement.WidthProperty);
            ConfigurationFormBorder.ClearValue(FrameworkElement.HeightProperty);
        };
        ConfigurationView.AccessPermissionsSaved += async (_, _) =>
        {
            if (!await RefreshCurrentUserPermissionsAsync())
            {
                return;
            }

            ModuleStatusText.Text = "Los permisos del usuario se actualizaron desde dbo.PerfilDetalle.";
        };
        SecurityView.CloseRequested += (_, _) =>
        {
            SecurityFormOverlay.Visibility = Visibility.Collapsed;
            _isSecurityFormOpen = false;
            SecurityFormBorder.ClearValue(FrameworkElement.WidthProperty);
            SecurityFormBorder.ClearValue(FrameworkElement.HeightProperty);
        };
        SizeChanged += Page_SizeChanged;
        ClockText.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss");
        _clockTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();
        _plcStatusTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _plcStatusTimer.Interval = TimeSpan.FromSeconds(5);
        _plcStatusTimer.Tick += PlcStatusTimer_Tick;
        _canvasLayoutSaveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _canvasLayoutSaveTimer.Interval = TimeSpan.FromMilliseconds(350);
        _canvasLayoutSaveTimer.Tick += CanvasLayoutSaveTimer_Tick;
    }

    private void AddCanvasDeleteAccelerator(Windows.System.VirtualKey key)
    {
        var accelerator = new KeyboardAccelerator { Key = key };
        accelerator.Invoked += CanvasDeleteAccelerator_Invoked;
        MainPageRoot.KeyboardAccelerators.Add(accelerator);
    }

    private async void CanvasDeleteAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_isCanvasLocked
            || _selectedCanvasMessage is null
            || IsTextInputSource(Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot)))
        {
            return;
        }

        args.Handled = true;
        await RemoveSelectedCanvasMessageAsync();
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainPage_Loaded;
        UsernameTextBox.Focus(FocusState.Programmatic);
        try
        {
            var savedState = await _designerCanvasStore.LoadAsync();
            _designerCanvasState.IsLocked = savedState.IsLocked;
            _designerCanvasState.ImagePath = savedState.ImagePath;
            _designerCanvasState.CanvasWidth = savedState.CanvasWidth;
            _designerCanvasState.CanvasHeight = savedState.CanvasHeight;
            _designerCanvasState.MessageTextOverrides = new Dictionary<string, string>(
                savedState.MessageTextOverrides ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            _designerCanvasState.Controls = (savedState.Controls ?? new List<DesignerCanvasControlState>())
                .Where(control => control is not null
                    && !string.IsNullOrWhiteSpace(control.Id)
                    && control.Left >= 0
                    && control.Top >= 0
                    && double.IsFinite(control.Left)
                    && double.IsFinite(control.Top))
                .ToList();
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "Mensaje", StringComparison.OrdinalIgnoreCase)
                         && string.IsNullOrWhiteSpace(control.Text)))
            {
                control.Text = GetDefaultMessageText(control.MessageControl);
            }
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         !string.IsNullOrWhiteSpace(control.MessageControl)))
            {
                _designerCanvasState.MessageTextOverrides.TryAdd(control.MessageControl!, control.Text);
            }

            PrepareDesignerCanvasForViewport(
                savedState.CanvasWidth,
                savedState.CanvasHeight,
                DesignCanvas.ActualWidth,
                DesignCanvas.ActualHeight);

            if (!string.IsNullOrWhiteSpace(savedState.ImagePath))
            {
                var imageLoadError = await LoadMachineImageToDesignCanvasAsync(savedState.ImagePath, persistState: false);
                if (imageLoadError is not null)
                {
                    ModuleStatusText.Text = $"No se pudo restaurar la imagen del lienzo: {imageLoadError}";
                }
            }

            var hasSavedAdditionalImages = _designerCanvasState.Controls.Any(control =>
                string.Equals(control.Type, "ImagenAdicional", StringComparison.OrdinalIgnoreCase));
            var additionalImagesLoaded = !hasSavedAdditionalImages || await LoadAdditionalImageConfigurationsAsync();
            var hasSavedPumpImages = _designerCanvasState.Controls.Any(control =>
                string.Equals(control.Type, "ImagenBomba", StringComparison.OrdinalIgnoreCase));
            var pumpImagesLoaded = !hasSavedPumpImages || await LoadPumpImageConfigurationsAsync();
            var hasSavedValveImages = _designerCanvasState.Controls.Any(control =>
                string.Equals(control.Type, "ImagenValvula", StringComparison.OrdinalIgnoreCase));
            var valveImagesLoaded = !hasSavedValveImages || await LoadValveImageConfigurationsAsync();
            foreach (var control in _designerCanvasState.Controls)
            {
                if (string.Equals(control.Type, "ImagenAdicional", StringComparison.OrdinalIgnoreCase)
                    && !additionalImagesLoaded)
                {
                    continue;
                }

                if (string.Equals(control.Type, "ImagenBomba", StringComparison.OrdinalIgnoreCase)
                    && !pumpImagesLoaded)
                {
                    continue;
                }

                if (string.Equals(control.Type, "ImagenValvula", StringComparison.OrdinalIgnoreCase)
                    && !valveImagesLoaded)
                {
                    continue;
                }

                await AddCanvasControlToSurfaceAsync(control);
            }

            _isCanvasLocked = savedState.IsLocked;
            _isDesignerCanvasLoaded = true;
            UpdateCanvasEditMode();
            await SaveDesignerCanvasStateAsync();
        }
        catch (IOException exception)
        {
            ModuleStatusText.Text = $"No se pudo cargar el diseño guardado del lienzo: {exception.Message}";
            UpdateCanvasEditMode();
        }
        catch (UnauthorizedAccessException exception)
        {
            ModuleStatusText.Text = $"No hay permisos para leer el diseño guardado del lienzo: {exception.Message}";
            UpdateCanvasEditMode();
        }
        catch (JsonException exception)
        {
            ModuleStatusText.Text = $"El archivo del diseño guardado del lienzo no es válido: {exception.Message}";
            UpdateCanvasEditMode();
        }
        catch (InvalidDataException exception)
        {
            ModuleStatusText.Text = exception.Message;
            UpdateCanvasEditMode();
        }
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _plcStatusTimer.Stop();
    }

    private async void PlcStatusTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        await RefreshPlcStatusAsync();
    }

    private async Task RefreshPlcStatusAsync()
    {
        if (_isPlcStatusCheckRunning)
        {
            return;
        }

        _isPlcStatusCheckRunning = true;
        try
        {
            var additionalImageControls = _designerCanvasState.Controls
                .Where(control =>
                    IsCanvasPlcImageControl(control)
                    && !string.IsNullOrWhiteSpace(control.ImageControl))
                .Select(control => control.ImageControl!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var additionalValueControls = _designerCanvasState.Controls
                .Select(GetCanvasValueControlName)
                .Where(control => !string.IsNullOrWhiteSpace(control))
                .Select(control => control!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var status = await _plcCommunicationService.CheckConnectionAsync(
                additionalImageControls,
                additionalValueControls);

            var imageStatesChanged = false;
            if (_simulatedCanvasImageStates.Count == 0
                && status.State == PlcConnectionState.Connected
                && status.AdditionalImageValues is { } additionalImageValues)
            {
                foreach (var control in _designerCanvasState.Controls.Where(control =>
                             IsCanvasImageControl(control)))
                {
                    if (control.ImageControl is null
                        || !additionalImageValues.TryGetValue(control.ImageControl, out var value))
                    {
                        continue;
                    }

                    var isOn = value != 0;
                    if (control.IsOn == isOn)
                    {
                        continue;
                    }

                    control.IsOn = isOn;
                    imageStatesChanged = true;
                    var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
                        candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
                    if (container?.Child is Image image)
                    {
                        await SetAdditionalImageSourceAsync(image, control);
                    }
                }

            }

            if (imageStatesChanged)
            {
                await SaveDesignerCanvasStateAsync();
            }

            var additionalTextValues = status.State == PlcConnectionState.Connected
                ? status.AdditionalTextValues
                : null;
            var useSimulatedReadings = IsSimulatedReadingsActive;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         GetCanvasValueControlName(control) is not null))
            {
                var controlName = GetCanvasValueControlName(control);
                if (controlName is null)
                {
                    continue;
                }

                if (useSimulatedReadings
                    && _simulatedCanvasReadings.TryGetValue(controlName, out var simulatedValue))
                {
                    SetCanvasValue(control, simulatedValue.ToString("0.##", CultureInfo.CurrentCulture));
                    continue;
                }

                if (additionalTextValues is not null
                    && additionalTextValues.TryGetValue(controlName, out var value))
                {
                    SetCanvasValue(control, value.ToString("0.##", CultureInfo.CurrentCulture));
                }
                else
                {
                    SetCanvasValue(control, "????");
                }
            }

            var message = status.State switch
            {
                PlcConnectionState.Connected => $"Sesión iniciada • PLC conectado ({status.Port})",
                PlcConnectionState.Disconnected => $"Sesión iniciada • PLC sin comunicación ({status.Port})",
                PlcConnectionState.PortBusy => $"Sesión iniciada • puerto PLC ocupado ({status.Port})",
                _ => "Sesión iniciada • PLC sin verificar"
            };
            var color = status.State switch
            {
                PlcConnectionState.Connected => "#72D49A",
                PlcConnectionState.Disconnected or PlcConnectionState.PortBusy => "#F87171",
                _ => "#FBBF24"
            };

            SetSystemStatus(message, color, status.Detail);
            if ((_lastPlcConnectionState != status.State || _lastPlcStatusDetail != status.Detail)
                && _simulatedProcessState == SimulatedProcessState.Idle)
            {
                ModuleStatusText.Text = $"Estado PLC: {status.Detail}";
                _lastPlcConnectionState = status.State;
                _lastPlcStatusDetail = status.Detail;
            }
        }
        finally
        {
            _isPlcStatusCheckRunning = false;
        }
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isSecurityFormOpen)
        {
            SizeSecurityForm(e.NewSize);
        }
        else if (_isCompanyConfigurationOpen)
        {
            SizeCompanyConfigurationForm(e.NewSize);
        }
        else if (_isRemissionConfigurationOpen)
        {
            SizeRemissionConfigurationForm(e.NewSize);
        }
        else if (_isImagesOnConfigurationOpen)
        {
            SizeImagesOnConfigurationForm(e.NewSize);
        }
        else if (_isImagesOffConfigurationOpen)
        {
            SizeImagesOnConfigurationForm(e.NewSize);
        }
        else if (_isImageSizesConfigurationOpen)
        {
            SizeImagesOnConfigurationForm(e.NewSize);
        }
        else if (_isMessagesConfigurationOpen)
        {
            SizeImagesOnConfigurationForm(e.NewSize);
        }
        else if (_isAdditionalConfigurationOpen)
        {
            SizeCompanyConfigurationForm(e.NewSize);
        }
        else if (_isOrdersConfigurationOpen)
        {
            SizeOrdersConfigurationForm(e.NewSize);
        }
        else if (_isAccessConfigurationOpen)
        {
            SizeAccessConfigurationForm(e.NewSize);
        }

        var isCompact = e.NewSize.Width < 1100;
        var showPlantTools = _showPlantTools && !isCompact;
        PlantActionColumn.Width = showPlantTools ? new GridLength(_plantToolsWidth) : new GridLength(0);
        PlantToolsResizeColumn.Width = showPlantTools ? new GridLength(8) : new GridLength(0);
        PlantToolsPanel.Visibility = showPlantTools ? Visibility.Visible : Visibility.Collapsed;
        PlantToolsResizeHandle.Visibility = showPlantTools ? Visibility.Visible : Visibility.Collapsed;
        DashboardPanelsScrollViewer.Height = Math.Clamp(
            DashboardPanelsScrollViewer.Height,
            120,
            Math.Max(120, e.NewSize.Height * 0.55));
    }

    private void ViewPanel_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem { Tag: string panel } menuItem)
        {
            return;
        }

        var visibility = menuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        switch (panel)
        {
            case "DesignPanel":
                DesignPanel.Visibility = visibility;
                break;
            case "StatusPanel":
                StatusPanel.Visibility = visibility;
                break;
            case "DashboardPanelsScrollViewer":
                DashboardPanelsScrollViewer.Visibility = visibility;
                DashboardPanelsResizeHandle.Visibility = visibility;
                break;
            case "PlantToolsPanel":
                _showPlantTools = menuItem.IsChecked;
                PlantToolsPanel.Visibility = visibility;
                var showPlantTools = _showPlantTools && ActualWidth >= 1100;
                PlantActionColumn.Width = showPlantTools ? new GridLength(_plantToolsWidth) : new GridLength(0);
                PlantToolsResizeColumn.Width = showPlantTools ? new GridLength(8) : new GridLength(0);
                PlantToolsPanel.Visibility = showPlantTools ? Visibility.Visible : Visibility.Collapsed;
                PlantToolsResizeHandle.Visibility = showPlantTools ? Visibility.Visible : Visibility.Collapsed;
                break;
        }
    }

    private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var theme = ReferenceEquals(sender, LightThemeMenuItem)
            ? ElementTheme.Light
            : ElementTheme.Dark;
        ApplyTheme(theme, persist: true);
    }

    private void ApplyTheme(ElementTheme theme, bool persist)
    {
        RequestedTheme = theme;
        _themeBrushes.ApplyTheme(theme);
        UpdateInventoryStatusStyle();
        ConfigurationView.ApplyTheme(theme);
        MachineConfigurationView.ApplyTheme(theme);
        SecurityView.ApplyTheme(theme);
        StatusInfoBarStyler.Apply(SupplierEditorStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(SupplierFormStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(FormulaEditorStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(FormulaFormStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(ClientFormStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(ClientEditorStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(TruckEditorStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(OperatorEditorStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(OrderEditorStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(OrderFormStatusInfoBar, theme);
        StatusInfoBarStyler.Apply(LoginErrorInfoBar, theme);
        StatusInfoBarStyler.Apply(TruckFormStatusInfoBar, theme);
        DarkThemeMenuItem.IsChecked = theme == ElementTheme.Dark;
        LightThemeMenuItem.IsChecked = theme == ElementTheme.Light;

        if (persist)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ThemeSettingsPath)!);
                File.WriteAllText(ThemeSettingsPath, theme == ElementTheme.Light ? "Light" : "Dark");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ModuleStatusText.Text = $"No se pudo guardar la preferencia del tema: {exception.Message}";
            }
        }
    }

    private ElementTheme LoadSavedTheme()
    {
        try
        {
            return File.Exists(ThemeSettingsPath)
                && string.Equals(File.ReadAllText(ThemeSettingsPath).Trim(), "Light", StringComparison.OrdinalIgnoreCase)
                    ? ElementTheme.Light
                    : ElementTheme.Dark;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ModuleStatusText.Text = $"No se pudo leer la preferencia del tema: {exception.Message}";
            return ElementTheme.Dark;
        }
    }

    private void PlantToolsResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPlantToolsResizeActive = true;
        _plantToolsResizeStartX = e.GetCurrentPoint(DashboardContentGrid).Position.X;
        _plantToolsResizeStartWidth = _plantToolsWidth;
        PlantToolsResizeHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void PlantToolsResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPlantToolsResizeActive)
        {
            return;
        }

        var currentX = e.GetCurrentPoint(DashboardContentGrid).Position.X;
        var requestedWidth = _plantToolsResizeStartWidth - (currentX - _plantToolsResizeStartX);
        var maximumWidth = Math.Max(MinimumPlantToolsWidth, DashboardContentGrid.ActualWidth - MinimumWorkspaceWidth - PlantToolsResizeColumn.ActualWidth);
        _plantToolsWidth = Math.Clamp(requestedWidth, MinimumPlantToolsWidth, maximumWidth);
        PlantActionColumn.Width = new GridLength(_plantToolsWidth);
        e.Handled = true;
    }

    private void PlantToolsResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPlantToolsResizeActive = false;
        PlantToolsResizeHandle.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void PlantToolsResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPlantToolsResizeActive = false;
    }

    private void DashboardPanelsResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDashboardResizeActive = true;
        _dashboardResizeStartY = e.GetCurrentPoint(MainWorkspacePanel).Position.Y;
        _dashboardPanelsStartHeight = DashboardPanelsScrollViewer.Height;
        DashboardPanelsResizeHandle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void DashboardPanelsResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDashboardResizeActive)
        {
            return;
        }

        var currentY = e.GetCurrentPoint(MainWorkspacePanel).Position.Y;
        var requestedHeight = _dashboardPanelsStartHeight - (currentY - _dashboardResizeStartY);
        DashboardPanelsScrollViewer.Height = Math.Clamp(
            requestedHeight,
            120,
            Math.Max(120, MainWorkspacePanel.ActualHeight * 0.55));
        e.Handled = true;
    }

    private void DashboardPanelsResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDashboardResizeActive = false;
        DashboardPanelsResizeHandle.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void DashboardPanelsResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDashboardResizeActive = false;
    }

    private void DashboardPanelsScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DashboardPanelsGrid.Width = Math.Max(1520, e.NewSize.Width);
        DashboardPanelsGrid.Height = e.NewSize.Height;
    }

    private void DesignCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var height = e.NewSize.Height;
        if (!_isDesignerCanvasLoaded
            || width <= 0
            || height <= 0)
        {
            return;
        }

        if (_designerCanvasState.CanvasWidth <= 0 || _designerCanvasState.CanvasHeight <= 0)
        {
            var previousSizes = _designerCanvasState.Controls.ToDictionary(
                control => control.Id,
                GetCanvasControlSize,
                StringComparer.Ordinal);
            PrepareDesignerCanvasForViewport(0, 0, width, height);
            foreach (var control in _designerCanvasState.Controls)
            {
                var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
                    candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
                if (container is null || !previousSizes.TryGetValue(control.Id, out var previousSize))
                {
                    continue;
                }

                ResizeCanvasControlVisual(container, control, previousSize.Width, previousSize.Height);
                Canvas.SetLeft(container, control.Left);
                Canvas.SetTop(container, control.Top);
            }

            DesignCanvas.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
            {
                Rect = new Windows.Foundation.Rect(0, 0, width, height)
            };
            UpdateCanvasTransformHandles(_selectedCanvasMessage);
            _canvasLayoutSaveTimer.Stop();
            _canvasLayoutSaveTimer.Start();
            return;
        }

        var scaleX = width / _designerCanvasState.CanvasWidth;
        var scaleY = height / _designerCanvasState.CanvasHeight;
        if (!double.IsFinite(scaleX) || !double.IsFinite(scaleY) || scaleX <= 0 || scaleY <= 0)
        {
            return;
        }

        foreach (var control in _designerCanvasState.Controls)
        {
            var oldSize = GetCanvasControlSize(control);
            var newWidth = Math.Min(width, Math.Max(1, oldSize.Width * scaleX));
            var newHeight = Math.Min(height, Math.Max(1, oldSize.Height * scaleY));
            control.Left = Math.Clamp(control.Left * scaleX, 0, Math.Max(0, width - newWidth));
            control.Top = Math.Clamp(control.Top * scaleY, 0, Math.Max(0, height - newHeight));
            control.Width = newWidth;
            control.Height = newHeight;

            var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
                candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
            if (container is not null)
            {
                ResizeCanvasControlVisual(container, control, oldSize.Width, oldSize.Height);
                Canvas.SetLeft(container, control.Left);
                Canvas.SetTop(container, control.Top);
            }
        }

        _designerCanvasState.CanvasWidth = width;
        _designerCanvasState.CanvasHeight = height;
        DesignCanvas.Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, width, height)
        };
        UpdateCanvasTransformHandles(_selectedCanvasMessage);
        _canvasLayoutSaveTimer.Stop();
        _canvasLayoutSaveTimer.Start();
    }

    private static (double Width, double Height) GetCanvasControlSize(DesignerCanvasControlState control)
    {
        var defaultSize = control.Type switch
        {
            "Mensaje" => (260d, 56d),
            "CtlAdicional" or "CtlAgregado" or "CtlAgregados" or "CtlCemento"
                or "CtlAditivo" or "CtlAgua" or "CtlFibra" => (180d, 54d),
            "ImagenAdicional" or "ImagenBomba" or "ImagenValvula" => (40d, 40d),
            _ => (190d, 56d)
        };

        return (
            control.Width is > 0 ? control.Width.Value : defaultSize.Item1,
            control.Height is > 0 ? control.Height.Value : defaultSize.Item2);
    }

    private static void ResizeCanvasControlVisual(
        Border container,
        DesignerCanvasControlState control,
        double previousWidth,
        double previousHeight)
    {
        var width = control.Width!.Value;
        var height = control.Height!.Value;
        var fontScale = Math.Sqrt((width / previousWidth) * (height / previousHeight));
        container.Width = width;
        container.Height = height;

        if (string.Equals(control.Type, "Mensaje", StringComparison.OrdinalIgnoreCase)
            && container.Child is TextBlock message)
        {
            message.Width = width;
            message.Height = height;
            message.FontSize = GetResponsiveMessageFontSize(control, width, height);
            return;
        }

        if (IsCanvasImageControl(control) && container.Child is Image image)
        {
            image.Width = width;
            image.Height = height;
            return;
        }

        if (container.Child is not StackPanel layout)
        {
            return;
        }

        layout.Width = width;
        layout.Height = height;
        if (layout.Children.Count != 2)
        {
            return;
        }

        if (layout.Children[0] is Border captionBackground
            && captionBackground.Child is TextBlock caption)
        {
            captionBackground.MaxWidth = width;
            caption.FontSize = Math.Clamp(caption.FontSize * fontScale, 8, 96);
        }

        if (layout.Children[1] is Border valueBox)
        {
            valueBox.Width = Math.Min(80, width);
            valueBox.MinHeight = Math.Min(22, height);
            if (valueBox.Child is TextBlock valueText)
            {
                valueText.FontSize = Math.Clamp(valueText.FontSize * fontScale, 8, 96);
            }
        }
    }

    private void PrepareDesignerCanvasForViewport(
        double savedWidth,
        double savedHeight,
        double viewportWidth,
        double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        var contentWidth = _designerCanvasState.Controls
            .Select(control => control.Left + GetCanvasControlSize(control).Width)
            .DefaultIfEmpty(0)
            .Max();
        var contentHeight = _designerCanvasState.Controls
            .Select(control => control.Top + GetCanvasControlSize(control).Height)
            .DefaultIfEmpty(0)
            .Max();
        var sourceWidth = savedWidth > 0 ? savedWidth : Math.Max(viewportWidth, contentWidth);
        var sourceHeight = savedHeight > 0 ? savedHeight : Math.Max(viewportHeight, contentHeight);
        var scaleX = viewportWidth / sourceWidth;
        var scaleY = viewportHeight / sourceHeight;

        foreach (var control in _designerCanvasState.Controls)
        {
            var oldSize = GetCanvasControlSize(control);
            var width = Math.Min(viewportWidth, Math.Max(1, oldSize.Width * scaleX));
            var height = Math.Min(viewportHeight, Math.Max(1, oldSize.Height * scaleY));
            control.Left = Math.Clamp(control.Left * scaleX, 0, Math.Max(0, viewportWidth - width));
            control.Top = Math.Clamp(control.Top * scaleY, 0, Math.Max(0, viewportHeight - height));
            control.Width = width;
            control.Height = height;
        }

        _designerCanvasState.CanvasWidth = viewportWidth;
        _designerCanvasState.CanvasHeight = viewportHeight;
    }

    private void CanvasLayoutSaveTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_isDesignerCanvasLoaded)
        {
            _ = SaveDesignerCanvasStateAsync();
        }
    }

    private void ApplyMachineDashboardOptions(MachineDashboardOptions options)
    {
        DownloadActionButton.Visibility = Visibility.Visible;

        HumidityPanel.Visibility = options.HumidityEnabled ? Visibility.Visible : Visibility.Collapsed;
        HumidityPanelColumn.Width = options.HumidityEnabled
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        HumidityPanelColumn.MinWidth = options.HumidityEnabled ? 360 : 0;

        AbsorptionPanel.Visibility = options.AbsorptionEnabled ? Visibility.Visible : Visibility.Collapsed;
        AbsorptionPanelColumn.Width = options.AbsorptionEnabled
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        AbsorptionPanelColumn.MinWidth = options.AbsorptionEnabled ? 360 : 0;
    }

    private void ApplyMachineAdjustmentValues(IReadOnlyDictionary<string, string> values)
    {
        _dashboardMachineConfiguration = values;
        RefreshFormulaHopperStates();

        for (var index = 1; index <= 8; index++)
        {
            var materialName = values.GetValueOrDefault($"A{index}");
            var humidity = HumidityRows[index - 1];
            humidity.Nombre = string.IsNullOrWhiteSpace(materialName) ? $"AGREGADO {index}" : materialName;
            humidity.Valor = values.GetValueOrDefault($"H{index}", "0");

            var absorption = AbsorptionRows[index - 1];
            absorption.Nombre = humidity.Nombre;
            absorption.Valor = values.GetValueOrDefault($"O{index}", "0");
        }
    }

    private void LoadDashboardMaterials(
        IReadOnlyDictionary<string, string> configuration)
    {
        _dashboardMachineConfiguration = configuration;
        _hasDashboardMaterialData = true;
        RefreshDashboardMaterials();
    }

    private void RefreshDashboardMaterials()
    {
        if (!_hasDashboardMaterialData)
        {
            return;
        }

        MaterialRows.Clear();
        foreach (var control in _designerCanvasState.Controls)
        {
            var binding = GetDashboardMaterialBinding(control);
            if (binding is null)
            {
                continue;
            }

            var parameter = binding.Value.Parameter;
            var name = _dashboardMachineConfiguration.GetValueOrDefault(parameter)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "DESHABILITADA";
            }

            var formulaQuantityField = GetDashboardFormulaQuantityField(parameter);
            var formulaQuantity = formulaQuantityField is not null
                ? _dashboardFormulaValues?.GetValueOrDefault(formulaQuantityField)?.Trim()
                : null;
            var quantity = name.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(formulaQuantity)
                    ? "0"
                    : formulaQuantity;

            MaterialRows.Add(new MaterialRow(name, quantity));
        }

        MaterialRowPairs.Clear();
        foreach (var pair in CreateMaterialPairs())
        {
            MaterialRowPairs.Add(pair);
        }
    }

    private static string? GetDashboardFormulaQuantityField(string parameter)
    {
        if (parameter.StartsWith('A') || parameter.StartsWith('C') || parameter.StartsWith('D'))
        {
            return $"{parameter}C";
        }

        return parameter switch
        {
            "G1" => "AguaC",
            "G2" => "Agua2C",
            "G3" => "Agua3C",
            "FI1" => "Fibra1C",
            "FI2" => "Fibra2C",
            "FI3" => "Fibra3C",
            _ => null
        };
    }

    private void RefreshFormulaHopperStates()
    {
        foreach (var (fieldName, state) in _formulaMaterialStates)
        {
            var parameter = GetMachineMaterialParameter(fieldName);
            var material = _dashboardMachineConfiguration.GetValueOrDefault(parameter)?.Trim();
            state.Text = string.IsNullOrWhiteSpace(material) ? "DESHABILITADA" : material;
        }
    }

    private static string GetMachineMaterialParameter(string formulaField) => formulaField switch
    {
        "Agua" => "G1",
        "Agua2" => "G2",
        "Agua3" => "G3",
        "Fibra1" => "FI1",
        "Fibra2" => "FI2",
        "Fibra3" => "FI3",
        _ => formulaField
    };

    private IEnumerable<MaterialRowPair> CreateMaterialPairs() =>
        Enumerable.Range(0, (MaterialRows.Count + 1) / 2)
            .Select(index => new MaterialRowPair(
                MaterialRows[index * 2],
                index * 2 + 1 < MaterialRows.Count
                    ? MaterialRows[index * 2 + 1]
                    : new MaterialRow(string.Empty, string.Empty)));

    private static (string Parameter, string Category)? GetDashboardMaterialBinding(
        DesignerCanvasControlState control)
    {
        if (string.Equals(control.Type, "CtlAgregado", StringComparison.OrdinalIgnoreCase)
            && TryGetCanvasMaterialControlIndex(control.AggregateControl, "ctlAgregado", out var aggregateIndex))
        {
            return ($"A{aggregateIndex}", "AGREGADOS");
        }

        if (string.Equals(control.Type, "CtlCemento", StringComparison.OrdinalIgnoreCase)
            && TryGetCanvasMaterialControlIndex(control.CementControl, "ctlCemento", out var cementIndex))
        {
            return ($"C{cementIndex}", "CEMENTOS");
        }

        if (string.Equals(control.Type, "CtlAditivo", StringComparison.OrdinalIgnoreCase)
            && TryGetCanvasMaterialControlIndex(control.AdditiveControl, "ctlAditivo", out var additiveIndex))
        {
            return ($"D{additiveIndex}", "ADITIVOS");
        }

        if (string.Equals(control.Type, "CtlAgua", StringComparison.OrdinalIgnoreCase))
        {
            var waterIndex = string.Equals(control.WaterControl, "ctlAgua", StringComparison.OrdinalIgnoreCase)
                ? 1
                : TryGetCanvasMaterialControlIndex(control.WaterControl, "ctlAgua", out var parsedWaterIndex)
                    ? parsedWaterIndex
                    : 0;
            return waterIndex > 0 ? ($"G{waterIndex}", "AGUAS") : null;
        }

        if (string.Equals(control.Type, "CtlFibra", StringComparison.OrdinalIgnoreCase)
            && TryGetCanvasMaterialControlIndex(control.FiberControl, "ctlFibra", out var fiberIndex))
        {
            return ($"FI{fiberIndex}", "FIBRAS");
        }

        return null;
    }

    private static bool TryGetCanvasMaterialControlIndex(string? controlName, string prefix, out int index)
    {
        index = 0;
        return controlName is not null
            && controlName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                controlName.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out index)
            && index > 0;
    }

    private IEnumerable<RemissionFieldPair> CreateRemissionPairs() =>
        Enumerable.Range(0, RemissionRows.Count / 2)
            .Select(index => new RemissionFieldPair(RemissionRows[index * 2], RemissionRows[index * 2 + 1]));

    private async void SaveDashboardAdjustment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string parameterPrefix } saveButton)
        {
            return;
        }

        if (!await RefreshCurrentUserPermissionsAsync()
            || !RequireActionPermission("Maquina", "Editar"))
        {
            return;
        }

        var rows = parameterPrefix == "H" ? HumidityRows : AbsorptionRows;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!double.TryParse(row.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                && !double.TryParse(row.Valor, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
                || !double.IsFinite(value)
                || value < 0)
            {
                ModuleStatusText.Text = $"El porcentaje de {row.Nombre} debe ser un número válido mayor o igual que cero.";
                return;
            }

            values[row.Parameter] = value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        saveButton.IsEnabled = false;
        ModuleStatusText.Text = "Guardando ajuste en la configuración de la máquina...";
        try
        {
            await _machineConfigurationService.SaveConfigurationAsync(values);
            ModuleStatusText.Text = $"Ajuste de {(parameterPrefix == "H" ? "humedad" : "absorción")} guardado en BlapaciSQL.";
        }
        catch (SqlException exception)
        {
            ModuleStatusText.Text = $"No se pudo guardar el ajuste (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ModuleStatusText.Text = exception.Message;
        }
        finally
        {
            saveButton.IsEnabled = true;
        }
    }

    private async void ModuleNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string module })
        {
            return;
        }

        if (TryGetPermissionModule(module, out var permissionModule))
        {
            if (!await RefreshCurrentUserPermissionsAsync())
            {
                return;
            }

            if (!HasNavigationPermission(module, permissionModule))
            {
                if (sender is MenuFlyoutItem deniedItem)
                {
                    deniedItem.IsEnabled = false;
                }

                ModuleStatusText.Text = HasModulePermission(permissionModule)
                    ? $"Tu perfil no tiene permiso de Visualizar en {permissionModule}."
                    : $"Tu perfil no tiene acceso al módulo {permissionModule}.";
                return;
            }
        }

        if (module == "Seguridad")
        {
            if (_authenticatedUserId is not int userId)
            {
                return;
            }

            SizeSecurityForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
            await SecurityView.SetAccessAsync(userId, _authenticatedUsername, _currentUserPermissions);
            _isSecurityFormOpen = true;
            SecurityFormOverlay.Visibility = Visibility.Visible;
            return;
        }

        if (module == "Config:Machine")
        {
            MachineConfigurationOverlay.Visibility = Visibility.Visible;
            await MachineConfigurationView.LoadAsync();
            return;
        }

        if (module.StartsWith("Config:", StringComparison.Ordinal) && sender is MenuFlyoutItem configurationItem)
        {
            var selectedTab = module switch
            {
                "Config:PLC" => 1,
                "Config:Company" => 2,
                "Config:Remission" => 3,
                "Config:ImagesOn" => 4,
                "Config:ImagesOff" => 5,
                "Config:ImageSize" => 6,
                "Config:Messages" => 7,
                "Config:Additional" => 8,
                "Config:Orders" => 9,
                "Config:BackupReports" or "Config:BackupDatabase" => 3,
                "Config:Access" => 10,
                _ => -1
            };

            if (selectedTab >= 0)
            {
                var backupToolsOnly = module is "Config:BackupReports" or "Config:BackupDatabase";
                ConfigurationView.ShowBackupToolsOnly(backupToolsOnly);
                _isCompanyConfigurationOpen = selectedTab == 2;
                _isRemissionConfigurationOpen = selectedTab == 3;
                _isImagesOnConfigurationOpen = selectedTab == 4;
                _isImagesOffConfigurationOpen = selectedTab == 5;
                _isImageSizesConfigurationOpen = selectedTab == 6;
                _isMessagesConfigurationOpen = selectedTab == 7;
                _isAdditionalConfigurationOpen = selectedTab == 8;
                _isOrdersConfigurationOpen = selectedTab == 9;
                _isAccessConfigurationOpen = selectedTab == 10;
                if (_isCompanyConfigurationOpen)
                {
                    SizeCompanyConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isRemissionConfigurationOpen)
                {
                    SizeRemissionConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isImagesOnConfigurationOpen)
                {
                    SizeImagesOnConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isImagesOffConfigurationOpen)
                {
                    SizeImagesOnConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isImageSizesConfigurationOpen)
                {
                    SizeImagesOnConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isMessagesConfigurationOpen)
                {
                    SizeImagesOnConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isAdditionalConfigurationOpen)
                {
                    SizeCompanyConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isOrdersConfigurationOpen)
                {
                    SizeOrdersConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else if (_isAccessConfigurationOpen)
                {
                    SizeAccessConfigurationForm(new Windows.Foundation.Size(ActualWidth, ActualHeight));
                }
                else
                {
                    ConfigurationFormBorder.ClearValue(FrameworkElement.WidthProperty);
                    ConfigurationFormBorder.ClearValue(FrameworkElement.HeightProperty);
                }

                ConfigurationView.ShowTab(selectedTab, onlySelectedTab: selectedTab is >= 1 and <= 10);
                ConfigurationFormOverlay.Visibility = Visibility.Visible;
                if (selectedTab == 1)
                {
                    await ConfigurationView.LoadPlcConfigurationAsync();
                }
                else if (selectedTab == 2)
                {
                    await ConfigurationView.LoadCompanyConfigurationAsync();
                }
                else if (selectedTab == 3 && !backupToolsOnly && HasModulePermission("Datos Remision"))
                {
                    await ConfigurationView.LoadRemissionConfigurationAsync();
                }
                else if (selectedTab == 4)
                {
                    await ConfigurationView.LoadImageConfigurationAsync(isOn: true);
                }
                else if (selectedTab == 5)
                {
                    await ConfigurationView.LoadImageConfigurationAsync(isOn: false);
                }
                else if (selectedTab == 6)
                {
                    await ConfigurationView.LoadImageSizesAsync();
                }
                else if (selectedTab == 7)
                {
                    await ConfigurationView.LoadMessagesAsync();
                }
                else if (selectedTab == 8)
                {
                    await ConfigurationView.LoadAdditionalConfigurationAsync();
                }
                else if (selectedTab == 9)
                {
                    if (HasModulePermission("Configurar Pedido"))
                    {
                        await ConfigurationView.LoadOrderConfigurationAsync();
                        await ConfigurationView.LoadOrderOptionsAsync();
                    }

                    if (HasModulePermission("Turnos"))
                    {
                        await ConfigurationView.LoadOrderSchedulesAsync();
                    }
                }
                else if (selectedTab == 10)
                {
                    await ConfigurationView.LoadAccessProfilesAsync();
                }

                return;
            }

            ModuleStatusText.Text = $"{configurationItem.Text}: pantalla pendiente de migración. No se ejecutó ninguna operación de la aplicación original.";
            return;
        }

        if (module is "Entradas" or "Salidas")
        {
            InventoryFormOverlay.Visibility = Visibility.Visible;
            if (module == "Entradas")
            {
                InventoryEntriesCheckBox.IsChecked = true;
            }
            else
            {
                InventoryExitsCheckBox.IsChecked = true;
            }

            await LoadInventoryAsync();
            return;
        }

        if (module == "ReportCenter")
        {
            ReportFormOverlay.Visibility = Visibility.Visible;
            OpenReportCenter();
            return;
        }

        if (module == "Fórmulas")
        {
            FormulaFormOverlay.Visibility = Visibility.Visible;
            await LoadFormulasAsync();
            return;
        }

        if (module == "Proveedores")
        {
            SupplierFormOverlay.Visibility = Visibility.Visible;
            await LoadSuppliersAsync();
            return;
        }

        if (module == "Clientes")
        {
            ClientFormOverlay.Visibility = Visibility.Visible;
            await LoadClientsAsync();
            return;
        }

        if (module == "Camiones")
        {
            TruckFormOverlay.Visibility = Visibility.Visible;
            await LoadTrucksAsync();
            return;
        }

        if (module == "Pedidos")
        {
            OrderFormOverlay.Visibility = Visibility.Visible;
            await LoadOrdersAsync();
            return;
        }

        if (module == "Carga Manual")
        {
            ManualLoadSaveButton.IsEnabled = AccessPermissionLookup.IsActionEnabled(
                _currentUserPermissions,
                "Carga Manual",
                "Alta");
            ManualLoadOverlay.Visibility = Visibility.Visible;
            await LoadManualLoadRecipesAsync();
            return;
        }

        if (module == "Configuración")
        {
            ConfigurationView.ShowBackupToolsOnly(false);
            _isCompanyConfigurationOpen = false;
            _isRemissionConfigurationOpen = false;
            _isImagesOnConfigurationOpen = false;
            _isImagesOffConfigurationOpen = false;
            _isImageSizesConfigurationOpen = false;
            _isMessagesConfigurationOpen = false;
            _isAdditionalConfigurationOpen = false;
            ConfigurationFormBorder.ClearValue(FrameworkElement.WidthProperty);
            ConfigurationFormBorder.ClearValue(FrameworkElement.HeightProperty);
            ConfigurationView.ShowTab(0, onlySelectedTab: false);
            ConfigurationFormOverlay.Visibility = Visibility.Visible;
            return;
        }

        ModuleStatusText.Text = $"{module}: pantalla pendiente de migración. No se ejecutó ninguna operación de la aplicación original.";
    }

    private void SizeCompanyConfigurationForm(Windows.Foundation.Size availableSize)
    {
        ConfigurationFormBorder.Width = Math.Min(1120, Math.Max(0, availableSize.Width - 48));
        ConfigurationFormBorder.Height = Math.Min(420, Math.Max(0, availableSize.Height - 48));
    }

    private void SizeRemissionConfigurationForm(Windows.Foundation.Size availableSize)
    {
        ConfigurationFormBorder.Width = Math.Min(1600, Math.Max(0, availableSize.Width - 24));
        ConfigurationFormBorder.Height = Math.Min(1000, Math.Max(0, availableSize.Height - 24));
    }

    private void SizeImagesOnConfigurationForm(Windows.Foundation.Size availableSize)
    {
        ConfigurationFormBorder.Width = Math.Min(1760, Math.Max(0, availableSize.Width - 24));
        ConfigurationFormBorder.Height = Math.Min(1050, Math.Max(0, availableSize.Height - 24));
    }

    private void SizeOrdersConfigurationForm(Windows.Foundation.Size availableSize)
    {
        ConfigurationFormBorder.Width = Math.Min(1120, Math.Max(0, availableSize.Width - 24));
        ConfigurationFormBorder.Height = Math.Min(760, Math.Max(0, availableSize.Height - 24));
    }

    private void SizeAccessConfigurationForm(Windows.Foundation.Size availableSize)
    {
        ConfigurationFormBorder.Width = Math.Min(1900, Math.Max(0, availableSize.Width - 24));
        ConfigurationFormBorder.Height = Math.Min(1100, Math.Max(0, availableSize.Height - 24));
    }

    private void SizeSecurityForm(Windows.Foundation.Size availableSize)
    {
        SecurityFormBorder.Width = Math.Min(560, Math.Max(0, availableSize.Width - 24));
        SecurityFormBorder.Height = Math.Min(440, Math.Max(0, availableSize.Height - 24));
    }

    private void CloseManualLoad_Click(object sender, RoutedEventArgs e)
    {
        ManualLoadOverlay.Visibility = Visibility.Collapsed;
    }

    private async void ProgramAction_Click(object sender, RoutedEventArgs e)
    {
        if (!await RefreshCurrentUserPermissionsAsync()
            || !HasModulePermission("Carga Manual")
            || !HasActionPermission("Carga Manual", "Alta"))
        {
            ModuleStatusText.Text = "Tu perfil no tiene permiso para programar cargas.";
            return;
        }

        var recipes = new ComboBox
        {
            DisplayMemberPath = "Code",
            PlaceholderText = "Selecciona receta",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var description = new TextBox { IsReadOnly = true };
        var quantity = new NumberBox { Value = 0, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var clients = new ComboBox
        {
            DisplayMemberPath = "Client",
            IsEditable = false,
            PlaceholderText = "Selecciona cliente",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var construction = new ComboBox
        {
            IsEditable = false,
            IsEnabled = false,
            PlaceholderText = "Selecciona obra",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var trucks = new ComboBox
        {
            DisplayMemberPath = "Unit",
            IsEditable = true,
            PlaceholderText = "Selecciona camión",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var operators = new ComboBox
        {
            DisplayMemberPath = "Name",
            IsEditable = true,
            PlaceholderText = "Selecciona chofer",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var capacity = new TextBox { IsReadOnly = true };
        var orders = new ComboBox { IsEditable = true, PlaceholderText = "Selecciona pedido", HorizontalAlignment = HorizontalAlignment.Stretch };
        var orderStatus = new TextBox { IsReadOnly = true };
        var products = new ComboBox
        {
            IsEditable = true,
            IsEnabled = false,
            PlaceholderText = "Selecciona producto",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var paymentConditions = new ComboBox
        {
            IsEditable = true,
            IsEnabled = false,
            PlaceholderText = "Selecciona condición",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var dischargeTypes = new ComboBox
        {
            IsEditable = true,
            IsEnabled = false,
            PlaceholderText = "Selecciona tipo",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var dispatcher = new TextBox { Text = "1" };
        var remissionNumber = new TextBox { IsReadOnly = true };
        var orderedQuantity = new NumberBox { Value = 0, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var suppliedQuantity = new NumberBox { Value = 0, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var toSupplyQuantity = new NumberBox { Value = 0, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var observations = new TextBox { AcceptsReturn = true, Height = 72, TextWrapping = TextWrapping.Wrap };
        var status = new InfoBar { IsOpen = false, IsClosable = false };
        SetProgramStatus("Cargando catálogos...", InfoBarSeverity.Informational);
        ContentDialog? programDialog = null;
        var orderStatusRequestId = 0;

        recipes.SelectionChanged += (_, _) =>
        {
            if (recipes.SelectedItem is ProgramFormulaOption selectedRecipe)
            {
                description.Text = selectedRecipe.Description;
            }

            _dashboardFormulaValues = (recipes.SelectedItem as ProgramFormulaOption)?.Values
                ?? _simulatedRemission?.FormulaValues;
            RefreshDashboardMaterials();

            var hasSelectedRecipe = recipes.SelectedItem is ProgramFormulaOption;
            products.IsEnabled = hasSelectedRecipe;
            paymentConditions.IsEnabled = hasSelectedRecipe;
            dischargeTypes.IsEnabled = hasSelectedRecipe;
        };
        clients.SelectionChanged += (_, _) =>
        {
            if (clients.SelectedItem is not ClientRecord selectedClient)
            {
                construction.ItemsSource = null;
                construction.SelectedItem = null;
                construction.Text = string.Empty;
                construction.PlaceholderText = "Selecciona un cliente";
                construction.IsEnabled = false;
                return;
            }

            var clientConstruction = selectedClient.Construction.Trim();
            var constructions = string.IsNullOrWhiteSpace(clientConstruction)
                ? Array.Empty<string>()
                : new[] { clientConstruction };
            construction.ItemsSource = constructions;
            construction.SelectedItem = constructions.FirstOrDefault();
            construction.PlaceholderText = constructions.Length > 0
                ? "Obra del cliente"
                : "Cliente sin obra configurada";
            construction.IsEnabled = constructions.Length > 0;
        };
        trucks.SelectionChanged += (_, _) =>
        {
            if (trucks.SelectedItem is TruckRecord selectedTruck)
            {
                capacity.Text = selectedTruck.Capacity;
            }
        };
        orders.SelectionChanged += async (_, _) =>
        {
            var requestId = ++orderStatusRequestId;
            var selectedOrder = orders.SelectedItem as string;
            ClearProgramOrderFields();

            if (string.IsNullOrWhiteSpace(selectedOrder) ||
                string.Equals(selectedOrder, "Ocasional", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!int.TryParse(selectedOrder, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNumber))
            {
                SetProgramStatus($"El número de pedido «{selectedOrder}» no es válido.", InfoBarSeverity.Error);
                return;
            }

            SetProgramStatus($"Consultando datos del pedido {orderNumber}...", InfoBarSeverity.Informational);
            try
            {
                var order = await _orderService.GetManualLoadOrderDetailsAsync(orderNumber);
                if (requestId != orderStatusRequestId ||
                    !string.Equals(orders.SelectedItem as string, selectedOrder, StringComparison.Ordinal))
                {
                    return;
                }

                if (order is null)
                {
                    SetProgramStatus($"No se encontró información activa para el pedido {orderNumber}.", InfoBarSeverity.Warning);
                    return;
                }

                SetProgramComboValue(
                    recipes,
                    order.Recipe,
                    item => item is ProgramFormulaOption recipe ? recipe.Code : null);
                description.Text = !string.IsNullOrWhiteSpace(order.Description)
                    ? order.Description
                    : (recipes.SelectedItem as ProgramFormulaOption)?.Description ?? string.Empty;
                SetProgramComboValue(clients, order.Client, item => item is ClientRecord client ? client.Client : null);
                orderStatus.Text = order.Status;
                SetProgramComboValue(products, order.Product, item => item as string);
                SetProgramComboValue(paymentConditions, order.PaymentCondition, item => item as string);
                SetProgramComboValue(dischargeTypes, order.DischargeType, item => item as string);
                orderedQuantity.Value = (double)(order.Quantity ?? 0);
                suppliedQuantity.Value = (double)(order.Supplied ?? 0);
                toSupplyQuantity.Value = (double)(order.ToSupply ?? 0);
                observations.Text = order.Observations;
                SetProgramStatus($"Datos del pedido {orderNumber} cargados.", InfoBarSeverity.Success);
            }
            catch (SqlException exception)
            {
                if (requestId == orderStatusRequestId)
                {
                    SetProgramStatus($"No se pudo consultar el pedido (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
                }
            }
            catch (InvalidOperationException exception)
            {
                if (requestId == orderStatusRequestId)
                {
                    SetProgramStatus($"No se pudo consultar el pedido: {exception.Message}", InfoBarSeverity.Error);
                }
            }
        };

        void ClearProgramOrderFields()
        {
            recipes.SelectedItem = null;
            description.Text = string.Empty;
            clients.SelectedItem = null;
            clients.Text = string.Empty;
            construction.SelectedItem = null;
            construction.Text = string.Empty;
            orderStatus.Text = string.Empty;
            products.SelectedItem = null;
            products.Text = string.Empty;
            paymentConditions.SelectedItem = null;
            paymentConditions.Text = string.Empty;
            dischargeTypes.SelectedItem = null;
            dischargeTypes.Text = string.Empty;
            orderedQuantity.Value = 0;
            suppliedQuantity.Value = 0;
            toSupplyQuantity.Value = 0;
            observations.Text = string.Empty;
        }

        var programButton = new Button
        {
            Content = "Programar",
            IsEnabled = false,
            Style = (Style)Resources["DefaultButtonStyle"]
        };
        var lastLoadButton = new Button
        {
            Content = "Última carga",
            IsEnabled = false,
            Style = (Style)Resources["DefaultButtonStyle"]
        };
        ToolTipService.SetToolTip(
            programButton,
            "Valida y muestra una vista previa local. No guarda cambios ni envía órdenes al PLC.");
        ToolTipService.SetToolTip(lastLoadButton, "Carga los datos de la última descarga registrada.");
        programButton.Click += async (_, _) =>
        {
            var validationErrors = new List<string>();
            var selectedRecipe = recipes.SelectedItem as ProgramFormulaOption;
            var selectedTruck = trucks.SelectedItem as TruckRecord;
            var selectedOrder = orders.SelectedItem as string;
            if (selectedRecipe is null)
            {
                validationErrors.Add("Selecciona una receta.");
            }

            var selectedClient = clients.SelectedItem as ClientRecord;
            if (selectedClient is null || string.IsNullOrWhiteSpace(selectedClient.Client))
            {
                validationErrors.Add("Selecciona un cliente.");
            }

            var selectedConstruction = construction.SelectedItem as string;
            if (selectedClient is not null && string.IsNullOrWhiteSpace(selectedConstruction))
            {
                validationErrors.Add($"El cliente «{selectedClient.Client}» no tiene una obra configurada.");
            }

            if (string.IsNullOrWhiteSpace(selectedOrder))
            {
                validationErrors.Add("Selecciona un pedido.");
            }

            if (selectedTruck is null || string.IsNullOrWhiteSpace(selectedTruck.Unit))
            {
                validationErrors.Add("Selecciona un camión de la lista.");
            }

            if (!TryGetPositiveDecimal(quantity, out var validatedQuantity))
            {
                validationErrors.Add("La cantidad debe ser un número mayor que cero.");
            }

            var hasValidOrderedQuantity = TryGetNonNegativeDecimal(orderedQuantity, out var validatedOrderedQuantity);
            var hasValidSuppliedQuantity = TryGetNonNegativeDecimal(suppliedQuantity, out var validatedSuppliedQuantity);
            var hasValidToSupplyQuantity = TryGetNonNegativeDecimal(toSupplyQuantity, out var validatedToSupplyQuantity);
            if (!hasValidOrderedQuantity || !hasValidSuppliedQuantity || !hasValidToSupplyQuantity)
            {
                validationErrors.Add("Pedidos, surtidos y por surtir deben ser números iguales o mayores que cero.");
            }

            if (validationErrors.Count > 0)
            {
                SetProgramStatus(string.Join(" ", validationErrors), InfoBarSeverity.Error);
                return;
            }

            var validatedClient = selectedClient!;
            var validatedConstruction = selectedConstruction!;
            SetProgramStatus("Validando los materiales de la fórmula contra las tolvas...", InfoBarSeverity.Informational);
            programButton.IsEnabled = false;
            IReadOnlyDictionary<string, string> currentMachineConfiguration;
            try
            {
                var machineConfiguration = await _machineConfigurationService.GetConfigurationAsync();
                currentMachineConfiguration = machineConfiguration.Values;
            }
            catch (SqlException exception)
            {
                SetProgramStatus($"No se pudieron validar los materiales de las tolvas (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
                return;
            }
            catch (InvalidOperationException exception)
            {
                SetProgramStatus($"No se pudieron validar los materiales de las tolvas: {exception.Message}", InfoBarSeverity.Error);
                return;
            }
            finally
            {
                programButton.IsEnabled = true;
            }

            if (HasFormulaMaterialMismatch(selectedRecipe!.Values, currentMachineConfiguration))
            {
                SetProgramStatus(
                    "Error de materiales: Los materiales configurados en la fórmula no corresponden a los que se encuentran actualmente en las tolvas.",
                    InfoBarSeverity.Error);
                return;
            }

            SetProgramStatus(string.Join(
                Environment.NewLine,
                "Validación local correcta (modo de prueba).",
                $"Receta: {selectedRecipe!.Code} — {selectedRecipe.Description}",
                $"Cantidad: {validatedQuantity.ToString(CultureInfo.CurrentCulture)}",
                $"Cliente: {validatedClient.Client}",
                $"Obra: {validatedConstruction}",
                $"Camión: {selectedTruck!.Unit}",
                $"Chofer: {operators.Text.Trim()}",
                $"Remisión: {remissionNumber.Text.Trim()}",
                $"Pedido: {selectedOrder}",
                $"Pedidos / surtidos / por surtir: {validatedOrderedQuantity.ToString(CultureInfo.CurrentCulture)} / " +
                $"{validatedSuppliedQuantity.ToString(CultureInfo.CurrentCulture)} / " +
                $"{validatedToSupplyQuantity.ToString(CultureInfo.CurrentCulture)}",
                "Validación local solamente: no se guardó ni se envió ninguna orden al PLC."),
                InfoBarSeverity.Success);
            RemissionRows[0].Valor = remissionNumber.Text.Trim();
            RemissionRows[1].Valor = selectedRecipe!.Code;
            RemissionRows[2].Valor = selectedRecipe.Description;
            RemissionRows[3].Valor = validatedQuantity.ToString(CultureInfo.CurrentCulture);
            RemissionRows[4].Valor = validatedClient.Client;
            RemissionRows[5].Valor = validatedConstruction;
            RemissionRows[6].Valor = selectedTruck!.Unit;
            RemissionRows[7].Valor = orders.Text.Trim();
            RemissionPanel.IsExpanded = true;
            await ResetSimulatedCanvasImageStatesAsync();
            _dashboardFormulaValues = selectedRecipe!.Values;
            _simulatedCanvasReadings.Clear();
            RefreshDashboardMaterials();
            ModuleStatusText.Text = "Receta preparada para cargarse";
            _remissionBeforeSimulation ??= RemissionRows.Select(row => row.Valor).ToArray();
            _simulatedRemission = new SimulatedRemission(
                remissionNumber.Text.Trim(),
                selectedRecipe.Code,
                selectedRecipe.Description,
                validatedQuantity.ToString(CultureInfo.CurrentCulture),
                validatedClient.Client,
                validatedConstruction,
                selectedTruck!.Unit,
                operators.Text.Trim(),
                orders.Text.Trim(),
                orderStatus.Text.Trim(),
                products.Text.Trim(),
                paymentConditions.Text.Trim(),
                dischargeTypes.Text.Trim(),
                dispatcher.Text.Trim(),
                validatedOrderedQuantity.ToString(CultureInfo.CurrentCulture),
                validatedSuppliedQuantity.ToString(CultureInfo.CurrentCulture),
                validatedToSupplyQuantity.ToString(CultureInfo.CurrentCulture),
                observations.Text.Trim(),
                selectedRecipe.Values);
            _simulatedProcessState = SimulatedProcessState.Prepared;
            UpdateSimulatedActionButtons();
            programDialog?.Hide();
        };
        lastLoadButton.Click += async (_, _) => await LoadLatestDownloadAsync();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(programButton);
        toolbar.Children.Add(lastLoadButton);

        var formContent = new StackPanel { Spacing = 8, Width = 480 };
        formContent.Children.Add(toolbar);
        formContent.Children.Add(status);
        var sections = new StackPanel { Spacing = 10 };
        sections.Children.Add(CreateProgramFormSection(
            "Datos de Fórmula",
            ("Código de receta:", (Control)recipes),
            ("Descripción:", description),
            ("Cantidad:", quantity),
            ("Cliente *:", clients),
            ("Obra *:", construction),
            ("Camión:", trucks),
            ("Chofer:", operators),
            ("Capacidad:", capacity)));
        sections.Children.Add(CreateProgramFormSection(
            "Datos de Remisión",
            ("Pedido:", orders),
            ("Estatus:", orderStatus),
            ("Producto:", products),
            ("Condiciones pago:", paymentConditions),
            ("Tipo descarga:", dischargeTypes),
            ("Despachador:", dispatcher),
            ("No de remisión:", remissionNumber)));
        sections.Children.Add(CreateProgramFormSection(
            "Datos de Remisión Opcionales",
            ("Pedidos (m³):", orderedQuantity),
            ("Surtidos (m³):", suppliedQuantity),
            ("x Surtir (m³):", toSupplyQuantity),
            ("Observaciones:", observations)));
        var scrollViewer = new ScrollViewer
        {
            Content = sections,
            MaxHeight = 560,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        formContent.Children.Add(scrollViewer);

        programDialog = new ContentDialog
        {
            Title = "Programar Carga Automática",
            Content = formContent,
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = RequestedTheme,
            XamlRoot = XamlRoot
        };
        programDialog.Closed += (_, _) =>
        {
            _dashboardFormulaValues = _simulatedRemission?.FormulaValues;
            RefreshDashboardMaterials();
        };
        var dialogOperation = programDialog.ShowAsync();

        var catalogsLoaded = false;
        try
        {
            var formulasTask = _formulaService.GetFormulasAsync();
            var orderNumbersTask = _orderService.GetOrderNumbersAsync();
            var clientsTask = _clientService.GetClientsAsync();
            var dischargeTypesTask = _orderService.GetDischargeTypesAsync();
            var productsTask = _orderService.GetProductsAsync();
            var paymentConditionsTask = _orderService.GetPaymentConditionsAsync();
            var operatorsTask = _truckService.GetOperatorsAsync();
            var trucksTask = _truckService.GetTrucksAsync();
            var remissionTask = _orderService.GetNextManualLoadRemissionAsync();
            await Task.WhenAll(
                formulasTask,
                orderNumbersTask,
                clientsTask,
                dischargeTypesTask,
                productsTask,
                paymentConditionsTask,
                operatorsTask,
                trucksTask,
                remissionTask);

            var clientData = await clientsTask;
            var formulaData = await formulasTask;
            recipes.ItemsSource = formulaData.ActiveRows
                .Select(row => new ProgramFormulaOption(
                    row.Values.GetValueOrDefault("Cod_Formula") ?? string.Empty,
                    row.Values.GetValueOrDefault("Descripcion") ?? string.Empty,
                    row.Values))
                .Where(formula => !string.IsNullOrWhiteSpace(formula.Code))
                .ToArray();
            clients.ItemsSource = clientData.Clients;
            construction.ItemsSource = clientData.Clients
                .Select(client => client.Construction.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            trucks.ItemsSource = (await trucksTask).Trucks;
            operators.ItemsSource = (await operatorsTask).Operators;
            orders.ItemsSource = new[] { "Ocasional" }.Concat(await orderNumbersTask).ToArray();
            products.ItemsSource = await productsTask;
            paymentConditions.ItemsSource = await paymentConditionsTask;
            dischargeTypes.ItemsSource = await dischargeTypesTask;
            remissionNumber.Text = await remissionTask;
            SetProgramStatus(
                $"Catálogos cargados: {formulaData.ActiveRows.Count} fórmulas. Pedidos disponibles: {orders.Items.Count - 1}. " +
                "No se guardan ni se envían órdenes al PLC.",
                InfoBarSeverity.Informational);
            catalogsLoaded = true;
        }
        catch (SqlException exception)
        {
            SetProgramStatus($"No se pudieron cargar los catálogos (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetProgramStatus($"No se pudieron cargar los catálogos: {exception.Message}", InfoBarSeverity.Error);
        }

        programButton.IsEnabled = catalogsLoaded;
        lastLoadButton.IsEnabled = catalogsLoaded;
        await dialogOperation;

        async Task LoadLatestDownloadAsync()
        {
            lastLoadButton.IsEnabled = false;
            SetProgramStatus("Consultando la última descarga registrada...", InfoBarSeverity.Informational);
            try
            {
                var latest = await _orderService.GetLatestDownloadAsync();
                if (latest is null)
                {
                    SetProgramStatus("No hay registros en la tabla Descarga.", InfoBarSeverity.Warning);
                    return;
                }

                var recipeOptions = recipes.ItemsSource is IEnumerable<ProgramFormulaOption> loadedRecipes
                    ? loadedRecipes.ToList()
                    : new List<ProgramFormulaOption>();
                var recipe = recipeOptions.FirstOrDefault(option =>
                    string.Equals(option.Code, latest.RecipeCode, StringComparison.OrdinalIgnoreCase));
                if (recipe is null && !string.IsNullOrWhiteSpace(latest.RecipeCode))
                {
                    recipe = new ProgramFormulaOption(
                        latest.RecipeCode,
                        latest.Description,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                    recipeOptions.Add(recipe);
                    recipes.ItemsSource = recipeOptions;
                }

                recipes.SelectedItem = recipe;
                description.Text = !string.IsNullOrWhiteSpace(latest.Description)
                    ? latest.Description
                    : recipe?.Description ?? string.Empty;
                SetProgramComboValue(clients, latest.Client, item => item is ClientRecord client ? client.Client : null);
                SetProgramComboValue(trucks, latest.TruckUnit, item => item is TruckRecord truck ? truck.Unit : item as string);
                SetProgramComboValue(operators, latest.Operator, item => item is OperatorRecord operatorRecord ? operatorRecord.Name : item as string);
                if (trucks.SelectedItem is TruckRecord selectedTruck)
                {
                    capacity.Text = selectedTruck.Capacity;
                }

                SetProgramComboValue(orders, latest.OrderNumber, item => item as string);
                orderStatus.Text = latest.OrderStatus;
                SetProgramComboValue(products, latest.Product, item => item as string);
                SetProgramComboValue(paymentConditions, latest.PaymentCondition, item => item as string);
                SetProgramComboValue(dischargeTypes, latest.DischargeType, item => item as string);
                dispatcher.Text = latest.Dispatcher;
                remissionNumber.Text = latest.Remission;
                if (TryParseProgramNumber(latest.Quantity, out var quantityValue))
                {
                    quantity.Value = quantityValue;
                }

                if (latest.OrderedQuantity.HasValue)
                {
                    orderedQuantity.Value = (double)latest.OrderedQuantity.Value;
                }

                if (latest.SuppliedQuantity.HasValue)
                {
                    suppliedQuantity.Value = (double)latest.SuppliedQuantity.Value;
                }

                if (latest.ToSupplyQuantity.HasValue)
                {
                    toSupplyQuantity.Value = (double)latest.ToSupplyQuantity.Value;
                }

                observations.Text = latest.Observations;
                var date = latest.Date?.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture) ?? "fecha no disponible";
                SetProgramStatus($"Datos cargados desde la última descarga ({date}, remisión {latest.Remission}).", InfoBarSeverity.Success);
            }
            catch (SqlException exception)
            {
                SetProgramStatus($"No se pudo consultar la última descarga (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
            }
            catch (InvalidOperationException exception)
            {
                SetProgramStatus($"No se pudo cargar la última descarga: {exception.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                lastLoadButton.IsEnabled = true;
            }
        }

        void SetProgramStatus(string message, InfoBarSeverity severity)
        {
            SetFormStatus(status, message, severity);
        }
    }

    private async void SimulatedLoad_Click(object sender, RoutedEventArgs e)
    {
        if (_simulatedProcessState != SimulatedProcessState.Prepared || _simulatedRemission is null)
        {
            ModuleStatusText.Text = "Primero prepara una remisión.";
            return;
        }

        try
        {
            LoadSimulatedRecipeReadings(_simulatedRemission.FormulaValues);
        }
        catch (InvalidOperationException exception)
        {
            ModuleStatusText.Text = $"No se pudo simular la carga: {exception.Message}";
            return;
        }

        await BeginSimulatedCanvasImageCycleAsync();
        _simulatedProcessState = SimulatedProcessState.LoadReceived;
        ModuleStatusText.Text = "Fin de carga recibida";
        UpdateSimulatedActionButtons();
    }

    private async void SimulatedDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_simulatedRemission is null
            || _simulatedProcessState is not (SimulatedProcessState.LoadReceived or SimulatedProcessState.AwaitingReport))
        {
            ModuleStatusText.Text = "Se requiere una remisión preparada y una carga recibida.";
            return;
        }

        if (_simulatedProcessState == SimulatedProcessState.LoadReceived)
        {
            _simulatedProcessState = SimulatedProcessState.Downloading;
            ModuleStatusText.Text = "Maquina Descargando";
            UpdateSimulatedActionButtons();
            var loadedReadings = _simulatedCanvasReadings
                .Where(reading => !reading.Key.Equals("ctlAgregados", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(reading => reading.Key, reading => reading.Value, StringComparer.OrdinalIgnoreCase);
            var simulatedImages = _designerCanvasState.Controls
                .Where(IsCanvasImageControl)
                .OrderBy(control => control.ImageControl, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            const int progressSteps = 20;
            for (var step = 1; step <= progressSteps; step++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                ApplySimulatedCanvasReadings(loadedReadings, (progressSteps - step) / (decimal)progressSteps);
                var imagesToTurnOff = (int)Math.Ceiling(simulatedImages.Length * step / (double)progressSteps);
                foreach (var control in simulatedImages.Take(imagesToTurnOff))
                {
                    if (_simulatedCanvasImageStates.GetValueOrDefault(control.Id))
                    {
                        await SetSimulatedCanvasImageStateAsync(control, false);
                    }
                }
            }

            _simulatedProcessState = SimulatedProcessState.AwaitingReport;
        }

        await SaveSimulatedRemissionPdfAsync();
    }

    private void SimulatedPause_Click(object sender, RoutedEventArgs e)
    {
        if (_simulatedProcessState == SimulatedProcessState.Paused)
        {
            _simulatedProcessState = _stateBeforePause;
            ModuleStatusText.Text = _simulatedProcessState == SimulatedProcessState.Prepared
                ? "Receta preparada para cargarse"
                : "Fin de carga recibida";
        }
        else if (_simulatedProcessState is SimulatedProcessState.Prepared or SimulatedProcessState.LoadReceived)
        {
            _stateBeforePause = _simulatedProcessState;
            _simulatedProcessState = SimulatedProcessState.Paused;
            ModuleStatusText.Text = "Sistema en pausa";
        }
        else
        {
            ModuleStatusText.Text = "No se puede pausar después de iniciar la descarga.";
            return;
        }

        UpdateSimulatedActionButtons();
    }

    private async void SimulatedCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_remissionBeforeSimulation is { Length: 8 } previousValues)
        {
            for (var index = 0; index < previousValues.Length; index++)
            {
                RemissionRows[index].Valor = previousValues[index];
            }
        }

        _remissionBeforeSimulation = null;
        _simulatedRemission = null;
        _simulatedProcessState = SimulatedProcessState.Idle;
        _simulatedCanvasReadings.Clear();
        await ResetSimulatedCanvasImageStatesAsync();
        _dashboardFormulaValues = null;
        RefreshDashboardMaterials();
        RemissionPanel.IsExpanded = true;
        ModuleStatusText.Text = "Proceso cancelado";
        UpdateSimulatedActionButtons();
    }

    private async Task SaveSimulatedRemissionPdfAsync()
    {
        if (_simulatedRemission is not { } remission)
        {
            ModuleStatusText.Text = "No hay una remisión para exportar.";
            return;
        }

        if (Application.Current is not App { MainWindow: { } window })
        {
            ModuleStatusText.Text = "No se pudo abrir el selector para guardar el PDF.";
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Documento PDF", new List<string> { ".pdf" });
            picker.SuggestedFileName = $"Remision_{SanitizeFileName(remission.Remission)}";
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                ModuleStatusText.Text = "Se canceló el guardado del PDF. Usa DESCARGAR para reintentar.";
                UpdateSimulatedActionButtons();
                return;
            }

            var report = BuildSimulatedRemissionReport(remission);
            await File.WriteAllBytesAsync(file.Path, _reportExportService.BuildPdf(report));
            _simulatedProcessState = SimulatedProcessState.Complete;
            _remissionBeforeSimulation = null;
            ModuleStatusText.Text = "Remision Completa";
            RemissionPanel.IsExpanded = true;
            UpdateSimulatedActionButtons();
        }
        catch (COMException exception)
        {
            ModuleStatusText.Text = $"No se pudo abrir el selector de PDF: {exception.Message}";
            UpdateSimulatedActionButtons();
        }
        catch (IOException exception)
        {
            ModuleStatusText.Text = $"No se pudo guardar el PDF: {exception.Message}";
            UpdateSimulatedActionButtons();
        }
        catch (UnauthorizedAccessException exception)
        {
            ModuleStatusText.Text = $"No hay permiso para guardar el PDF: {exception.Message}";
            UpdateSimulatedActionButtons();
        }
        catch (InvalidOperationException exception)
        {
            ModuleStatusText.Text = $"No se pudo generar el PDF: {exception.Message}";
            UpdateSimulatedActionButtons();
        }
        catch (ArgumentException exception)
        {
            ModuleStatusText.Text = $"Los datos de la remisión no son válidos para PDF: {exception.Message}";
            UpdateSimulatedActionButtons();
        }
    }

    private static ReportCatalogResult BuildSimulatedRemissionReport(SimulatedRemission remission)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Estado", "Remisión completa (SIMULACIÓN; no representa producción real)" },
            new[] { "No. remisión", remission.Remission },
            new[] { "Código de receta", remission.RecipeCode },
            new[] { "Descripción", remission.Description },
            new[] { "Cantidad", remission.Quantity },
            new[] { "Cliente", remission.Client },
            new[] { "Obra", remission.Construction },
            new[] { "Camión", remission.Truck },
            new[] { "Chofer", remission.Operator },
            new[] { "Pedido", remission.Order },
            new[] { "Estatus del pedido", remission.OrderStatus },
            new[] { "Producto", remission.Product },
            new[] { "Condiciones de pago", remission.PaymentCondition },
            new[] { "Tipo de descarga", remission.DischargeType },
            new[] { "Despachador", remission.Dispatcher },
            new[] { "Pedidos (m³)", remission.OrderedQuantity },
            new[] { "Surtidos (m³)", remission.SuppliedQuantity },
            new[] { "Por surtir (m³)", remission.ToSupplyQuantity },
            new[] { "Observaciones", remission.Observations },
            new[] { "Materiales", "Valores del catálogo de receta; no son pesajes reales" }
        };

        foreach (var (nameField, quantityField) in GetFormulaMaterialFields())
        {
            var name = remission.FormulaValues.GetValueOrDefault(nameField)?.Trim() ?? string.Empty;
            var value = remission.FormulaValues.GetValueOrDefault(quantityField)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name)
                || name.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            rows.Add(new[] { name, value });
        }

        var definition = new ReportCatalogDefinition(
            "remision-simulada",
            $"Remisión {remission.Remission} (SIMULACIÓN)",
            "Vista previa local de una remisión; no acredita una carga real.",
            Array.Empty<string>(),
            string.Empty);
        return new ReportCatalogResult(definition, new[] { "Dato", "Valor" }, rows, false);
    }

    private static IEnumerable<(string NameField, string QuantityField)> GetFormulaMaterialFields()
    {
        for (var index = 1; index <= 8; index++)
        {
            yield return ($"A{index}", $"A{index}C");
        }

        for (var index = 1; index <= 5; index++)
        {
            yield return ($"C{index}", $"C{index}C");
        }

        for (var index = 1; index <= 6; index++)
        {
            yield return ($"D{index}", $"D{index}C");
        }

        yield return ("Agua", "AguaC");
        yield return ("Agua2", "Agua2C");
        yield return ("Agua3", "Agua3C");
        yield return ("Fibra1", "Fibra1C");
        yield return ("Fibra2", "Fibra2C");
        yield return ("Fibra3", "Fibra3C");
    }

    private static bool HasFormulaMaterialMismatch(
        IReadOnlyDictionary<string, string> formulaValues,
        IReadOnlyDictionary<string, string> machineConfiguration)
    {
        foreach (var (nameField, _) in GetFormulaMaterialFields())
        {
            var formulaMaterial = formulaValues.GetValueOrDefault(nameField)?.Trim() ?? string.Empty;
            var machineParameter = GetMachineMaterialParameter(nameField);
            var hopperMaterial = machineConfiguration.GetValueOrDefault(machineParameter)?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(formulaMaterial))
            {
                formulaMaterial = "DESHABILITADA";
            }

            if (!formulaMaterial.Equals(hopperMaterial, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateSimulatedActionButtons()
    {
        var hasPermission = HasModulePermission("Carga Manual") && HasActionPermission("Carga Manual", "Alta");
        ProgramActionButton.IsEnabled = hasPermission
            && _simulatedProcessState is SimulatedProcessState.Idle or SimulatedProcessState.Complete;
        LoadActionButton.IsEnabled = _simulatedProcessState == SimulatedProcessState.Prepared;
        DownloadActionButton.Visibility = Visibility.Visible;
        DownloadActionButton.IsEnabled =
            _simulatedProcessState is SimulatedProcessState.LoadReceived or SimulatedProcessState.AwaitingReport;
        DownloadActionButton.Content = "Descargar";
        PauseActionButton.IsEnabled =
            _simulatedProcessState is SimulatedProcessState.Prepared or SimulatedProcessState.LoadReceived or SimulatedProcessState.Paused;
        PauseActionButton.Content = _simulatedProcessState == SimulatedProcessState.Paused
            ? "Reanudar"
            : "Pausar";
        CancelActionButton.IsEnabled =
            _simulatedProcessState is SimulatedProcessState.Prepared
                or SimulatedProcessState.LoadReceived
                or SimulatedProcessState.Paused
                or SimulatedProcessState.AwaitingReport;
    }

    private bool IsSimulatedReadingsActive =>
        _simulatedProcessState is SimulatedProcessState.LoadReceived
            or SimulatedProcessState.Downloading
            or SimulatedProcessState.AwaitingReport
            or SimulatedProcessState.Complete
        || (_simulatedProcessState == SimulatedProcessState.Paused
            && _stateBeforePause == SimulatedProcessState.LoadReceived);

    private async Task BeginSimulatedCanvasImageCycleAsync()
    {
        var imageControls = _designerCanvasState.Controls
            .Where(IsCanvasImageControl)
            .ToArray();
        _simulatedCanvasImageStates.Clear();
        foreach (var control in imageControls)
        {
            _simulatedCanvasImageStates.Add(control.Id, true);
        }

        foreach (var control in imageControls)
        {
            await SetSimulatedCanvasImageStateAsync(control, true);
        }
    }

    private async Task SetSimulatedCanvasImageStateAsync(
        DesignerCanvasControlState control,
        bool isOn)
    {
        _simulatedCanvasImageStates[control.Id] = isOn;
        var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
            candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
        if (container?.Child is Image image)
        {
            await SetAdditionalImageSourceAsync(image, control, isOn);
        }
    }

    private async Task ResetSimulatedCanvasImageStatesAsync()
    {
        var imageControls = _designerCanvasState.Controls
            .Where(IsCanvasImageControl)
            .Where(control => _simulatedCanvasImageStates.ContainsKey(control.Id))
            .ToArray();
        _simulatedCanvasImageStates.Clear();
        foreach (var control in imageControls)
        {
            var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
                candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
            if (container?.Child is Image image)
            {
                await SetAdditionalImageSourceAsync(image, control, control.IsOn);
            }
        }
    }

    private void LoadSimulatedRecipeReadings(IReadOnlyDictionary<string, string> formulaValues)
    {
        var readings = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var control in _designerCanvasState.Controls)
        {
            var controlName = GetCanvasValueControlName(control);
            var binding = GetDashboardMaterialBinding(control);
            if (controlName is null || binding is null)
            {
                continue;
            }

            var (parameter, _) = binding.Value;
            var materialName = _dashboardMachineConfiguration.GetValueOrDefault(parameter)?.Trim();
            if (string.IsNullOrWhiteSpace(materialName)
                || materialName.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase)
                || readings.ContainsKey(controlName))
            {
                continue;
            }

            var quantityField = GetDashboardFormulaQuantityField(parameter);
            var rawQuantity = quantityField is null
                ? null
                : formulaValues.GetValueOrDefault(quantityField)?.Trim();
            if (!TryParseSimulatedQuantity(rawQuantity, out var quantity))
            {
                throw new InvalidOperationException(
                    $"No se pudo simular la lectura de {materialName}: la cantidad '{rawQuantity}' no es válida.");
            }

            readings.Add(controlName, quantity);
        }

        ApplySimulatedCanvasReadings(
            readings.Where(reading => !reading.Key.Equals("ctlAgregados", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(reading => reading.Key, reading => reading.Value, StringComparer.OrdinalIgnoreCase),
            1m);
    }

    private void ApplySimulatedCanvasReadings(
        IReadOnlyDictionary<string, decimal> loadedReadings,
        decimal remainingFraction)
    {
        _simulatedCanvasReadings.Clear();
        decimal total = 0;
        foreach (var (controlName, loadedValue) in loadedReadings)
        {
            var remainingValue = loadedValue * remainingFraction;
            _simulatedCanvasReadings[controlName] = remainingValue;
            total += remainingValue;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(GetCanvasValueControlName(control), "ctlAgregados", StringComparison.OrdinalIgnoreCase)))
        {
            _simulatedCanvasReadings["ctlAgregados"] = total;
        }

        foreach (var control in _designerCanvasState.Controls)
        {
            var controlName = GetCanvasValueControlName(control);
            if (controlName is not null
                && _simulatedCanvasReadings.TryGetValue(controlName, out var value))
            {
                SetCanvasValue(control, value.ToString("0.##", CultureInfo.CurrentCulture));
            }
        }
    }

    private static bool TryParseSimulatedQuantity(string? text, out decimal quantity)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            quantity = 0;
            return true;
        }

        return (decimal.TryParse(
                    text,
                    NumberStyles.Number,
                    CultureInfo.CurrentCulture,
                    out quantity)
                || decimal.TryParse(
                    text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out quantity))
            && quantity >= 0;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = string.Concat(fileName.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character));
        return string.IsNullOrWhiteSpace(sanitized) ? "Prueba" : sanitized;
    }

    private static void SetProgramComboValue(ComboBox combo, string value, Func<object, string?> getItemValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var matchingItem = combo.Items
            .FirstOrDefault(item => string.Equals(getItemValue(item), value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (matchingItem is not null)
        {
            combo.SelectedItem = matchingItem;
        }
        else if (combo.IsEditable)
        {
            combo.Text = value.Trim();
        }
    }

    private static bool TryParseProgramNumber(string value, out double number)
    {
        return double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)
            || double.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out number);
    }

    private static bool TryGetPositiveDecimal(NumberBox input, out decimal value)
    {
        value = 0;
        return double.IsFinite(input.Value) &&
               input.Value > 0 &&
               decimal.TryParse(
                   input.Value.ToString(CultureInfo.InvariantCulture),
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private Border CreateProgramFormSection(string title, params (string Label, Control Field)[] fields)
    {
        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = _themeBrushes.GetBrush("#F1F5F9")
        });

        foreach (var (label, field) in fields)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var labelText = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = _themeBrushes.GetBrush("#E6EDF7")
            };
            Grid.SetColumn(field, 1);
            row.Children.Add(labelText);
            row.Children.Add(field);
            content.Children.Add(row);
        }

        return new Border
        {
            Child = content,
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = _themeBrushes.GetBrush("#40516A"),
            Background = _themeBrushes.GetBrush("#172337")
        };
    }

    private sealed record ProgramFormulaOption(
        string Code,
        string Description,
        IReadOnlyDictionary<string, string> Values);

    private sealed record SimulatedRemission(
        string Remission,
        string RecipeCode,
        string Description,
        string Quantity,
        string Client,
        string Construction,
        string Truck,
        string Operator,
        string Order,
        string OrderStatus,
        string Product,
        string PaymentCondition,
        string DischargeType,
        string Dispatcher,
        string OrderedQuantity,
        string SuppliedQuantity,
        string ToSupplyQuantity,
        string Observations,
        IReadOnlyDictionary<string, string> FormulaValues);

    private enum SimulatedProcessState
    {
        Idle,
        Prepared,
        LoadReceived,
        Paused,
        Downloading,
        AwaitingReport,
        Complete
    }

    private async Task LoadManualLoadRecipesAsync()
    {
        var currentLocalTime = DateTime.Now;
        ManualLoadQuantityNumberBox.Value = 0;
        ManualLoadActualRemissionDatePicker.Date = new DateTimeOffset(currentLocalTime.Date);
        ManualLoadActualRemissionTimePicker.Time = new TimeSpan(currentLocalTime.Hour, currentLocalTime.Minute, 0);
        ManualLoadShifts.Clear();
        ManualLoadRemissionShiftComboBox.SelectedItem = null;
        ManualLoadRemissionShiftComboBox.IsEnabled = false;
        ManualLoadRemissionShiftComboBox.PlaceholderText = "Cargando turnos...";
        ManualLoadStatusText.Text = "Consultando catálogos y pedidos en BlapaciSQL...";
        ManualLoadRecipeComboBox.IsEnabled = false;
        ManualLoadOrderComboBox.IsEnabled = false;
        ManualLoadOrderNumbers.Clear();
        ManualLoadOrderNumbers.Add("Ocasional");
        ManualLoadOrderComboBox.SelectedItem = null;
        try
        {
            var recipes = await _orderService.GetRecipesAsync();
            var orderNumbers = await _orderService.GetOrderNumbersAsync();
            var clients = await _clientService.GetClientsAsync();
            var dischargeTypes = await _orderService.GetDischargeTypesAsync();
            var products = await _orderService.GetProductsAsync();
            var paymentConditions = await _orderService.GetPaymentConditionsAsync();
            var operators = await _truckService.GetOperatorsAsync();
            var trucks = await _truckService.GetTrucksAsync();
            var schedules = await _orderScheduleConfigurationService.GetSchedulesAsync();
            var nextRemission = await _orderService.GetNextManualLoadRemissionAsync();

            ManualLoadRecipes.Clear();
            foreach (var recipe in recipes)
            {
                ManualLoadRecipes.Add(recipe);
            }

            OrderClients.Clear();
            foreach (var client in clients.Clients)
            {
                OrderClients.Add(client);
            }

            OrderDischargeTypes.Clear();
            foreach (var dischargeType in dischargeTypes)
            {
                OrderDischargeTypes.Add(dischargeType);
            }

            OrderProducts.Clear();
            foreach (var product in products)
            {
                OrderProducts.Add(product);
            }

            OrderPaymentConditions.Clear();
            foreach (var condition in paymentConditions)
            {
                OrderPaymentConditions.Add(condition);
            }

            ManualLoadConstructionOptions.Clear();

            ManualLoadOperators.Clear();
            foreach (var operatorRecord in operators.Operators)
            {
                ManualLoadOperators.Add(operatorRecord);
            }

            ManualLoadTrucks.Clear();
            foreach (var truck in trucks.Trucks)
            {
                ManualLoadTrucks.Add(truck);
            }

            foreach (var schedule in schedules)
            {
                var shiftDescription = string.Join(
                    " ",
                    schedule.Description
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim())
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(shiftDescription))
                {
                    ManualLoadShifts.Add(shiftDescription);
                }
            }

            ManualLoadRemissionShiftComboBox.IsEnabled = ManualLoadShifts.Count > 0;
            ManualLoadRemissionShiftComboBox.PlaceholderText = ManualLoadShifts.Count > 0
                ? "Selecciona turno"
                : "No hay turnos configurados";

            ManualLoadOrderNumbers.Clear();
            ManualLoadOrderNumbers.Add("Ocasional");
            foreach (var orderNumber in orderNumbers)
            {
                ManualLoadOrderNumbers.Add(orderNumber);
            }

            ManualLoadOrderComboBox.SelectedItem = null;
            ManualLoadRemissionTextBox.Text = nextRemission;
            ManualLoadStatusText.Text = $"Fórmulas: {ManualLoadRecipes.Count}. Pedidos activos: {orderNumbers.Count}. Turnos: {ManualLoadShifts.Count}.";
        }
        catch (SqlException exception)
        {
            ManualLoadRemissionShiftComboBox.PlaceholderText = "Turnos no disponibles";
            ManualLoadStatusText.Text = $"No se pudieron cargar los catálogos de Carga Manual en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        finally
        {
            ManualLoadRecipeComboBox.IsEnabled = true;
            ManualLoadOrderComboBox.IsEnabled = true;
        }
    }

    private void ManualLoadRecipe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var recipe = ManualLoadRecipeComboBox.SelectedItem as OrderRecipe;
        if (recipe is not null)
        {
            ManualLoadDescriptionTextBox.Text = recipe.Description;
        }

        ResetManualLoadMaterialInputs();
        UpdateManualLoadMaterialInputs(recipe);
        SetManualLoadRequiredFieldHighlight(new HashSet<string>(StringComparer.Ordinal));
    }

    private void UpdateManualLoadMaterialInputs(OrderRecipe? recipe)
    {
        foreach (var (_, slot, input) in GetManualLoadMaterialInputs())
        {
            input.IsEnabled = recipe?.MaterialSlots.Contains(slot) == true;
        }
    }

    private (string Column, string Slot, TextBox Input)[] GetManualLoadMaterialInputs() =>
    [
        ("A1C", "A1", ManualLoadA1TextBox),
        ("A2C", "A2", ManualLoadA2TextBox),
        ("A3C", "A3", ManualLoadA3TextBox),
        ("A4C", "A4", ManualLoadA4TextBox),
        ("A5C", "A5", ManualLoadA5TextBox),
        ("A6C", "A6", ManualLoadA6TextBox),
        ("A7C", "A7", ManualLoadA7TextBox),
        ("A8C", "A8", ManualLoadA8TextBox),
        ("C1C", "C1", ManualLoadC1TextBox),
        ("C2C", "C2", ManualLoadC2TextBox),
        ("C3C", "C3", ManualLoadC3TextBox),
        ("C4C", "C4", ManualLoadC4TextBox),
        ("C5C", "C5", ManualLoadC5TextBox),
        ("D1C", "D1", ManualLoadD1TextBox),
        ("D2C", "D2", ManualLoadD2TextBox),
        ("D3C", "D3", ManualLoadD3TextBox),
        ("D4C", "D4", ManualLoadD4TextBox),
        ("D5C", "D5", ManualLoadD5TextBox),
        ("D6C", "D6", ManualLoadD6TextBox),
        ("Agua", "Agua", ManualLoadWater1TextBox),
        ("Agua2", "Agua2", ManualLoadWater2TextBox),
        ("Agua3", "Agua3", ManualLoadWater3TextBox),
        ("FI1C", "Fibra1", ManualLoadFI1TextBox),
        ("FI2C", "Fibra2", ManualLoadFI2TextBox),
        ("FI3C", "Fibra3", ManualLoadFI3TextBox)
    ];

    private void ResetManualLoadMaterialInputs()
    {
        foreach (var (_, _, input) in GetManualLoadMaterialInputs())
        {
            input.Text = string.Empty;
        }
    }

    private async void ManualLoadOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var requestId = ++_manualLoadOrderRequestId;
        var selectedOrder = ManualLoadOrderComboBox.SelectedItem as string;
        if (!_syncingManualLoadOrderSelection)
        {
            _syncingManualLoadOrderSelection = true;
            ManualLoadRemissionOrderComboBox.SelectedItem = selectedOrder;
            _syncingManualLoadOrderSelection = false;
        }

        var isOccasional = string.Equals(selectedOrder, "Ocasional", StringComparison.OrdinalIgnoreCase);
        ManualLoadRecipeComboBox.IsEnabled = isOccasional;
        ManualLoadDescriptionTextBox.IsReadOnly = !isOccasional;

        if (isOccasional || string.IsNullOrWhiteSpace(selectedOrder))
        {
            ClearManualLoadOrderFields();
            ManualLoadStatusText.Text = isOccasional
                ? "Ocasional: captura manual de receta y datos de remisión."
                : "Selecciona un pedido activo para cargar sus datos.";
            return;
        }

        if (!int.TryParse(selectedOrder, NumberStyles.Integer, CultureInfo.InvariantCulture, out var orderNumber))
        {
            ManualLoadStatusText.Text = $"El número de pedido «{selectedOrder}» no es válido.";
            return;
        }

        ClearManualLoadOrderFields();
        ManualLoadRecipeComboBox.IsEnabled = false;
        ManualLoadStatusText.Text = $"Consultando datos del pedido {orderNumber}...";
        try
        {
            var order = await _orderService.GetManualLoadOrderDetailsAsync(orderNumber);
            if (requestId != _manualLoadOrderRequestId ||
                !string.Equals(ManualLoadOrderComboBox.SelectedItem as string, selectedOrder, StringComparison.Ordinal))
            {
                return;
            }

            if (order is null)
            {
                ClearManualLoadOrderFields();
                ManualLoadStatusText.Text = $"No se encontró el pedido {orderNumber}.";
                return;
            }

            var matchingRecipe = ManualLoadRecipes.FirstOrDefault(item =>
                string.Equals(item.Code, order.Recipe, StringComparison.OrdinalIgnoreCase));
            ManualLoadRecipeComboBox.SelectedItem = matchingRecipe;
            if (matchingRecipe is null)
            {
                ManualLoadRecipeComboBox.Text = order.Recipe;
            }

            ManualLoadDescriptionTextBox.Text = order.Description;
            ManualLoadRemissionClientComboBox.SelectedItem = OrderClients.FirstOrDefault(client =>
                string.Equals(client.Client, order.Client, StringComparison.OrdinalIgnoreCase));
            if (ManualLoadRemissionClientComboBox.SelectedItem is null)
            {
                ManualLoadRemissionClientComboBox.Text = order.Client;
            }

            ManualLoadRemissionConstructionComboBox.Text = order.Construction;
            ManualLoadRemissionStatusTextBox.Text = order.Status;
            ManualLoadRemissionProductComboBox.SelectedItem = FindOption(OrderProducts, order.Product);
            ManualLoadRemissionProductComboBox.Text = order.Product;
            ManualLoadRemissionPaymentComboBox.SelectedItem = FindOption(OrderPaymentConditions, order.PaymentCondition);
            ManualLoadRemissionPaymentComboBox.Text = order.PaymentCondition;
            ManualLoadRemissionDischargeComboBox.SelectedItem = FindOption(OrderDischargeTypes, order.DischargeType);
            ManualLoadRemissionDischargeComboBox.Text = order.DischargeType;
            ManualLoadOrderedQuantityNumberBox.Value = (double)(order.Quantity ?? 0);
            ManualLoadSuppliedQuantityNumberBox.Value = (double)(order.Supplied ?? 0);
            ManualLoadToSupplyQuantityNumberBox.Value = (double)(order.ToSupply ?? 0);
            ManualLoadQuantityNumberBox.Value = 0;
            ManualLoadRemissionObservationsTextBox.Text = order.Observations;
            ManualLoadStatusText.Text = order.ToSupply > 0
                ? $"Datos cargados del pedido activo {orderNumber}. La cantidad a cargar inicia en 0."
                : $"Datos cargados del pedido activo {orderNumber}. No tiene cantidad pendiente; captura manualmente una cantidad mayor que cero.";
        }
        catch (SqlException exception)
        {
            if (requestId == _manualLoadOrderRequestId)
            {
                ManualLoadStatusText.Text = $"No se pudo consultar el pedido en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
            }
        }
        finally
        {
            ManualLoadRecipeComboBox.IsEnabled = string.Equals(
                ManualLoadOrderComboBox.SelectedItem as string,
                "Ocasional",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ManualLoadRemissionOrder_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingManualLoadOrderSelection)
        {
            return;
        }

        _syncingManualLoadOrderSelection = true;
        ManualLoadOrderComboBox.SelectedItem = ManualLoadRemissionOrderComboBox.SelectedItem;
        _syncingManualLoadOrderSelection = false;
    }

    private void ManualLoadRemissionClient_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ManualLoadConstructionOptions.Clear();
        ManualLoadRemissionConstructionComboBox.SelectedItem = null;
        ManualLoadRemissionConstructionComboBox.Text = string.Empty;

        if (ManualLoadRemissionClientComboBox.SelectedItem is not ClientRecord selectedClient)
        {
            ManualLoadRemissionConstructionComboBox.IsEnabled = false;
            ManualLoadRemissionConstructionComboBox.PlaceholderText = "Selecciona cliente";
            return;
        }

        var constructions = OrderClients
            .Where(client => string.Equals(client.Client, selectedClient.Client, StringComparison.OrdinalIgnoreCase))
            .Select(client => client.Construction.Trim())
            .Where(construction => !string.IsNullOrWhiteSpace(construction))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(construction => construction, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        foreach (var construction in constructions)
        {
            ManualLoadConstructionOptions.Add(construction);
        }

        ManualLoadRemissionConstructionComboBox.IsEnabled = constructions.Length > 0;
        ManualLoadRemissionConstructionComboBox.PlaceholderText = constructions.Length > 0
            ? "Selecciona obra"
            : "Cliente sin obras configuradas";

        if (constructions.Length == 1)
        {
            ManualLoadRemissionConstructionComboBox.SelectedItem = constructions[0];
            ManualLoadRemissionConstructionComboBox.Text = constructions[0];
        }
    }

    private void ClearManualLoadOrderFields()
    {
        ManualLoadQuantityNumberBox.Value = 0;
        ManualLoadRecipeComboBox.SelectedItem = null;
        ManualLoadRecipeComboBox.Text = string.Empty;
        ResetManualLoadMaterialInputs();
        ManualLoadDescriptionTextBox.Text = string.Empty;
        ManualLoadRemissionClientComboBox.SelectedItem = null;
        ManualLoadRemissionClientComboBox.Text = string.Empty;
        ManualLoadRemissionConstructionComboBox.Text = string.Empty;
        ManualLoadRemissionStatusTextBox.Text = string.Empty;
        ManualLoadRemissionProductComboBox.SelectedItem = null;
        ManualLoadRemissionProductComboBox.Text = string.Empty;
        ManualLoadRemissionPaymentComboBox.SelectedItem = null;
        ManualLoadRemissionPaymentComboBox.Text = string.Empty;
        ManualLoadRemissionDischargeComboBox.SelectedItem = null;
        ManualLoadRemissionDischargeComboBox.Text = string.Empty;
        ManualLoadOrderedQuantityNumberBox.Value = 0;
        ManualLoadSuppliedQuantityNumberBox.Value = 0;
        ManualLoadToSupplyQuantityNumberBox.Value = 0;
        ManualLoadRemissionObservationsTextBox.Text = string.Empty;
    }

    private async void SaveManualLoad_Click(object sender, RoutedEventArgs e)
    {
        ManualLoadStatusInfoBar.IsOpen = false;

        if (!await RefreshCurrentUserPermissionsAsync())
        {
            ManualLoadSaveButton.IsEnabled = false;
            ManualLoadStatusText.Text = "No se pudieron verificar los permisos. No se guardó la carga.";
            return;
        }

        if (!AccessPermissionLookup.IsActionEnabled(_currentUserPermissions, "Carga Manual", "Alta"))
        {
            ManualLoadSaveButton.IsEnabled = false;
            ManualLoadStatusText.Text = "Tu perfil no tiene permiso para guardar cargas manuales.";
            return;
        }

        bool RequireText(string? value, string label, Control control)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            SetFormStatus(ManualLoadStatusInfoBar, $"El campo {label} es obligatorio.", InfoBarSeverity.Error);
            control.Focus(FocusState.Programmatic);
            return false;
        }

        if (ManualLoadRecipeComboBox.SelectedItem is not OrderRecipe selectedRecipe)
        {
            SetFormStatus(
                ManualLoadStatusInfoBar,
                "Selecciona un código de receta para continuar.",
                InfoBarSeverity.Error);
            ManualLoadRecipeComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (ManualLoadOrderComboBox.SelectedItem is not string selectedOrderOption ||
            string.IsNullOrWhiteSpace(selectedOrderOption))
        {
            SetFormStatus(
                ManualLoadStatusInfoBar,
                "Selecciona un pedido o la opción Ocasional para continuar.",
                InfoBarSeverity.Error);
            ManualLoadOrderComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var recipeCode = selectedRecipe.Code.Trim();

        if (!double.IsFinite(ManualLoadQuantityNumberBox.Value) ||
            ManualLoadQuantityNumberBox.Value <= 0 ||
            !decimal.TryParse(
                ManualLoadQuantityNumberBox.Value.ToString(CultureInfo.InvariantCulture),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var quantity))
        {
            SetFormStatus(
                ManualLoadStatusInfoBar,
                "La cantidad a cargar debe ser mayor a cero.",
                InfoBarSeverity.Error);
            ManualLoadQuantityNumberBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!TryGetNonNegativeDecimal(ManualLoadOrderedQuantityNumberBox, out var orderedQuantity) ||
            !TryGetNonNegativeDecimal(ManualLoadSuppliedQuantityNumberBox, out var suppliedQuantity) ||
            !TryGetNonNegativeDecimal(ManualLoadToSupplyQuantityNumberBox, out var toSupplyQuantity))
        {
            ManualLoadStatusText.Text = "Las cantidades de pedidos, surtidos y por surtir deben ser números iguales o mayores que cero.";
            return;
        }

        var materialFields = GetManualLoadMaterialInputs();
        var materialQuantities = new Dictionary<string, string?>(StringComparer.Ordinal);
        var activeMaterialFields = materialFields
            .Where(field => selectedRecipe?.MaterialSlots.Contains(field.Slot) == true)
            .ToArray();
        var invalidRequiredMaterialFields = activeMaterialFields
            .Where(field => string.IsNullOrWhiteSpace(field.Input.Text))
            .Select(field => field.Column)
            .ToHashSet(StringComparer.Ordinal);
        SetManualLoadRequiredFieldHighlight(invalidRequiredMaterialFields);
        if (invalidRequiredMaterialFields.Count > 0)
        {
            SetFormStatus(
                ManualLoadStatusInfoBar,
                "Todos los campos de materiales habilitados son obligatorios.",
                InfoBarSeverity.Error);
            return;
        }

        foreach (var (column, slot, input) in materialFields)
        {
            if (selectedRecipe?.MaterialSlots.Contains(slot) != true)
            {
                materialQuantities[column] = null;
                continue;
            }

            var value = input.Text.Trim();
            if (string.IsNullOrEmpty(value))
            {
                materialQuantities[column] = null;
                continue;
            }

            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var materialQuantity) ||
                materialQuantity < 0)
            {
                SetManualLoadRequiredFieldHighlight(new HashSet<string>(StringComparer.Ordinal) { column });
                ManualLoadStatusText.Text = $"{input.Header} debe ser un número igual o mayor que cero.";
                input.Focus(FocusState.Programmatic);
                return;
            }

            materialQuantities[column] = materialQuantity.ToString(
                "0.############################",
                CultureInfo.InvariantCulture);
        }

        int? orderNumber = null;
        if (ManualLoadOrderComboBox.SelectedItem is string selectedOrder &&
            !selectedOrder.Equals("Ocasional", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(selectedOrder, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOrder))
            {
                ManualLoadStatusText.Text = "El número de pedido seleccionado no es válido.";
                ManualLoadOrderComboBox.Focus(FocusState.Programmatic);
                return;
            }

            orderNumber = parsedOrder;
        }

        var client = (ManualLoadRemissionClientComboBox.SelectedItem as ClientRecord)?.Client
            ?? ManualLoadRemissionClientComboBox.Text.Trim();
        if (!RequireText(client, "Cliente", ManualLoadRemissionClientComboBox))
        {
            return;
        }

        var construction = ManualLoadRemissionConstructionComboBox.Text.Trim();
        if (!RequireText(construction, "Obra", ManualLoadRemissionConstructionComboBox))
        {
            return;
        }

        var description = ManualLoadDescriptionTextBox.Text.Trim();
        if (!RequireText(description, "Descripción de la receta", ManualLoadDescriptionTextBox))
        {
            return;
        }

        var product = ManualLoadRemissionProductComboBox.Text.Trim();
        if (!RequireText(product, "Producto", ManualLoadRemissionProductComboBox))
        {
            return;
        }

        var paymentCondition = ManualLoadRemissionPaymentComboBox.Text.Trim();
        if (!RequireText(paymentCondition, "Condiciones de pago", ManualLoadRemissionPaymentComboBox))
        {
            return;
        }

        var dischargeType = ManualLoadRemissionDischargeComboBox.Text.Trim();
        if (!RequireText(dischargeType, "Tipo de descarga", ManualLoadRemissionDischargeComboBox))
        {
            return;
        }

        var selectedOperator = ManualLoadRemissionOperatorComboBox.SelectedItem as OperatorRecord;
        if (selectedOperator is null)
        {
            SetFormStatus(ManualLoadStatusInfoBar, "Selecciona un operador.", InfoBarSeverity.Error);
            ManualLoadRemissionOperatorComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var selectedTruck = ManualLoadRemissionTruckComboBox.SelectedItem as TruckRecord;
        if (selectedTruck is null)
        {
            SetFormStatus(ManualLoadStatusInfoBar, "Selecciona un camión.", InfoBarSeverity.Error);
            ManualLoadRemissionTruckComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var dispatcher = ManualLoadRemissionDispatcherTextBox.Text.Trim();
        if (!RequireText(dispatcher, "Despachador", ManualLoadRemissionDispatcherTextBox))
        {
            return;
        }

        var observations = ManualLoadRemissionObservationsTextBox.Text.Trim();
        if (!RequireText(observations, "Observaciones", ManualLoadRemissionObservationsTextBox))
        {
            return;
        }

        var shift = ManualLoadRemissionShiftComboBox.SelectedItem as string;
        if (!RequireText(shift, "Turno", ManualLoadRemissionShiftComboBox))
        {
            return;
        }

        if (!selectedOrderOption.Equals("Ocasional", StringComparison.OrdinalIgnoreCase) &&
            !RequireText(ManualLoadRemissionStatusTextBox.Text, "Estatus", ManualLoadRemissionStatusTextBox))
        {
            return;
        }

        ManualLoadSaveButton.IsEnabled = false;
        ManualLoadStatusText.Text = "Guardando carga manual en BlapaciSQL...";
        try
        {
            var remission = await _orderService.SaveManualLoadAsync(
                recipeCode,
                quantity,
                materialQuantities,
                activeMaterialFields.Select(field => field.Column).ToHashSet(StringComparer.Ordinal),
                dischargeType,
                orderNumber,
                client,
                (ManualLoadRemissionClientComboBox.SelectedItem as ClientRecord)?.Id,
                construction,
                product,
                paymentCondition,
                description,
                selectedOperator.Name,
                selectedTruck.Unit,
                dispatcher,
                shift,
                ManualLoadActualRemissionDatePicker.Date.DateTime,
                ManualLoadActualRemissionTimePicker.Time,
                orderedQuantity,
                suppliedQuantity,
                toSupplyQuantity,
                observations);

            ManualLoadRemissionTextBox.Text = remission;
            ManualLoadStatusText.Text = $"Carga de la remisión {remission} guardada correctamente.";
            SetFormStatus(
                ManualLoadStatusInfoBar,
                $"Carga de la remisión {remission} guardada correctamente.",
                InfoBarSeverity.Success);
            ModuleStatusText.Text = $"Carga manual de la remisión {remission} guardada en BlapaciSQL.";
            SetManualLoadRequiredFieldHighlight(new HashSet<string>(StringComparer.Ordinal));
        }
        catch (SqlException exception)
        {
            ManualLoadStatusText.Text = $"No se pudo guardar la carga en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(ManualLoadStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            ManualLoadSaveButton.IsEnabled = AccessPermissionLookup.IsActionEnabled(
                _currentUserPermissions,
                "Carga Manual",
                "Alta");
        }
    }

    private static bool TryGetNonNegativeDecimal(NumberBox input, out decimal value)
    {
        value = 0;
        return double.IsFinite(input.Value) &&
               input.Value >= 0 &&
               decimal.TryParse(
                   input.Value.ToString(CultureInfo.InvariantCulture),
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out value);
    }

    private void SetManualLoadRequiredFieldHighlight(ISet<string> invalidFields)
    {
        _manualLoadInvalidMaterialFields.Clear();
        foreach (var invalidField in invalidFields)
        {
            _manualLoadInvalidMaterialFields.Add(invalidField);
        }

        ApplyManualLoadRequiredFieldHighlight();
    }

    private void ClearManualLoadMaterialHighlightWhenValid(string column, TextBox input)
    {
        if (!_manualLoadInvalidMaterialFields.Contains(column) ||
            !decimal.TryParse(input.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ||
            value < 0)
        {
            return;
        }

        _manualLoadInvalidMaterialFields.Remove(column);
        ApplyManualLoadRequiredFieldHighlight();
    }

    private void ApplyManualLoadRequiredFieldHighlight()
    {
        var normalBrush = _themeBrushes.GetBrush("#40516A");
        var errorBrush = _themeBrushes.GetBrush("#EF4444");
        foreach (var (column, _, input) in GetManualLoadMaterialInputs())
        {
            var isInvalid = _manualLoadInvalidMaterialFields.Contains(column);
            input.BorderBrush = isInvalid ? errorBrush : normalBrush;
            input.BorderThickness = isInvalid ? new Thickness(2) : new Thickness(1);
        }
    }

    private async Task LoadOrdersAsync()
    {
        SetFormStatus(OrderFormStatusInfoBar, "Consultando pedidos en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            var clientData = await _clientService.GetClientsAsync();
            var data = await _orderService.GetOrdersAsync();
            var recipes = await _orderService.GetRecipesAsync();
            var dischargeTypes = await _orderService.GetDischargeTypesAsync();
            var products = await _orderService.GetProductsAsync();
            var paymentConditions = await _orderService.GetPaymentConditionsAsync();
            OrderClients.Clear();
            foreach (var client in clientData.Clients)
            {
                OrderClients.Add(client);
            }

            OrderRecipes.Clear();
            foreach (var recipe in recipes)
            {
                OrderRecipes.Add(recipe);
            }

            OrderDischargeTypes.Clear();
            foreach (var dischargeType in dischargeTypes)
            {
                OrderDischargeTypes.Add(dischargeType);
            }

            OrderProducts.Clear();
            foreach (var product in products)
            {
                OrderProducts.Add(product);
            }

            OrderPaymentConditions.Clear();
            foreach (var condition in paymentConditions)
            {
                OrderPaymentConditions.Add(condition);
            }

            ActiveOrders.Clear();
            CompletedOrders.Clear();
            CancelledOrders.Clear();
            foreach (var order in data.Active)
            {
                ActiveOrders.Add(order);
            }

            foreach (var order in data.Completed)
            {
                CompletedOrders.Add(order);
            }

            foreach (var order in data.Cancelled)
            {
                CancelledOrders.Add(order);
            }

            ShowActiveOrders();
            OrderActiveTabButton.Content = $"Activos (A,P) ({ActiveOrders.Count})";
            OrderCompletedTabButton.Content = $"Terminados (T) ({CompletedOrders.Count})";
            OrderCancelledTabButton.Content = $"Cancelados (C) ({CancelledOrders.Count})";
            SetFormStatus(
                OrderFormStatusInfoBar,
                $"Pedidos cargados: {ActiveOrders.Count} activos, {CompletedOrders.Count} terminados y {CancelledOrders.Count} cancelados. Clientes disponibles: {OrderClients.Count}.",
                InfoBarSeverity.Informational);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                OrderFormStatusInfoBar,
                $"No se pudieron consultar los pedidos en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void ShowOrders(IEnumerable<OrderRecord> orders, Button selectedTab)
    {
        if (_selectedOrderRow is not null)
        {
            _selectedOrderRow.Background = _themeBrushes.GetBrush("#1E2D43");
        }

        _selectedOrder = null;
        _selectedOrderRow = null;
        _showingActiveOrders = selectedTab == OrderActiveTabButton;
        _showingCancelledOrders = selectedTab == OrderCancelledTabButton;
        VisibleOrders.Clear();
        foreach (var order in orders)
        {
            VisibleOrders.Add(order);
        }

        var activeBrush = _themeBrushes.GetBrush("#0078D4");
        var inactiveBrush = _themeBrushes.GetBrush("#26364D");
        OrderActiveTabButton.Background = selectedTab == OrderActiveTabButton ? activeBrush : inactiveBrush;
        OrderCompletedTabButton.Background = selectedTab == OrderCompletedTabButton ? activeBrush : inactiveBrush;
        OrderCancelledTabButton.Background = selectedTab == OrderCancelledTabButton ? activeBrush : inactiveBrush;
        foreach (var tab in new[] { OrderActiveTabButton, OrderCompletedTabButton, OrderCancelledTabButton })
        {
            tab.Foreground = _themeBrushes.GetBrush("#F1F5F9");
        }
        AddOrderButton.Content = _showingCancelledOrders ? "Restaurar" : "Agregar";
        AddOrderButton.IsEnabled = selectedTab == OrderActiveTabButton;
        UpdateOrderActionButtons();
    }

    private void ShowActiveOrders() => ShowOrders(ActiveOrders, OrderActiveTabButton);
    private void ShowCompletedOrders() => ShowOrders(CompletedOrders, OrderCompletedTabButton);
    private void ShowCancelledOrders() => ShowOrders(CancelledOrders, OrderCancelledTabButton);

    private void ActiveOrdersTab_Click(object sender, RoutedEventArgs e) => ShowActiveOrders();
    private void CompletedOrdersTab_Click(object sender, RoutedEventArgs e) => ShowCompletedOrders();
    private void CancelledOrdersTab_Click(object sender, RoutedEventArgs e) => ShowCancelledOrders();

    private void OrderRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Grid row || row.DataContext is not OrderRecord order)
        {
            return;
        }

        if (_selectedOrderRow is not null)
        {
            _selectedOrderRow.Background = _themeBrushes.GetBrush("#1E2D43");
        }

        _selectedOrder = order;
        _selectedOrderRow = row;
        row.Background = _themeBrushes.GetBrush("#0058A0");
        UpdateOrderActionButtons();
    }

    private void UpdateOrderActionButtons()
    {
        var hasSelection = _selectedOrder is not null;
        var canChange = _showingActiveOrders &&
            _selectedOrder?.Status.Trim() is "A" or "P";
        AddOrderButton.IsEnabled = (_showingActiveOrders || (_showingCancelledOrders && hasSelection))
            && HasActionPermission("Pedidos", "Alta");
        EditOrderButton.IsEnabled = canChange && HasActionPermission("Pedidos", "Editar");
        CancelOrderButton.IsEnabled = canChange && HasActionPermission("Pedidos", "Baja");
        ViewOrderButton.IsEnabled = hasSelection && HasActionPermission("Pedidos", "Visualizar");
    }

    private void EditOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Pedidos", "Editar"))
        {
            return;
        }

        if (_selectedOrder is null || !_showingActiveOrders)
        {
            SetFormStatus(OrderFormStatusInfoBar, "Selecciona un pedido activo para editar.", InfoBarSeverity.Error);
            return;
        }

        ShowOrderEditor(_selectedOrder, readOnly: false);
    }

    private void ViewOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Pedidos", "Visualizar"))
        {
            return;
        }

        if (_selectedOrder is null)
        {
            SetFormStatus(OrderFormStatusInfoBar, "Selecciona un pedido para visualizar.", InfoBarSeverity.Error);
            return;
        }

        ShowOrderEditor(_selectedOrder, readOnly: true);
    }

    private async void CancelOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Pedidos", "Baja"))
        {
            return;
        }

        if (_selectedOrder is null || !_showingActiveOrders)
        {
            SetFormStatus(OrderFormStatusInfoBar, "Selecciona un pedido activo para cancelar.", InfoBarSeverity.Error);
            return;
        }

        var order = _selectedOrder;
        var confirmation = new ContentDialog
        {
            Title = "Cancelar pedido",
            Content = $"¿Deseas cancelar el pedido {order.Number} de {order.Client}?",
            PrimaryButtonText = "Cancelar pedido",
            CloseButtonText = "Volver",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        CancelOrderButton.IsEnabled = false;
        SetFormStatus(OrderFormStatusInfoBar, $"Cancelando pedido {order.Number} en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            await _orderService.CancelOrderAsync(order.Id);
            await LoadOrdersAsync();
            SetFormStatus(OrderFormStatusInfoBar, $"Pedido {order.Number} cancelado correctamente.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                OrderFormStatusInfoBar,
                $"No se pudo cancelar el pedido en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(OrderFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateOrderActionButtons();
        }
    }

    private async void AddOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Pedidos", "Alta"))
        {
            return;
        }

        if (_showingCancelledOrders)
        {
            await RestoreSelectedOrderAsync();
            return;
        }

        if (!AddOrderButton.IsEnabled)
        {
            SetFormStatus(OrderFormStatusInfoBar, "El formulario solo se puede abrir desde la pestaña de pedidos activos.", InfoBarSeverity.Error);
            return;
        }

        var nextNumber = ActiveOrders
            .Concat(CompletedOrders)
            .Concat(CancelledOrders)
            .Select(order => int.TryParse(order.Number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
        OrderDatePicker.Date = DateTimeOffset.Now;
        OrderDeliveryDatePicker.Date = DateTimeOffset.Now;
        OrderDeliveryTimePicker.Time = DateTime.Now.TimeOfDay;
        OrderNumberTextBox.Text = nextNumber.ToString(CultureInfo.InvariantCulture);
        OrderClientComboBox.SelectedItem = null;
        OrderConstructionComboBox.SelectedItem = null;
        OrderRequestedByTextBox.Text = string.Empty;
        OrderAuthorizedByTextBox.Text = string.Empty;
        OrderPhoneTextBox.Text = string.Empty;
        OrderEmailTextBox.Text = string.Empty;
        OrderQuantityTextBox.Text = "0.00";
        OrderRecipeComboBox.SelectedItem = null;
        OrderDescriptionTextBox.Text = string.Empty;
        OrderDischargeTypeComboBox.SelectedItem = null;
        OrderShippingTextBox.Text = "0.00";
        OrderIntervalNumberBox.Value = 41;
        OrderProductComboBox.SelectedItem = null;
        OrderPaymentConditionsComboBox.SelectedItem = null;
        OrderObservationsTextBox.Text = string.Empty;
        OrderEditorStatusInfoBar.IsOpen = false;
        _editingOrder = false;
        SetOrderEditorReadOnly(false);
        OrderEditorTitleText.Text = "Agregar Pedido";
        OrderEditorSaveButton.Content = "Guardar";
        OrderEditorSaveButton.IsEnabled = HasActionPermission("Pedidos", "Alta");
        OrderEditorSaveButton.Visibility = Visibility.Visible;
        OrderEditorOverlay.Visibility = Visibility.Visible;
    }

    private async Task RestoreSelectedOrderAsync()
    {
        if (!RequireActionPermission("Pedidos", "Alta"))
        {
            return;
        }

        if (_selectedOrder is null || !_showingCancelledOrders)
        {
            SetFormStatus(OrderFormStatusInfoBar, "Selecciona un pedido cancelado para restaurarlo.", InfoBarSeverity.Error);
            return;
        }

        var order = _selectedOrder;
        AddOrderButton.IsEnabled = false;
        SetFormStatus(OrderFormStatusInfoBar, $"Restaurando pedido {order.Number}...", InfoBarSeverity.Informational);
        try
        {
            await _orderService.RestoreOrderAsync(order.Id);
            await LoadOrdersAsync();
            ShowActiveOrders();
            SetFormStatus(OrderFormStatusInfoBar, $"El pedido {order.Number} se restauró y está activo.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                OrderFormStatusInfoBar,
                $"No se pudo restaurar el pedido en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(OrderFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateOrderActionButtons();
        }
    }

    private void ShowOrderEditor(OrderRecord order, bool readOnly)
    {
        _editingOrder = !readOnly;
        OrderEditorTitleText.Text = readOnly ? $"Visualizar Pedido {order.Number}" : $"Editar Pedido {order.Number}";
        OrderEditorSaveButton.Content = "Guardar cambios";
        OrderEditorSaveButton.IsEnabled = !readOnly && HasActionPermission(
            "Pedidos",
            _editingOrder ? "Editar" : "Alta");
        OrderEditorSaveButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
        OrderDatePicker.Date = new DateTimeOffset(order.DateValue ?? DateTime.Today);
        OrderDeliveryDatePicker.Date = new DateTimeOffset(order.DeliveryDateValue ?? DateTime.Today);
        OrderDeliveryTimePicker.Time = order.DeliveryTimeValue ?? TimeSpan.Zero;
        OrderNumberTextBox.Text = order.Number;

        OrderClientComboBox.SelectedItem = OrderClients.FirstOrDefault(
            item => string.Equals(item.Client, order.Client, StringComparison.OrdinalIgnoreCase));
        OrderConstructionComboBox.Text = order.Construction;
        OrderRequestedByTextBox.Text = order.Buyer;
        OrderAuthorizedByTextBox.Text = order.AuthorizedBy;
        OrderPhoneTextBox.Text = order.Phone;
        OrderEmailTextBox.Text = order.Email;
        OrderQuantityTextBox.Text = (order.QuantityValue ?? 0).ToString("0.##", CultureInfo.CurrentCulture);
        OrderRecipeComboBox.SelectedItem = OrderRecipes.FirstOrDefault(
            item => string.Equals(item.Code, order.Recipe, StringComparison.OrdinalIgnoreCase));
        OrderDescriptionTextBox.Text = order.Description;
        OrderDischargeTypeComboBox.SelectedItem = FindOption(OrderDischargeTypes, order.DischargeType);
        OrderShippingTextBox.Text = (order.Shipping ?? 0).ToString("0.##", CultureInfo.CurrentCulture);
        OrderIntervalNumberBox.Value = order.IntervalMinutes ?? 0;
        OrderProductComboBox.SelectedItem = FindOption(OrderProducts, order.Product);
        OrderPaymentConditionsComboBox.SelectedItem = FindOption(OrderPaymentConditions, order.PaymentCondition);
        OrderObservationsTextBox.Text = order.Observations;
        OrderEditorStatusInfoBar.IsOpen = false;
        SetOrderEditorReadOnly(readOnly);
        OrderEditorOverlay.Visibility = Visibility.Visible;
    }

    private static string? FindOption(IEnumerable<string> options, string value) =>
        options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));

    private void SetOrderEditorReadOnly(bool readOnly)
    {
        OrderClientComboBox.IsEnabled = !readOnly;
        OrderConstructionComboBox.IsEnabled = !readOnly && OrderClientComboBox.SelectedItem is ClientRecord;
        OrderAuthorizedByTextBox.IsReadOnly = readOnly;
        OrderPhoneTextBox.IsReadOnly = readOnly;
        OrderEmailTextBox.IsReadOnly = readOnly;
        OrderDeliveryDatePicker.IsEnabled = !readOnly;
        OrderDeliveryTimePicker.IsEnabled = !readOnly;
        OrderQuantityTextBox.IsReadOnly = readOnly;
        OrderRecipeComboBox.IsEnabled = !readOnly;
        OrderDischargeTypeComboBox.IsEnabled = !readOnly;
        OrderShippingTextBox.IsReadOnly = readOnly;
        OrderIntervalNumberBox.IsEnabled = !readOnly;
        OrderProductComboBox.IsEnabled = !readOnly;
        OrderPaymentConditionsComboBox.IsEnabled = !readOnly;
        OrderObservationsTextBox.IsReadOnly = readOnly;
        OrderDatePicker.IsEnabled = false;
    }

    private void CloseOrderEditor_Click(object sender, RoutedEventArgs e)
    {
        OrderEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private async void SaveOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Pedidos", _editingOrder ? "Editar" : "Alta"))
        {
            return;
        }

        if (OrderClientComboBox.SelectedItem is not ClientRecord client)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "Selecciona un cliente.", InfoBarSeverity.Error);
            OrderClientComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var authorizedBy = OrderAuthorizedByTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(authorizedBy))
        {
            SetFormStatus(OrderEditorStatusInfoBar, "El campo Autoriza Pedido es obligatorio.", InfoBarSeverity.Error);
            OrderAuthorizedByTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (authorizedBy.Length > 255)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "Autoriza Pedido no puede superar 255 caracteres.", InfoBarSeverity.Error);
            OrderAuthorizedByTextBox.Focus(FocusState.Programmatic);
            return;
        }

        var phone = OrderPhoneTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(phone) && (!SupplierDataValidator.IsValidPhone(phone) || phone.Length > 50))
        {
            SetFormStatus(OrderEditorStatusInfoBar, "El teléfono debe tener entre 7 y 15 dígitos; permite +, espacios, guiones y paréntesis, con un máximo de 50 caracteres.", InfoBarSeverity.Error);
            OrderPhoneTextBox.Focus(FocusState.Programmatic);
            return;
        }

        var email = OrderEmailTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(email) && (!SupplierDataValidator.IsValidEmail(email) || email.Length > 80))
        {
            SetFormStatus(OrderEditorStatusInfoBar, "Escribe un correo válido (por ejemplo, nombre@dominio.com) de máximo 80 caracteres.", InfoBarSeverity.Error);
            OrderEmailTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!decimal.TryParse(OrderQuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) ||
            quantity <= 0)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "La cantidad debe ser un número mayor que cero.", InfoBarSeverity.Error);
            OrderQuantityTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (OrderRecipeComboBox.SelectedItem is not OrderRecipe recipe)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "Selecciona una receta.", InfoBarSeverity.Error);
            OrderRecipeComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (OrderDischargeTypeComboBox.SelectedItem is not string dischargeType)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "Selecciona el tipo de descarga.", InfoBarSeverity.Error);
            OrderDischargeTypeComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (OrderProductComboBox.SelectedItem is not string product)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "Selecciona un producto.", InfoBarSeverity.Error);
            OrderProductComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (OrderPaymentConditionsComboBox.SelectedItem is not string paymentCondition ||
            string.IsNullOrWhiteSpace(paymentCondition))
        {
            SetFormStatus(OrderEditorStatusInfoBar, "Selecciona las condiciones de pago.", InfoBarSeverity.Error);
            OrderPaymentConditionsComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var observations = OrderObservationsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(observations))
        {
            SetFormStatus(OrderEditorStatusInfoBar, "El campo Observaciones es obligatorio.", InfoBarSeverity.Error);
            OrderObservationsTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!decimal.TryParse(OrderShippingTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var shipping) ||
            shipping < 0)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "El envío debe ser un número igual o mayor que cero.", InfoBarSeverity.Error);
            OrderShippingTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!double.IsFinite(OrderIntervalNumberBox.Value) || OrderIntervalNumberBox.Value < 0)
        {
            SetFormStatus(OrderEditorStatusInfoBar, "El intervalo debe ser un número igual o mayor que cero.", InfoBarSeverity.Error);
            OrderIntervalNumberBox.Focus(FocusState.Programmatic);
            return;
        }

        OrderEditorSaveButton.IsEnabled = false;
        SetFormStatus(OrderEditorStatusInfoBar, "Guardando pedido en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            var construction = OrderConstructionComboBox.Text.Trim();
            var buyer = OrderRequestedByTextBox.Text.Trim();
            int orderNumber;
            if (_editingOrder && _selectedOrder is not null)
            {
                await _orderService.UpdateOrderAsync(
                    _selectedOrder.Id,
                    _selectedOrder.Status.Trim(),
                    client.Client,
                    construction,
                    buyer,
                    authorizedBy,
                    phone,
                    email,
                    OrderDatePicker.Date.DateTime,
                    OrderDeliveryDatePicker.Date.DateTime,
                    OrderDeliveryTimePicker.Time,
                    quantity,
                    recipe.Code,
                    recipe.Description,
                    dischargeType,
                    shipping,
                    OrderIntervalNumberBox.Value,
                    product,
                    paymentCondition,
                    observations);
                orderNumber = int.Parse(_selectedOrder.Number, CultureInfo.InvariantCulture);
            }
            else
            {
                orderNumber = await _orderService.AddOrderAsync(
                client.Client,
                construction,
                buyer,
                authorizedBy,
                phone,
                email,
                OrderDatePicker.Date.DateTime,
                OrderDeliveryDatePicker.Date.DateTime,
                OrderDeliveryTimePicker.Time,
                quantity,
                recipe.Code,
                recipe.Description,
                dischargeType,
                shipping,
                OrderIntervalNumberBox.Value,
                product,
                paymentCondition,
                observations,
                _authenticatedUsername);
            }

            OrderNumberTextBox.Text = orderNumber.ToString(CultureInfo.InvariantCulture);
            OrderEditorOverlay.Visibility = Visibility.Collapsed;
            await LoadOrdersAsync();
            SetFormStatus(
                OrderFormStatusInfoBar,
                _editingOrder
                    ? $"Pedido {orderNumber} actualizado correctamente."
                    : $"Pedido {orderNumber} guardado correctamente.",
                InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(OrderEditorStatusInfoBar, $"No se pudo guardar el pedido en BlapaciSQL (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(OrderEditorStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            SetFormStatus(OrderEditorStatusInfoBar, $"Revisa la fecha, hora o intervalo: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            OrderEditorSaveButton.IsEnabled = HasActionPermission(
                "Pedidos",
                _editingOrder ? "Editar" : "Alta");
        }
    }

    private void OrderClientComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrderClientComboBox.SelectedItem is not ClientRecord selectedClient)
        {
            OrderConstructionComboBox.ItemsSource = null;
            OrderConstructionComboBox.Text = string.Empty;
            OrderConstructionComboBox.IsEnabled = false;
            OrderRequestedByTextBox.Text = string.Empty;
            return;
        }

        OrderRequestedByTextBox.Text = selectedClient.Buyer;
        var construction = selectedClient.Construction.Trim();
        OrderConstructionComboBox.ItemsSource = string.IsNullOrWhiteSpace(construction)
            ? Array.Empty<string>()
            : new[] { construction };
        OrderConstructionComboBox.SelectedItem = string.IsNullOrWhiteSpace(construction) ? null : construction;
        OrderConstructionComboBox.IsEnabled = !string.IsNullOrWhiteSpace(construction);
    }

    private void OrderRecipeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OrderDescriptionTextBox.Text = (OrderRecipeComboBox.SelectedItem as OrderRecipe)?.Description ?? string.Empty;
    }

    private void CloseOrderForm_Click(object sender, RoutedEventArgs e)
    {
        OrderFormOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task LoadTrucksAsync()
    {
        SetFormStatus(TruckFormStatusInfoBar, "Consultando camiones y operadores en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            var trucks = await _truckService.GetTrucksAsync();
            var operators = await _truckService.GetOperatorsAsync();
            _selectedTruck = null;
            _selectedTruckRow = null;
            _selectedOperator = null;
            _selectedOperatorRow = null;
            UpdateTruckActionButtons();
            TruckRecords.Clear();
            OperatorRecords.Clear();
            foreach (var truck in trucks.Trucks)
            {
                TruckRecords.Add(truck);
            }

            foreach (var operatorRecord in operators.Operators)
            {
                OperatorRecords.Add(operatorRecord);
            }

            SetFormStatus(TruckFormStatusInfoBar, $"Camiones: {trucks.Trucks.Count}; operadores: {operators.Operators.Count}.", InfoBarSeverity.Informational);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                TruckFormStatusInfoBar,
                $"No se pudieron consultar camiones u operadores en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CloseTruckForm_Click(object sender, RoutedEventArgs e)
    {
        TruckFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void TruckTab_Click(object sender, RoutedEventArgs e)
    {
        var showTrucks = (Button)sender == TruckTabButton;
        TruckTablePanel.Visibility = showTrucks ? Visibility.Visible : Visibility.Collapsed;
        OperatorTablePanel.Visibility = showTrucks ? Visibility.Collapsed : Visibility.Visible;
        TruckTabButton.Opacity = showTrucks ? 1 : 0.65;
        OperatorTabButton.Opacity = showTrucks ? 0.65 : 1;
        EditTruckButton.Visibility = showTrucks ? Visibility.Visible : Visibility.Collapsed;
        DeleteTruckButton.Visibility = showTrucks ? Visibility.Visible : Visibility.Collapsed;
        ViewTruckButton.Visibility = showTrucks ? Visibility.Visible : Visibility.Collapsed;
        EditOperatorButton.Visibility = showTrucks ? Visibility.Collapsed : Visibility.Visible;
        DeleteOperatorButton.Visibility = showTrucks ? Visibility.Collapsed : Visibility.Visible;
        ViewOperatorButton.Visibility = showTrucks ? Visibility.Collapsed : Visibility.Visible;
        UpdateTruckActionButtons();
    }

    private void AddTruck_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", "Alta"))
        {
            return;
        }

        if (OperatorTablePanel.Visibility == Visibility.Visible)
        {
            ShowOperatorEditor(null, readOnly: false);
            return;
        }

        if (TruckTablePanel.Visibility != Visibility.Visible)
        {
            SetFormStatus(TruckFormStatusInfoBar, "Selecciona una pestaña para agregar un registro.", InfoBarSeverity.Error);
            return;
        }

        ShowTruckEditor(null, readOnly: false);
    }

    private void CloseOperatorEditor_Click(object sender, RoutedEventArgs e)
    {
        OperatorEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private void OperatorRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Grid row || row.DataContext is not OperatorRecord operatorRecord)
        {
            return;
        }

        if (_selectedOperatorRow is not null)
        {
            _selectedOperatorRow.Opacity = 1;
        }

        _selectedOperatorRow = row;
        _selectedOperatorRow.Opacity = 0.72;
        _selectedOperator = operatorRecord;
        UpdateTruckActionButtons();
    }

    private void EditOperator_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", "Editar"))
        {
            return;
        }

        if (_selectedOperator is null)
        {
            SetFormStatus(TruckFormStatusInfoBar, "Selecciona un operador para editar.", InfoBarSeverity.Error);
            return;
        }

        ShowOperatorEditor(_selectedOperator, readOnly: false);
    }

    private void ViewOperator_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", "Visualizar"))
        {
            return;
        }

        if (_selectedOperator is null)
        {
            SetFormStatus(TruckFormStatusInfoBar, "Selecciona un operador para visualizar.", InfoBarSeverity.Error);
            return;
        }

        ShowOperatorEditor(_selectedOperator, readOnly: true);
    }

    private void ShowOperatorEditor(OperatorRecord? operatorRecord, bool readOnly)
    {
        _editingOperator = operatorRecord is not null && !readOnly;
        OperatorEditorTitleText.Text = readOnly ? "Visualizar Operador" : _editingOperator ? "Editar Operador" : "Agregar Operador";
        OperatorEditorSaveButton.Content = _editingOperator ? "Guardar cambios" : "Guardar";
        OperatorEditorSaveButton.IsEnabled = !readOnly && HasActionPermission(
            "Camiones",
            _editingOperator ? "Editar" : "Alta");
        OperatorEditorSaveButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
        OperatorNameTextBox.Text = operatorRecord?.Name ?? string.Empty;
        OperatorInitialsTextBox.Text = operatorRecord?.Initials ?? string.Empty;
        OperatorNameTextBox.IsReadOnly = readOnly;
        OperatorInitialsTextBox.IsReadOnly = readOnly;
        OperatorEditorStatusInfoBar.IsOpen = false;
        OperatorEditorOverlay.Visibility = Visibility.Visible;
        if (!readOnly)
        {
            OperatorNameTextBox.Focus(FocusState.Programmatic);
        }
    }

    private async void SaveOperator_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", _editingOperator ? "Editar" : "Alta"))
        {
            return;
        }

        var name = OperatorNameTextBox.Text.Trim();
        var initials = OperatorInitialsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetFormStatus(OperatorEditorStatusInfoBar, "El nombre del operador es obligatorio.", InfoBarSeverity.Error);
            OperatorNameTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (string.IsNullOrWhiteSpace(initials))
        {
            SetFormStatus(OperatorEditorStatusInfoBar, "Las iniciales del operador son obligatorias.", InfoBarSeverity.Error);
            OperatorInitialsTextBox.Focus(FocusState.Programmatic);
            return;
        }

        OperatorEditorSaveButton.IsEnabled = false;
        SetFormStatus(OperatorEditorStatusInfoBar, "Guardando operador en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            if (_editingOperator && _selectedOperator is not null)
            {
                await _truckService.UpdateOperatorAsync(_selectedOperator.Id, name, initials);
            }
            else
            {
                await _truckService.AddOperatorAsync(name, initials);
            }

            OperatorEditorOverlay.Visibility = Visibility.Collapsed;
            await LoadTrucksAsync();
            SetFormStatus(
                TruckFormStatusInfoBar,
                _editingOperator
                    ? $"Operador {name} actualizado correctamente."
                    : $"Operador {name} guardado correctamente.",
                InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(OperatorEditorStatusInfoBar, $"No se pudo guardar el operador en BlapaciSQL (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(OperatorEditorStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            OperatorEditorSaveButton.IsEnabled = HasActionPermission(
                "Camiones",
                _editingOperator ? "Editar" : "Alta");
        }
    }

    private async void DeleteOperator_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", "Baja"))
        {
            return;
        }

        if (_selectedOperator is null)
        {
            SetFormStatus(TruckFormStatusInfoBar, "Selecciona un operador para borrar.", InfoBarSeverity.Error);
            return;
        }

        var operatorRecord = _selectedOperator;
        var confirmation = new ContentDialog
        {
            Title = "Borrar operador",
            Content = $"¿Deseas borrar al operador \"{operatorRecord.Name}\" (código {operatorRecord.Code})? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        DeleteOperatorButton.IsEnabled = false;
        try
        {
            await _truckService.DeleteOperatorAsync(operatorRecord.Id);
            await LoadTrucksAsync();
            SetFormStatus(TruckFormStatusInfoBar, $"Operador {operatorRecord.Name} borrado correctamente.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                TruckFormStatusInfoBar,
                $"No se pudo borrar el operador en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(TruckFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateTruckActionButtons();
        }
    }

    private void TruckRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Grid row || row.DataContext is not TruckRecord truck)
        {
            return;
        }

        if (_selectedTruckRow is not null)
        {
            _selectedTruckRow.Opacity = 1;
        }

        _selectedTruckRow = row;
        _selectedTruckRow.Opacity = 0.72;
        _selectedTruck = truck;
        UpdateTruckActionButtons();
    }

    private void EditTruck_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", "Editar"))
        {
            return;
        }

        if (_selectedTruck is null)
        {
            SetFormStatus(TruckFormStatusInfoBar, "Selecciona un camión para editar.", InfoBarSeverity.Error);
            return;
        }

        ShowTruckEditor(_selectedTruck, readOnly: false);
    }

    private void ViewTruck_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", "Visualizar"))
        {
            return;
        }

        if (_selectedTruck is null)
        {
            SetFormStatus(TruckFormStatusInfoBar, "Selecciona un camión para visualizar.", InfoBarSeverity.Error);
            return;
        }

        ShowTruckEditor(_selectedTruck, readOnly: true);
    }

    private void ShowTruckEditor(TruckRecord? truck, bool readOnly)
    {
        _editingTruck = truck is not null && !readOnly;
        TruckEditorSaveButton.Content = _editingTruck ? "Guardar cambios" : "Guardar";
        TruckEditorSaveButton.IsEnabled = !readOnly && HasActionPermission(
            "Camiones",
            _editingTruck ? "Editar" : "Alta");
        TruckEditorSaveButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
        TruckEditorTitleText.Text = readOnly ? "Visualizar Camión" : _editingTruck ? "Editar Camión" : "Agregar Camión";
        TruckOperatorComboBox.SelectedItem = truck is null
            ? null
            : OperatorRecords.FirstOrDefault(operatorRecord =>
                string.Equals(operatorRecord.Initials, truck.Initials, StringComparison.OrdinalIgnoreCase));
        TruckOperatorComboBox.PlaceholderText = truck?.Name ?? string.Empty;
        TruckOperatorComboBox.IsEnabled = !readOnly;
        TruckOperatorInitialsTextBox.Text = truck?.Initials ?? string.Empty;
        TruckPlatesTextBox.Text = truck?.Plates ?? string.Empty;
        TruckUnitTextBox.Text = truck?.Unit ?? string.Empty;
        TruckCapacityTextBox.Text = truck?.Capacity ?? "0.0";
        TruckPlatesTextBox.IsReadOnly = readOnly;
        TruckUnitTextBox.IsReadOnly = readOnly;
        TruckCapacityTextBox.IsReadOnly = readOnly;
        TruckEditorStatusInfoBar.IsOpen = false;
        TruckEditorOverlay.Visibility = Visibility.Visible;
        if (!readOnly)
        {
            TruckOperatorComboBox.Focus(FocusState.Programmatic);
        }
    }

    private void TruckOperatorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TruckOperatorComboBox.SelectedItem is OperatorRecord selectedOperator)
        {
            TruckOperatorInitialsTextBox.Text = selectedOperator.Initials;
        }
    }

    private void CloseTruckEditor_Click(object sender, RoutedEventArgs e)
    {
        TruckEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private async void SaveTruck_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", _editingTruck ? "Editar" : "Alta"))
        {
            return;
        }

        var selectedOperator = TruckOperatorComboBox.SelectedItem as OperatorRecord;
        if (selectedOperator is null && (!_editingTruck || _selectedTruck is null))
        {
            SetFormStatus(TruckEditorStatusInfoBar, "Selecciona un operador.", InfoBarSeverity.Error);
            TruckOperatorComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var plates = TruckPlatesTextBox.Text.Trim();
        var unit = TruckUnitTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(plates))
        {
            SetFormStatus(TruckEditorStatusInfoBar, "El número de placas es obligatorio.", InfoBarSeverity.Error);
            TruckPlatesTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            SetFormStatus(TruckEditorStatusInfoBar, "La unidad es obligatoria.", InfoBarSeverity.Error);
            TruckUnitTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!decimal.TryParse(TruckCapacityTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var capacity)
            && !decimal.TryParse(TruckCapacityTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out capacity))
        {
            SetFormStatus(TruckEditorStatusInfoBar, "La capacidad debe ser un número válido.", InfoBarSeverity.Error);
            TruckCapacityTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (capacity <= 0)
        {
            SetFormStatus(TruckEditorStatusInfoBar, "La capacidad debe ser mayor que cero.", InfoBarSeverity.Error);
            TruckCapacityTextBox.Focus(FocusState.Programmatic);
            return;
        }

        TruckEditorSaveButton.IsEnabled = false;
        SetFormStatus(TruckEditorStatusInfoBar, "Guardando camión en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            var initials = selectedOperator?.Initials ?? _selectedTruck!.Initials;
            var name = selectedOperator?.Name ?? _selectedTruck!.Name;
            if (_editingTruck && _selectedTruck is not null)
            {
                await _truckService.UpdateTruckAsync(_selectedTruck.Id, initials, name, plates, unit, capacity);
            }
            else
            {
                await _truckService.AddTruckAsync(initials, name, plates, unit, capacity);
            }

            TruckEditorOverlay.Visibility = Visibility.Collapsed;
            await LoadTrucksAsync();
            SetFormStatus(
                TruckFormStatusInfoBar,
                _editingTruck
                    ? $"Camión {unit} actualizado correctamente."
                    : $"Camión {unit} guardado correctamente.",
                InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(TruckEditorStatusInfoBar, $"No se pudo guardar el camión en BlapaciSQL (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(TruckEditorStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            TruckEditorSaveButton.IsEnabled = HasActionPermission(
                "Camiones",
                _editingTruck ? "Editar" : "Alta");
        }
    }

    private async void DeleteTruck_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Camiones", "Baja"))
        {
            return;
        }

        if (_selectedTruck is null)
        {
            SetFormStatus(TruckFormStatusInfoBar, "Selecciona un camión para borrar.", InfoBarSeverity.Error);
            return;
        }

        var truck = _selectedTruck;
        var confirmation = new ContentDialog
        {
            Title = "Borrar camión",
            Content = $"¿Deseas borrar el camión {truck.Code} ({truck.Unit}, placas {truck.Plates})? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        DeleteTruckButton.IsEnabled = false;
        try
        {
            await _truckService.DeleteTruckAsync(truck.Id);
            await LoadTrucksAsync();
            SetFormStatus(TruckFormStatusInfoBar, $"Camión {truck.Unit} borrado correctamente.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                TruckFormStatusInfoBar,
                $"No se pudo borrar el camión en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(TruckFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateTruckActionButtons();
        }
    }

    private async Task LoadClientsAsync()
    {
        SetFormStatus(ClientFormStatusInfoBar, "Consultando clientes en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            var data = await _clientService.GetClientsAsync();
            ClientRecords.Clear();
            ClientColumns.Clear();
            foreach (var column in data.Columns)
            {
                ClientColumns.Add(new ClientCell(column.Title, string.Empty, column.Width));
            }

            foreach (var client in data.Clients)
            {
                ClientRecords.Add(client);
            }

            SetFormStatus(ClientFormStatusInfoBar, $"Clientes cargados desde BlapaciSQL: {data.Clients.Count}.", InfoBarSeverity.Informational);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                ClientFormStatusInfoBar,
                $"No se pudieron consultar los clientes en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CloseClientForm_Click(object sender, RoutedEventArgs e)
    {
        ClientFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void ClientListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedClient = ClientListView.SelectedItem as ClientRecord;
        UpdateClientActionButtons();
    }

    private void AddClient_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Clientes", "Alta"))
        {
            return;
        }

        ShowClientEditor(null, readOnly: false);
    }

    private void EditClient_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Clientes", "Editar"))
        {
            return;
        }

        if (_selectedClient is null)
        {
            SetFormStatus(ClientFormStatusInfoBar, "Selecciona un cliente para editar.", InfoBarSeverity.Error);
            return;
        }

        ShowClientEditor(_selectedClient, readOnly: false);
    }

    private void ViewClient_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Clientes", "Visualizar"))
        {
            return;
        }

        if (_selectedClient is null)
        {
            SetFormStatus(ClientFormStatusInfoBar, "Selecciona un cliente para visualizar.", InfoBarSeverity.Error);
            return;
        }

        ShowClientEditor(_selectedClient, readOnly: true);
    }

    private void ShowClientEditor(ClientRecord? client, bool readOnly)
    {
        _editingClient = client is not null && !readOnly;
        ClientEditorTitleText.Text = readOnly ? "Visualizar Cliente" : _editingClient ? "Editar Cliente" : "Agregar Cliente";
        ClientEditorSaveButton.Content = _editingClient ? "Guardar cambios" : "Guardar";
        ClientEditorSaveButton.IsEnabled = !readOnly && HasActionPermission(
            "Clientes",
            _editingClient ? "Editar" : "Alta");
        ClientEditorSaveButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
        ClientNameTextBox.Text = client?.Client ?? string.Empty;
        ClientRfcTextBox.Text = client?.Rfc ?? string.Empty;
        ClientAddressTextBox.Text = client?.Address ?? string.Empty;
        ClientNeighborhoodTextBox.Text = client?.Neighborhood ?? string.Empty;
        ClientCityTextBox.Text = client?.City ?? string.Empty;
        ClientStateTextBox.Text = client?.State ?? string.Empty;
        ClientPostalCodeTextBox.Text = client?.PostalCode ?? string.Empty;
        ClientPhoneTextBox.Text = client?.Phone ?? string.Empty;
        ClientResidentTextBox.Text = client?.Resident ?? string.Empty;
        ClientBuyerTextBox.Text = client?.Buyer ?? string.Empty;
        ClientConstructionTextBox.Text = client?.Construction ?? string.Empty;
        ClientEmailTextBox.Text = client?.Email ?? string.Empty;
        ClientWebsiteTextBox.Text = client?.Website ?? string.Empty;
        ClientNameTextBox.IsReadOnly = readOnly;
        ClientRfcTextBox.IsReadOnly = readOnly;
        ClientAddressTextBox.IsReadOnly = readOnly;
        ClientNeighborhoodTextBox.IsReadOnly = readOnly;
        ClientCityTextBox.IsReadOnly = readOnly;
        ClientStateTextBox.IsReadOnly = readOnly;
        ClientPostalCodeTextBox.IsReadOnly = readOnly;
        ClientPhoneTextBox.IsReadOnly = readOnly;
        ClientResidentTextBox.IsReadOnly = readOnly;
        ClientBuyerTextBox.IsReadOnly = readOnly;
        ClientConstructionTextBox.IsReadOnly = readOnly;
        ClientEmailTextBox.IsReadOnly = readOnly;
        ClientWebsiteTextBox.IsReadOnly = readOnly;
        ClientEditorStatusInfoBar.IsOpen = false;
        ClientEditorOverlay.Visibility = Visibility.Visible;
        if (!readOnly)
        {
            ClientNameTextBox.Focus(FocusState.Programmatic);
        }
    }

    private void CloseClientEditor_Click(object sender, RoutedEventArgs e)
    {
        ClientEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private async void SaveClient_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Clientes", _editingClient ? "Editar" : "Alta"))
        {
            return;
        }

        var clientDetails = new ClientDetails(
            ClientNameTextBox.Text,
            ClientRfcTextBox.Text,
            ClientAddressTextBox.Text,
            ClientNeighborhoodTextBox.Text,
            ClientCityTextBox.Text,
            ClientStateTextBox.Text,
            ClientPostalCodeTextBox.Text,
            ClientPhoneTextBox.Text,
            ClientResidentTextBox.Text,
            ClientBuyerTextBox.Text,
            ClientConstructionTextBox.Text,
            ClientEmailTextBox.Text,
            ClientWebsiteTextBox.Text);
        var validationError = ClientDataValidator.Validate(clientDetails);
        if (validationError is not null)
        {
            SetFormStatus(ClientEditorStatusInfoBar, validationError.Message, InfoBarSeverity.Error);
            GetClientTextBox(validationError.Field).Focus(FocusState.Programmatic);
            return;
        }

        var client = clientDetails.Client.Trim();

        ClientEditorSaveButton.IsEnabled = false;
        SetFormStatus(ClientEditorStatusInfoBar, "Guardando cliente en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            if (_editingClient && _selectedClient is not null)
            {
                await _clientService.UpdateClientAsync(
                    _selectedClient.Id,
                    clientDetails.Client, clientDetails.Rfc, clientDetails.Address, clientDetails.Neighborhood,
                    clientDetails.City, clientDetails.State, clientDetails.PostalCode, clientDetails.Phone,
                    clientDetails.Resident, clientDetails.Buyer, clientDetails.Construction,
                    clientDetails.Email, clientDetails.Website);
            }
            else
            {
                await _clientService.AddClientAsync(
                    clientDetails.Client, clientDetails.Rfc, clientDetails.Address, clientDetails.Neighborhood,
                    clientDetails.City, clientDetails.State, clientDetails.PostalCode, clientDetails.Phone,
                    clientDetails.Resident, clientDetails.Buyer, clientDetails.Construction,
                    clientDetails.Email, clientDetails.Website);
            }

            ClientEditorOverlay.Visibility = Visibility.Collapsed;
            await LoadClientsAsync();
            SetFormStatus(
                ClientFormStatusInfoBar,
                _editingClient
                    ? $"Cliente {client} actualizado correctamente."
                    : $"Cliente {client} guardado correctamente.",
                InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(ClientEditorStatusInfoBar, $"No se pudo guardar el cliente en BlapaciSQL (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(ClientEditorStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            ClientEditorSaveButton.IsEnabled = HasActionPermission(
                "Clientes",
                _editingClient ? "Editar" : "Alta");
        }
    }

    private TextBox GetClientTextBox(ClientField field) => field switch
    {
        ClientField.Client => ClientNameTextBox,
        ClientField.Rfc => ClientRfcTextBox,
        ClientField.Address => ClientAddressTextBox,
        ClientField.Neighborhood => ClientNeighborhoodTextBox,
        ClientField.City => ClientCityTextBox,
        ClientField.State => ClientStateTextBox,
        ClientField.PostalCode => ClientPostalCodeTextBox,
        ClientField.Phone => ClientPhoneTextBox,
        ClientField.Resident => ClientResidentTextBox,
        ClientField.Buyer => ClientBuyerTextBox,
        ClientField.Construction => ClientConstructionTextBox,
        ClientField.Email => ClientEmailTextBox,
        ClientField.Website => ClientWebsiteTextBox,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };

    private async void DeleteClient_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Clientes", "Baja"))
        {
            return;
        }

        if (_selectedClient is null)
        {
            SetFormStatus(ClientFormStatusInfoBar, "Selecciona un cliente para borrar.", InfoBarSeverity.Error);
            return;
        }

        var client = _selectedClient;
        var confirmation = new ContentDialog
        {
            Title = "Borrar cliente",
            Content = $"¿Deseas borrar el cliente \"{client.Client}\" (código {client.Id})? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        DeleteClientButton.IsEnabled = false;
        try
        {
            await _clientService.DeleteClientAsync(client.Id);
            await LoadClientsAsync();
            SetFormStatus(ClientFormStatusInfoBar, $"Cliente {client.Client} borrado correctamente.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                ClientFormStatusInfoBar,
                $"No se pudo borrar el cliente en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(ClientFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateClientActionButtons();
        }
    }

    private async Task LoadSuppliersAsync()
    {
        SetFormStatus(SupplierFormStatusInfoBar, "Consultando proveedores en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            var data = await _supplierService.GetSuppliersAsync();
            SupplierRecords.Clear();
            SupplierColumns.Clear();
            foreach (var column in data.Columns)
            {
                SupplierColumns.Add(new SupplierCell(column.Title, string.Empty, column.Width));
            }

            foreach (var supplier in data.Suppliers)
            {
                SupplierRecords.Add(supplier);
            }

            SetFormStatus(SupplierFormStatusInfoBar, $"Proveedores cargados desde BlapaciSQL: {data.Suppliers.Count}.", InfoBarSeverity.Informational);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                SupplierFormStatusInfoBar,
                $"No se pudieron consultar los proveedores en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void CloseSupplierForm_Click(object sender, RoutedEventArgs e)
    {
        SupplierFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void SupplierListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedSupplier = SupplierListView.SelectedItem as SupplierRecord;
        UpdateSupplierActionButtons();
    }

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Proveedores", "Alta"))
        {
            return;
        }

        ShowSupplierEditor(null, readOnly: false);
    }

    private void EditSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Proveedores", "Editar"))
        {
            return;
        }

        if (_selectedSupplier is null)
        {
            SetFormStatus(SupplierFormStatusInfoBar, "Selecciona un proveedor para editar.", InfoBarSeverity.Error);
            return;
        }

        ShowSupplierEditor(_selectedSupplier, readOnly: false);
    }

    private void ViewSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Proveedores", "Visualizar"))
        {
            return;
        }

        if (_selectedSupplier is null)
        {
            SetFormStatus(SupplierFormStatusInfoBar, "Selecciona un proveedor para visualizar.", InfoBarSeverity.Error);
            return;
        }

        ShowSupplierEditor(_selectedSupplier, readOnly: true);
    }

    private void ShowSupplierEditor(SupplierRecord? supplier, bool readOnly)
    {
        _editingSupplier = supplier is not null && !readOnly;
        SupplierEditorTitleText.Text = readOnly ? "Visualizar Proveedor" : _editingSupplier ? "Editar Proveedor" : "Agregar Proveedor";
        SupplierEditorSaveButton.Content = _editingSupplier ? "Guardar cambios" : "Guardar";
        SupplierEditorSaveButton.IsEnabled = !readOnly && HasActionPermission(
            "Proveedores",
            _editingSupplier ? "Editar" : "Alta");
        SupplierEditorSaveButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
        SupplierNameTextBox.Text = supplier?.Supplier ?? string.Empty;
        SupplierRfcTextBox.Text = supplier?.Rfc ?? string.Empty;
        SupplierAddressTextBox.Text = supplier?.Address ?? string.Empty;
        SupplierNeighborhoodTextBox.Text = supplier?.Neighborhood ?? string.Empty;
        SupplierCityTextBox.Text = supplier?.City ?? string.Empty;
        SupplierStateTextBox.Text = supplier?.State ?? string.Empty;
        SupplierPostalCodeTextBox.Text = supplier?.PostalCode ?? string.Empty;
        SupplierPhoneTextBox.Text = supplier?.Phone ?? string.Empty;
        SupplierContactTextBox.Text = supplier?.Contact ?? string.Empty;
        SupplierEmailTextBox.Text = supplier?.Email ?? string.Empty;
        SupplierWebsiteTextBox.Text = supplier?.Website ?? string.Empty;
        SupplierNameTextBox.IsReadOnly = readOnly;
        SupplierRfcTextBox.IsReadOnly = readOnly;
        SupplierAddressTextBox.IsReadOnly = readOnly;
        SupplierNeighborhoodTextBox.IsReadOnly = readOnly;
        SupplierCityTextBox.IsReadOnly = readOnly;
        SupplierStateTextBox.IsReadOnly = readOnly;
        SupplierPostalCodeTextBox.IsReadOnly = readOnly;
        SupplierPhoneTextBox.IsReadOnly = readOnly;
        SupplierContactTextBox.IsReadOnly = readOnly;
        SupplierEmailTextBox.IsReadOnly = readOnly;
        SupplierWebsiteTextBox.IsReadOnly = readOnly;
        SupplierEditorStatusInfoBar.IsOpen = false;
        SupplierEditorOverlay.Visibility = Visibility.Visible;
        if (!readOnly)
        {
            SupplierNameTextBox.Focus(FocusState.Programmatic);
        }
    }

    private void CloseSupplierEditor_Click(object sender, RoutedEventArgs e)
    {
        SupplierEditorOverlay.Visibility = Visibility.Collapsed;
    }

    private async void SaveSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Proveedores", _editingSupplier ? "Editar" : "Alta"))
        {
            return;
        }

        var supplier = new SupplierDetails(
            SupplierNameTextBox.Text,
            SupplierRfcTextBox.Text,
            SupplierAddressTextBox.Text,
            SupplierNeighborhoodTextBox.Text,
            SupplierCityTextBox.Text,
            SupplierStateTextBox.Text,
            SupplierPostalCodeTextBox.Text,
            SupplierPhoneTextBox.Text,
            SupplierContactTextBox.Text,
            SupplierEmailTextBox.Text,
            SupplierWebsiteTextBox.Text);
        var validationError = SupplierDataValidator.Validate(supplier);
        if (validationError is not null)
        {
            SetSupplierEditorStatus(validationError.Message, InfoBarSeverity.Error);
            GetSupplierTextBox(validationError.Field).Focus(FocusState.Programmatic);
            return;
        }

        var supplierName = supplier.Supplier.Trim();

        SupplierEditorSaveButton.IsEnabled = false;
        SetSupplierEditorStatus("Guardando proveedor en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            if (_editingSupplier && _selectedSupplier is not null)
            {
                await _supplierService.UpdateSupplierAsync(
                    _selectedSupplier.Id,
                    supplier);
            }
            else
            {
                await _supplierService.AddSupplierAsync(supplier);
            }

            SupplierEditorOverlay.Visibility = Visibility.Collapsed;
            await LoadSuppliersAsync();
            SetFormStatus(
                SupplierFormStatusInfoBar,
                _editingSupplier
                    ? $"Proveedor {supplierName} actualizado correctamente."
                    : $"Proveedor {supplierName} guardado correctamente.",
                InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetSupplierEditorStatus(
                $"No se pudo guardar el proveedor en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetSupplierEditorStatus(exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SupplierEditorSaveButton.IsEnabled = HasActionPermission(
                "Proveedores",
                _editingSupplier ? "Editar" : "Alta");
        }
    }

    private void SetSupplierEditorStatus(string message, InfoBarSeverity severity)
    {
        SetFormStatus(SupplierEditorStatusInfoBar, message, severity);
    }

    private void SetFormStatus(InfoBar infoBar, string message, InfoBarSeverity severity)
    {
        infoBar.Message = message;
        infoBar.Severity = severity;
        StatusInfoBarStyler.Apply(infoBar, RequestedTheme);
        infoBar.IsOpen = true;
    }

    private TextBox GetSupplierTextBox(SupplierField field) => field switch
    {
        SupplierField.Supplier => SupplierNameTextBox,
        SupplierField.Rfc => SupplierRfcTextBox,
        SupplierField.Address => SupplierAddressTextBox,
        SupplierField.Neighborhood => SupplierNeighborhoodTextBox,
        SupplierField.City => SupplierCityTextBox,
        SupplierField.State => SupplierStateTextBox,
        SupplierField.PostalCode => SupplierPostalCodeTextBox,
        SupplierField.Phone => SupplierPhoneTextBox,
        SupplierField.Contact => SupplierContactTextBox,
        SupplierField.Email => SupplierEmailTextBox,
        SupplierField.Website => SupplierWebsiteTextBox,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };

    private async void DeleteSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Proveedores", "Baja"))
        {
            return;
        }

        if (_selectedSupplier is null)
        {
            SetFormStatus(SupplierFormStatusInfoBar, "Selecciona un proveedor para borrar.", InfoBarSeverity.Error);
            return;
        }

        var supplier = _selectedSupplier;
        var confirmation = new ContentDialog
        {
            Title = "Borrar proveedor",
            Content = $"¿Deseas borrar el proveedor \"{supplier.Supplier}\" (código {supplier.Id})? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        DeleteSupplierButton.IsEnabled = false;
        try
        {
            await _supplierService.DeleteSupplierAsync(supplier.Id);
            await LoadSuppliersAsync();
            SetFormStatus(SupplierFormStatusInfoBar, $"Proveedor {supplier.Supplier} borrado correctamente.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                SupplierFormStatusInfoBar,
                $"No se pudo borrar el proveedor en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(SupplierFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateSupplierActionButtons();
        }
    }

    private async Task LoadFormulasAsync()
    {
        SetFormStatus(FormulaFormStatusInfoBar, "Consultando fórmulas en BlapaciSQL...", InfoBarSeverity.Informational);

        try
        {
            var data = await _formulaService.GetFormulasAsync();
            FormulaColumns.Clear();
            ActiveFormulaRows.Clear();
            CancelledFormulaRows.Clear();

            foreach (var column in data.Columns)
            {
                FormulaColumns.Add(column);
            }

            foreach (var row in data.ActiveRows)
            {
                ActiveFormulaRows.Add(row);
            }

            foreach (var row in data.CancelledRows)
            {
                CancelledFormulaRows.Add(row);
            }

            ShowActiveFormulas();
            FormulaActiveTabButton.Content = $"Fórmulas Activas ({ActiveFormulaRows.Count})";
            FormulaCancelledTabButton.Content = $"Fórmulas Canceladas ({CancelledFormulaRows.Count})";
            SetFormStatus(
                FormulaFormStatusInfoBar,
                $"Fórmulas cargadas desde BlapaciSQL: {data.ActiveRows.Count} activas y {data.CancelledRows.Count} canceladas. Desplázate horizontalmente para ver todos los campos.",
                InfoBarSeverity.Informational);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                FormulaFormStatusInfoBar,
                $"No se pudieron consultar las fórmulas en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void ShowActiveFormulas()
    {
        _showingCancelledFormulas = false;
        FormulaAddRestoreButton.Content = "Agregar";
        ClearFormulaSelection();
        ShowFormulaRows(ActiveFormulaRows);
        FormulaActiveTabButton.Background = _themeBrushes.GetBrush("#0078D4");
        FormulaCancelledTabButton.Background = _themeBrushes.GetBrush("#26364D");
        FormulaActiveTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
        FormulaCancelledTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
        UpdateFormulaActionButtons();
    }

    private void ShowCancelledFormulas()
    {
        _showingCancelledFormulas = true;
        FormulaAddRestoreButton.Content = "Restaurar";
        ClearFormulaSelection();
        ShowFormulaRows(CancelledFormulaRows);
        FormulaActiveTabButton.Background = _themeBrushes.GetBrush("#26364D");
        FormulaCancelledTabButton.Background = _themeBrushes.GetBrush("#0078D4");
        FormulaActiveTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
        FormulaCancelledTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
        UpdateFormulaActionButtons();
    }

    private void ShowFormulaRows(IEnumerable<FormulaRow> rows)
    {
        VisibleFormulaRows.Clear();
        foreach (var row in rows)
        {
            VisibleFormulaRows.Add(row);
        }
    }

    private void ActiveFormulasTab_Click(object sender, RoutedEventArgs e)
    {
        ShowActiveFormulas();
    }

    private void CancelledFormulasTab_Click(object sender, RoutedEventArgs e)
    {
        ShowCancelledFormulas();
    }

    private void CloseFormulaForm_Click(object sender, RoutedEventArgs e)
    {
        FormulaFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void FormulaRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not FormulaRow formula)
        {
            return;
        }

        ClearFormulaSelection();
        _selectedFormula = formula;
        _selectedFormulaBorder = border;
        border.Background = _themeBrushes.GetBrush("#005EA7");
        UpdateFormulaActionButtons();
        SetFormStatus(
            FormulaFormStatusInfoBar,
            $"Fórmula seleccionada: {formula.Values.GetValueOrDefault("Cod_Formula", string.Empty)} (ID {formula.Id}).",
            InfoBarSeverity.Informational);
        e.Handled = true;
    }

    private void ClearFormulaSelection()
    {
        if (_selectedFormulaBorder is not null)
        {
            _selectedFormulaBorder.Background = _themeBrushes.GetBrush("#1E2D43");
        }

        _selectedFormulaBorder = null;
        _selectedFormula = null;
        UpdateFormulaActionButtons();
    }

    private async void AddFormula_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Fórmulas", "Alta"))
        {
            return;
        }

        if (_showingCancelledFormulas)
        {
            await RestoreSelectedFormulaAsync();
            return;
        }

        _editingFormula = false;
        FormulaEditorTitle.Text = "Fórmulas";
        var values = FormulaService.EditableFields.ToDictionary(field => field, _ => string.Empty, StringComparer.Ordinal);
        foreach (var field in FormulaService.EditableFields)
        {
            if (field is "A1" or "A2" or "A3" or "A4" or "A5" or "A6" or "A7" or "A8"
                or "C1" or "C2" or "C3" or "C4" or "C5"
                or "D1" or "D2" or "D3" or "D4" or "D5" or "D6"
                or "Agua" or "Agua2" or "Agua3"
                or "Fibra1" or "Fibra2" or "Fibra3")
            {
                values[field] = "Deshabilitada";
            }
            else if (field.EndsWith('C'))
            {
                values[field] = "0";
            }
        }

        await OpenFormulaEditorAsync(values);
    }

    private async Task RestoreSelectedFormulaAsync()
    {
        if (!RequireActionPermission("Fórmulas", "Alta"))
        {
            return;
        }

        if (_selectedFormula is null || !_showingCancelledFormulas)
        {
            SetFormStatus(FormulaFormStatusInfoBar, "Selecciona una fórmula cancelada para restaurarla.", InfoBarSeverity.Error);
            return;
        }

        var formula = _selectedFormula;
        var code = formula.Values.GetValueOrDefault("Cod_Formula", string.Empty);
        FormulaAddRestoreButton.IsEnabled = false;
        SetFormStatus(FormulaFormStatusInfoBar, $"Restaurando fórmula {code}...", InfoBarSeverity.Informational);
        try
        {
            await _formulaService.RestoreFormulaAsync(formula.Id);
            await LoadFormulasAsync();
            ShowActiveFormulas();
            SetFormStatus(FormulaFormStatusInfoBar, $"La fórmula {code} se restauró y está activa.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                FormulaFormStatusInfoBar,
                $"No se pudo restaurar la fórmula (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(FormulaFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateFormulaActionButtons();
        }
    }

    private async void EditFormula_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Fórmulas", "Editar"))
        {
            return;
        }

        if (_selectedFormula is null || _showingCancelledFormulas)
        {
            SetFormStatus(FormulaFormStatusInfoBar, "Selecciona una fórmula activa para editar.", InfoBarSeverity.Error);
            return;
        }

        _editingFormula = true;
        _viewingFormula = false;
        FormulaEditorTitle.Text = "Fórmulas";
        await OpenFormulaEditorAsync(_selectedFormula.Values);
    }

    private async void ViewFormula_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Fórmulas", "Visualizar"))
        {
            return;
        }

        if (_selectedFormula is null)
        {
            SetFormStatus(FormulaFormStatusInfoBar, "Selecciona una fórmula para visualizar.", InfoBarSeverity.Error);
            return;
        }

        _editingFormula = false;
        _viewingFormula = true;
        FormulaEditorTitle.Text = "Fórmulas";
        await OpenFormulaEditorAsync(_selectedFormula.Values);
    }

    private async Task OpenFormulaEditorAsync(IReadOnlyDictionary<string, string> values)
    {
        SetFormStatus(FormulaEditorStatusInfoBar, "Cargando materiales...", InfoBarSeverity.Informational);
        FormulaEditorSaveButton.IsEnabled = false;
        FormulaEditorOverlay.Visibility = Visibility.Visible;
        try
        {
            var machineConfiguration = await _machineConfigurationService.GetConfigurationAsync();
            _dashboardMachineConfiguration = machineConfiguration.Values;
            var inventory = await _inventoryService.GetInventoryAsync();
            _formulaMaterialOptions.Clear();
            _formulaMaterialOptions["A"] = inventory.Where(item => item.Category == "AGREGADOS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["C"] = inventory.Where(item => item.Category == "CEMENTOS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["D"] = inventory.Where(item => item.Category == "ADITIVOS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["Agua"] = inventory.Where(item => item.Category == "AGUAS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["Fibra"] = inventory.Where(item => item.Category == "FIBRAS").Select(item => item.Name).ToArray();

            var editorValues = values;
            if (!_editingFormula && !_viewingFormula)
            {
                var newFormulaValues = values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                SetNewFormulaMaterialValues(newFormulaValues, machineConfiguration.Values);
                editorValues = newFormulaValues;
            }

            BuildFormulaEditor(editorValues);
            foreach (var control in _formulaEditorInputs.Values)
            {
                control.IsEnabled = !_viewingFormula;
            }

            FormulaEditorSaveButton.Visibility = _viewingFormula ? Visibility.Collapsed : Visibility.Visible;
            FormulaEditorStatusInfoBar.IsOpen = false;
            FormulaEditorSaveButton.IsEnabled = !_viewingFormula
                && HasActionPermission("Fórmulas", _editingFormula ? "Editar" : "Alta");
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                FormulaEditorStatusInfoBar,
                $"No se pudieron cargar la configuración de tolvas o los materiales desde BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
    }

    private static void SetNewFormulaMaterialValues(
        IDictionary<string, string> values,
        IReadOnlyDictionary<string, string> machineConfiguration)
    {
        var formulaMaterialFields = Enumerable.Range(1, 8).Select(index => $"A{index}")
            .Concat(Enumerable.Range(1, 5).Select(index => $"C{index}"))
            .Concat(Enumerable.Range(1, 6).Select(index => $"D{index}"))
            .Concat(new[] { "Agua", "Agua2", "Agua3", "Fibra1", "Fibra2", "Fibra3" });

        foreach (var field in formulaMaterialFields)
        {
            values[field] = machineConfiguration.GetValueOrDefault(GetMachineMaterialParameter(field)) is { Length: > 0 } material
                ? material
                : "Deshabilitada";
        }
    }

    private void BuildFormulaEditor(IReadOnlyDictionary<string, string> values)
    {
        FormulaEditorConfigurationHost.Children.Clear();
        FormulaEditorAdditionalHost.Children.Clear();
        _formulaEditorInputs.Clear();
        _formulaMaterialStates.Clear();

        var formulaTypePanel = CreateFormulaGroup("Tipo de Fórmula");
        AddFormulaTextField(formulaTypePanel, "Cod_Formula", values, "Código");
        AddFormulaTextField(formulaTypePanel, "Descripcion", values, "Descripción");
        FormulaEditorConfigurationHost.Children.Add(formulaTypePanel);

        var materialColumns = new Grid { ColumnSpacing = 10 };
        materialColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        materialColumns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftMaterials = new StackPanel { Spacing = 10 };
        leftMaterials.Children.Add(CreateFormulaMaterialGroup("Material por metro cúbico - Agregados", "Agregado", "A", 8, "Kg.", values));
        leftMaterials.Children.Add(CreateFormulaMaterialGroup("Cemento (por almacén)", "Cemento", "C", 5, "Kg.", values));
        var rightMaterials = new StackPanel { Spacing = 10 };
        rightMaterials.Children.Add(CreateFormulaMaterialGroup("Material por metro cúbico - Aditivos", "Aditivo", "D", 6, "Lt.", values));
        rightMaterials.Children.Add(CreateFormulaMaterialGroup("Agua (por almacén)", "Agua", "Agua", 3, "Lt.", values));
        rightMaterials.Children.Add(CreateFormulaMaterialGroup("Fibra (por almacén)", "Fibra", "Fibra", 3, "Kg.", values));

        Grid.SetColumn(leftMaterials, 0);
        Grid.SetColumn(rightMaterials, 1);
        materialColumns.Children.Add(leftMaterials);
        materialColumns.Children.Add(rightMaterials);
        FormulaEditorConfigurationHost.Children.Add(materialColumns);

        var additionalData = CreateFormulaGroup("Datos Adicionales");
        AddFormulaTextField(additionalData, "TMA", values, "TMA (mm)");
        AddFormulaTextField(additionalData, "Resistencia", values, "Resistencia (Kg/Cm²)");
        AddFormulaTextField(additionalData, "Edad", values, "Edad (días)");
        AddFormulaTextField(additionalData, "Revenimiento", values, "Revenimiento");
        AddFormulaTextField(additionalData, "Aditivos", values, "Aditivos");
        AddFormulaTextField(additionalData, "Colocacion", values, "Colocación");
        FormulaEditorAdditionalHost.Children.Add(additionalData);
        FormulaEditorConfigurationTab_Click(this, new RoutedEventArgs());
    }

    private StackPanel CreateFormulaGroup(string title)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var border = new Border
        {
            Background = _themeBrushes.GetBrush("#172337"),
            BorderBrush = _themeBrushes.GetBrush("#40516A"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = panel
        };
        var wrapper = new StackPanel { Spacing = 8 };
        wrapper.Children.Add(border);
        wrapper.Tag = panel;
        return wrapper;
    }

    private void AddFormulaTextField(StackPanel group, string fieldName, IReadOnlyDictionary<string, string> values, string labelText)
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var label = new TextBlock
        {
            Text = labelText,
            VerticalAlignment = VerticalAlignment.Center
        };
        var input = CreateFormulaTextBox(fieldName, values);
        Grid.SetColumn(input, 1);
        row.Children.Add(label);
        row.Children.Add(input);
        GetFormulaGroupContent(group).Children.Add(row);
    }

    private StackPanel CreateFormulaMaterialGroup(
        string title,
        string labelPrefix,
        string fieldPrefix,
        int count,
        string unit,
        IReadOnlyDictionary<string, string> values)
    {
        var group = CreateFormulaGroup(title);
        for (var index = 1; index <= count; index++)
        {
            var materialField = fieldPrefix == "Agua" ? (index == 1 ? "Agua" : $"Agua{index}") : $"{fieldPrefix}{index}";
            var quantityField = $"{materialField}C";
            var row = new Grid { ColumnSpacing = 6, Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
            var label = new TextBlock
            {
                Text = $"{labelPrefix} {index}:",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12
            };
            var quantity = CreateFormulaTextBox(quantityField, values);
            quantity.MaxLength = 8;
            quantity.PlaceholderText = "0.0";
            var unitLabel = new TextBlock { Text = unit, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
            var materialPicker = new ComboBox
            {
                ItemsSource = _formulaMaterialOptions.GetValueOrDefault(fieldPrefix, Array.Empty<string>()),
                Foreground = _themeBrushes.GetBrush("#F1F5F9"),
                Background = _themeBrushes.GetBrush("#111B2B"),
                Tag = materialField
            };
            UpdateFormulaMaterialPicker(materialPicker, materialField, values);
            materialPicker.SelectionChanged += FormulaMaterialPicker_SelectionChanged;
            var machineParameter = GetMachineMaterialParameter(materialField);
            var hopperMaterial = _dashboardMachineConfiguration.GetValueOrDefault(machineParameter)?.Trim();
            var materialState = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(hopperMaterial) ? "DESHABILITADA" : hopperMaterial,
                Foreground = _themeBrushes.GetBrush("#22C55E"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _formulaMaterialStates[materialField] = materialState;
            Grid.SetColumn(label, 0);
            Grid.SetColumn(quantity, 1);
            Grid.SetColumn(unitLabel, 2);
            Grid.SetColumn(materialPicker, 3);
            Grid.SetColumn(materialState, 4);
            row.Children.Add(label);
            row.Children.Add(quantity);
            row.Children.Add(unitLabel);
            row.Children.Add(materialPicker);
            row.Children.Add(materialState);
            GetFormulaGroupContent(group).Children.Add(row);
            _formulaEditorInputs.Add(materialField, materialPicker);
        }

        return group;
    }

    private void FormulaMaterialPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewingFormula || sender is not ComboBox)
        {
            return;
        }

        var mismatchMessage = GetFormulaMaterialMismatchMessage();
        if (mismatchMessage is not null)
        {
            SetFormStatus(
                FormulaEditorStatusInfoBar,
                mismatchMessage,
                InfoBarSeverity.Error);
        }
        else if (FormulaEditorStatusInfoBar.Severity == InfoBarSeverity.Error
                 && FormulaEditorStatusInfoBar.Message.StartsWith(
                     "El material seleccionado no coincide con el configurado en la tolva:",
                     StringComparison.Ordinal))
        {
            FormulaEditorStatusInfoBar.IsOpen = false;
        }
    }

    private string? GetFormulaMaterialMismatchMessage()
    {
        var mismatches = _formulaEditorInputs
            .Where(pair => pair.Value is ComboBox)
            .Select(pair =>
            {
                var selectedMaterial = ((ComboBox)pair.Value).SelectedItem as string;
                var configuredMaterial = _dashboardMachineConfiguration
                    .GetValueOrDefault(GetMachineMaterialParameter(pair.Key))?.Trim();
                selectedMaterial = string.IsNullOrWhiteSpace(selectedMaterial) ? "DESHABILITADA" : selectedMaterial.Trim();
                configuredMaterial = string.IsNullOrWhiteSpace(configuredMaterial) ? "DESHABILITADA" : configuredMaterial;
                return (Field: pair.Key, Selected: selectedMaterial, Configured: configuredMaterial);
            })
            .Where(item => !item.Selected.Equals(item.Configured, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return mismatches.Length == 0
            ? null
            : $"El material seleccionado no coincide con el configurado en la tolva: {string.Join(
                "; ",
                mismatches.Select(item =>
                    $"{GetFormulaMaterialDisplayName(item.Field)}: «{item.Selected}» (tolva: «{item.Configured}»)"))}.";
    }

    private static string GetFormulaMaterialDisplayName(string fieldName)
    {
        var materialName = fieldName switch
        {
            var field when field.StartsWith("Agua", StringComparison.Ordinal) => "Agua",
            var field when field.StartsWith('A') => "Agregado",
            var field when field.StartsWith('C') => "Cemento",
            var field when field.StartsWith('D') => "Aditivo",
            _ => "Fibra"
        };
        var slot = fieldName switch
        {
            "Agua" => "1",
            var field when field.StartsWith("Agua", StringComparison.Ordinal) => field["Agua".Length..],
            var field when field.StartsWith("Fibra", StringComparison.Ordinal) => field["Fibra".Length..],
            _ => fieldName[1..]
        };
        return $"{materialName} {slot}";
    }

    private static StackPanel GetFormulaGroupContent(StackPanel group) => (StackPanel)group.Tag;

    private TextBox CreateFormulaTextBox(string fieldName, IReadOnlyDictionary<string, string> values)
    {
        var input = new TextBox
        {
            Text = values.GetValueOrDefault(fieldName, string.Empty),
            MaxLength = FormulaService.GetFieldMaxLength(fieldName),
            Foreground = _themeBrushes.GetBrush("#F1F5F9"),
            Background = _themeBrushes.GetBrush("#111B2B"),
            Tag = fieldName
        };
        _formulaEditorInputs[fieldName] = input;
        return input;
    }

    private void FormulaEditorConfigurationTab_Click(object sender, RoutedEventArgs e)
    {
        FormulaEditorConfigurationHost.Visibility = Visibility.Visible;
        FormulaEditorAdditionalHost.Visibility = Visibility.Collapsed;
        FormulaEditorConfigurationTabButton.Background = _themeBrushes.GetBrush("#0078D4");
        FormulaEditorAdditionalTabButton.Background = _themeBrushes.GetBrush("#26364D");
        FormulaEditorConfigurationTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
        FormulaEditorAdditionalTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
    }

    private void FormulaEditorAdditionalTab_Click(object sender, RoutedEventArgs e)
    {
        FormulaEditorConfigurationHost.Visibility = Visibility.Collapsed;
        FormulaEditorAdditionalHost.Visibility = Visibility.Visible;
        FormulaEditorConfigurationTabButton.Background = _themeBrushes.GetBrush("#26364D");
        FormulaEditorAdditionalTabButton.Background = _themeBrushes.GetBrush("#0078D4");
        FormulaEditorConfigurationTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
        FormulaEditorAdditionalTabButton.Foreground = _themeBrushes.GetBrush("#F1F5F9");
    }

    private void UpdateFormulaMaterialPicker(ComboBox picker, string fieldName, IReadOnlyDictionary<string, string> values)
    {
        if (values.TryGetValue(fieldName, out var selectedValue)
            && !string.IsNullOrWhiteSpace(selectedValue)
            && !_formulaMaterialOptions.GetValueOrDefault(GetMaterialCategory(fieldName), Array.Empty<string>()).Contains(selectedValue, StringComparer.OrdinalIgnoreCase))
        {
            picker.ItemsSource = _formulaMaterialOptions.GetValueOrDefault(GetMaterialCategory(fieldName), Array.Empty<string>())
                .Concat(new[] { selectedValue }).ToArray();
        }

        var valueToSelect = values.GetValueOrDefault(fieldName);
        picker.SelectedItem = picker.Items.Cast<string>()
            .FirstOrDefault(item => item.Equals(valueToSelect, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetMaterialCategory(string fieldName) => fieldName switch
    {
        var name when name.StartsWith("Agua", StringComparison.Ordinal) => "Agua",
        var name when name.StartsWith('A') => "A",
        var name when name.StartsWith('C') => "C",
        var name when name.StartsWith('D') => "D",
        _ => "Fibra"
    };

    private void CancelFormulaEditor_Click(object sender, RoutedEventArgs e)
    {
        FormulaEditorOverlay.Visibility = Visibility.Collapsed;
        FormulaEditorSaveButton.Visibility = Visibility.Visible;
        FormulaEditorSaveButton.IsEnabled = HasActionPermission(
            "Fórmulas",
            _editingFormula ? "Editar" : "Alta");
        _viewingFormula = false;
    }

    private async void SaveFormulaEditor_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Fórmulas", _editingFormula ? "Editar" : "Alta"))
        {
            return;
        }

        var values = _formulaEditorInputs.ToDictionary(
            pair => pair.Key,
            pair => pair.Value switch
            {
                TextBox textBox => textBox.Text.Trim(),
                ComboBox comboBox => comboBox.SelectedItem as string ?? string.Empty,
                _ => string.Empty
            },
            StringComparer.Ordinal);
        var formulaCode = values.GetValueOrDefault("Cod_Formula", string.Empty);
        var formulaDescription = values.GetValueOrDefault("Descripcion", string.Empty);
        var identityError = FormulaService.GetFormulaIdentityValidationError(formulaCode, formulaDescription);
        if (identityError is not null)
        {
            SetFormStatus(FormulaEditorStatusInfoBar, identityError, InfoBarSeverity.Error);
            var invalidField = identityError.StartsWith("El código", StringComparison.Ordinal)
                ? "Cod_Formula"
                : "Descripcion";
            if (_formulaEditorInputs.TryGetValue(invalidField, out var input))
            {
                input.Focus(FocusState.Programmatic);
            }

            return;
        }

        var quantityError = FormulaService.GetFormulaMaterialQuantityValidationError(values);
        if (quantityError is not null)
        {
            SetFormStatus(FormulaEditorStatusInfoBar, quantityError, InfoBarSeverity.Error);
            return;
        }

        var materialMismatch = GetFormulaMaterialMismatchMessage();
        if (materialMismatch is not null)
        {
            SetFormStatus(FormulaEditorStatusInfoBar, materialMismatch, InfoBarSeverity.Error);
            return;
        }

        FormulaEditorSaveButton.IsEnabled = false;
        SetFormStatus(FormulaEditorStatusInfoBar, "Guardando fórmula en BlapaciSQL...", InfoBarSeverity.Informational);
        try
        {
            if (_editingFormula)
            {
                if (_selectedFormula is null)
                {
                    SetFormStatus(FormulaEditorStatusInfoBar, "Selecciona una fórmula activa para editar.", InfoBarSeverity.Error);
                    return;
                }

                await _formulaService.UpdateFormulaAsync(_selectedFormula.Id, values);
            }
            else
            {
                await _formulaService.AddFormulaAsync(values, _authenticatedUsername);
            }

            FormulaEditorOverlay.Visibility = Visibility.Collapsed;
            await LoadFormulasAsync();
            SetFormStatus(
                FormulaFormStatusInfoBar,
                _editingFormula
                    ? "Fórmula actualizada correctamente."
                    : "Fórmula agregada correctamente.",
                InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(FormulaEditorStatusInfoBar, $"No se pudo guardar la fórmula (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(FormulaEditorStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            FormulaEditorSaveButton.IsEnabled = HasActionPermission(
                "Fórmulas",
                _editingFormula ? "Editar" : "Alta");
        }
    }

    private async void DeleteFormula_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireActionPermission("Fórmulas", "Baja"))
        {
            return;
        }

        if (_selectedFormula is null || _showingCancelledFormulas)
        {
            SetFormStatus(FormulaFormStatusInfoBar, "Selecciona una fórmula activa para borrar.", InfoBarSeverity.Error);
            return;
        }

        var code = _selectedFormula.Values.GetValueOrDefault("Cod_Formula", string.Empty);
        var confirmation = new ContentDialog
        {
            Title = "Cancelar fórmula",
            Content = $"¿Deseas borrar la fórmula \"{code}\"? Se conservará en la pestaña Fórmulas Canceladas.",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        FormulaDeleteButton.IsEnabled = false;
        FormulaEditButton.IsEnabled = false;
        try
        {
            await _formulaService.CancelFormulaAsync(_selectedFormula.Id, _authenticatedUsername);
            await LoadFormulasAsync();
            SetFormStatus(FormulaFormStatusInfoBar, $"La fórmula {code} se movió a Fórmulas Canceladas.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetFormStatus(
                FormulaFormStatusInfoBar,
                $"No se pudo borrar la fórmula (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetFormStatus(FormulaFormStatusInfoBar, exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            UpdateFormulaActionButtons();
        }
    }

    private async Task<bool> LoadInventoryAsync()
    {
        SetInventoryStatus("Consultando existencias en BlapaciSQL...", InfoBarSeverity.Informational);

        try
        {
            var machineConfiguration = await _machineConfigurationService.GetConfigurationAsync();
            var inventory = await _inventoryService.GetInventoryAsync();
            var suppliers = await _inventoryService.GetSuppliersAsync();
            AggregateInventory.Clear();
            AdditiveInventory.Clear();
            WaterInventory.Clear();
            CementInventory.Clear();
            FiberInventory.Clear();
            InventorySuppliers.Clear();

            foreach (var material in inventory)
            {
                var isDisabledOption = material.Name.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase);
                if (isDisabledOption
                    ? !HasDisabledMachineMaterial(material.Category, machineConfiguration.Values)
                    : !IsConfiguredMachineMaterial(material, machineConfiguration.Values))
                {
                    continue;
                }

                var target = material.Category switch
                {
                    "AGREGADOS" => AggregateInventory,
                    "ADITIVOS" => AdditiveInventory,
                    "AGUAS" => WaterInventory,
                    "CEMENTOS" => CementInventory,
                    "FIBRAS" => FiberInventory,
                    _ => throw new InvalidOperationException($"Categoría de inventario no reconocida: {material.Category}.")
                };
                target.Add(material);
            }

            foreach (var supplier in suppliers)
            {
                InventorySuppliers.Add(supplier);
            }

            InventorySupplierComboBox.SelectedItem =
                InventorySuppliers.FirstOrDefault(supplier => supplier.Equals("PROVEEDOR GENERAL", StringComparison.OrdinalIgnoreCase))
                ?? InventorySuppliers.FirstOrDefault();
            UpdateInventoryTypes();
            var displayedMaterialCount = AggregateInventory.Count
                + AdditiveInventory.Count
                + WaterInventory.Count
                + CementInventory.Count
                + FiberInventory.Count;
            SetInventoryStatus(
                $"Existencias actualizadas desde BlapaciSQL. Registros: {displayedMaterialCount}; proveedores: {suppliers.Count}.",
                InfoBarSeverity.Success);
            return true;
        }
        catch (SqlException exception)
        {
            SetInventoryStatus(
                $"No se pudieron cargar las existencias o los proveedores desde BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
            return false;
        }
    }

    private void SetInventoryStatus(string message, InfoBarSeverity severity)
    {
        InventoryFormStatusInfoBar.Message = message;
        InventoryFormStatusInfoBar.Severity = severity;
        UpdateInventoryStatusStyle();
        InventoryFormStatusInfoBar.IsOpen = true;
    }

    private void UpdateInventoryStatusStyle()
    {
        StatusInfoBarStyler.Apply(InventoryFormStatusInfoBar, RequestedTheme);
    }

    private static bool HasDisabledMachineMaterial(
        string category,
        IReadOnlyDictionary<string, string> configuration)
    {
        var (parameterPrefix, slotCount) = GetMachineMaterialParameterRange(category);
        return Enumerable.Range(1, slotCount).Any(index =>
            configuration.GetValueOrDefault($"{parameterPrefix}{index}")?
                .Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool IsConfiguredMachineMaterial(
        InventoryMaterial material,
        IReadOnlyDictionary<string, string> configuration)
    {
        var (parameterPrefix, slotCount) = GetMachineMaterialParameterRange(material.Category);
        var materialName = material.Name.Trim();
        return Enumerable.Range(1, slotCount).Any(index =>
            configuration.GetValueOrDefault($"{parameterPrefix}{index}")?
                .Equals(materialName, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static (string ParameterPrefix, int SlotCount) GetMachineMaterialParameterRange(string category) =>
        category switch
        {
            "AGREGADOS" => ("A", 8),
            "ADITIVOS" => ("D", 6),
            "AGUAS" => ("G", 3),
            "CEMENTOS" => ("C", 5),
            "FIBRAS" => ("FI", 3),
            _ => throw new InvalidOperationException($"Categoría de inventario no reconocida: {category}.")
        };

    private void InventoryMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateInventoryTypes();
    }

    private void UpdateInventoryTypes()
    {
        SelectedInventoryMaterials.Clear();
        InventoryTypeComboBox.SelectedItem = null;

        if (InventoryMaterialComboBox.SelectedItem is not ComboBoxItem { Content: string category })
        {
            return;
        }

        var materials = category switch
        {
            "AGREGADOS" => AggregateInventory,
            "ADITIVOS" => AdditiveInventory,
            "AGUAS" => WaterInventory,
            "CEMENTOS" => CementInventory,
            "FIBRAS" => FiberInventory,
            _ => throw new InvalidOperationException($"Categoría de inventario no reconocida: {category}.")
        };

        foreach (var material in materials)
        {
            SelectedInventoryMaterials.Add(material);
        }

        if (SelectedInventoryMaterials.Count == 0)
        {
            SetInventoryStatus(
                $"No hay materiales disponibles para {category}. Actualiza las existencias e inténtalo de nuevo.",
                InfoBarSeverity.Warning);
        }
    }

    private void CloseInventoryForm_Click(object sender, RoutedEventArgs e)
    {
        InventoryFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void CloseReportForm_Click(object sender, RoutedEventArgs e)
    {
        ReportFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void OpenReportCenter()
    {
        _currentReportResult = null;
        SetReportExportButtonsEnabled(false);
        ReportResultsHost.Children.Clear();
        ReportCatalogItems.Clear();
        foreach (var report in _reportCatalogService.GetCatalog().Where(CanViewReport))
        {
            ReportCatalogItems.Add(report);
        }

        ReportStartDatePicker.Date = new DateTimeOffset(DateTime.Today.AddDays(-6));
        ReportEndDatePicker.Date = new DateTimeOffset(DateTime.Today);
        ReportDatasetComboBox.SelectedIndex = ReportCatalogItems.Count > 0 ? 0 : -1;
        ReportFormStatusText.Text = ReportCatalogItems.Count > 0
            ? "Selecciona una vista y un periodo. El catálogo consulta únicamente información autorizada."
            : "No tienes permisos de visualización para las vistas disponibles.";
    }

    private bool CanViewReport(ReportCatalogDefinition report) =>
        report.PermissionModules.Any(module => HasActionPermission(module, "Visualizar"));

    private bool CanExportReport(ReportCatalogDefinition report) =>
        report.PermissionModules.Any(module => HasActionPermission(module, "Exportar"));

    private void ReportDatasetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReportDatasetComboBox.SelectedItem is ReportCatalogDefinition report)
        {
            _currentReportResult = null;
            ReportResultsHost.Children.Clear();
            SetReportExportButtonsEnabled(false);
            ReportFormStatusText.Text = report.Description;
        }
    }

    private void SetReportExportButtonsEnabled(bool isEnabled)
    {
        ExportCsvReportButton.IsEnabled = isEnabled;
        ExportExcelReportButton.IsEnabled = isEnabled;
        ExportPdfReportButton.IsEnabled = isEnabled;
    }

    private async void RunReport_Click(object sender, RoutedEventArgs e)
    {
        if (ReportDatasetComboBox.SelectedItem is not ReportCatalogDefinition report)
        {
            ReportFormStatusText.Text = "Selecciona una vista del catálogo.";
            return;
        }

        if (!await RefreshCurrentUserPermissionsAsync() || !CanViewReport(report))
        {
            ReportFormStatusText.Text = "Tu perfil no tiene permiso para consultar esta vista.";
            return;
        }

        var startDate = ReportStartDatePicker.Date.Date;
        var endDate = ReportEndDatePicker.Date.Date;
        if (startDate > endDate)
        {
            ReportFormStatusText.Text = "La fecha inicial no puede ser posterior a la fecha final.";
            return;
        }

        RunReportButton.IsEnabled = false;
        SetReportExportButtonsEnabled(false);
        ReportResultsHost.Children.Clear();
        ReportFormStatusText.Text = "Consultando BlapaciSQL...";
        try
        {
            _currentReportResult = await _reportCatalogService.ExecuteAsync(
                report.Id,
                startDate,
                endDate.AddDays(1));
            RenderReportResult(_currentReportResult);
            var rowCount = _currentReportResult.Rows.Count;
            ReportFormStatusText.Text = _currentReportResult.IsTruncated
                ? $"Se muestran los primeros {rowCount:N0} registros. Reduce el periodo para consultar más detalle."
                : $"Consulta completada. Registros: {rowCount:N0}.";
            SetReportExportButtonsEnabled(rowCount > 0 && CanExportReport(report));
        }
        catch (SqlException exception)
        {
            _currentReportResult = null;
            ReportFormStatusText.Text = $"No se pudo generar el reporte (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            _currentReportResult = null;
            ReportFormStatusText.Text = exception.Message;
        }
        finally
        {
            RunReportButton.IsEnabled = true;
        }
    }

    private void RenderReportResult(ReportCatalogResult result)
    {
        ReportResultsHost.Children.Clear();
        var columnWidth = result.Columns.Count > 10 ? 145d : 180d;
        ReportResultsHost.MinWidth = Math.Max(1000, result.Columns.Count * columnWidth);
        ReportResultsHost.Children.Add(CreateReportResultRow(result.Columns, columnWidth, isHeader: true));
        foreach (var row in result.Rows)
        {
            ReportResultsHost.Children.Add(CreateReportResultRow(row, columnWidth, isHeader: false));
        }

        if (result.Rows.Count == 0)
        {
            ReportResultsHost.Children.Add(new TextBlock
            {
                Text = "No se encontraron registros en el periodo seleccionado.",
                Margin = new Thickness(12),
                Foreground = _themeBrushes.GetBrush("#A9B8CC")
            });
        }
    }

    private Grid CreateReportResultRow(IReadOnlyList<string> values, double columnWidth, bool isHeader)
    {
        var row = new Grid
        {
            Background = _themeBrushes.GetBrush(isHeader ? "#263B5A" : "#1E2D43"),
            MinHeight = isHeader ? 36 : 32,
            ColumnSpacing = 1
        };
        for (var index = 0; index < values.Count; index++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columnWidth) });
            var cell = new Border
            {
                Padding = new Thickness(8, 4, 8, 4),
                BorderBrush = _themeBrushes.GetBrush("#40516A"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new TextBlock
                {
                    Text = values[index],
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = _themeBrushes.GetBrush(isHeader ? "#F1F5F9" : "#E6EDF7"),
                    FontWeight = isHeader ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
                }
            };
            ToolTipService.SetToolTip(cell, values[index]);
            Grid.SetColumn(cell, index);
            row.Children.Add(cell);
        }

        return row;
    }

    private async void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string format }
            || format is not ("csv" or "xlsx" or "pdf"))
        {
            ReportFormStatusText.Text = "El formato de exportación seleccionado no es válido.";
            return;
        }

        if (_currentReportResult is not { Rows.Count: > 0 } result)
        {
            ReportFormStatusText.Text = "Genera un reporte con resultados antes de exportar.";
            return;
        }

        if (!await RefreshCurrentUserPermissionsAsync() || !CanExportReport(result.Definition))
        {
            SetReportExportButtonsEnabled(false);
            ReportFormStatusText.Text = "Tu perfil no tiene permiso para exportar esta vista.";
            return;
        }

        if (Application.Current is not App { MainWindow: { } window })
        {
            ReportFormStatusText.Text = "No se pudo abrir el selector de archivos.";
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            var (fileTypeName, extension, formatName) = format switch
            {
                "csv" => ("Archivo CSV", ".csv", "CSV"),
                "xlsx" => ("Libro de Excel", ".xlsx", "Excel"),
                "pdf" => ("Documento PDF", ".pdf", "PDF"),
                _ => throw new InvalidOperationException("El formato de exportación seleccionado no es válido.")
            };
            picker.FileTypeChoices.Add(fileTypeName, new List<string> { extension });
            picker.SuggestedFileName = $"{result.Definition.Id}_{DateTime.Now:yyyyMMdd}";
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var contents = format switch
            {
                "csv" => _reportExportService.BuildCsv(result),
                "xlsx" => _reportExportService.BuildExcel(result),
                "pdf" => _reportExportService.BuildPdf(result),
                _ => throw new InvalidOperationException("El formato de exportación seleccionado no es válido.")
            };
            await File.WriteAllBytesAsync(file.Path, contents);
            ReportFormStatusText.Text = $"Reporte exportado como {formatName}: {file.Path}";
        }
        catch (COMException exception)
        {
            ReportFormStatusText.Text = $"No se pudo abrir el selector de archivos: {exception.Message}";
        }
        catch (IOException exception)
        {
            ReportFormStatusText.Text = $"No se pudo escribir el archivo de reporte: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportFormStatusText.Text = $"No hay permisos para guardar el archivo de reporte: {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ReportFormStatusText.Text = $"No se pudo generar el archivo de reporte: {exception.Message}";
        }
        catch (ArgumentException exception)
        {
            ReportFormStatusText.Text = $"Los datos del reporte no son válidos para exportar: {exception.Message}";
        }
    }

    private void InventoryOperation_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { IsChecked: true } selectedOperation)
        {
            var otherOperation = ReferenceEquals(selectedOperation, InventoryEntriesCheckBox)
                ? InventoryExitsCheckBox
                : InventoryEntriesCheckBox;
            otherOperation.IsChecked = false;
        }

        var operation = InventoryEntriesCheckBox.IsChecked == true
            ? "Entradas"
            : InventoryExitsCheckBox.IsChecked == true
                ? "Salidas"
                : null;

        if (InventoryEntriesCheckBox.IsChecked == true)
        {
            InventoryMovementTitleText.Text = "Entrada General";
        }
        else if (InventoryExitsCheckBox.IsChecked == true)
        {
            InventoryMovementTitleText.Text = "Salida General";
        }

        InventorySaveButton.IsEnabled = operation is not null
            && HasActionPermission(operation, "Alta");
        SetInventoryStatus(
            operation is null
                ? "Selecciona Entradas para agregar o Salidas para quitar existencias."
                : $"{operation}: completa los datos y pulsa Guardar para registrar el movimiento.",
            InfoBarSeverity.Informational);
    }

    private async void SaveInventory_Click(object sender, RoutedEventArgs e)
    {
        var isEntry = InventoryEntriesCheckBox.IsChecked == true;
        var isExit = InventoryExitsCheckBox.IsChecked == true;
        var permissionModule = isEntry ? "Entradas" : "Salidas";
        if (!RequireActionPermission(permissionModule, "Alta"))
        {
            return;
        }

        if (isEntry == isExit)
        {
            SetInventoryStatus("Selecciona Entradas para agregar o Salidas para quitar existencias.", InfoBarSeverity.Warning);
            return;
        }

        if (InventoryMaterialComboBox.SelectedItem is not ComboBoxItem { Content: string category }
            || InventoryTypeComboBox.SelectedItem is not InventoryMaterial material)
        {
            SetInventoryStatus("Selecciona el material y el tipo que deseas registrar.", InfoBarSeverity.Warning);
            return;
        }

        if (!decimal.TryParse(InventoryQuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity)
            && !decimal.TryParse(InventoryQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity))
        {
            SetInventoryStatus("Ingresa una cantidad numérica válida.", InfoBarSeverity.Warning);
            return;
        }

        if (quantity <= 0)
        {
            SetInventoryStatus("La cantidad debe ser mayor que cero.", InfoBarSeverity.Warning);
            return;
        }

        var supplier = InventorySupplierComboBox.SelectedItem as string ?? string.Empty;
        if (supplier.Length == 0)
        {
            SetInventoryStatus("Selecciona un proveedor.", InfoBarSeverity.Warning);
            return;
        }

        if (_authenticatedUsername.Length is 0 or > 10)
        {
            SetInventoryStatus("El usuario no es válido para registrar movimientos (máximo 10 caracteres).", InfoBarSeverity.Warning);
            return;
        }

        var origin = InventoryOriginTextBox.Text.Trim();
        var invoice = InventoryInvoiceTextBox.Text.Trim();
        if (origin.Length > 50 || invoice.Length > 50 || supplier.Length > 50)
        {
            SetInventoryStatus("Proveedor, origen y factura deben tener como máximo 50 caracteres.", InfoBarSeverity.Warning);
            return;
        }

        InventorySaveButton.IsEnabled = false;
        SetInventoryStatus("Guardando el movimiento en BlapaciSQL...", InfoBarSeverity.Informational);

        try
        {
            await _inventoryService.SaveInventoryMovementAsync(
                category,
                material.Name,
                quantity,
                supplier,
                origin,
                invoice,
                _authenticatedUsername,
                isExit);

            InventoryQuantityTextBox.Text = "0.0";
            if (await LoadInventoryAsync())
            {
                SetInventoryStatus(
                    $"{(isExit ? "Salida" : "Entrada")} de {material.Name} guardada correctamente. Existencia actualizada.",
                    InfoBarSeverity.Success);
            }
            else
            {
                SetInventoryStatus(
                    $"Movimiento guardado, pero no se pudo actualizar la vista. {InventoryFormStatusInfoBar.Message}",
                    InfoBarSeverity.Warning);
            }
        }
        catch (SqlException exception)
        {
            SetInventoryStatus(
                $"No se pudo guardar el movimiento en BlapaciSQL (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetInventoryStatus(exception.Message, InfoBarSeverity.Error);
        }
        catch (OverflowException)
        {
            SetInventoryStatus("La cantidad excede el valor numérico que puede almacenarse en el inventario.", InfoBarSeverity.Error);
        }
        finally
        {
            var selectedModule = InventoryEntriesCheckBox.IsChecked == true
                ? "Entradas"
                : InventoryExitsCheckBox.IsChecked == true
                    ? "Salidas"
                    : null;
            InventorySaveButton.IsEnabled = selectedModule is not null
                && HasActionPermission(selectedModule, "Alta");
        }
    }

    private async void LoadTemplateImage_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked)
        {
            return;
        }

        if (Application.Current is not App { MainWindow: { } window })
        {
            ModuleStatusText.Text = "No se pudo abrir el selector de archivos porque la ventana principal no está disponible.";
            return;
        }

        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

            var file = await picker.PickSingleFileAsync();
            if (file is not null)
            {
                await LoadTemplateImageAsync(file);
            }
        }
        catch (COMException)
        {
            ModuleStatusText.Text = "No se pudo abrir el selector o leer la imagen. Inténtalo de nuevo.";
        }
        catch (IOException)
        {
            ModuleStatusText.Text = "No se pudo leer la imagen seleccionada. Verifica que el archivo siga disponible.";
        }
        catch (UnauthorizedAccessException)
        {
            ModuleStatusText.Text = "No hay permisos para leer la imagen seleccionada.";
        }
    }

    private void DesignSurface_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = !_isCanvasLocked && e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private async void DesignSurface_Drop(object sender, DragEventArgs e)
    {
        if (_isCanvasLocked)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            ModuleStatusText.Text = "Arrastra un archivo de imagen PNG, JPG o BMP al lienzo.";
            return;
        }

        try
        {
            var storageItems = await e.DataView.GetStorageItemsAsync();
            var imageFile = storageItems.OfType<StorageFile>().FirstOrDefault(file => SupportedImageTypes.Contains(file.FileType));
            if (imageFile is null)
            {
                ModuleStatusText.Text = "No se encontró una imagen compatible. Usa un archivo PNG, JPG o BMP.";
                return;
            }

            await LoadTemplateImageAsync(imageFile);
        }
        catch (COMException)
        {
            ModuleStatusText.Text = "No se pudo leer la imagen soltada. Inténtalo de nuevo.";
        }
        catch (IOException)
        {
            ModuleStatusText.Text = "No se pudo leer la imagen soltada. Verifica que el archivo siga disponible.";
        }
        catch (UnauthorizedAccessException)
        {
            ModuleStatusText.Text = "No hay permisos para leer la imagen soltada.";
        }
    }

    private async Task LoadTemplateImageAsync(StorageFile file, bool persistState = true)
    {
        if (_isCanvasLocked)
        {
            return;
        }

        if (!SupportedImageTypes.Contains(file.FileType))
        {
            ModuleStatusText.Text = "Formato no compatible. Usa una imagen PNG, JPG o BMP.";
            return;
        }

        var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
        using (var stream = await file.OpenReadAsync())
        {
            await bitmap.SetSourceAsync(stream);
        }

        var storedImagePath = persistState
            ? await _designerCanvasStore.CopyImageToStoreAsync(file)
            : file.Path;
        TemplateImage.Source = bitmap;
        EmptyCanvasPrompt.Visibility = Visibility.Collapsed;
        _designerCanvasState.ImagePath = storedImagePath;
        ModuleStatusText.Text = $"Plantilla cargada: {file.Name}. El lienzo está listo para agregar controles.";
        if (persistState)
        {
            await SaveDesignerCanvasStateAsync();
        }
    }

    private async Task<string?> LoadMachineImageToDesignCanvasAsync(string path, bool persistState = true)
    {
        if (_isCanvasLocked)
        {
            return "El lienzo está bloqueado. Desbloquéalo antes de cambiar la imagen.";
        }

        var extension = Path.GetExtension(path);
        if (!SupportedImageTypes.Contains(extension))
        {
            return "Formato no compatible. Usa una imagen PNG, JPG o BMP.";
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            await LoadTemplateImageAsync(file, persistState);
            return null;
        }
        catch (COMException)
        {
            return "Windows no pudo abrir el archivo de imagen.";
        }
        catch (FileNotFoundException)
        {
            return "No se encontró el archivo seleccionado.";
        }
        catch (DirectoryNotFoundException)
        {
            return "No se encontró la carpeta de la imagen seleccionada.";
        }
        catch (IOException)
        {
            return "No se pudo leer el archivo de imagen.";
        }
        catch (UnauthorizedAccessException)
        {
            return "No hay permisos para leer el archivo de imagen.";
        }
        catch (ArgumentException)
        {
            return "La ruta del archivo de imagen no es válida.";
        }
    }

    private void CanvasLockButton_Click(object sender, RoutedEventArgs e)
    {
        _isCanvasLocked = !_isCanvasLocked;
        _designerCanvasState.IsLocked = _isCanvasLocked;
        UpdateCanvasEditMode();
        _ = SaveDesignerCanvasStateAsync();
    }

    private async void CanvasResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked)
        {
            return;
        }

        _designerCanvasState.ImagePath = null;
        _designerCanvasState.Controls.Clear();
        _designerCanvasState.MessageTextOverrides.Clear();
        TemplateImage.Source = null;
        EmptyCanvasPrompt.Visibility = Visibility.Visible;
        _selectedCanvasMessage = null;
        _movingCanvasControl = null;
        _resizingCanvasControl = null;
        _activeCanvasResizeHandle = null;
        UpdateCanvasTransformHandles(null);
        DesignCanvas.Children.Clear();
        _canvasValueCaptions.Clear();
        _canvasValueTexts.Clear();
        RefreshDashboardMaterials();
        ModuleStatusText.Text = "Lienzo restablecido. Carga una imagen o agrega controles para comenzar.";
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private void UpdateCanvasEditMode()
    {
        CanvasLockIcon.Glyph = _isCanvasLocked ? "\uE72E" : "\uE785";
        var tooltip = _isCanvasLocked ? "Desbloquear lienzo" : "Bloquear lienzo";
        ToolTipService.SetToolTip(CanvasLockButton, tooltip);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(CanvasLockButton, tooltip);
        CanvasResetButton.IsEnabled = !_isCanvasLocked;
        foreach (var button in MessageControlPalette.Children.OfType<Button>())
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is MessageConfigurationRecord message
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.MessageControl, message.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in ConfiguredAdditionalControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is AdditionalConfigurationPaletteItem item
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.AdditionalControl, item.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in AggregateControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is AggregateConfigurationPaletteItem item
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.AggregateControl, item.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in CementControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is CementConfigurationPaletteItem item
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.CementControl, item.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in AdditiveControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is AdditiveConfigurationPaletteItem item
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.AdditiveControl, item.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in WaterControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is WaterConfigurationPaletteItem item
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.WaterControl, item.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in FiberControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is FiberConfigurationPaletteItem item
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.FiberControl, item.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in AdditionalImageControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is AdditionalImageConfigurationRecord image
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.ImageControl, image.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in ValveImageControlPalette.Children
                     .OfType<StackPanel>()
                     .SelectMany(column => column.Children.OfType<Button>()))
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is AdditionalImageConfigurationRecord image
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.ImageControl, image.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var button in PumpImageControlPalette.Children.OfType<Button>())
        {
            button.IsEnabled = !_isCanvasLocked
                && button.Tag is AdditionalImageConfigurationRecord image
                && !_designerCanvasState.Controls.Any(control =>
                    string.Equals(control.ImageControl, image.Control, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var control in DesignCanvas.Children.OfType<Border>())
        {
            control.IsHitTestVisible = !_isCanvasLocked;
        }

        foreach (var handle in _canvasTransformHandles)
        {
            handle.Visibility = _isCanvasLocked ? Visibility.Collapsed : Visibility.Visible;
        }

        DesignCanvas.IsHitTestVisible = !_isCanvasLocked;
        DesignSurface.AllowDrop = !_isCanvasLocked;
        if (EmptyCanvasPrompt.Visibility == Visibility.Visible)
        {
            EmptyCanvasPrompt.IsHitTestVisible = !_isCanvasLocked;
            EmptyCanvasPrompt.Opacity = _isCanvasLocked ? 0.55 : 1;
        }

    }

    private async void PlantToolsPanel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlantToolsPanel.SelectedItem is TabViewItem { Header: "Controles" })
        {
            await RefreshMessagePaletteAsync();
            await RefreshAdditionalImagePaletteAsync();
            await RefreshConfiguredAdditionalPaletteAsync();
            await RefreshMaterialControlPalettesAsync();
            await RefreshPumpImagePaletteAsync();
            await RefreshValveImagePaletteAsync();
        }
    }

    private async Task RefreshMaterialControlPalettesAsync()
    {
        await RefreshAggregatePaletteAsync();
        await RefreshCementPaletteAsync();
        await RefreshAdditivePaletteAsync();
        await RefreshWaterPaletteAsync();
        await RefreshFiberPaletteAsync();
    }

    private async Task RefreshMessagePaletteAsync()
    {
        if (_isMessagePaletteLoading)
        {
            return;
        }

        _isMessagePaletteLoading = true;
        MessagePaletteStatusText.Text = "Cargando mensajes desde la configuración...";
        MessageControlPalette.Children.Clear();
        try
        {
            var configuredMessages = await _messageConfigurationService.GetMessagesAsync();
            foreach (var index in Enumerable.Range(1, 10))
            {
                var control = $"Mensaje{index}";
                if (!configuredMessages.TryGetValue(control, out var message))
                {
                    continue;
                }

                var button = new Button
                {
                    Content = new TextBlock
                    {
                        Text = $"Mensaje {index}",
                        TextWrapping = TextWrapping.Wrap
                    },
                    Tag = message,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                button.Click += AddConfiguredMessage_Click;
                MessageControlPalette.Children.Add(button);
            }

            var missingControls = Enumerable.Range(1, 10)
                .Select(index => $"Mensaje{index}")
                .Where(control => !configuredMessages.ContainsKey(control))
                .ToArray();
            MessagePaletteStatusText.Text = missingControls.Length == 0
                ? "Selecciona un mensaje para agregarlo al lienzo."
                : $"No se encontraron en dbo.Mensajes: {string.Join(", ", missingControls)}.";
        }
        catch (SqlException exception)
        {
            MessagePaletteStatusText.Text = $"No se pudieron cargar los mensajes (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            MessagePaletteStatusText.Text = $"No se pudieron cargar los mensajes: {exception.Message}";
        }
        finally
        {
            _isMessagePaletteLoading = false;
            UpdateCanvasEditMode();
        }
    }

    private async void AddConfiguredMessage_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: MessageConfigurationRecord message })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.MessageControl, message.Control, StringComparison.OrdinalIgnoreCase)))
        {
            MessagePaletteStatusText.Text = $"{message.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "Mensaje",
            MessageControl = message.Control,
            Text = GetMessageText(message),
            Format = message.Format,
            Width = 260,
            Height = 56,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - 260)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - 56))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async Task RefreshAdditionalImagePaletteAsync()
    {
        if (_isAdditionalImagePaletteLoading)
        {
            return;
        }

        _isAdditionalImagePaletteLoading = true;
        AdditionalImagePaletteStatusText.Text = "Cargando imágenes adicionales...";
        AdditionalImageControlColumn1.Children.Clear();
        AdditionalImageControlColumn2.Children.Clear();
        AdditionalImageControlColumn3.Children.Clear();
        try
        {
            if (!await LoadAdditionalImageConfigurationsAsync())
            {
                return;
            }

            foreach (var index in Enumerable.Range(1, 25))
            {
                var name = $"AdI{index}";
                if (!_additionalImageConfigurations.TryGetValue(name, out var image))
                {
                    continue;
                }

                var button = new Button
                {
                    Content = name,
                    Tag = image,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                button.Click += AddConfiguredAdditionalImage_Click;
                var column = ((index - 1) / 10) switch
                {
                    0 => AdditionalImageControlColumn1,
                    1 => AdditionalImageControlColumn2,
                    _ => AdditionalImageControlColumn3
                };
                button.HorizontalContentAlignment = HorizontalAlignment.Center;
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                column.Children.Add(button);
            }

            var missingControls = Enumerable.Range(1, 25)
                .Select(index => $"AdI{index}")
                .Where(control => !_additionalImageConfigurations.ContainsKey(control))
                .ToArray();
            AdditionalImagePaletteStatusText.Text = missingControls.Length == 0
                ? "Selecciona un adicional para agregarlo al lienzo."
                : $"No se encontraron en dbo.Imagenes: {string.Join(", ", missingControls)}.";

            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "ImagenAdicional", StringComparison.OrdinalIgnoreCase)))
            {
                if (!_additionalImageConfigurations.TryGetValue(control.ImageControl ?? string.Empty, out var image))
                {
                    continue;
                }

                control.ImageOnPath = image.ImageOn;
                control.ImageOffPath = image.ImageOff;
                var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
                    candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
                if (container?.Child is Image canvasImage)
                {
                    await SetAdditionalImageSourceAsync(canvasImage, control);
                }
            }
        }
        finally
        {
            _isAdditionalImagePaletteLoading = false;
            UpdateCanvasEditMode();
        }
    }

    private async Task RefreshConfiguredAdditionalPaletteAsync()
    {
        if (_isConfiguredAdditionalPaletteLoading)
        {
            _configuredAdditionalPaletteRefreshPending = true;
            return;
        }

        _isConfiguredAdditionalPaletteLoading = true;
        ConfiguredAdditionalPaletteStatusText.Text = "Cargando controles adicionales...";
        ConfiguredAdditionalControlColumn1.Children.Clear();
        ConfiguredAdditionalControlColumn2.Children.Clear();
        try
        {
            var configuration = await _additionalConfigurationService.GetConfigurationAsync();
            var itemsByControl = new Dictionary<string, AdditionalConfigurationPaletteItem>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var index in Enumerable.Range(1, 16))
            {
                var parameter = $"I{index}";
                if (!configuration.Selections.TryGetValue(parameter, out var value))
                {
                    continue;
                }

                var item = new AdditionalConfigurationPaletteItem(
                    $"ctlAdicionale{index}",
                    value);
                itemsByControl.Add(item.Control, item);
                var button = new Button
                {
                    Content = $"CtlAd{index}",
                    Tag = item,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                ToolTipService.SetToolTip(button, $"{item.Control} - {item.Text}");
                button.Click += AddConfiguredAdditionalControl_Click;
                var column = index % 2 == 1
                    ? ConfiguredAdditionalControlColumn1
                    : ConfiguredAdditionalControlColumn2;
                column.Children.Add(button);
            }

            var canvasChanged = false;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "CtlAdicional", StringComparison.OrdinalIgnoreCase)))
            {
                if (!itemsByControl.TryGetValue(control.AdditionalControl ?? string.Empty, out var item)
                    || string.Equals(control.Text, item.Text, StringComparison.Ordinal))
                {
                    continue;
                }

                control.Text = item.Text;
                SetCanvasValueCaption(control, item.Text);

                canvasChanged = true;
            }

            if (canvasChanged)
            {
                await SaveDesignerCanvasStateAsync();
            }

            ConfiguredAdditionalPaletteStatusText.Text =
                "Selecciona un control para agregarlo al lienzo.";
        }
        catch (SqlException exception)
        {
            ConfiguredAdditionalPaletteStatusText.Text =
                $"No se pudieron cargar los controles adicionales (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ConfiguredAdditionalPaletteStatusText.Text =
                $"No se pudieron cargar los controles adicionales: {exception.Message}";
        }
        finally
        {
            _isConfiguredAdditionalPaletteLoading = false;
            UpdateCanvasEditMode();
            if (_configuredAdditionalPaletteRefreshPending)
            {
                _configuredAdditionalPaletteRefreshPending = false;
                await RefreshConfiguredAdditionalPaletteAsync();
            }
        }
    }

    private async Task RefreshAggregatePaletteAsync()
    {
        if (_isAggregatePaletteLoading)
        {
            _aggregatePaletteRefreshPending = true;
            return;
        }

        _isAggregatePaletteLoading = true;
        AggregatePaletteStatusText.Text = "Cargando agregados...";
        AggregateControlColumn1.Children.Clear();
        try
        {
            var configuration = await _machineConfigurationService.GetConfigurationAsync();
            var options = configuration.MaterialOptions["AGREGADOS"];
            if (options.Count == 0)
            {
                throw new InvalidOperationException("dbo.Agregados no contiene opciones.");
            }

            var itemsByControl = new Dictionary<string, AggregateConfigurationPaletteItem>(
                StringComparer.OrdinalIgnoreCase);
            var totalItem = new AggregateConfigurationPaletteItem(
                "ctlAgregados",
                "Agregados",
                "AGREGADOS");
            itemsByControl.Add(totalItem.Control, totalItem);

            var totalButton = new Button
            {
                Content = totalItem.Caption,
                Tag = totalItem,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Style = (Style)Resources["DefaultButtonStyle"]
            };
            ToolTipService.SetToolTip(totalButton, $"{totalItem.Control} - {totalItem.Caption}");
            totalButton.Click += AddAggregateControl_Click;
            AggregateControlColumn1.Children.Add(totalButton);

            foreach (var index in Enumerable.Range(1, 8))
            {
                var parameter = $"A{index}";
                if (!configuration.Values.TryGetValue(parameter, out var selectedValue))
                {
                    throw new InvalidOperationException(
                        $"Falta el parámetro '{parameter}' en dbo.ConfigMaq.");
                }

                var material = options.FirstOrDefault(option =>
                    string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
                if (material is null)
                {
                    throw new InvalidOperationException(
                        $"El valor '{selectedValue}' de '{parameter}' no existe en dbo.Agregados.");
                }

                var item = new AggregateConfigurationPaletteItem(
                    $"ctlAgregado{index}",
                    parameter,
                    material);
                itemsByControl.Add(item.Control, item);

                var button = new Button
                {
                    Content = item.Caption,
                    Tag = item,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                ToolTipService.SetToolTip(button, $"{item.Control} - {item.Caption}");
                button.Click += AddAggregateControl_Click;
                AggregateControlColumn1.Children.Add(button);
            }

            var canvasChanged = false;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "CtlAgregado", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(control.Type, "CtlAgregados", StringComparison.OrdinalIgnoreCase)))
            {
                if (!itemsByControl.TryGetValue(control.AggregateControl ?? string.Empty, out var item))
                {
                    continue;
                }

                if (!string.Equals(control.Text, item.Caption, StringComparison.Ordinal)
                    || control.IsConfiguredDisabled != item.IsDisabled)
                {
                    control.Text = item.Caption;
                    control.IsConfiguredDisabled = item.IsDisabled;
                    SetCanvasValue(control, item.IsDisabled ? "—" : "????");
                    canvasChanged = true;
                }

                SetCanvasValueCaption(control, item.Caption);
            }

            if (canvasChanged)
            {
                await SaveDesignerCanvasStateAsync();
            }

            AggregatePaletteStatusText.Text = "Selecciona un agregado para agregarlo al lienzo.";
        }
        catch (SqlException exception)
        {
            AggregatePaletteStatusText.Text =
                $"No se pudieron cargar los agregados (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            AggregatePaletteStatusText.Text = $"No se pudieron cargar los agregados: {exception.Message}";
        }
        finally
        {
            _isAggregatePaletteLoading = false;
            UpdateCanvasEditMode();
            if (_aggregatePaletteRefreshPending)
            {
                _aggregatePaletteRefreshPending = false;
                await RefreshAggregatePaletteAsync();
            }
        }
    }

    private async void AddAggregateControl_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: AggregateConfigurationPaletteItem item })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.AggregateControl, item.Control, StringComparison.OrdinalIgnoreCase)))
        {
            AggregatePaletteStatusText.Text = $"{item.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = string.Equals(item.Control, "ctlAgregados", StringComparison.OrdinalIgnoreCase)
                ? "CtlAgregados"
                : "CtlAgregado",
            AggregateControl = item.Control,
            Text = item.Caption,
            IsConfiguredDisabled = item.IsDisabled,
            Width = 180,
            Height = 54,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - 180)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - 54))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async Task RefreshCementPaletteAsync()
    {
        if (_isCementPaletteLoading)
        {
            _cementPaletteRefreshPending = true;
            return;
        }

        _isCementPaletteLoading = true;
        CementPaletteStatusText.Text = "Cargando cementos...";
        CementControlColumn1.Children.Clear();
        try
        {
            var configuration = await _machineConfigurationService.GetConfigurationAsync();
            var options = configuration.MaterialOptions["CEMENTOS"];
            if (options.Count == 0)
            {
                throw new InvalidOperationException("dbo.Cementos no contiene opciones.");
            }

            var itemsByControl = new Dictionary<string, CementConfigurationPaletteItem>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var index in Enumerable.Range(1, 5))
            {
                var parameter = $"C{index}";
                if (!configuration.Values.TryGetValue(parameter, out var selectedValue))
                {
                    throw new InvalidOperationException(
                        $"Falta el parámetro '{parameter}' en dbo.ConfigMaq.");
                }

                var material = options.FirstOrDefault(option =>
                    string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
                if (material is null)
                {
                    throw new InvalidOperationException(
                        $"El valor '{selectedValue}' de '{parameter}' no existe en dbo.Cementos.");
                }

                var item = new CementConfigurationPaletteItem(
                    $"ctlCemento{index}",
                    parameter,
                    index == 1 ? "Principal" : $"Auxiliar {index - 1}",
                    material);
                itemsByControl.Add(item.Control, item);

                var button = new Button
                {
                    Content = $"{item.Label}: {material}",
                    Tag = item,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                ToolTipService.SetToolTip(button, $"{item.Control} - {item.Label} - {material}");
                button.Click += AddCementControl_Click;
                CementControlColumn1.Children.Add(button);
            }

            var canvasChanged = false;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "CtlCemento", StringComparison.OrdinalIgnoreCase)))
            {
                if (!itemsByControl.TryGetValue(control.CementControl ?? string.Empty, out var item))
                {
                    continue;
                }

                if (!string.Equals(control.Text, item.Caption, StringComparison.Ordinal)
                    || control.IsConfiguredDisabled != item.IsDisabled)
                {
                    control.Text = item.Caption;
                    control.IsConfiguredDisabled = item.IsDisabled;
                    SetCanvasValue(control, item.IsDisabled ? "—" : "????");
                    canvasChanged = true;
                }

                SetCanvasValueCaption(control, item.Caption);
            }

            if (canvasChanged)
            {
                await SaveDesignerCanvasStateAsync();
            }

            CementPaletteStatusText.Text = "Selecciona un cemento para agregarlo al lienzo.";
        }
        catch (SqlException exception)
        {
            CementPaletteStatusText.Text =
                $"No se pudieron cargar los cementos (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            CementPaletteStatusText.Text = $"No se pudieron cargar los cementos: {exception.Message}";
        }
        finally
        {
            _isCementPaletteLoading = false;
            UpdateCanvasEditMode();
            if (_cementPaletteRefreshPending)
            {
                _cementPaletteRefreshPending = false;
                await RefreshCementPaletteAsync();
            }
        }
    }

    private async void AddCementControl_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: CementConfigurationPaletteItem item })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.CementControl, item.Control, StringComparison.OrdinalIgnoreCase)))
        {
            CementPaletteStatusText.Text = $"{item.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "CtlCemento",
            CementControl = item.Control,
            Text = item.Caption,
            IsConfiguredDisabled = item.IsDisabled,
            Width = 180,
            Height = 54,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - 180)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - 54))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async Task RefreshAdditivePaletteAsync()
    {
        if (_isAdditivePaletteLoading)
        {
            _additivePaletteRefreshPending = true;
            return;
        }

        _isAdditivePaletteLoading = true;
        AdditivePaletteStatusText.Text = "Cargando aditivos...";
        AdditiveControlColumn1.Children.Clear();
        try
        {
            var configuration = await _machineConfigurationService.GetConfigurationAsync();
            var options = configuration.MaterialOptions["ADITIVOS"];
            if (options.Count == 0)
            {
                throw new InvalidOperationException("dbo.Aditivos no contiene opciones.");
            }

            var itemsByControl = new Dictionary<string, AdditiveConfigurationPaletteItem>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var index in Enumerable.Range(1, 6))
            {
                var parameter = $"D{index}";
                if (!configuration.Values.TryGetValue(parameter, out var selectedValue))
                {
                    throw new InvalidOperationException(
                        $"Falta el parámetro '{parameter}' en dbo.ConfigMaq.");
                }

                var material = options.FirstOrDefault(option =>
                    string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
                if (material is null)
                {
                    throw new InvalidOperationException(
                        $"El valor '{selectedValue}' de '{parameter}' no existe en dbo.Aditivos.");
                }

                var item = new AdditiveConfigurationPaletteItem(
                    $"ctlAditivo{index}",
                    parameter,
                    $"Aditivo {index}",
                    material);
                itemsByControl.Add(item.Control, item);

                var button = new Button
                {
                    Content = $"{item.Label}: {material}",
                    Tag = item,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                ToolTipService.SetToolTip(button, $"{item.Control} - {item.Label} - {material}");
                button.Click += AddAdditiveControl_Click;
                AdditiveControlColumn1.Children.Add(button);
            }

            var canvasChanged = false;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "CtlAditivo", StringComparison.OrdinalIgnoreCase)))
            {
                if (!itemsByControl.TryGetValue(control.AdditiveControl ?? string.Empty, out var item))
                {
                    continue;
                }

                if (!string.Equals(control.Text, item.Caption, StringComparison.Ordinal)
                    || control.IsConfiguredDisabled != item.IsDisabled)
                {
                    control.Text = item.Caption;
                    control.IsConfiguredDisabled = item.IsDisabled;
                    SetCanvasValue(control, item.IsDisabled
                        ? "—"
                        : "????");
                    canvasChanged = true;
                }

                SetCanvasValueCaption(control, item.Caption);
            }

            if (canvasChanged)
            {
                await SaveDesignerCanvasStateAsync();
            }

            AdditivePaletteStatusText.Text = "Selecciona un aditivo para agregarlo al lienzo.";
        }
        catch (SqlException exception)
        {
            AdditivePaletteStatusText.Text =
                $"No se pudieron cargar los aditivos (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            AdditivePaletteStatusText.Text = $"No se pudieron cargar los aditivos: {exception.Message}";
        }
        finally
        {
            _isAdditivePaletteLoading = false;
            UpdateCanvasEditMode();
            if (_additivePaletteRefreshPending)
            {
                _additivePaletteRefreshPending = false;
                await RefreshAdditivePaletteAsync();
            }
        }
    }

    private async void AddAdditiveControl_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: AdditiveConfigurationPaletteItem item })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.AdditiveControl, item.Control, StringComparison.OrdinalIgnoreCase)))
        {
            AdditivePaletteStatusText.Text = $"{item.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "CtlAditivo",
            AdditiveControl = item.Control,
            Text = item.Caption,
            IsConfiguredDisabled = item.IsDisabled,
            Width = 180,
            Height = 54,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - 180)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - 54))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async Task RefreshWaterPaletteAsync()
    {
        if (_isWaterPaletteLoading)
        {
            _waterPaletteRefreshPending = true;
            return;
        }

        _isWaterPaletteLoading = true;
        WaterPaletteStatusText.Text = "Cargando configuración de agua...";
        WaterControlColumn1.Children.Clear();
        try
        {
            var configuration = await _machineConfigurationService.GetConfigurationAsync();
            var options = configuration.MaterialOptions["AGUA"];
            if (options.Count == 0)
            {
                throw new InvalidOperationException("dbo.Agua no contiene opciones.");
            }

            var itemsByControl = new Dictionary<string, WaterConfigurationPaletteItem>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var index in Enumerable.Range(1, 3))
            {
                var parameter = $"G{index}";
                if (!configuration.Values.TryGetValue(parameter, out var selectedValue))
                {
                    throw new InvalidOperationException(
                        $"Falta el parámetro '{parameter}' en dbo.ConfigMaq.");
                }

                var material = options.FirstOrDefault(option =>
                    string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
                if (material is null)
                {
                    throw new InvalidOperationException(
                        $"El valor '{selectedValue}' de '{parameter}' no existe en dbo.Agua.");
                }

                var item = new WaterConfigurationPaletteItem(
                    index == 1 ? "ctlAgua" : $"ctlAgua{index}",
                    parameter,
                    $"Agua {index}",
                    material);
                itemsByControl.Add(item.Control, item);

                var button = new Button
                {
                    Content = $"{item.Label}: {material}",
                    Tag = item,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                ToolTipService.SetToolTip(button, $"{item.Control} - {item.Label} - {material}");
                button.Click += AddWaterControl_Click;
                WaterControlColumn1.Children.Add(button);
            }

            var canvasChanged = false;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "CtlAgua", StringComparison.OrdinalIgnoreCase)))
            {
                if (!itemsByControl.TryGetValue(control.WaterControl ?? string.Empty, out var item))
                {
                    continue;
                }

                if (!string.Equals(control.Text, item.Caption, StringComparison.Ordinal)
                    || control.IsConfiguredDisabled != item.IsDisabled)
                {
                    control.Text = item.Caption;
                    control.IsConfiguredDisabled = item.IsDisabled;
                    SetCanvasValue(control, item.IsDisabled ? "—" : "????");
                    canvasChanged = true;
                }

                SetCanvasValueCaption(control, item.Caption);
            }

            if (canvasChanged)
            {
                await SaveDesignerCanvasStateAsync();
            }

            WaterPaletteStatusText.Text = "Selecciona un control de agua para agregarlo al lienzo.";
        }
        catch (SqlException exception)
        {
            WaterPaletteStatusText.Text =
                $"No se pudo cargar la configuración de agua (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            WaterPaletteStatusText.Text = $"No se pudo cargar la configuración de agua: {exception.Message}";
        }
        finally
        {
            _isWaterPaletteLoading = false;
            UpdateCanvasEditMode();
            if (_waterPaletteRefreshPending)
            {
                _waterPaletteRefreshPending = false;
                await RefreshWaterPaletteAsync();
            }
        }
    }

    private async void AddWaterControl_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: WaterConfigurationPaletteItem item })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.WaterControl, item.Control, StringComparison.OrdinalIgnoreCase)))
        {
            WaterPaletteStatusText.Text = $"{item.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "CtlAgua",
            WaterControl = item.Control,
            Text = item.Caption,
            IsConfiguredDisabled = item.IsDisabled,
            Width = 180,
            Height = 54,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - 180)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - 54))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async Task RefreshFiberPaletteAsync()
    {
        if (_isFiberPaletteLoading)
        {
            _fiberPaletteRefreshPending = true;
            return;
        }

        _isFiberPaletteLoading = true;
        FiberPaletteStatusText.Text = "Cargando configuración de fibras...";
        FiberControlColumn1.Children.Clear();
        try
        {
            var configuration = await _machineConfigurationService.GetConfigurationAsync();
            var options = configuration.MaterialOptions["FIBRAS"];
            if (options.Count == 0)
            {
                throw new InvalidOperationException("dbo.Fibras no contiene opciones.");
            }

            var itemsByControl = new Dictionary<string, FiberConfigurationPaletteItem>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var index in Enumerable.Range(1, 3))
            {
                var parameter = $"FI{index}";
                if (!configuration.Values.TryGetValue(parameter, out var selectedValue))
                {
                    throw new InvalidOperationException(
                        $"Falta el parámetro '{parameter}' en dbo.ConfigMaq.");
                }

                var material = options.FirstOrDefault(option =>
                    string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
                if (material is null)
                {
                    throw new InvalidOperationException(
                        $"El valor '{selectedValue}' de '{parameter}' no existe en dbo.Fibras.");
                }

                var item = new FiberConfigurationPaletteItem(
                    $"ctlFibra{index}",
                    parameter,
                    $"Fibra {index}",
                    material);
                itemsByControl.Add(item.Control, item);

                var button = new Button
                {
                    Content = $"{item.Label}: {material}",
                    Tag = item,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                ToolTipService.SetToolTip(button, $"{item.Control} - {item.Label} - {material}");
                button.Click += AddFiberControl_Click;
                FiberControlColumn1.Children.Add(button);
            }

            var canvasChanged = false;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "CtlFibra", StringComparison.OrdinalIgnoreCase)))
            {
                if (!itemsByControl.TryGetValue(control.FiberControl ?? string.Empty, out var item))
                {
                    continue;
                }

                if (!string.Equals(control.Text, item.Caption, StringComparison.Ordinal)
                    || control.IsConfiguredDisabled != item.IsDisabled)
                {
                    control.Text = item.Caption;
                    control.IsConfiguredDisabled = item.IsDisabled;
                    SetCanvasValue(control, item.IsDisabled ? "—" : "????");
                    canvasChanged = true;
                }

                SetCanvasValueCaption(control, item.Caption);
            }

            if (canvasChanged)
            {
                await SaveDesignerCanvasStateAsync();
            }

            FiberPaletteStatusText.Text = "Selecciona una fibra para agregarla al lienzo.";
        }
        catch (SqlException exception)
        {
            FiberPaletteStatusText.Text =
                $"No se pudieron cargar las fibras (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            FiberPaletteStatusText.Text = $"No se pudieron cargar las fibras: {exception.Message}";
        }
        finally
        {
            _isFiberPaletteLoading = false;
            UpdateCanvasEditMode();
            if (_fiberPaletteRefreshPending)
            {
                _fiberPaletteRefreshPending = false;
                await RefreshFiberPaletteAsync();
            }
        }
    }

    private async void AddFiberControl_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: FiberConfigurationPaletteItem item })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.FiberControl, item.Control, StringComparison.OrdinalIgnoreCase)))
        {
            FiberPaletteStatusText.Text = $"{item.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "CtlFibra",
            FiberControl = item.Control,
            Text = item.Caption,
            IsConfiguredDisabled = item.IsDisabled,
            Width = 180,
            Height = 54,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - 180)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - 54))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async void AddConfiguredAdditionalControl_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: AdditionalConfigurationPaletteItem item })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.AdditionalControl, item.Control, StringComparison.OrdinalIgnoreCase)))
        {
            ConfiguredAdditionalPaletteStatusText.Text = $"{item.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "CtlAdicional",
            AdditionalControl = item.Control,
            Text = item.Text,
            Width = 180,
            Height = 54,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - 180)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - 54))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async Task<bool> LoadAdditionalImageConfigurationsAsync()
    {
        try
        {
            _additionalImageConfigurations =
                await _imageConfigurationService.GetAdditionalImageConfigurationsAsync();
            return true;
        }
        catch (SqlException exception)
        {
            AdditionalImagePaletteStatusText.Text =
                $"No se pudieron cargar las imágenes adicionales (SQL {exception.Number}): {exception.Message}";
            ModuleStatusText.Text = AdditionalImagePaletteStatusText.Text;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            AdditionalImagePaletteStatusText.Text =
                $"No se pudieron cargar las imágenes adicionales: {exception.Message}";
            ModuleStatusText.Text = AdditionalImagePaletteStatusText.Text;
            return false;
        }
    }

    private async Task RefreshPumpImagePaletteAsync()
    {
        PumpImagePaletteStatusText.Text = "Cargando bombas...";
        PumpImageControlPalette.Children.Clear();
        try
        {
            if (!await LoadPumpImageConfigurationsAsync())
            {
                return;
            }

            for (var index = 0; index < ImageConfigurationService.PumpControls.Count; index++)
            {
                var name = ImageConfigurationService.PumpControls[index];
                if (!_pumpImageConfigurations.TryGetValue(name, out var image))
                {
                    continue;
                }

                var button = new Button
                {
                    Content = name,
                    Tag = image,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                button.Click += AddConfiguredPumpImage_Click;
                PumpImageControlPalette.Children.Add(button);
            }

            var missingControls = ImageConfigurationService.PumpControls
                .Where(control => !_pumpImageConfigurations.ContainsKey(control))
                .ToArray();
            PumpImagePaletteStatusText.Text = missingControls.Length == 0
                ? "Selecciona una bomba para agregarla al lienzo."
                : $"No se encontraron en dbo.Imagenes: {string.Join(", ", missingControls)}.";

            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "ImagenBomba", StringComparison.OrdinalIgnoreCase)))
            {
                if (!_pumpImageConfigurations.TryGetValue(control.ImageControl ?? string.Empty, out var image))
                {
                    continue;
                }

                control.ImageOnPath = image.ImageOn;
                control.ImageOffPath = image.ImageOff;
                var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
                    candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
                if (container?.Child is Image canvasImage)
                {
                    await SetAdditionalImageSourceAsync(canvasImage, control);
                }
            }
        }
        finally
        {
            UpdateCanvasEditMode();
        }
    }

    private async Task<bool> LoadPumpImageConfigurationsAsync()
    {
        try
        {
            _pumpImageConfigurations = await _imageConfigurationService.GetPumpImageConfigurationsAsync();
            return true;
        }
        catch (SqlException exception)
        {
            PumpImagePaletteStatusText.Text =
                $"No se pudieron cargar las bombas (SQL {exception.Number}): {exception.Message}";
            ModuleStatusText.Text = PumpImagePaletteStatusText.Text;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            PumpImagePaletteStatusText.Text = $"No se pudieron cargar las bombas: {exception.Message}";
            ModuleStatusText.Text = PumpImagePaletteStatusText.Text;
            return false;
        }
    }

    private async Task RefreshValveImagePaletteAsync()
    {
        ValveImagePaletteStatusText.Text = "Cargando válvulas...";
        ValveImageControlColumn1.Children.Clear();
        ValveImageControlColumn2.Children.Clear();
        try
        {
            if (!await LoadValveImageConfigurationsAsync())
            {
                return;
            }

            for (var index = 0; index < ImageConfigurationService.ValveControls.Count; index++)
            {
                var name = ImageConfigurationService.ValveControls[index];
                if (!_valveImageConfigurations.TryGetValue(name, out var image))
                {
                    continue;
                }

                var button = new Button
                {
                    Content = name,
                    Tag = image,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Style = (Style)Resources["DefaultButtonStyle"]
                };
                button.Click += AddConfiguredValveImage_Click;
                var column = index % 2 == 0
                    ? ValveImageControlColumn1
                    : ValveImageControlColumn2;
                column.Children.Add(button);
            }

            var missingControls = ImageConfigurationService.ValveControls
                .Where(control => !_valveImageConfigurations.ContainsKey(control))
                .ToArray();
            ValveImagePaletteStatusText.Text = missingControls.Length == 0
                ? "Selecciona una válvula para agregarla al lienzo."
                : $"No se encontraron en dbo.Imagenes: {string.Join(", ", missingControls)}.";

            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.Type, "ImagenValvula", StringComparison.OrdinalIgnoreCase)))
            {
                if (!_valveImageConfigurations.TryGetValue(control.ImageControl ?? string.Empty, out var image))
                {
                    continue;
                }

                control.ImageOnPath = image.ImageOn;
                control.ImageOffPath = image.ImageOff;
                var container = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
                    candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
                if (container?.Child is Image canvasImage)
                {
                    await SetAdditionalImageSourceAsync(canvasImage, control);
                }
            }
        }
        finally
        {
            UpdateCanvasEditMode();
        }
    }

    private async Task<bool> LoadValveImageConfigurationsAsync()
    {
        try
        {
            _valveImageConfigurations = await _imageConfigurationService.GetValveImageConfigurationsAsync();
            return true;
        }
        catch (SqlException exception)
        {
            ValveImagePaletteStatusText.Text =
                $"No se pudieron cargar las válvulas (SQL {exception.Number}): {exception.Message}";
            ModuleStatusText.Text = ValveImagePaletteStatusText.Text;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            ValveImagePaletteStatusText.Text = $"No se pudieron cargar las válvulas: {exception.Message}";
            ModuleStatusText.Text = ValveImagePaletteStatusText.Text;
            return false;
        }
    }

    private async void AddConfiguredAdditionalImage_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: AdditionalImageConfigurationRecord image })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.ImageControl, image.Control, StringComparison.OrdinalIgnoreCase)))
        {
            AdditionalImagePaletteStatusText.Text = $"{image.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var width = image.Width is > 0 ? image.Width.Value : 40;
        var height = image.Height is > 0 ? image.Height.Value : 40;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "ImagenAdicional",
            ImageControl = image.Control,
            ImageOnPath = image.ImageOn,
            ImageOffPath = image.ImageOff,
            IsOn = false,
            Width = width,
            Height = height,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - width)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - height))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async void AddConfiguredPumpImage_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: AdditionalImageConfigurationRecord image })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.ImageControl, image.Control, StringComparison.OrdinalIgnoreCase)))
        {
            PumpImagePaletteStatusText.Text = $"{image.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var width = image.Width is > 0 ? image.Width.Value : 40;
        var height = image.Height is > 0 ? image.Height.Value : 40;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "ImagenBomba",
            ImageControl = image.Control,
            ImageOnPath = image.ImageOn,
            ImageOffPath = image.ImageOff,
            IsOn = false,
            Width = width,
            Height = height,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - width)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - height))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async void AddConfiguredValveImage_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Button { Tag: AdditionalImageConfigurationRecord image })
        {
            return;
        }

        if (_designerCanvasState.Controls.Any(control =>
                string.Equals(control.ImageControl, image.Control, StringComparison.OrdinalIgnoreCase)))
        {
            ValveImagePaletteStatusText.Text = $"{image.Control} ya está agregado al lienzo.";
            UpdateCanvasEditMode();
            return;
        }

        var index = _designerCanvasState.Controls.Count;
        var width = image.Width is > 0 ? image.Width.Value : 40;
        var height = image.Height is > 0 ? image.Height.Value : 40;
        var control = new DesignerCanvasControlState
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = "ImagenValvula",
            ImageControl = image.Control,
            ImageOnPath = image.ImageOn,
            ImageOffPath = image.ImageOff,
            IsOn = false,
            Width = width,
            Height = height,
            Left = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualWidth - width)),
            Top = Math.Clamp(24 + (index % 8) * 24, 0, Math.Max(0, DesignCanvas.ActualHeight - height))
        };
        _designerCanvasState.Controls.Add(control);
        await AddCanvasControlToSurfaceAsync(control);
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private string GetMessageText(MessageConfigurationRecord message)
    {
        if (_designerCanvasState.MessageTextOverrides.TryGetValue(message.Control, out var savedText))
        {
            return savedText;
        }

        return string.IsNullOrWhiteSpace(message.Text)
            ? GetDefaultMessageText(message.Control)
            : message.Text;
    }

    private async Task ApplySavedMessagesToCanvasAsync(
        IReadOnlyCollection<MessageConfigurationUpdate> updates)
    {
        foreach (var update in updates)
        {
            _designerCanvasState.MessageTextOverrides[update.Control] = update.Message;
            foreach (var control in _designerCanvasState.Controls.Where(control =>
                         string.Equals(control.MessageControl, update.Control, StringComparison.OrdinalIgnoreCase)))
            {
                control.Text = update.Message;
                control.Format = update.Format;
                if (DesignCanvas.Children.OfType<Border>().FirstOrDefault(container =>
                        ReferenceEquals(container.Tag, control)) is { Child: TextBlock messageLabel } container)
                {
                    messageLabel.Text = control.Text;
                    messageLabel.Foreground = GetMessageForeground(control);
                    messageLabel.FontSize = GetResponsiveMessageFontSize(
                        control,
                        messageLabel.Width,
                        messageLabel.Height);
                    var fontFamily = GetMessageFontFamily(control.Format);
                    if (fontFamily is null)
                    {
                        messageLabel.ClearValue(TextBlock.FontFamilyProperty);
                    }
                    else
                    {
                        messageLabel.FontFamily = fontFamily;
                    }
                }
            }

            foreach (var button in MessageControlPalette.Children.OfType<Button>())
            {
                if (button.Tag is MessageConfigurationRecord message
                    && string.Equals(message.Control, update.Control, StringComparison.OrdinalIgnoreCase))
                {
                    button.Tag = message with { Text = update.Message, Format = update.Format };
                }
            }
        }

        await SaveDesignerCanvasStateAsync();
    }

    private static string GetDefaultMessageText(string? control)
    {
        return string.IsNullOrWhiteSpace(control) ? "Mensaje" : control.Trim();
    }

    private async Task AddCanvasControlToSurfaceAsync(DesignerCanvasControlState control)
    {
        if (string.Equals(control.Type, "Mensaje", StringComparison.OrdinalIgnoreCase))
        {
            AddCanvasMessageToSurface(control);
            return;
        }

        if (string.Equals(control.Type, "CtlAdicional", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "CtlAgregado", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "CtlAgregados", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "CtlCemento", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "CtlAditivo", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "CtlAgua", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "CtlFibra", StringComparison.OrdinalIgnoreCase))
        {
            AddCanvasValueControlToSurface(control);
            return;
        }

        if (IsCanvasImageControl(control))
        {
            await AddCanvasAdditionalImageToSurfaceAsync(control);
            return;
        }

        var container = new Border
        {
            Width = control.Width ?? 190,
            Height = control.Height ?? double.NaN,
            Padding = new Thickness(6),
            Background = _themeBrushes.GetBrush("#E6111C2D"),
            BorderBrush = _themeBrushes.GetBrush("#0078D4"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Tag = control
        };
        var layout = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var moveHandle = new Button
        {
            Content = "↕",
            Padding = new Thickness(4, 0, 4, 0),
            Tag = container
        };
        ToolTipService.SetToolTip(moveHandle, "Arrastra para mover");
        moveHandle.PointerPressed += CanvasControlMoveHandle_PointerPressed;
        moveHandle.PointerMoved += CanvasControlMoveHandle_PointerMoved;
        moveHandle.PointerReleased += CanvasControlMoveHandle_PointerReleased;
        moveHandle.PointerCaptureLost += CanvasControlMoveHandle_PointerCaptureLost;
        var typeLabel = new TextBlock
        {
            Text = control.Type,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _themeBrushes.GetBrush("#A9B8CC")
        };
        Grid.SetColumn(typeLabel, 1);
        var removeButton = new Button
        {
            Content = "×",
            Tag = container,
            Padding = new Thickness(5, 0, 5, 0)
        };
        ToolTipService.SetToolTip(removeButton, "Quitar del lienzo");
        removeButton.Click += RemoveCanvasControl_Click;
        Grid.SetColumn(removeButton, 2);

        FrameworkElement visual;
        visual = control.Type switch
        {
            "Botón" => new Button { Content = control.Text, HorizontalAlignment = HorizontalAlignment.Stretch },
            "Interruptor" => CreateCanvasToggle(control),
            _ => new TextBlock
            {
                Text = control.Text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _themeBrushes.GetBrush("#F1F5F9")
            }
        };

        Grid.SetRow(visual, 1);
        Grid.SetColumnSpan(visual, 4);
        layout.Children.Add(moveHandle);
        layout.Children.Add(typeLabel);
        layout.Children.Add(removeButton);
        layout.Children.Add(visual);
        container.Child = layout;
        Canvas.SetLeft(container, control.Left);
        Canvas.SetTop(container, control.Top);
        DesignCanvas.Children.Add(container);
        container.IsHitTestVisible = !_isCanvasLocked;
    }

    private void AddCanvasValueControlToSurface(DesignerCanvasControlState control)
    {
        var width = control.Width ?? 180;
        var height = control.Height ?? 54;
        var caption = new TextBlock
        {
            Text = control.Text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = GetCanvasCaptionBrush(control)
        };
        var captionBackground = new Border
        {
            MaxWidth = width,
            Padding = new Thickness(5, 2, 5, 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(210, 0, 0, 0)),
            CornerRadius = new CornerRadius(3),
            Child = caption
        };
        var valueText = new TextBlock
        {
            Text = control.IsConfiguredDisabled
                ? "—"
                : "????",
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = _themeBrushes.GetBrush("#00FF00")
        };
        var valueBox = new Border
        {
            Width = Math.Min(80, width),
            MinHeight = 22,
            Padding = new Thickness(4, 0, 4, 0),
            Background = _themeBrushes.GetBrush("#000000"),
            BorderBrush = _themeBrushes.GetBrush("#E6EDF7"),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = valueText
        };
        var layout = new StackPanel
        {
            Width = width,
            Height = height,
            Spacing = 2,
            Children = { captionBackground, valueBox }
        };
        var container = new Border
        {
            Width = width,
            Height = height,
            Padding = new Thickness(0),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Tag = control,
            Child = layout
        };
        var contextMenu = new MenuFlyout();
        var colorItem = new MenuFlyoutItem { Text = "Cambiar color de etiqueta", Tag = control };
        colorItem.Click += CanvasCaptionColor_Click;
        contextMenu.Items.Add(colorItem);
        var removeItem = new MenuFlyoutItem { Text = "Quitar del lienzo", Tag = container };
        removeItem.Click += RemoveCanvasControl_Click;
        contextMenu.Items.Add(removeItem);
        container.ContextFlyout = contextMenu;
        container.PointerPressed += CanvasMessage_PointerPressed;
        container.PointerMoved += CanvasMessage_PointerMoved;
        container.PointerReleased += CanvasMessage_PointerReleased;
        container.PointerCaptureLost += CanvasMessage_PointerCaptureLost;
        Canvas.SetLeft(container, control.Left);
        Canvas.SetTop(container, control.Top);
        DesignCanvas.Children.Add(container);
        _canvasValueCaptions[control.Id] = caption;
        _canvasValueTexts[control.Id] = valueText;
        container.IsHitTestVisible = !_isCanvasLocked;
        RefreshDashboardMaterials();
    }

    private void SetCanvasValueCaption(DesignerCanvasControlState control, string text)
    {
        control.Text = text;
        if (_canvasValueCaptions.TryGetValue(control.Id, out var caption))
        {
            caption.Text = control.Text;
            caption.Foreground = GetCanvasCaptionBrush(control);
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush GetCanvasCaptionBrush(DesignerCanvasControlState control)
    {
        if (TryParseCanvasCaptionColor(control.CaptionColor, out var color))
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        }

        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
    }

    private static bool TryParseCanvasCaptionColor(string? value, out Windows.UI.Color color)
    {
        color = Microsoft.UI.Colors.White;
        if (value is not { Length: 7 } || value[0] != '#')
        {
            return false;
        }

        try
        {
            color = Microsoft.UI.ColorHelper.FromArgb(
                255,
                Convert.ToByte(value.Substring(1, 2), 16),
                Convert.ToByte(value.Substring(3, 2), 16),
                Convert.ToByte(value.Substring(5, 2), 16));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async void CanvasCaptionColor_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not MenuFlyoutItem { Tag: DesignerCanvasControlState control })
        {
            return;
        }

        if (!_canvasValueCaptions.TryGetValue(control.Id, out var caption))
        {
            return;
        }

        await ShowCanvasCaptionColorPickerAsync(control, caption);
    }

    private async void CanvasMessageColor_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not MenuFlyoutItem { Tag: DesignerCanvasControlState control })
        {
            return;
        }

        var message = DesignCanvas.Children.OfType<Border>().FirstOrDefault(candidate =>
            candidate.Tag is DesignerCanvasControlState state && state.Id == control.Id);
        if (message?.Child is not TextBlock textBlock)
        {
            return;
        }

        await ShowCanvasCaptionColorPickerAsync(control, textBlock);
    }

    private async Task ShowCanvasCaptionColorPickerAsync(
        DesignerCanvasControlState control,
        TextBlock caption)
    {
        if (!TryParseCanvasCaptionColor(control.CaptionColor, out var currentColor))
        {
            currentColor = (caption.Foreground as Microsoft.UI.Xaml.Media.SolidColorBrush)?.Color
                ?? Microsoft.UI.Colors.White;
        }

        var picker = new ColorPicker
        {
            Color = currentColor,
            IsAlphaEnabled = false,
            IsColorPreviewVisible = true,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        picker.ColorChanged += (_, args) =>
        {
            control.CaptionColor =
                $"#{args.NewColor.R:X2}{args.NewColor.G:X2}{args.NewColor.B:X2}";
            caption.Foreground = GetCanvasCaptionBrush(control);
        };

        var dialog = new ContentDialog
        {
            Title = "Color del texto",
            Content = picker,
            PrimaryButtonText = "Listo",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
        await SaveDesignerCanvasStateAsync();
    }

    private void SetCanvasValue(DesignerCanvasControlState control, string value)
    {
        if (_canvasValueTexts.TryGetValue(control.Id, out var valueText))
        {
            valueText.Text = value;
        }
    }

    private static bool IsCanvasImageControl(DesignerCanvasControlState control)
    {
        return string.Equals(control.Type, "ImagenAdicional", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "ImagenBomba", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "ImagenValvula", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetCanvasValueControlName(DesignerCanvasControlState control)
    {
        if (string.Equals(control.Type, "CtlAdicional", StringComparison.OrdinalIgnoreCase))
        {
            return control.AdditionalControl;
        }

        if (string.Equals(control.Type, "CtlAgregado", StringComparison.OrdinalIgnoreCase))
        {
            return control.IsConfiguredDisabled ? null : control.AggregateControl;
        }

        if (string.Equals(control.Type, "CtlAgregados", StringComparison.OrdinalIgnoreCase))
        {
            return "ctlAgregados";
        }

        if (string.Equals(control.Type, "CtlFibra", StringComparison.OrdinalIgnoreCase))
        {
            return control.IsConfiguredDisabled ? null : control.FiberControl;
        }

        return string.Equals(control.Type, "CtlCemento", StringComparison.OrdinalIgnoreCase)
            ? (control.IsConfiguredDisabled ? null : control.CementControl)
            : string.Equals(control.Type, "CtlAditivo", StringComparison.OrdinalIgnoreCase)
                ? (control.IsConfiguredDisabled ? null : control.AdditiveControl)
                : string.Equals(control.Type, "CtlAgua", StringComparison.OrdinalIgnoreCase)
                    ? (control.IsConfiguredDisabled ? null : control.WaterControl)
                    : null;
    }

    private static bool IsCanvasPlcImageControl(DesignerCanvasControlState control)
    {
        return string.Equals(control.Type, "ImagenAdicional", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "ImagenBomba", StringComparison.OrdinalIgnoreCase)
            || string.Equals(control.Type, "ImagenValvula", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AddCanvasAdditionalImageToSurfaceAsync(DesignerCanvasControlState control)
    {
        var image = new Image
        {
            Width = control.Width ?? 40,
            Height = control.Height ?? 40,
            Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform,
            IsHitTestVisible = false
        };
        var container = new Border
        {
            Width = image.Width,
            Height = image.Height,
            Padding = new Thickness(0),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Tag = control,
            Child = image
        };
        var contextMenu = new MenuFlyout();
        var removeItem = new MenuFlyoutItem { Text = "Quitar del lienzo", Tag = container };
        removeItem.Click += RemoveCanvasControl_Click;
        contextMenu.Items.Add(removeItem);
        container.ContextFlyout = contextMenu;
        container.PointerPressed += CanvasMessage_PointerPressed;
        container.PointerMoved += CanvasMessage_PointerMoved;
        container.PointerReleased += CanvasMessage_PointerReleased;
        container.PointerCaptureLost += CanvasMessage_PointerCaptureLost;
        Canvas.SetLeft(container, control.Left);
        Canvas.SetTop(container, control.Top);
        DesignCanvas.Children.Add(container);
        container.IsHitTestVisible = !_isCanvasLocked;
        await SetAdditionalImageSourceAsync(image, control);
    }

    private async Task SetAdditionalImageSourceAsync(
        Image image,
        DesignerCanvasControlState control,
        bool? isOnOverride = null)
    {
        var isOn = isOnOverride ?? control.IsOn;
        var path = isOn ? control.ImageOnPath : control.ImageOffPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            image.Source = null;
            ModuleStatusText.Text =
                $"No hay una ruta configurada para la imagen {(isOn ? "On" : "Off")} de {control.ImageControl}.";
            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using (var stream = await file.OpenReadAsync())
            {
                await bitmap.SetSourceAsync(stream);
            }

            image.Source = bitmap;
        }
        catch (COMException)
        {
            ModuleStatusText.Text = $"Windows no pudo abrir la imagen de {control.ImageControl}: {path}";
        }
        catch (FileNotFoundException)
        {
            ModuleStatusText.Text = $"No se encontró la imagen de {control.ImageControl}: {path}";
        }
        catch (DirectoryNotFoundException)
        {
            ModuleStatusText.Text = $"No se encontró la carpeta de la imagen de {control.ImageControl}: {path}";
        }
        catch (IOException exception)
        {
            ModuleStatusText.Text = $"No se pudo leer la imagen de {control.ImageControl}: {exception.Message}";
        }
        catch (UnauthorizedAccessException)
        {
            ModuleStatusText.Text = $"No hay permisos para leer la imagen de {control.ImageControl}: {path}";
        }
        catch (ArgumentException)
        {
            ModuleStatusText.Text = $"La ruta de la imagen de {control.ImageControl} no es válida: {path}";
        }
    }

    private void AddCanvasMessageToSurface(DesignerCanvasControlState control)
    {
        var messageLabel = new TextBlock
        {
            Text = control.Text,
            Width = control.Width ?? 260,
            Height = control.Height ?? 56,
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetMessageForeground(control),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        messageLabel.FontSize = GetResponsiveMessageFontSize(
            control,
            messageLabel.Width,
            messageLabel.Height);

        var fontFamily = GetMessageFontFamily(control.Format);
        if (fontFamily is not null)
        {
            messageLabel.FontFamily = fontFamily;
        }

        var container = new Border
        {
            Width = messageLabel.Width,
            Height = messageLabel.Height,
            Padding = new Thickness(5, 3, 5, 3),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(210, 0, 0, 0)),
            Tag = control,
            Child = messageLabel
        };
        var contextMenu = new MenuFlyout();
        var colorItem = new MenuFlyoutItem { Text = "Cambiar color del mensaje", Tag = control };
        colorItem.Click += CanvasMessageColor_Click;
        contextMenu.Items.Add(colorItem);
        var removeItem = new MenuFlyoutItem { Text = "Quitar del lienzo", Tag = container };
        removeItem.Click += RemoveCanvasControl_Click;
        contextMenu.Items.Add(removeItem);
        container.ContextFlyout = contextMenu;
        container.PointerPressed += CanvasMessage_PointerPressed;
        container.PointerMoved += CanvasMessage_PointerMoved;
        container.PointerReleased += CanvasMessage_PointerReleased;
        container.PointerCaptureLost += CanvasMessage_PointerCaptureLost;
        if (!string.IsNullOrWhiteSpace(control.MessageControl))
        {
            container.DoubleTapped += CanvasMessage_DoubleTapped;
        }
        Canvas.SetLeft(container, control.Left);
        Canvas.SetTop(container, control.Top);
        DesignCanvas.Children.Add(container);
        container.IsHitTestVisible = !_isCanvasLocked;
    }

    private void CanvasMessage_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Border container
            || container.Tag is not DesignerCanvasControlState control)
        {
            return;
        }

        if (!ReferenceEquals(_selectedCanvasMessage, container))
        {
            _selectedCanvasMessage = container;
            UpdateCanvasTransformHandles(container);
        }

        _movingCanvasControl = container;
        var canvasPosition = e.GetCurrentPoint(DesignCanvas).Position;
        _canvasControlPointerOffset = new Windows.Foundation.Point(
            canvasPosition.X - Canvas.GetLeft(container),
            canvasPosition.Y - Canvas.GetTop(container));
        container.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void DesignSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.OriginalSource is Microsoft.UI.Xaml.DependencyObject source
            && IsWithinElement(source, _selectedCanvasMessage))
        {
            return;
        }

        _selectedCanvasMessage = null;
        UpdateCanvasTransformHandles(null);
    }

    private static bool IsWithinElement(
        Microsoft.UI.Xaml.DependencyObject source,
        Microsoft.UI.Xaml.DependencyObject? target)
    {
        if (target is null)
        {
            return false;
        }

        for (var current = source; current is not null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, target))
            {
                return true;
            }
        }

        return false;
    }

    private void CanvasMessage_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked)
        {
            return;
        }

        if (_resizingCanvasControl is { } resizing)
        {
            var position = e.GetCurrentPoint(DesignCanvas).Position;
            ResizeCanvasMessage(resizing, position);
            e.Handled = true;
            return;
        }

        if (_movingCanvasControl is { } moving)
        {
            var position = e.GetCurrentPoint(DesignCanvas).Position;
            Canvas.SetLeft(moving, Math.Clamp(
                position.X - _canvasControlPointerOffset.X,
                0,
                Math.Max(0, DesignCanvas.ActualWidth - moving.ActualWidth)));
            Canvas.SetTop(moving, Math.Clamp(
                position.Y - _canvasControlPointerOffset.Y,
                0,
                Math.Max(0, DesignCanvas.ActualHeight - moving.ActualHeight)));
            if (ReferenceEquals(_selectedCanvasMessage, moving))
            {
                UpdateCanvasTransformHandles(moving);
            }

            e.Handled = true;
        }
    }

    private async void CanvasMessage_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        await SaveCanvasMessageGestureAsync();
        if (sender is Border container)
        {
            container.ReleasePointerCapture(e.Pointer);
        }

        _movingCanvasControl = null;
        _resizingCanvasControl = null;
        e.Handled = true;
    }

    private async void CanvasMessage_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        await SaveCanvasMessageGestureAsync();
        _movingCanvasControl = null;
        _resizingCanvasControl = null;
    }

    private async Task SaveCanvasMessageGestureAsync()
    {
        if (_movingCanvasControl?.Tag is DesignerCanvasControlState movingControl)
        {
            movingControl.Left = Canvas.GetLeft(_movingCanvasControl);
            movingControl.Top = Canvas.GetTop(_movingCanvasControl);
        }

        if (_resizingCanvasControl?.Tag is DesignerCanvasControlState resizingControl)
        {
            resizingControl.Width = _resizingCanvasControl.Width;
            resizingControl.Height = _resizingCanvasControl.Height;
            resizingControl.Left = Canvas.GetLeft(_resizingCanvasControl);
            resizingControl.Top = Canvas.GetTop(_resizingCanvasControl);
        }

        if (_movingCanvasControl is not null || _resizingCanvasControl is not null)
        {
            await SaveDesignerCanvasStateAsync();
        }
    }

    private void UpdateCanvasTransformHandles(Border? selectedMessage)
    {
        if (selectedMessage is not null && !_isCanvasLocked
            && _canvasTransformHandles.Count == 8
            && _canvasTransformHandles.All(handle =>
                handle.Tag is CanvasResizeHandleInfo handleInfo
                && ReferenceEquals(handleInfo.Container, selectedMessage)))
        {
            foreach (var handle in _canvasTransformHandles)
            {
                var handleInfo = (CanvasResizeHandleInfo)handle.Tag;
                var (horizontalAnchor, verticalAnchor) = GetResizeHandleAnchor(handleInfo.Edges);
                Canvas.SetLeft(
                    handle,
                    Canvas.GetLeft(selectedMessage) + selectedMessage.Width * horizontalAnchor - handle.Width / 2);
                Canvas.SetTop(
                    handle,
                    Canvas.GetTop(selectedMessage) + selectedMessage.Height * verticalAnchor - handle.Height / 2);
            }

            return;
        }

        foreach (var handle in _canvasTransformHandles)
        {
            DesignCanvas.Children.Remove(handle);
        }

        _canvasTransformHandles.Clear();
        if (selectedMessage is null || _isCanvasLocked
            || selectedMessage.Tag is not DesignerCanvasControlState)
        {
            return;
        }

        var handlePositions = new[]
        {
            (CanvasResizeEdges.Left | CanvasResizeEdges.Top, 0d, 0d),
            (CanvasResizeEdges.Top, 0.5d, 0d),
            (CanvasResizeEdges.Right | CanvasResizeEdges.Top, 1d, 0d),
            (CanvasResizeEdges.Right, 1d, 0.5d),
            (CanvasResizeEdges.Right | CanvasResizeEdges.Bottom, 1d, 1d),
            (CanvasResizeEdges.Bottom, 0.5d, 1d),
            (CanvasResizeEdges.Left | CanvasResizeEdges.Bottom, 0d, 1d),
            (CanvasResizeEdges.Left, 0d, 0.5d)
        };

        foreach (var (edges, horizontalAnchor, verticalAnchor) in handlePositions)
        {
            var handle = new Border
            {
                Width = 12,
                Height = 12,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                BorderBrush = _themeBrushes.GetBrush("#0078D4"),
                BorderThickness = new Thickness(2),
                Tag = new CanvasResizeHandleInfo(selectedMessage, edges)
            };
            handle.PointerPressed += CanvasTransformHandle_PointerPressed;
            handle.PointerMoved += CanvasTransformHandle_PointerMoved;
            handle.PointerReleased += CanvasTransformHandle_PointerReleased;
            handle.PointerCaptureLost += CanvasTransformHandle_PointerCaptureLost;
            handle.IsHitTestVisible = !_isCanvasLocked;
            Canvas.SetLeft(
                handle,
                Canvas.GetLeft(selectedMessage) + selectedMessage.Width * horizontalAnchor - handle.Width / 2);
            Canvas.SetTop(
                handle,
                Canvas.GetTop(selectedMessage) + selectedMessage.Height * verticalAnchor - handle.Height / 2);
            DesignCanvas.Children.Add(handle);
            _canvasTransformHandles.Add(handle);
        }
    }

    private static (double Horizontal, double Vertical) GetResizeHandleAnchor(CanvasResizeEdges edges)
    {
        var horizontal = edges.HasFlag(CanvasResizeEdges.Left)
            ? 0
            : edges.HasFlag(CanvasResizeEdges.Right) ? 1 : 0.5;
        var vertical = edges.HasFlag(CanvasResizeEdges.Top)
            ? 0
            : edges.HasFlag(CanvasResizeEdges.Bottom) ? 1 : 0.5;
        return (horizontal, vertical);
    }

    private void CanvasTransformHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Border { Tag: CanvasResizeHandleInfo resizeInfo } handle)
        {
            return;
        }

        _selectedCanvasMessage = resizeInfo.Container;
        _resizingCanvasControl = resizeInfo.Container;
        _activeCanvasResizeHandle = resizeInfo;
        _canvasControlResizeStart = e.GetCurrentPoint(DesignCanvas).Position;
        _canvasControlResizeStartPosition = new Windows.Foundation.Point(
            Canvas.GetLeft(resizeInfo.Container),
            Canvas.GetTop(resizeInfo.Container));
        _canvasControlResizeStartWidth = resizeInfo.Container.Width;
        _canvasControlResizeStartHeight = resizeInfo.Container.Height;
        handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void CanvasTransformHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked || _activeCanvasResizeHandle is null || _resizingCanvasControl is not { } resizing)
        {
            return;
        }

        ResizeCanvasMessage(resizing, e.GetCurrentPoint(DesignCanvas).Position);
        e.Handled = true;
    }

    private void ResizeCanvasMessage(Border message, Windows.Foundation.Point pointerPosition)
    {
        if (_activeCanvasResizeHandle is not { } resizeInfo)
        {
            return;
        }

        var deltaX = pointerPosition.X - _canvasControlResizeStart.X;
        var deltaY = pointerPosition.Y - _canvasControlResizeStart.Y;
        var left = _canvasControlResizeStartPosition.X;
        var top = _canvasControlResizeStartPosition.Y;
        var width = _canvasControlResizeStartWidth;
        var height = _canvasControlResizeStartHeight;

        if (resizeInfo.Edges.HasFlag(CanvasResizeEdges.Left))
        {
            left += deltaX;
            width -= deltaX;
        }
        else if (resizeInfo.Edges.HasFlag(CanvasResizeEdges.Right))
        {
            width += deltaX;
        }

        if (resizeInfo.Edges.HasFlag(CanvasResizeEdges.Top))
        {
            top += deltaY;
            height -= deltaY;
        }
        else if (resizeInfo.Edges.HasFlag(CanvasResizeEdges.Bottom))
        {
            height += deltaY;
        }

        var isConfiguredAdditional = message.Tag is DesignerCanvasControlState
        {
            Type: "CtlAdicional"
        };
        var minimumWidth = isConfiguredAdditional ? 80 : 40;
        var minimumHeight = isConfiguredAdditional ? 48 : 24;
        if (width < minimumWidth)
        {
            if (resizeInfo.Edges.HasFlag(CanvasResizeEdges.Left))
            {
                left -= minimumWidth - width;
            }

            width = minimumWidth;
        }

        if (height < minimumHeight)
        {
            if (resizeInfo.Edges.HasFlag(CanvasResizeEdges.Top))
            {
                top -= minimumHeight - height;
            }

            height = minimumHeight;
        }

        left = Math.Clamp(left, 0, Math.Max(0, DesignCanvas.ActualWidth - minimumWidth));
        top = Math.Clamp(top, 0, Math.Max(0, DesignCanvas.ActualHeight - minimumHeight));
        width = Math.Clamp(width, minimumWidth, Math.Max(minimumWidth, DesignCanvas.ActualWidth - left));
        height = Math.Clamp(height, minimumHeight, Math.Max(minimumHeight, DesignCanvas.ActualHeight - top));
        Canvas.SetLeft(message, left);
        Canvas.SetTop(message, top);
        message.Width = width;
        message.Height = height;
        if (message.Child is TextBlock messageLabel)
        {
            messageLabel.Width = width;
            messageLabel.Height = height;
            messageLabel.FontSize = GetResponsiveMessageFontSize(
                (DesignerCanvasControlState)message.Tag,
                width,
                height);
        }
        else if (message.Child is Image controlImage)
        {
            controlImage.Width = width;
            controlImage.Height = height;
        }
        else if (message.Child is StackPanel layout
                 && message.Tag is DesignerCanvasControlState { Type: "CtlAdicional" })
        {
            layout.Width = width;
            layout.Height = height;
            if (layout.Children.OfType<TextBlock>().FirstOrDefault() is { } caption)
            {
                caption.Width = width;
            }
        }

        UpdateCanvasTransformHandles(message);
    }

    private async void CanvasTransformHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        await SaveCanvasMessageGestureAsync();
        _activeCanvasResizeHandle = null;
        _resizingCanvasControl = null;
        if (sender is UIElement handle)
        {
            handle.ReleasePointerCapture(e.Pointer);
        }

        e.Handled = true;
    }

    private async void CanvasTransformHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        await SaveCanvasMessageGestureAsync();
        _activeCanvasResizeHandle = null;
        _resizingCanvasControl = null;
    }

    private async void CanvasMessage_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not Border { Tag: DesignerCanvasControlState control, Child: TextBlock messageLabel })
        {
            return;
        }

        var editor = new TextBox
        {
            Text = control.Text,
            MaxLength = 100,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap
        };
        var dialog = new ContentDialog
        {
            Title = control.MessageControl ?? "Mensaje",
            Content = editor,
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (!CanSaveConfigurationTab(7))
            {
                ModuleStatusText.Text = "Tu perfil no tiene permiso para editar los mensajes de la configuración.";
                return;
            }

            if (string.IsNullOrWhiteSpace(control.MessageControl))
            {
                ModuleStatusText.Text = "El mensaje seleccionado no está asociado a un control de dbo.Mensajes.";
                return;
            }

            try
            {
                var configuredMessages = await _messageConfigurationService.GetMessagesAsync();
                if (!configuredMessages.TryGetValue(control.MessageControl, out var configuredMessage))
                {
                    throw new InvalidOperationException(
                        $"No se encontró '{control.MessageControl}' en dbo.Mensajes.");
                }

                await _messageConfigurationService.SaveMessagesAsync(new[]
                {
                    new MessageConfigurationUpdate(
                        configuredMessage.IdMensaje,
                        configuredMessage.Control,
                        editor.Text,
                        configuredMessage.Format)
                });

                control.Text = editor.Text;
                _designerCanvasState.MessageTextOverrides[control.MessageControl] = editor.Text;
                messageLabel.Text = editor.Text;
                ConfigurationView.ReflectMessageText(control.MessageControl, editor.Text);
                ModuleStatusText.Text = $"{control.MessageControl} guardado en dbo.Mensajes.";
                await SaveDesignerCanvasStateAsync();
            }
            catch (SqlException exception)
            {
                ModuleStatusText.Text =
                    $"No se pudo guardar {control.MessageControl} en dbo.Mensajes (SQL {exception.Number}): {exception.Message}";
            }
            catch (InvalidOperationException exception)
            {
                ModuleStatusText.Text = $"No se pudo guardar el mensaje: {exception.Message}";
            }
        }

        e.Handled = true;
    }

    private FrameworkElement CreateCanvasToggle(DesignerCanvasControlState control)
    {
        var toggle = new ToggleSwitch
        {
            Header = control.Text,
            IsOn = control.IsOn,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        toggle.Toggled += async (_, _) =>
        {
            if (_isCanvasLocked)
            {
                return;
            }

            control.IsOn = toggle.IsOn;
            await SaveDesignerCanvasStateAsync();
        };
        return toggle;
    }

    private void CanvasControlMoveHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not FrameworkElement { Tag: Border container } handle)
        {
            return;
        }

        _movingCanvasControl = container;
        var pointerPosition = e.GetCurrentPoint(DesignCanvas).Position;
        _canvasControlPointerOffset = new Windows.Foundation.Point(
            pointerPosition.X - Canvas.GetLeft(container),
            pointerPosition.Y - Canvas.GetTop(container));
        handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void CanvasControlMoveHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked || _movingCanvasControl is not { } container)
        {
            return;
        }

        var position = e.GetCurrentPoint(DesignCanvas).Position;
        var left = Math.Clamp(position.X - _canvasControlPointerOffset.X, 0, Math.Max(0, DesignCanvas.ActualWidth - container.ActualWidth));
        var top = Math.Clamp(position.Y - _canvasControlPointerOffset.Y, 0, Math.Max(0, DesignCanvas.ActualHeight - container.ActualHeight));
        Canvas.SetLeft(container, left);
        Canvas.SetTop(container, top);
        e.Handled = true;
    }

    private async void CanvasControlMoveHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var movingControl = _movingCanvasControl;
        if (movingControl?.Tag is DesignerCanvasControlState control)
        {
            control.Left = Canvas.GetLeft(movingControl);
            control.Top = Canvas.GetTop(movingControl);
            await SaveDesignerCanvasStateAsync();
        }

        _movingCanvasControl = null;
        if (sender is UIElement handle)
        {
            handle.ReleasePointerCapture(e.Pointer);
        }

        e.Handled = true;
    }

    private async void CanvasControlMoveHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_movingCanvasControl?.Tag is DesignerCanvasControlState control)
        {
            control.Left = Canvas.GetLeft(_movingCanvasControl);
            control.Top = Canvas.GetTop(_movingCanvasControl);
            await SaveDesignerCanvasStateAsync();
        }

        _movingCanvasControl = null;
    }

    private async void RemoveCanvasControl_Click(object sender, RoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not FrameworkElement { Tag: Border container })
        {
            return;
        }

        if (container.Tag is DesignerCanvasControlState control)
        {
            _designerCanvasState.Controls.RemoveAll(item => item.Id == control.Id);
            _canvasValueCaptions.Remove(control.Id);
            _canvasValueTexts.Remove(control.Id);
            DesignCanvas.Children.Remove(container);
            if (ReferenceEquals(_selectedCanvasMessage, container))
            {
                _selectedCanvasMessage = null;
                UpdateCanvasTransformHandles(null);
            }

            RefreshDashboardMaterials();
            UpdateCanvasEditMode();
            await SaveDesignerCanvasStateAsync();
        }
    }

    private void CanvasControlResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked || sender is not FrameworkElement handle
            || handle.Tag is not Border container
            || container.Tag is not DesignerCanvasControlState control)
        {
            return;
        }

        _resizingCanvasControl = container;
        _canvasControlResizeStart = e.GetCurrentPoint(DesignCanvas).Position;
        _canvasControlResizeStartWidth = control.Width ?? container.ActualWidth;
        _canvasControlResizeStartHeight = control.Height ?? container.ActualHeight;
        handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void CanvasControlResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isCanvasLocked || _resizingCanvasControl is not { } container)
        {
            return;
        }

        var position = e.GetCurrentPoint(DesignCanvas).Position;
        var maxWidth = Math.Max(120, DesignCanvas.ActualWidth - Canvas.GetLeft(container));
        var maxHeight = Math.Max(36, DesignCanvas.ActualHeight - Canvas.GetTop(container));
        container.Width = Math.Clamp(_canvasControlResizeStartWidth + position.X - _canvasControlResizeStart.X, 120, maxWidth);
        container.Height = Math.Clamp(_canvasControlResizeStartHeight + position.Y - _canvasControlResizeStart.Y, 36, maxHeight);
        e.Handled = true;
    }

    private async void CanvasControlResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        await SaveResizedCanvasControlAsync();
        _resizingCanvasControl = null;
        if (sender is UIElement handle)
        {
            handle.ReleasePointerCapture(e.Pointer);
        }

        e.Handled = true;
    }

    private async void CanvasControlResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        await SaveResizedCanvasControlAsync();
        _resizingCanvasControl = null;
    }

    private async Task SaveResizedCanvasControlAsync()
    {
        if (_resizingCanvasControl?.Tag is DesignerCanvasControlState control)
        {
            control.Width = _resizingCanvasControl.Width;
            control.Height = _resizingCanvasControl.Height;
            await SaveDesignerCanvasStateAsync();
        }
    }

    private static double? GetMessageFontSize(string format)
    {
        foreach (var part in format.Split(',', StringSplitOptions.TrimEntries))
        {
            if (part.EndsWith("pt", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(part[..^2], NumberStyles.Number, CultureInfo.InvariantCulture, out var points)
                && points is >= 6 and <= 72)
            {
                return points * 4 / 3;
            }
        }

        return null;
    }

    private static double GetResponsiveMessageFontSize(
        DesignerCanvasControlState control,
        double width,
        double height)
    {
        var baseFontSize = GetMessageFontSize(control.Format) ?? 18;
        var scale = Math.Sqrt((width / 260) * (height / 56));
        return Math.Clamp(baseFontSize * scale, 8, 96);
    }

    [Flags]
    private enum CanvasResizeEdges
    {
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }

    private sealed record CanvasResizeHandleInfo(Border Container, CanvasResizeEdges Edges);
    private sealed record AdditionalConfigurationPaletteItem(string Control, string Text);
    private sealed record AggregateConfigurationPaletteItem(string Control, string Parameter, string Material)
    {
        public string Caption => Material;
        public bool IsDisabled => Material.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase);
    }
    private sealed record CementConfigurationPaletteItem(string Control, string Parameter, string Label, string Material)
    {
        public string Caption => Material;
        public bool IsDisabled => Material.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase);
    }
    private sealed record AdditiveConfigurationPaletteItem(string Control, string Parameter, string Label, string Material)
    {
        public string Caption => Material;
        public bool IsDisabled => Material.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase);
    }
    private sealed record WaterConfigurationPaletteItem(string Control, string Parameter, string Label, string Material)
    {
        public string Caption => Material;
        public bool IsDisabled => Material.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase);
    }
    private sealed record FiberConfigurationPaletteItem(string Control, string Parameter, string Label, string Material)
    {
        public string Caption => Material;
        public bool IsDisabled => Material.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase);
    }

    private static Microsoft.UI.Xaml.Media.FontFamily? GetMessageFontFamily(string format)
    {
        var fontFamily = format.Split(',', StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(fontFamily)
            ? null
            : new Microsoft.UI.Xaml.Media.FontFamily(fontFamily);
    }

    private Microsoft.UI.Xaml.Media.Brush GetMessageForeground(DesignerCanvasControlState control)
    {
        if (TryParseCanvasCaptionColor(control.CaptionColor, out var customColor))
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(customColor);
        }

        var colorName = control.Format.Split(',', StringSplitOptions.TrimEntries)
            .Select(part => part.Split('-', StringSplitOptions.TrimEntries).Last())
            .LastOrDefault(part => part is "Red" or "Blue" or "Green" or "Yellow" or "White" or "Black");
        var color = colorName switch
        {
            "Red" => Microsoft.UI.Colors.Red,
            "Blue" => Microsoft.UI.Colors.DeepSkyBlue,
            "Green" => Microsoft.UI.Colors.LimeGreen,
            "Yellow" => Microsoft.UI.Colors.Yellow,
            "Black" => Microsoft.UI.Colors.Black,
            _ => Microsoft.UI.Colors.White
        };
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
    }

    private async Task SaveDesignerCanvasStateAsync()
    {
        try
        {
            await _designerCanvasStore.SaveAsync(_designerCanvasState);
        }
        catch (IOException exception)
        {
            ModuleStatusText.Text = $"No se pudo guardar el diseño del lienzo: {exception.Message}";
        }
        catch (UnauthorizedAccessException exception)
        {
            ModuleStatusText.Text = $"No hay permisos para guardar el diseño del lienzo: {exception.Message}";
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var username = UsernameTextBox.Text.Trim();
        var password = PasswordInput.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            ShowLoginError("Ingresa el usuario y la contraseña.");
            return;
        }

        LoginButton.IsEnabled = false;
        LoginProgress.IsActive = true;
        LoginProgress.Visibility = Visibility.Visible;
        LoginErrorInfoBar.IsOpen = false;

        try
        {
            var user = await _authenticationService.AuthenticateAsync(username, password);
            if (user is null)
            {
                ShowLoginError("Usuario o contraseña no válidos.");
                return;
            }

            CurrentUserText.Text = $"USUARIO: {user.Username}";
            CurrentRoleText.Text = $"TIPO: {user.Role}";
            _authenticatedUserId = user.UserId;
            _authenticatedUsername = user.Username;
            _authenticatedName = user.Name;
            _accessLogRecordId = user.AccessRecordId;
            LogoutButton.Visibility = Visibility.Visible;
            SetSystemStatus("Sesión iniciada • verificando configuración...", "#FBBF24");
            await RefreshCurrentUserPermissionsAsync();
            LoginShell.Visibility = Visibility.Collapsed;
            DashboardShell.Visibility = Visibility.Visible;
            await LoadPlantNameAsync();
            await RefreshMaterialControlPalettesAsync();
            _plcStatusTimer.Start();
            await RefreshPlcStatusAsync();
        }
        catch (SqlException exception)
        {
            ShowLoginError($"No se pudo validar el acceso o registrar la entrada en BlapaciSQL (SQL {exception.Number}): {exception.Message}");
        }
        catch (AccessLogException exception)
        {
            ShowLoginError(exception.Message);
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginProgress.IsActive = false;
            LoginProgress.Visibility = Visibility.Collapsed;
        }
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        LogoutButton.IsEnabled = false;
        try
        {
            await CompleteAccessSessionAsync();
            _authenticatedUserId = null;
            _authenticatedUsername = string.Empty;
            _authenticatedName = null;
            _currentUserPermissions = Array.Empty<AccessPermissionEntry>();
            UsernameTextBox.Text = string.Empty;
            PasswordInput.Password = string.Empty;
            CurrentUserText.Text = "USUARIO:";
            CurrentRoleText.Text = "TIPO:";
            LogoutButton.Visibility = Visibility.Collapsed;
            DashboardShell.Visibility = Visibility.Collapsed;
            LoginShell.Visibility = Visibility.Visible;
            _plcStatusTimer.Stop();
            SetSystemStatus("Esperando inicio de sesión", "#FBBF24");
        }
        catch (SqlException exception)
        {
            await ShowAccessLogErrorAsync(
                "No se pudo cerrar la sesión",
                $"No se pudo registrar la salida en BlapaciSQL (SQL {exception.Number}): {exception.Message}");
        }
        catch (AccessLogException exception)
        {
            await ShowAccessLogErrorAsync("No se pudo cerrar la sesión", exception.Message);
        }
        finally
        {
            LogoutButton.IsEnabled = true;
        }
    }

    public async Task CompleteAccessSessionAsync()
    {
        await _accessSessionEndLock.WaitAsync();
        try
        {
            if (_accessLogRecordId is not int accessRecordId)
            {
                return;
            }

            await _authenticationService.RecordLogoutAsync(
                _authenticatedUsername,
                _authenticatedName,
                accessRecordId);
            _accessLogRecordId = null;
        }
        finally
        {
            _accessSessionEndLock.Release();
        }
    }

    public async Task<bool> ConfirmCloseWithoutAccessLogAsync(Exception exception)
    {
        var dialog = new ContentDialog
        {
            Title = "No se pudo registrar la salida",
            Content = $"La sesión quedará sin hora de salida. {exception.Message}",
            PrimaryButtonText = "Cerrar sin registrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowAccessLogErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "Aceptar",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private void LoginInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && LoginButton.IsEnabled)
        {
            e.Handled = true;
            LoginButton_Click(LoginButton, new RoutedEventArgs());
        }
    }

    private async void MainPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Delete or Windows.System.VirtualKey.Back)
        {
            if (_isCanvasLocked
                || _selectedCanvasMessage is null
                || IsTextInputSource(e.OriginalSource))
            {
                return;
            }

            e.Handled = true;
            await RemoveSelectedCanvasMessageAsync();
            return;
        }

        if (e.Key != Windows.System.VirtualKey.Escape)
        {
            return;
        }

        e.Handled = true;
        if (Application.Current is App { MainWindow: { } window })
        {
            window.Close();
        }
    }

    private static bool IsTextInputSource(object? source)
    {
        var current = source as Microsoft.UI.Xaml.DependencyObject;
        while (current is not null)
        {
            if (current is TextBox or PasswordBox or RichEditBox)
            {
                return true;
            }

            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private async Task RemoveSelectedCanvasMessageAsync()
    {
        if (_isCanvasLocked || _selectedCanvasMessage is not { } selectedMessage
            || selectedMessage.Tag is not DesignerCanvasControlState control)
        {
            return;
        }

        _designerCanvasState.Controls.RemoveAll(item => item.Id == control.Id);
        _canvasValueCaptions.Remove(control.Id);
        _canvasValueTexts.Remove(control.Id);
        DesignCanvas.Children.Remove(selectedMessage);
        _selectedCanvasMessage = null;
        UpdateCanvasTransformHandles(null);
        RefreshDashboardMaterials();
        UpdateCanvasEditMode();
        await SaveDesignerCanvasStateAsync();
    }

    private async Task LoadPlantNameAsync()
    {
        PlantNameTextBlock.Text = "Cargando planta...";
        MachineNameTextBlock.Text = "Cargando máquina...";
        var errors = new List<string>();

        try
        {
            var plantName = await _companyConfigurationService.GetPlantNameAsync();
            if (string.IsNullOrWhiteSpace(plantName))
            {
                PlantNameTextBlock.Text = "Planta no configurada";
                errors.Add("No se encontró un valor para Param = 'ptl' en dbo.Cia_Datos.");
            }
            else
            {
                PlantNameTextBlock.Text = plantName;
            }
        }
        catch (SqlException exception)
        {
            PlantNameTextBlock.Text = "Planta no disponible";
            errors.Add($"No se pudo cargar la planta desde dbo.Cia_Datos (SQL {exception.Number}): {exception.Message}");
        }

        try
        {
            var machineName = await _companyConfigurationService.GetMachineNameAsync();
            MachineNameTextBlock.Text = string.IsNullOrWhiteSpace(machineName) ? "Máquina no configurada" : machineName;
            if (string.IsNullOrWhiteSpace(machineName))
            {
                errors.Add("No se encontró un valor para Param = 'Mq' en dbo.ConfigMaq.");
            }
        }
        catch (SqlException exception)
        {
            MachineNameTextBlock.Text = "Máquina no disponible";
            errors.Add($"No se pudo cargar la máquina desde dbo.ConfigMaq (SQL {exception.Number}): {exception.Message}");
        }

        try
        {
            var machineConfiguration = await _machineConfigurationService.GetConfigurationAsync();
            ApplyMachineAdjustmentValues(machineConfiguration.Values);
            LoadDashboardMaterials(machineConfiguration.Values);
            ApplyMachineDashboardOptions(new MachineDashboardOptions(
                machineConfiguration.Values.GetValueOrDefault("MU") == "1",
                machineConfiguration.Values.GetValueOrDefault("OS") == "1",
                machineConfiguration.Values.GetValueOrDefault("DC") == "1"));
        }
        catch (SqlException exception)
        {
            errors.Add($"No se pudieron cargar los materiales y ajustes del tablero desde BlapaciSQL (SQL {exception.Number}): {exception.Message}");
        }

        if (errors.Count == 0)
        {
            SetSystemStatus("Sesión iniciada • PLC: estado no comprobado", "#FBBF24");
        }
        else
        {
            SetSystemStatus("Sesión iniciada • configuración incompleta • PLC: estado no comprobado", "#FBBF24");
            ModuleStatusText.Text = string.Join(Environment.NewLine, errors);
        }
    }

    private void SetSystemStatus(string message, string color, string? detail = null)
    {
        SystemStatusText.Text = message;
        SystemStatusText.Foreground = _themeBrushes.GetBrush(color);
        ToolTipService.SetToolTip(SystemStatusText, detail ?? message);
    }

    private void ShowLoginError(string message)
    {
        SetFormStatus(LoginErrorInfoBar, message, InfoBarSeverity.Error);
    }

    private async Task<bool> RefreshCurrentUserPermissionsAsync()
    {
        if (_authenticatedUserId is not int userId)
        {
            _currentUserPermissions = Array.Empty<AccessPermissionEntry>();
            ApplyProfilePermissions();
            return false;
        }

        try
        {
            _currentUserPermissions = await _accessConfigurationService.GetUserPermissionsAsync(userId);
            ApplyProfilePermissions();
            return true;
        }
        catch (SqlException exception)
        {
            _currentUserPermissions = Array.Empty<AccessPermissionEntry>();
            ApplyProfilePermissions();
            ModuleStatusText.Text =
                $"No se pudieron cargar los permisos del usuario (SQL {exception.Number}): {exception.Message}";
            return false;
        }
    }

    private bool HasModulePermission(string module) =>
        AccessPermissionLookup.IsModuleEnabled(_currentUserPermissions, module);

    private bool HasNavigationPermission(string navigationTag, string permissionModule) =>
        HasModulePermission(permissionModule)
        && (!IsReportNavigation(navigationTag)
            || HasActionPermission(permissionModule, "Visualizar"));

    private static bool IsReportNavigation(string navigationTag) =>
        navigationTag.StartsWith("Reporte", StringComparison.OrdinalIgnoreCase)
        || navigationTag is "ReportCenter"
            or "Remisión anterior"
            or "Remisión de salida"
            or "Relación de producción";

    private bool HasActionPermission(string module, string action) =>
        AccessPermissionLookup.IsActionEnabled(_currentUserPermissions, module, action);

    private bool RequireActionPermission(string module, string action)
    {
        if (HasActionPermission(module, action))
        {
            return true;
        }

        ModuleStatusText.Text = $"Tu perfil no tiene permiso de {action} en {module}.";
        return false;
    }

    private static bool TryGetPermissionModule(string navigationTag, out string permissionModule)
    {
        permissionModule = navigationTag switch
        {
            "ReportCenter" => "Reportes",
            "Reporte de producción general" => "Produccion General",
            "Reporte de producción real" => "Produccion Real",
            "Reporte de entradas de material" => "Entradas Material",
            "Reporte de salidas de material" => "Salidas Material",
            "Reporte de existencias" => "ExistenciasR",
            "Reporte general de fórmulas" => "FormulasR",
            "Reporte general de clientes" => "ClientesR",
            "Reporte general de proveedor" => "ProveedoresR",
            "Remisión anterior" => "Remision Anterior",
            "Remisión de salida" => "Remision Salida",
            "Reporte de pedidos" => "PedidosR",
            "Relación de producción" => "Relacion",
            "Config:Machine" => "Maquina",
            "Config:PLC" => "PLC",
            "Config:Company" => "Datos Compañia",
            "Config:Remission" => "Ajuste Remision",
            "Config:ImagesOn" or "Config:ImagesOff" or "Config:ImageSize" or "Config:Messages" => "General",
            "Config:Additional" => "Adicionales",
            "Config:Orders" => "PedidosC",
            "Config:BackupReports" => "Respaldar Reportes",
            "Config:BackupDatabase" => "Respaldar DB",
            "Config:Access" => "Accesos",
            _ => navigationTag
        };

        return navigationTag is "Reporte de producción general"
            or "ReportCenter"
            or "Reporte de producción real"
            or "Reporte de entradas de material"
            or "Reporte de salidas de material"
            or "Reporte de existencias"
            or "Reporte general de fórmulas"
            or "Reporte general de clientes"
            or "Reporte general de proveedor"
            or "Remisión anterior"
            or "Remisión de salida"
            or "Reporte de pedidos"
            or "Relación de producción"
            or "Config:Machine"
            or "Config:PLC"
            or "Config:Company"
            or "Config:Remission"
            or "Config:ImagesOn"
            or "Config:ImagesOff"
            or "Config:ImageSize"
            or "Config:Messages"
            or "Config:Additional"
            or "Config:Orders"
            or "Config:BackupReports"
            or "Config:BackupDatabase"
            or "Config:Access"
            or "Seguridad"
            or "Carga Manual"
            or "Entradas"
            or "Salidas"
            or "Fórmulas"
            or "Proveedores"
            or "Clientes"
            or "Camiones"
            or "Pedidos";
    }

    private void ApplyProfilePermissions()
    {
        ProgramActionButton.IsEnabled =
            HasModulePermission("Carga Manual")
            && HasActionPermission("Carga Manual", "Alta")
            && (_simulatedProcessState is SimulatedProcessState.Idle or SimulatedProcessState.Complete);

        foreach (var menu in MainMenuBar.Items.OfType<MenuBarItem>())
        {
            var hasProfileItems = false;
            var hasEnabledProfileItem = false;
            foreach (var menuItem in menu.Items.OfType<Control>())
            {
                if (menuItem.Tag is not string tag || !TryGetPermissionModule(tag, out var permissionModule))
                {
                    continue;
                }

                var enabled = HasNavigationPermission(tag, permissionModule);
                menuItem.IsEnabled = enabled;
                hasProfileItems = true;
                hasEnabledProfileItem |= enabled;
            }

            if (hasProfileItems)
            {
                menu.IsEnabled = menu.Title == "Reportes"
                    ? HasModulePermission("Reportes")
                    : hasEnabledProfileItem;
            }
        }

        InventorySaveButton.IsEnabled =
            (InventoryEntriesCheckBox.IsChecked == true && HasActionPermission("Entradas", "Alta"))
            || (InventoryExitsCheckBox.IsChecked == true && HasActionPermission("Salidas", "Alta"));
        UpdateOrderActionButtons();
        UpdateFormulaActionButtons();
        UpdateSupplierActionButtons();
        UpdateClientActionButtons();
        UpdateTruckActionButtons();
        ManualLoadMenuItem.IsEnabled = HasModulePermission("Carga Manual");
        SecurityMenuItem.IsEnabled = HasModulePermission("Seguridad");
        MachineConfigurationView.RefreshPermissions();
        ConfigurationView.RefreshProfilePermissions();
    }

    private bool CanSaveConfigurationTab(int tab) => tab switch
    {
        1 => true,
        2 => HasActionPermission("Datos Compañia", "Editar")
            || HasActionPermission("Datos Compañia", "Alta"),
        3 => HasModulePermission("Ajuste Remision")
            && HasActionPermission("Datos Remision", "Editar")
            || HasModulePermission("Ajuste Remision")
            && HasActionPermission("Datos Remision", "Alta"),
        4 or 5 or 6 or 7 => HasActionPermission("General", "Editar")
            || HasActionPermission("General", "Alta"),
        8 => HasActionPermission("Adicionales", "Editar")
            || HasActionPermission("Adicionales", "Alta"),
        9 => HasModulePermission("PedidosC")
            && ((HasModulePermission("Configurar Pedido")
                    && (HasActionPermission("Configurar Pedido", "Editar")
                        || HasActionPermission("Configurar Pedido", "Alta")))
                || (HasModulePermission("Turnos")
                    && (HasActionPermission("Turnos", "Editar")
                        || HasActionPermission("Turnos", "Alta")))),
        10 => HasActionPermission("Accesos", "Alta")
            || HasActionPermission("Accesos", "Editar"),
        _ => false
    };

    private void UpdateFormulaActionButtons()
    {
        var hasSelection = _selectedFormula is not null;
        FormulaAddRestoreButton.IsEnabled = _showingCancelledFormulas
            ? hasSelection && HasActionPermission("Fórmulas", "Alta")
            : HasActionPermission("Fórmulas", "Alta");
        FormulaEditButton.IsEnabled = hasSelection && !_showingCancelledFormulas
            && HasActionPermission("Fórmulas", "Editar");
        FormulaDeleteButton.IsEnabled = hasSelection && !_showingCancelledFormulas
            && HasActionPermission("Fórmulas", "Baja");
        FormulaViewButton.IsEnabled = hasSelection && HasActionPermission("Fórmulas", "Visualizar");
    }

    private void UpdateSupplierActionButtons()
    {
        AddSupplierButton.IsEnabled = HasActionPermission("Proveedores", "Alta");
        EditSupplierButton.IsEnabled = _selectedSupplier is not null
            && HasActionPermission("Proveedores", "Editar");
        DeleteSupplierButton.IsEnabled = _selectedSupplier is not null
            && HasActionPermission("Proveedores", "Baja");
        ViewSupplierButton.IsEnabled = _selectedSupplier is not null
            && HasActionPermission("Proveedores", "Visualizar");
    }

    private void UpdateClientActionButtons()
    {
        AddClientButton.IsEnabled = HasActionPermission("Clientes", "Alta");
        EditClientButton.IsEnabled = _selectedClient is not null
            && HasActionPermission("Clientes", "Editar");
        DeleteClientButton.IsEnabled = _selectedClient is not null
            && HasActionPermission("Clientes", "Baja");
        ViewClientButton.IsEnabled = _selectedClient is not null
            && HasActionPermission("Clientes", "Visualizar");
    }

    private void UpdateTruckActionButtons()
    {
        var showTrucks = TruckTablePanel.Visibility == Visibility.Visible;
        AddTruckButton.IsEnabled = HasActionPermission("Camiones", "Alta");
        EditTruckButton.IsEnabled = showTrucks && _selectedTruck is not null
            && HasActionPermission("Camiones", "Editar");
        DeleteTruckButton.IsEnabled = showTrucks && _selectedTruck is not null
            && HasActionPermission("Camiones", "Baja");
        ViewTruckButton.IsEnabled = showTrucks && _selectedTruck is not null
            && HasActionPermission("Camiones", "Visualizar");
        EditOperatorButton.IsEnabled = !showTrucks && _selectedOperator is not null
            && HasActionPermission("Camiones", "Editar");
        DeleteOperatorButton.IsEnabled = !showTrucks && _selectedOperator is not null
            && HasActionPermission("Camiones", "Baja");
        ViewOperatorButton.IsEnabled = !showTrucks && _selectedOperator is not null
            && HasActionPermission("Camiones", "Visualizar");
    }

    private void ClockTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        ClockText.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss");
    }

    private static ObservableCollection<PlantValue> CreateAdjustmentRows(string parameterPrefix) =>
        new(Enumerable.Range(1, 8)
            .Select(index => new PlantValue($"{parameterPrefix}{index}", $"AGREGADO {index}", "0")));

    private static ObservableCollection<PlantValuePair> CreateAdjustmentPairs(
        ObservableCollection<PlantValue> values) =>
        new(Enumerable.Range(0, values.Count / 2)
            .Select(index => new PlantValuePair(values[index * 2], values[index * 2 + 1])));
}

public sealed class MaterialRow(string nombre, string cantidad)
{
    public string Nombre { get; } = nombre;
    public string Cantidad { get; } = cantidad;
}

public sealed class MaterialRowPair(MaterialRow first, MaterialRow second)
{
    public MaterialRow First { get; } = first;
    public MaterialRow Second { get; } = second;
}

public sealed class RemissionField(string titulo, string valor, bool editable) : INotifyPropertyChanged
{
    private string _valor = valor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Titulo { get; } = titulo;
    public string Valor
    {
        get => _valor;
        set
        {
            if (_valor == value)
            {
                return;
            }

            _valor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Valor)));
        }
    }

    public bool Editable { get; } = editable;
}

public sealed class RemissionFieldPair(RemissionField first, RemissionField second)
{
    public RemissionField First { get; } = first;
    public RemissionField Second { get; } = second;
}

public sealed class PlantValue(string parameter, string nombre, string valor) : INotifyPropertyChanged
{
    private string _nombre = nombre;
    private string _valor = valor;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Parameter { get; } = parameter;
    public string Nombre
    {
        get => _nombre;
        set
        {
            if (_nombre == value)
            {
                return;
            }

            _nombre = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Nombre)));
        }
    }

    public string Valor
    {
        get => _valor;
        set
        {
            if (_valor == value)
            {
                return;
            }

            _valor = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Valor)));
        }
    }

}

public sealed class PlantValuePair(PlantValue first, PlantValue second)
{
    public PlantValue First { get; } = first;
    public PlantValue Second { get; } = second;
}
