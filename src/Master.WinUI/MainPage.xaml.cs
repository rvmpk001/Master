using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System;
using System.Runtime.InteropServices;
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
    private readonly AuthenticationService _authenticationService = new();
    private readonly InventoryService _inventoryService = new();
    private readonly FormulaService _formulaService = new();
    private readonly SupplierService _supplierService = new();
    private readonly ClientService _clientService = new();
    private readonly TruckService _truckService = new();
    private readonly OrderService _orderService = new();
    private readonly CompanyConfigurationService _companyConfigurationService = new();
    private bool _showPlantTools = true;
    private bool _isDashboardResizeActive;
    private double _dashboardResizeStartY;
    private double _dashboardPanelsStartHeight;
    private bool _isPlantToolsResizeActive;
    private double _plantToolsResizeStartX;
    private double _plantToolsResizeStartWidth;
    private double _plantToolsWidth = DefaultPlantToolsWidth;
    private string _authenticatedUsername = string.Empty;
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

    public ObservableCollection<MaterialRow> MaterialRows { get; } = new()
    {
        new("GRAVA", "0"),
        new("ARENA", "0"),
        new("CEMENTO", "0"),
        new("ADITIVO 1", "0"),
        new("AGUA 1", "0")
    };

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

    public ObservableCollection<PlantValue> HumidityRows { get; } = CreateAdjustmentRows();
    public ObservableCollection<PlantValue> AbsorptionRows { get; } = CreateAdjustmentRows();
    public ObservableCollection<InventoryMaterial> AggregateInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> AdditiveInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> WaterInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> CementInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> FiberInventory { get; } = new();
    public ObservableCollection<InventoryMaterial> SelectedInventoryMaterials { get; } = new();
    public ObservableCollection<string> InventorySuppliers { get; } = new();
    public ObservableCollection<string> ReportDrivers { get; } = new() { "Todos" };
    public ObservableCollection<string> ReportFormulas { get; } = new() { "Todos" };
    public ObservableCollection<string> ReportClients { get; } = new() { "Todos" };
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
    public ObservableCollection<string> ManualLoadOrderNumbers { get; } = new() { "Ocasional" };
    public ObservableCollection<string> ManualLoadConstructionOptions { get; } = new();
    public ObservableCollection<OperatorRecord> ManualLoadOperators { get; } = new();
    public ObservableCollection<TruckRecord> ManualLoadTrucks { get; } = new();
    public ObservableCollection<string> OrderDischargeTypes { get; } = new();
    public ObservableCollection<string> OrderProducts { get; } = new();
    public ObservableCollection<string> OrderPaymentConditions { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        ConfigurationView.CloseRequested += (_, _) => ConfigurationFormOverlay.Visibility = Visibility.Collapsed;
        ReportPresetRadioButton.Checked += ReportPeriodMode_Checked;
        ReportCustomRadioButton.Checked += ReportPeriodMode_Checked;
        UpdateReportPeriodControls(isPresetMode: true);
        SizeChanged += Page_SizeChanged;
        ClockText.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss");
        _clockTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(1);
        _clockTimer.Tick += ClockTimer_Tick;
        _clockTimer.Start();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
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
        DashboardPanelsGrid.Width = Math.Max(1200, e.NewSize.Width);
        DashboardPanelsGrid.Height = e.NewSize.Height;
    }

    private async void ModuleNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string module })
        {
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

        if (module.StartsWith("Reporte", StringComparison.OrdinalIgnoreCase))
        {
            ReportFormOverlay.Visibility = Visibility.Visible;
            await LoadReportFiltersAsync();
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
            ManualLoadOverlay.Visibility = Visibility.Visible;
            await LoadManualLoadRecipesAsync();
            return;
        }

        if (module == "Configuración")
        {
            ConfigurationFormOverlay.Visibility = Visibility.Visible;
            return;
        }

        ModuleStatusText.Text = $"{module}: pantalla pendiente de migración. No se ejecutó ninguna operación de la aplicación original.";
    }

    private void CloseManualLoad_Click(object sender, RoutedEventArgs e)
    {
        ManualLoadOverlay.Visibility = Visibility.Collapsed;
    }

    private async Task LoadManualLoadRecipesAsync()
    {
        ManualLoadStatusText.Text = "Consultando catálogos y pedidos en BlapaciSQL...";
        ManualLoadRecipeComboBox.IsEnabled = false;
        ManualLoadOrderComboBox.IsEnabled = false;
        ManualLoadOrderNumbers.Clear();
        ManualLoadOrderNumbers.Add("Ocasional");
        ManualLoadOrderComboBox.SelectedItem = "Ocasional";
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
            foreach (var construction in clients.Clients
                         .Select(client => client.Construction.Trim())
                         .Where(construction => !string.IsNullOrWhiteSpace(construction))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(construction => construction, StringComparer.CurrentCultureIgnoreCase))
            {
                ManualLoadConstructionOptions.Add(construction);
            }

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

            ManualLoadOrderNumbers.Clear();
            ManualLoadOrderNumbers.Add("Ocasional");
            foreach (var orderNumber in orderNumbers)
            {
                ManualLoadOrderNumbers.Add(orderNumber);
            }

            ManualLoadOrderComboBox.SelectedItem = "Ocasional";
            ManualLoadRemissionTextBox.Text = nextRemission;
            ManualLoadStatusText.Text = $"Fórmulas: {ManualLoadRecipes.Count}. Pedidos activos: {orderNumbers.Count}.";
        }
        catch (SqlException exception)
        {
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
        if (ManualLoadRecipeComboBox.SelectedItem is OrderRecipe recipe)
        {
            ManualLoadDescriptionTextBox.Text = recipe.Description;
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
            ManualLoadQuantityNumberBox.Value = Math.Max(0, (double)(order.ToSupply ?? 0));
            ManualLoadRemissionObservationsTextBox.Text = order.Observations;
            ManualLoadStatusText.Text = order.ToSupply > 0
                ? $"Datos cargados del pedido activo {orderNumber}. Se sugirió la cantidad pendiente por surtir."
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

    private void ClearManualLoadOrderFields()
    {
        ManualLoadQuantityNumberBox.Value = 0;
        ManualLoadRecipeComboBox.SelectedItem = null;
        ManualLoadRecipeComboBox.Text = string.Empty;
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
        var recipeCode = ManualLoadRecipeComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(recipeCode))
        {
            ManualLoadStatusText.Text = "El código de receta es obligatorio.";
            ManualLoadRecipeComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!double.IsFinite(ManualLoadQuantityNumberBox.Value) ||
            ManualLoadQuantityNumberBox.Value <= 0 ||
            !decimal.TryParse(
                ManualLoadQuantityNumberBox.Value.ToString(CultureInfo.InvariantCulture),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var quantity))
        {
            ManualLoadStatusText.Text = "La cantidad debe ser un número mayor que cero.";
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

        var materialFields = new (string Column, TextBox Input)[]
        {
            ("A1C", ManualLoadA1TextBox),
            ("A2C", ManualLoadA2TextBox),
            ("A3C", ManualLoadA3TextBox),
            ("A4C", ManualLoadA4TextBox),
            ("A5C", ManualLoadA5TextBox),
            ("A6C", ManualLoadA6TextBox),
            ("A7C", ManualLoadA7TextBox),
            ("A8C", ManualLoadA8TextBox),
            ("C1C", ManualLoadC1TextBox),
            ("C2C", ManualLoadC2TextBox),
            ("C3C", ManualLoadC3TextBox),
            ("C4C", ManualLoadC4TextBox),
            ("C5C", ManualLoadC5TextBox),
            ("D1C", ManualLoadD1TextBox),
            ("D2C", ManualLoadD2TextBox),
            ("D3C", ManualLoadD3TextBox),
            ("D4C", ManualLoadD4TextBox),
            ("D5C", ManualLoadD5TextBox),
            ("D6C", ManualLoadD6TextBox),
            ("Agua", ManualLoadWater1TextBox),
            ("Agua2", ManualLoadWater2TextBox),
            ("Agua3", ManualLoadWater3TextBox),
            ("FI1C", ManualLoadFI1TextBox),
            ("FI2C", ManualLoadFI2TextBox),
            ("FI3C", ManualLoadFI3TextBox)
        };
        var materialQuantities = new Dictionary<string, string?>(StringComparer.Ordinal);
        var requiredMaterialFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "A1C",
            "A2C",
            "C1C",
            "Agua",
            "D1C",
            "D2C"
        };
        var invalidRequiredMaterialFields = materialFields
            .Where(field => requiredMaterialFields.Contains(field.Column))
            .Where(field =>
                !decimal.TryParse(field.Input.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ||
                value <= 0)
            .Select(field => field.Column)
            .ToHashSet(StringComparer.Ordinal);
        SetManualLoadRequiredFieldHighlight(invalidRequiredMaterialFields);
        if (invalidRequiredMaterialFields.Count > 0)
        {
            var firstInvalidField = materialFields.First(field => invalidRequiredMaterialFields.Contains(field.Column)).Input;
            ManualLoadStatusText.Text = $"{firstInvalidField.Header} es obligatorio y debe ser mayor que cero.";
            firstInvalidField.Focus(FocusState.Programmatic);
            return;
        }

        foreach (var (column, input) in materialFields)
        {
            var value = input.Text.Trim();
            if (string.IsNullOrEmpty(value))
            {
                materialQuantities[column] = null;
                continue;
            }

            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var materialQuantity) ||
                materialQuantity < 0)
            {
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

        ManualLoadSaveButton.IsEnabled = false;
        ManualLoadStatusText.Text = "Guardando carga manual en BlapaciSQL...";
        try
        {
            var remission = await _orderService.SaveManualLoadAsync(
                recipeCode,
                quantity,
                materialQuantities,
                ManualLoadRemissionDischargeComboBox.Text,
                orderNumber,
                (ManualLoadRemissionClientComboBox.SelectedItem as ClientRecord)?.Client
                    ?? ManualLoadRemissionClientComboBox.Text,
                ManualLoadRemissionConstructionComboBox.Text,
                ManualLoadRemissionProductComboBox.Text,
                ManualLoadRemissionPaymentComboBox.Text,
                ManualLoadDescriptionTextBox.Text,
                (ManualLoadRemissionOperatorComboBox.SelectedItem as OperatorRecord)?.Name,
                (ManualLoadRemissionTruckComboBox.SelectedItem as TruckRecord)?.Unit,
                orderedQuantity,
                suppliedQuantity,
                toSupplyQuantity,
                ManualLoadRemissionObservationsTextBox.Text);

            ManualLoadRemissionTextBox.Text = remission;
            ManualLoadStatusText.Text = $"Carga de la remisión {remission} guardada correctamente.";
            ModuleStatusText.Text = $"Carga manual de la remisión {remission} guardada en BlapaciSQL.";
            SetManualLoadRequiredFieldHighlight(new HashSet<string>(StringComparer.Ordinal));
        }
        catch (SqlException exception)
        {
            ManualLoadStatusText.Text = $"No se pudo guardar la carga en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ManualLoadStatusText.Text = exception.Message;
        }
        finally
        {
            ManualLoadSaveButton.IsEnabled = true;
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
        var normalBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 81, 106));
        var errorBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68));
        foreach (var (column, input) in new (string Column, TextBox Input)[]
        {
            ("A1C", ManualLoadA1TextBox),
            ("A2C", ManualLoadA2TextBox),
            ("C1C", ManualLoadC1TextBox),
            ("Agua", ManualLoadWater1TextBox),
            ("D1C", ManualLoadD1TextBox),
            ("D2C", ManualLoadD2TextBox)
        })
        {
            var isInvalid = invalidFields.Contains(column);
            input.BorderBrush = isInvalid ? errorBrush : normalBrush;
            input.BorderThickness = isInvalid ? new Thickness(2) : new Thickness(1);
        }
    }

    private async Task LoadOrdersAsync()
    {
        OrderFormStatusText.Text = "Consultando pedidos en BlapaciSQL...";
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
            OrderFormStatusText.Text = $"Pedidos cargados: {ActiveOrders.Count} activos, {CompletedOrders.Count} terminados y {CancelledOrders.Count} cancelados. Clientes disponibles: {OrderClients.Count}.";
        }
        catch (SqlException exception)
        {
            OrderFormStatusText.Text = $"No se pudieron consultar los pedidos en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
    }

    private void ShowOrders(IEnumerable<OrderRecord> orders, Button selectedTab)
    {
        if (_selectedOrderRow is not null)
        {
            _selectedOrderRow.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 30, 45, 67));
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

        var activeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
        var inactiveBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 54, 77));
        OrderActiveTabButton.Background = selectedTab == OrderActiveTabButton ? activeBrush : inactiveBrush;
        OrderCompletedTabButton.Background = selectedTab == OrderCompletedTabButton ? activeBrush : inactiveBrush;
        OrderCancelledTabButton.Background = selectedTab == OrderCancelledTabButton ? activeBrush : inactiveBrush;
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
            _selectedOrderRow.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 30, 45, 67));
        }

        _selectedOrder = order;
        _selectedOrderRow = row;
        row.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(255, 0, 88, 160));
        UpdateOrderActionButtons();
    }

    private void UpdateOrderActionButtons()
    {
        var hasSelection = _selectedOrder is not null;
        var canChange = _showingActiveOrders &&
            _selectedOrder?.Status.Trim() is "A" or "P";
        AddOrderButton.IsEnabled = _showingActiveOrders || (_showingCancelledOrders && hasSelection);
        EditOrderButton.IsEnabled = canChange;
        CancelOrderButton.IsEnabled = canChange;
        ViewOrderButton.IsEnabled = hasSelection;
    }

    private void EditOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder is null || !_showingActiveOrders)
        {
            OrderFormStatusText.Text = "Selecciona un pedido activo para editar.";
            return;
        }

        ShowOrderEditor(_selectedOrder, readOnly: false);
    }

    private void ViewOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder is null)
        {
            OrderFormStatusText.Text = "Selecciona un pedido para visualizar.";
            return;
        }

        ShowOrderEditor(_selectedOrder, readOnly: true);
    }

    private async void CancelOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrder is null || !_showingActiveOrders)
        {
            OrderFormStatusText.Text = "Selecciona un pedido activo para cancelar.";
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
        OrderFormStatusText.Text = $"Cancelando pedido {order.Number} en BlapaciSQL...";
        try
        {
            await _orderService.CancelOrderAsync(order.Id);
            await LoadOrdersAsync();
            OrderFormStatusText.Text = $"Pedido {order.Number} cancelado correctamente.";
        }
        catch (SqlException exception)
        {
            OrderFormStatusText.Text = $"No se pudo cancelar el pedido en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            OrderFormStatusText.Text = exception.Message;
        }
        finally
        {
            UpdateOrderActionButtons();
        }
    }

    private async void AddOrder_Click(object sender, RoutedEventArgs e)
    {
        if (_showingCancelledOrders)
        {
            await RestoreSelectedOrderAsync();
            return;
        }

        if (!AddOrderButton.IsEnabled)
        {
            OrderFormStatusText.Text = "El formulario solo se puede abrir desde la pestaña de pedidos activos.";
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
        OrderEditorStatusText.Text = string.Empty;
        _editingOrder = false;
        SetOrderEditorReadOnly(false);
        OrderEditorTitleText.Text = "Agregar Pedido";
        OrderEditorSaveButton.Content = "Guardar";
        OrderEditorSaveButton.Visibility = Visibility.Visible;
        OrderEditorOverlay.Visibility = Visibility.Visible;
    }

    private async Task RestoreSelectedOrderAsync()
    {
        if (_selectedOrder is null || !_showingCancelledOrders)
        {
            OrderFormStatusText.Text = "Selecciona un pedido cancelado para restaurarlo.";
            return;
        }

        var order = _selectedOrder;
        AddOrderButton.IsEnabled = false;
        OrderFormStatusText.Text = $"Restaurando pedido {order.Number}...";
        try
        {
            await _orderService.RestoreOrderAsync(order.Id);
            await LoadOrdersAsync();
            ShowActiveOrders();
            OrderFormStatusText.Text = $"El pedido {order.Number} se restauró y está activo.";
        }
        catch (SqlException exception)
        {
            OrderFormStatusText.Text = $"No se pudo restaurar el pedido en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            OrderFormStatusText.Text = exception.Message;
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
        OrderEditorStatusText.Text = string.Empty;
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
        if (OrderClientComboBox.SelectedItem is not ClientRecord client)
        {
            OrderEditorStatusText.Text = "Selecciona un cliente.";
            OrderClientComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var authorizedBy = OrderAuthorizedByTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(authorizedBy))
        {
            OrderEditorStatusText.Text = "El campo Autoriza Pedido es obligatorio.";
            OrderAuthorizedByTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (authorizedBy.Length > 255)
        {
            OrderEditorStatusText.Text = "Autoriza Pedido no puede superar 255 caracteres.";
            OrderAuthorizedByTextBox.Focus(FocusState.Programmatic);
            return;
        }

        var phone = OrderPhoneTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(phone) && (!SupplierDataValidator.IsValidPhone(phone) || phone.Length > 50))
        {
            OrderEditorStatusText.Text = "El teléfono debe tener entre 7 y 15 dígitos; permite +, espacios, guiones y paréntesis, con un máximo de 50 caracteres.";
            OrderPhoneTextBox.Focus(FocusState.Programmatic);
            return;
        }

        var email = OrderEmailTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(email) && (!SupplierDataValidator.IsValidEmail(email) || email.Length > 80))
        {
            OrderEditorStatusText.Text = "Escribe un correo válido (por ejemplo, nombre@dominio.com) de máximo 80 caracteres.";
            OrderEmailTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!decimal.TryParse(OrderQuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) ||
            quantity <= 0)
        {
            OrderEditorStatusText.Text = "La cantidad debe ser un número mayor que cero.";
            OrderQuantityTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (OrderRecipeComboBox.SelectedItem is not OrderRecipe recipe)
        {
            OrderEditorStatusText.Text = "Selecciona una receta.";
            OrderRecipeComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (OrderDischargeTypeComboBox.SelectedItem is not string dischargeType)
        {
            OrderEditorStatusText.Text = "Selecciona el tipo de descarga.";
            OrderDischargeTypeComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (OrderProductComboBox.SelectedItem is not string product)
        {
            OrderEditorStatusText.Text = "Selecciona un producto.";
            OrderProductComboBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!decimal.TryParse(OrderShippingTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var shipping) ||
            shipping < 0)
        {
            OrderEditorStatusText.Text = "El envío debe ser un número igual o mayor que cero.";
            OrderShippingTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!double.IsFinite(OrderIntervalNumberBox.Value) || OrderIntervalNumberBox.Value < 0)
        {
            OrderEditorStatusText.Text = "El intervalo debe ser un número igual o mayor que cero.";
            OrderIntervalNumberBox.Focus(FocusState.Programmatic);
            return;
        }

        OrderEditorSaveButton.IsEnabled = false;
        OrderEditorStatusText.Text = "Guardando pedido en BlapaciSQL...";
        try
        {
            var construction = OrderConstructionComboBox.Text.Trim();
            var buyer = OrderRequestedByTextBox.Text.Trim();
            var paymentCondition = OrderPaymentConditionsComboBox.SelectedItem as string ?? string.Empty;
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
                    OrderObservationsTextBox.Text.Trim());
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
                OrderObservationsTextBox.Text.Trim());
            }

            OrderNumberTextBox.Text = orderNumber.ToString(CultureInfo.InvariantCulture);
            OrderEditorOverlay.Visibility = Visibility.Collapsed;
            await LoadOrdersAsync();
            OrderFormStatusText.Text = _editingOrder
                ? $"Pedido {orderNumber} actualizado correctamente."
                : $"Pedido {orderNumber} guardado correctamente.";
        }
        catch (SqlException exception)
        {
            OrderEditorStatusText.Text = $"No se pudo guardar el pedido en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            OrderEditorStatusText.Text = exception.Message;
        }
        catch (ArgumentOutOfRangeException exception)
        {
            OrderEditorStatusText.Text = $"Revisa la fecha, hora o intervalo: {exception.Message}";
        }
        finally
        {
            OrderEditorSaveButton.IsEnabled = true;
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
        TruckFormStatusText.Text = "Consultando camiones y operadores en BlapaciSQL...";
        try
        {
            var trucks = await _truckService.GetTrucksAsync();
            var operators = await _truckService.GetOperatorsAsync();
            _selectedTruck = null;
            _selectedTruckRow = null;
            _selectedOperator = null;
            _selectedOperatorRow = null;
            EditTruckButton.IsEnabled = false;
            DeleteTruckButton.IsEnabled = false;
            ViewTruckButton.IsEnabled = false;
            EditOperatorButton.IsEnabled = false;
            DeleteOperatorButton.IsEnabled = false;
            ViewOperatorButton.IsEnabled = false;
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

            TruckFormStatusText.Text = $"Camiones: {trucks.Trucks.Count}; operadores: {operators.Operators.Count}.";
        }
        catch (SqlException exception)
        {
            TruckFormStatusText.Text = $"No se pudieron consultar camiones u operadores en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
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
        AddTruckButton.IsEnabled = true;
        EditTruckButton.IsEnabled = showTrucks && _selectedTruck is not null;
        DeleteTruckButton.IsEnabled = showTrucks && _selectedTruck is not null;
        ViewTruckButton.IsEnabled = showTrucks && _selectedTruck is not null;
        EditTruckButton.Visibility = showTrucks ? Visibility.Visible : Visibility.Collapsed;
        DeleteTruckButton.Visibility = showTrucks ? Visibility.Visible : Visibility.Collapsed;
        ViewTruckButton.Visibility = showTrucks ? Visibility.Visible : Visibility.Collapsed;
        EditOperatorButton.Visibility = showTrucks ? Visibility.Collapsed : Visibility.Visible;
        DeleteOperatorButton.Visibility = showTrucks ? Visibility.Collapsed : Visibility.Visible;
        ViewOperatorButton.Visibility = showTrucks ? Visibility.Collapsed : Visibility.Visible;
        EditOperatorButton.IsEnabled = !showTrucks && _selectedOperator is not null;
        DeleteOperatorButton.IsEnabled = !showTrucks && _selectedOperator is not null;
        ViewOperatorButton.IsEnabled = !showTrucks && _selectedOperator is not null;
    }

    private void AddTruck_Click(object sender, RoutedEventArgs e)
    {
        if (OperatorTablePanel.Visibility == Visibility.Visible)
        {
            ShowOperatorEditor(null, readOnly: false);
            return;
        }

        if (TruckTablePanel.Visibility != Visibility.Visible)
        {
            TruckFormStatusText.Text = "Selecciona una pestaña para agregar un registro.";
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
        EditOperatorButton.IsEnabled = true;
        DeleteOperatorButton.IsEnabled = true;
        ViewOperatorButton.IsEnabled = true;
    }

    private void EditOperator_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOperator is null)
        {
            TruckFormStatusText.Text = "Selecciona un operador para editar.";
            return;
        }

        ShowOperatorEditor(_selectedOperator, readOnly: false);
    }

    private void ViewOperator_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOperator is null)
        {
            TruckFormStatusText.Text = "Selecciona un operador para visualizar.";
            return;
        }

        ShowOperatorEditor(_selectedOperator, readOnly: true);
    }

    private void ShowOperatorEditor(OperatorRecord? operatorRecord, bool readOnly)
    {
        _editingOperator = operatorRecord is not null && !readOnly;
        OperatorEditorTitleText.Text = readOnly ? "Visualizar Operador" : _editingOperator ? "Editar Operador" : "Agregar Operador";
        OperatorEditorSaveButton.Content = _editingOperator ? "Guardar cambios" : "Guardar";
        OperatorEditorSaveButton.Visibility = readOnly ? Visibility.Collapsed : Visibility.Visible;
        OperatorNameTextBox.Text = operatorRecord?.Name ?? string.Empty;
        OperatorInitialsTextBox.Text = operatorRecord?.Initials ?? string.Empty;
        OperatorNameTextBox.IsReadOnly = readOnly;
        OperatorInitialsTextBox.IsReadOnly = readOnly;
        OperatorEditorStatusText.Text = string.Empty;
        OperatorEditorOverlay.Visibility = Visibility.Visible;
        if (!readOnly)
        {
            OperatorNameTextBox.Focus(FocusState.Programmatic);
        }
    }

    private async void SaveOperator_Click(object sender, RoutedEventArgs e)
    {
        var name = OperatorNameTextBox.Text.Trim();
        var initials = OperatorInitialsTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            OperatorEditorStatusText.Text = "El nombre del operador es obligatorio.";
            OperatorNameTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (string.IsNullOrWhiteSpace(initials))
        {
            OperatorEditorStatusText.Text = "Las iniciales del operador son obligatorias.";
            OperatorInitialsTextBox.Focus(FocusState.Programmatic);
            return;
        }

        OperatorEditorSaveButton.IsEnabled = false;
        OperatorEditorStatusText.Text = "Guardando operador en BlapaciSQL...";
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
            TruckFormStatusText.Text = _editingOperator
                ? $"Operador {name} actualizado correctamente."
                : $"Operador {name} guardado correctamente.";
        }
        catch (SqlException exception)
        {
            OperatorEditorStatusText.Text = $"No se pudo guardar el operador en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            OperatorEditorStatusText.Text = exception.Message;
        }
        finally
        {
            OperatorEditorSaveButton.IsEnabled = true;
        }
    }

    private async void DeleteOperator_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOperator is null)
        {
            TruckFormStatusText.Text = "Selecciona un operador para borrar.";
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
            TruckFormStatusText.Text = $"Operador {operatorRecord.Name} borrado correctamente.";
        }
        catch (SqlException exception)
        {
            TruckFormStatusText.Text = $"No se pudo borrar el operador en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            TruckFormStatusText.Text = exception.Message;
        }
        finally
        {
            DeleteOperatorButton.IsEnabled = _selectedOperator is not null;
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
        EditTruckButton.IsEnabled = true;
        DeleteTruckButton.IsEnabled = true;
        ViewTruckButton.IsEnabled = true;
    }

    private void EditTruck_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTruck is null)
        {
            TruckFormStatusText.Text = "Selecciona un camión para editar.";
            return;
        }

        ShowTruckEditor(_selectedTruck, readOnly: false);
    }

    private void ViewTruck_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTruck is null)
        {
            TruckFormStatusText.Text = "Selecciona un camión para visualizar.";
            return;
        }

        ShowTruckEditor(_selectedTruck, readOnly: true);
    }

    private void ShowTruckEditor(TruckRecord? truck, bool readOnly)
    {
        _editingTruck = truck is not null && !readOnly;
        TruckEditorSaveButton.Content = _editingTruck ? "Guardar cambios" : "Guardar";
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
        TruckEditorStatusText.Text = string.Empty;
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
        var selectedOperator = TruckOperatorComboBox.SelectedItem as OperatorRecord;
        if (selectedOperator is null && (!_editingTruck || _selectedTruck is null))
        {
            TruckEditorStatusText.Text = "Selecciona un operador.";
            TruckOperatorComboBox.Focus(FocusState.Programmatic);
            return;
        }

        var plates = TruckPlatesTextBox.Text.Trim();
        var unit = TruckUnitTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(plates))
        {
            TruckEditorStatusText.Text = "El número de placas es obligatorio.";
            TruckPlatesTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            TruckEditorStatusText.Text = "La unidad es obligatoria.";
            TruckUnitTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (!decimal.TryParse(TruckCapacityTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var capacity)
            && !decimal.TryParse(TruckCapacityTextBox.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out capacity))
        {
            TruckEditorStatusText.Text = "La capacidad debe ser un número válido.";
            TruckCapacityTextBox.Focus(FocusState.Programmatic);
            return;
        }

        if (capacity <= 0)
        {
            TruckEditorStatusText.Text = "La capacidad debe ser mayor que cero.";
            TruckCapacityTextBox.Focus(FocusState.Programmatic);
            return;
        }

        TruckEditorSaveButton.IsEnabled = false;
        TruckEditorStatusText.Text = "Guardando camión en BlapaciSQL...";
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
            TruckFormStatusText.Text = _editingTruck
                ? $"Camión {unit} actualizado correctamente."
                : $"Camión {unit} guardado correctamente.";
        }
        catch (SqlException exception)
        {
            TruckEditorStatusText.Text = $"No se pudo guardar el camión en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            TruckEditorStatusText.Text = exception.Message;
        }
        finally
        {
            TruckEditorSaveButton.IsEnabled = true;
        }
    }

    private async void DeleteTruck_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTruck is null)
        {
            TruckFormStatusText.Text = "Selecciona un camión para borrar.";
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
            TruckFormStatusText.Text = $"Camión {truck.Unit} borrado correctamente.";
        }
        catch (SqlException exception)
        {
            TruckFormStatusText.Text = $"No se pudo borrar el camión en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            TruckFormStatusText.Text = exception.Message;
        }
        finally
        {
            DeleteTruckButton.IsEnabled = _selectedTruck is not null;
        }
    }

    private async Task LoadClientsAsync()
    {
        ClientFormStatusText.Text = "Consultando clientes en BlapaciSQL...";
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

            ClientFormStatusText.Text = $"Clientes cargados desde BlapaciSQL: {data.Clients.Count}.";
        }
        catch (SqlException exception)
        {
            ClientFormStatusText.Text = $"No se pudieron consultar los clientes en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
    }

    private void CloseClientForm_Click(object sender, RoutedEventArgs e)
    {
        ClientFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void ClientListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedClient = ClientListView.SelectedItem as ClientRecord;
        EditClientButton.IsEnabled = _selectedClient is not null;
        DeleteClientButton.IsEnabled = _selectedClient is not null;
        ViewClientButton.IsEnabled = _selectedClient is not null;
    }

    private void AddClient_Click(object sender, RoutedEventArgs e)
    {
        ShowClientEditor(null, readOnly: false);
    }

    private void EditClient_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedClient is null)
        {
            ClientFormStatusText.Text = "Selecciona un cliente para editar.";
            return;
        }

        ShowClientEditor(_selectedClient, readOnly: false);
    }

    private void ViewClient_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedClient is null)
        {
            ClientFormStatusText.Text = "Selecciona un cliente para visualizar.";
            return;
        }

        ShowClientEditor(_selectedClient, readOnly: true);
    }

    private void ShowClientEditor(ClientRecord? client, bool readOnly)
    {
        _editingClient = client is not null && !readOnly;
        ClientEditorTitleText.Text = readOnly ? "Visualizar Cliente" : _editingClient ? "Editar Cliente" : "Agregar Cliente";
        ClientEditorSaveButton.Content = _editingClient ? "Guardar cambios" : "Guardar";
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
        ClientEditorStatusText.Text = string.Empty;
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
            ClientEditorStatusText.Text = validationError.Message;
            GetClientTextBox(validationError.Field).Focus(FocusState.Programmatic);
            return;
        }

        var client = clientDetails.Client.Trim();

        ClientEditorSaveButton.IsEnabled = false;
        ClientEditorStatusText.Text = "Guardando cliente en BlapaciSQL...";
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
            ClientFormStatusText.Text = _editingClient
                ? $"Cliente {client} actualizado correctamente."
                : $"Cliente {client} guardado correctamente.";
        }
        catch (SqlException exception)
        {
            ClientEditorStatusText.Text = $"No se pudo guardar el cliente en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ClientEditorStatusText.Text = exception.Message;
        }
        finally
        {
            ClientEditorSaveButton.IsEnabled = true;
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
        if (_selectedClient is null)
        {
            ClientFormStatusText.Text = "Selecciona un cliente para borrar.";
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
            ClientFormStatusText.Text = $"Cliente {client.Client} borrado correctamente.";
        }
        catch (SqlException exception)
        {
            ClientFormStatusText.Text = $"No se pudo borrar el cliente en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            ClientFormStatusText.Text = exception.Message;
        }
        finally
        {
            DeleteClientButton.IsEnabled = _selectedClient is not null;
        }
    }

    private async Task LoadSuppliersAsync()
    {
        SupplierFormStatusText.Text = "Consultando proveedores en BlapaciSQL...";
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

            SupplierFormStatusText.Text = $"Proveedores cargados desde BlapaciSQL: {data.Suppliers.Count}.";
        }
        catch (SqlException exception)
        {
            SupplierFormStatusText.Text = $"No se pudieron consultar los proveedores en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
    }

    private void CloseSupplierForm_Click(object sender, RoutedEventArgs e)
    {
        SupplierFormOverlay.Visibility = Visibility.Collapsed;
    }

    private void SupplierListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedSupplier = SupplierListView.SelectedItem as SupplierRecord;
        EditSupplierButton.IsEnabled = _selectedSupplier is not null;
        DeleteSupplierButton.IsEnabled = _selectedSupplier is not null;
        ViewSupplierButton.IsEnabled = _selectedSupplier is not null;
    }

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        ShowSupplierEditor(null, readOnly: false);
    }

    private void EditSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier is null)
        {
            SupplierFormStatusText.Text = "Selecciona un proveedor para editar.";
            return;
        }

        ShowSupplierEditor(_selectedSupplier, readOnly: false);
    }

    private void ViewSupplier_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSupplier is null)
        {
            SupplierFormStatusText.Text = "Selecciona un proveedor para visualizar.";
            return;
        }

        ShowSupplierEditor(_selectedSupplier, readOnly: true);
    }

    private void ShowSupplierEditor(SupplierRecord? supplier, bool readOnly)
    {
        _editingSupplier = supplier is not null && !readOnly;
        SupplierEditorTitleText.Text = readOnly ? "Visualizar Proveedor" : _editingSupplier ? "Editar Proveedor" : "Agregar Proveedor";
        SupplierEditorSaveButton.Content = _editingSupplier ? "Guardar cambios" : "Guardar";
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
        SupplierEditorStatusText.Text = string.Empty;
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
            SupplierEditorStatusText.Text = validationError.Message;
            GetSupplierTextBox(validationError.Field).Focus(FocusState.Programmatic);
            return;
        }

        var supplierName = supplier.Supplier.Trim();

        SupplierEditorSaveButton.IsEnabled = false;
        SupplierEditorStatusText.Text = "Guardando proveedor en BlapaciSQL...";
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
            SupplierFormStatusText.Text = _editingSupplier
                ? $"Proveedor {supplierName} actualizado correctamente."
                : $"Proveedor {supplierName} guardado correctamente.";
        }
        catch (SqlException exception)
        {
            SupplierEditorStatusText.Text = $"No se pudo guardar el proveedor en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            SupplierEditorStatusText.Text = exception.Message;
        }
        finally
        {
            SupplierEditorSaveButton.IsEnabled = true;
        }
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
        if (_selectedSupplier is null)
        {
            SupplierFormStatusText.Text = "Selecciona un proveedor para borrar.";
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
            SupplierFormStatusText.Text = $"Proveedor {supplier.Supplier} borrado correctamente.";
        }
        catch (SqlException exception)
        {
            SupplierFormStatusText.Text = $"No se pudo borrar el proveedor en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            SupplierFormStatusText.Text = exception.Message;
        }
    }

    private async Task LoadFormulasAsync()
    {
        FormulaFormStatusText.Text = "Consultando fórmulas en BlapaciSQL...";

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
            FormulaFormStatusText.Text = $"Fórmulas cargadas desde BlapaciSQL: {data.ActiveRows.Count} activas y {data.CancelledRows.Count} canceladas. Desplázate horizontalmente para ver todos los campos.";
        }
        catch (SqlException exception)
        {
            FormulaFormStatusText.Text = $"No se pudieron consultar las fórmulas en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
    }

    private void ShowActiveFormulas()
    {
        _showingCancelledFormulas = false;
        FormulaAddRestoreButton.Content = "Agregar";
        ClearFormulaSelection();
        ShowFormulaRows(ActiveFormulaRows);
        FormulaActiveTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(255, 0, 120, 212));
        FormulaCancelledTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(255, 38, 54, 77));
    }

    private void ShowCancelledFormulas()
    {
        _showingCancelledFormulas = true;
        FormulaAddRestoreButton.Content = "Restaurar";
        FormulaAddRestoreButton.IsEnabled = false;
        ClearFormulaSelection();
        ShowFormulaRows(CancelledFormulaRows);
        FormulaActiveTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(255, 38, 54, 77));
        FormulaCancelledTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(255, 0, 120, 212));
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
        border.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Windows.UI.Color.FromArgb(255, 0, 94, 167));
        FormulaEditButton.IsEnabled = !_showingCancelledFormulas;
        FormulaDeleteButton.IsEnabled = !_showingCancelledFormulas;
        if (_showingCancelledFormulas)
        {
            FormulaAddRestoreButton.IsEnabled = true;
        }
        FormulaViewButton.IsEnabled = true;
        FormulaFormStatusText.Text = $"Fórmula seleccionada: {formula.Values.GetValueOrDefault("Cod_Formula", string.Empty)} (ID {formula.Id}).";
        e.Handled = true;
    }

    private void ClearFormulaSelection()
    {
        if (_selectedFormulaBorder is not null)
        {
            _selectedFormulaBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 30, 45, 67));
        }

        _selectedFormulaBorder = null;
        _selectedFormula = null;
        FormulaEditButton.IsEnabled = false;
        FormulaDeleteButton.IsEnabled = false;
        FormulaAddRestoreButton.IsEnabled = !_showingCancelledFormulas;
        FormulaViewButton.IsEnabled = false;
    }

    private async void AddFormula_Click(object sender, RoutedEventArgs e)
    {
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
        if (_selectedFormula is null || !_showingCancelledFormulas)
        {
            FormulaFormStatusText.Text = "Selecciona una fórmula cancelada para restaurarla.";
            return;
        }

        var formula = _selectedFormula;
        var code = formula.Values.GetValueOrDefault("Cod_Formula", string.Empty);
        FormulaAddRestoreButton.IsEnabled = false;
        FormulaFormStatusText.Text = $"Restaurando fórmula {code}...";
        try
        {
            await _formulaService.RestoreFormulaAsync(formula.Id);
            await LoadFormulasAsync();
            ShowActiveFormulas();
            FormulaFormStatusText.Text = $"La fórmula {code} se restauró y está activa.";
        }
        catch (SqlException exception)
        {
            FormulaFormStatusText.Text = $"No se pudo restaurar la fórmula (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            FormulaFormStatusText.Text = exception.Message;
        }
        finally
        {
            FormulaAddRestoreButton.IsEnabled = _showingCancelledFormulas && _selectedFormula is not null;
        }
    }

    private async void EditFormula_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFormula is null || _showingCancelledFormulas)
        {
            FormulaFormStatusText.Text = "Selecciona una fórmula activa para editar.";
            return;
        }

        _editingFormula = true;
        _viewingFormula = false;
        FormulaEditorTitle.Text = "Fórmulas";
        await OpenFormulaEditorAsync(_selectedFormula.Values);
    }

    private async void ViewFormula_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFormula is null)
        {
            FormulaFormStatusText.Text = "Selecciona una fórmula para visualizar.";
            return;
        }

        _editingFormula = false;
        _viewingFormula = true;
        FormulaEditorTitle.Text = "Fórmulas";
        await OpenFormulaEditorAsync(_selectedFormula.Values);
    }

    private async Task OpenFormulaEditorAsync(IReadOnlyDictionary<string, string> values)
    {
        FormulaEditorStatusText.Text = "Cargando materiales...";
        FormulaEditorSaveButton.IsEnabled = false;
        FormulaEditorOverlay.Visibility = Visibility.Visible;
        try
        {
            var inventory = await _inventoryService.GetInventoryAsync();
            _formulaMaterialOptions.Clear();
            _formulaMaterialOptions["A"] = inventory.Where(item => item.Category == "AGREGADOS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["C"] = inventory.Where(item => item.Category == "CEMENTOS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["D"] = inventory.Where(item => item.Category == "ADITIVOS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["Agua"] = inventory.Where(item => item.Category == "AGUAS").Select(item => item.Name).ToArray();
            _formulaMaterialOptions["Fibra"] = inventory.Where(item => item.Category == "FIBRAS").Select(item => item.Name).ToArray();

            BuildFormulaEditor(values);
            foreach (var control in _formulaEditorInputs.Values)
            {
                control.IsEnabled = !_viewingFormula;
            }

            FormulaEditorSaveButton.Visibility = _viewingFormula ? Visibility.Collapsed : Visibility.Visible;
            FormulaEditorStatusText.Text = string.Empty;
            FormulaEditorSaveButton.IsEnabled = !_viewingFormula;
        }
        catch (SqlException exception)
        {
            FormulaEditorStatusText.Text = $"No se pudieron cargar los materiales desde BlapaciSQL (SQL {exception.Number}): {exception.Message}";
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
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 23, 35, 55)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 81, 106)),
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
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 245, 249)),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 17, 27, 43)),
                Tag = materialField
            };
            materialPicker.SelectionChanged += FormulaMaterial_SelectionChanged;
            UpdateFormulaMaterialPicker(materialPicker, materialField, values);
            var materialState = new TextBlock
            {
                Text = materialPicker.SelectedItem as string ?? "DESHABILITADA",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94)),
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

    private static StackPanel GetFormulaGroupContent(StackPanel group) => (StackPanel)group.Tag;

    private TextBox CreateFormulaTextBox(string fieldName, IReadOnlyDictionary<string, string> values)
    {
        var input = new TextBox
        {
            Text = values.GetValueOrDefault(fieldName, string.Empty),
            MaxLength = FormulaService.GetFieldMaxLength(fieldName),
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 245, 249)),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 17, 27, 43)),
            Tag = fieldName
        };
        _formulaEditorInputs[fieldName] = input;
        return input;
    }

    private void FormulaMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { Tag: string fieldName } comboBox
            && _formulaMaterialStates.TryGetValue(fieldName, out var state))
        {
            state.Text = comboBox.SelectedItem as string ?? "DESHABILITADA";
        }
    }

    private void FormulaEditorConfigurationTab_Click(object sender, RoutedEventArgs e)
    {
        FormulaEditorConfigurationHost.Visibility = Visibility.Visible;
        FormulaEditorAdditionalHost.Visibility = Visibility.Collapsed;
        FormulaEditorConfigurationTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
        FormulaEditorAdditionalTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 54, 77));
    }

    private void FormulaEditorAdditionalTab_Click(object sender, RoutedEventArgs e)
    {
        FormulaEditorConfigurationHost.Visibility = Visibility.Collapsed;
        FormulaEditorAdditionalHost.Visibility = Visibility.Visible;
        FormulaEditorConfigurationTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 38, 54, 77));
        FormulaEditorAdditionalTabButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
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
        FormulaEditorSaveButton.IsEnabled = true;
        _viewingFormula = false;
    }

    private async void SaveFormulaEditor_Click(object sender, RoutedEventArgs e)
    {
        var values = _formulaEditorInputs.ToDictionary(
            pair => pair.Key,
            pair => pair.Value switch
            {
                TextBox textBox => textBox.Text.Trim(),
                ComboBox comboBox => comboBox.SelectedItem as string ?? string.Empty,
                _ => string.Empty
            },
            StringComparer.Ordinal);
        var identityError = FormulaService.GetFormulaIdentityValidationError(
            values.GetValueOrDefault("Cod_Formula", string.Empty),
            values.GetValueOrDefault("Descripcion", string.Empty));
        if (identityError is not null)
        {
            FormulaEditorStatusText.Text = identityError;
            var invalidField = identityError.StartsWith("El código", StringComparison.Ordinal)
                ? "Cod_Formula"
                : "Descripcion";
            if (_formulaEditorInputs.TryGetValue(invalidField, out var input))
            {
                input.Focus(FocusState.Programmatic);
            }

            return;
        }

        FormulaEditorSaveButton.IsEnabled = false;
        FormulaEditorStatusText.Text = "Guardando fórmula en BlapaciSQL...";
        try
        {
            if (_editingFormula)
            {
                if (_selectedFormula is null)
                {
                    FormulaEditorStatusText.Text = "Selecciona una fórmula activa para editar.";
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
            FormulaFormStatusText.Text = _editingFormula
                ? "Fórmula actualizada correctamente."
                : "Fórmula agregada correctamente.";
        }
        catch (SqlException exception)
        {
            FormulaEditorStatusText.Text = $"No se pudo guardar la fórmula (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            FormulaEditorStatusText.Text = exception.Message;
        }
        finally
        {
            FormulaEditorSaveButton.IsEnabled = true;
        }
    }

    private async void DeleteFormula_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedFormula is null || _showingCancelledFormulas)
        {
            FormulaFormStatusText.Text = "Selecciona una fórmula activa para borrar.";
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
            FormulaFormStatusText.Text = $"La fórmula {code} se movió a Fórmulas Canceladas.";
        }
        catch (SqlException exception)
        {
            FormulaFormStatusText.Text = $"No se pudo borrar la fórmula (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            FormulaFormStatusText.Text = exception.Message;
        }
        finally
        {
            FormulaEditButton.IsEnabled = _selectedFormula is not null && !_showingCancelledFormulas;
            FormulaDeleteButton.IsEnabled = _selectedFormula is not null && !_showingCancelledFormulas;
        }
    }

    private async Task<bool> LoadInventoryAsync()
    {
        InventoryFormStatusText.Text = "Consultando existencias en BlapaciSQL...";

        try
        {
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
            InventoryFormStatusText.Text = $"Existencias actualizadas desde BlapaciSQL. Registros: {inventory.Count}; proveedores: {suppliers.Count}.";
            return true;
        }
        catch (SqlException exception)
        {
            InventoryFormStatusText.Text = $"No se pudieron cargar las existencias o los proveedores desde BlapaciSQL (SQL {exception.Number}): {exception.Message}";
            return false;
        }
    }

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
            InventoryAdditionalFieldsPanel.Visibility = Visibility.Visible;
            return;
        }

        InventoryAdditionalFieldsPanel.Visibility = category is "CEMENTOS" or "AGUAS" or "ADITIVOS" or "FIBRAS"
            ? Visibility.Collapsed
            : Visibility.Visible;

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
            InventoryFormStatusText.Text = $"No hay materiales disponibles para {category}. Actualiza las existencias e inténtalo de nuevo.";
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

    private async Task LoadReportFiltersAsync()
    {
        ReportFormStatusText.Text = "Consultando choferes y fórmulas en BlapaciSQL...";
        ReportDrivers.Clear();
        ReportDrivers.Add("Todos");
        ReportDriverComboBox.SelectedIndex = 0;
        ReportFormulas.Clear();
        ReportFormulas.Add("Todos");
        ReportFormulaComboBox.SelectedIndex = 0;
        ReportClients.Clear();
        ReportClients.Add("Todos");
        ReportClientComboBox.SelectedIndex = 0;

        var errors = new List<string>();
        try
        {
            var operators = await _truckService.GetOperatorsAsync();
            foreach (var operatorRecord in operators.Operators)
            {
                if (!string.IsNullOrWhiteSpace(operatorRecord.Name))
                {
                    ReportDrivers.Add(operatorRecord.Name);
                }
            }

        }
        catch (SqlException exception)
        {
            errors.Add($"No se pudieron cargar los choferes desde dbo.Operadores (SQL {exception.Number}): {exception.Message}");
        }

        try
        {
            var formulas = await _orderService.GetRecipesAsync();
            foreach (var formula in formulas)
            {
                ReportFormulas.Add(formula.Code);
            }
        }
        catch (SqlException exception)
        {
            errors.Add($"No se pudieron cargar las fórmulas desde dbo.Formulas (SQL {exception.Number}): {exception.Message}");
        }

        try
        {
            var clients = await _clientService.GetClientsAsync();
            foreach (var client in clients.Clients)
            {
                if (!string.IsNullOrWhiteSpace(client.Client))
                {
                    ReportClients.Add(client.Client);
                }
            }
        }
        catch (SqlException exception)
        {
            errors.Add($"No se pudieron cargar los clientes desde dbo.Clientes (SQL {exception.Number}): {exception.Message}");
        }

        ReportFormStatusText.Text = errors.Count == 0
            ? $"Choferes cargados: {ReportDrivers.Count - 1}; fórmulas cargadas: {ReportFormulas.Count - 1}; clientes cargados: {ReportClients.Count - 1}."
            : string.Join(Environment.NewLine, errors);
    }

    private void ReportPeriodMode_Checked(object sender, RoutedEventArgs e)
    {
        UpdateReportPeriodControls(ReferenceEquals(sender, ReportPresetRadioButton));
    }

    private void UpdateReportPeriodControls(bool isPresetMode)
    {
        ReportPresetComboBox.IsEnabled = isPresetMode;
        ReportStartDatePicker.IsEnabled = !isPresetMode;
        ReportEndDatePicker.IsEnabled = !isPresetMode;
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

        InventorySaveButton.IsEnabled = operation is not null;
        InventoryFormStatusText.Text = operation is null
            ? "Selecciona Entradas para agregar o Salidas para quitar existencias."
            : $"{operation}: completa los datos y pulsa Guardar para registrar el movimiento.";
    }

    private async void SaveInventory_Click(object sender, RoutedEventArgs e)
    {
        var isEntry = InventoryEntriesCheckBox.IsChecked == true;
        var isExit = InventoryExitsCheckBox.IsChecked == true;
        if (isEntry == isExit)
        {
            InventoryFormStatusText.Text = "Selecciona Entradas para agregar o Salidas para quitar existencias.";
            return;
        }

        if (InventoryMaterialComboBox.SelectedItem is not ComboBoxItem { Content: string category }
            || InventoryTypeComboBox.SelectedItem is not InventoryMaterial material)
        {
            InventoryFormStatusText.Text = "Selecciona el material y el tipo que deseas registrar.";
            return;
        }

        if (!decimal.TryParse(InventoryQuantityTextBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity)
            && !decimal.TryParse(InventoryQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity))
        {
            InventoryFormStatusText.Text = "Ingresa una cantidad numérica válida.";
            return;
        }

        if (quantity <= 0)
        {
            InventoryFormStatusText.Text = "La cantidad debe ser mayor que cero.";
            return;
        }

        var requiresAdditionalFields = category is not ("CEMENTOS" or "AGUAS" or "ADITIVOS" or "FIBRAS");
        var supplier = InventorySupplierComboBox.SelectedItem as string ?? string.Empty;
        if (requiresAdditionalFields && supplier.Length == 0)
        {
            InventoryFormStatusText.Text = "Selecciona un proveedor.";
            return;
        }

        if (_authenticatedUsername.Length is 0 or > 10)
        {
            InventoryFormStatusText.Text = "El usuario no es válido para registrar movimientos (máximo 10 caracteres).";
            return;
        }

        var origin = InventoryOriginTextBox.Text.Trim();
        var invoice = InventoryInvoiceTextBox.Text.Trim();
        if (origin.Length > 50 || invoice.Length > 50 || supplier.Length > 50)
        {
            InventoryFormStatusText.Text = "Proveedor, origen y factura deben tener como máximo 50 caracteres.";
            return;
        }

        InventorySaveButton.IsEnabled = false;
        InventoryFormStatusText.Text = "Guardando el movimiento en BlapaciSQL...";

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
                InventoryFormStatusText.Text = $"{(isExit ? "Salida" : "Entrada")} de {material.Name} guardada correctamente. Existencia actualizada.";
            }
            else
            {
                InventoryFormStatusText.Text = $"Movimiento guardado, pero no se pudo actualizar la vista. {InventoryFormStatusText.Text}";
            }
        }
        catch (SqlException exception)
        {
            InventoryFormStatusText.Text = $"No se pudo guardar el movimiento en BlapaciSQL (SQL {exception.Number}): {exception.Message}";
        }
        catch (InvalidOperationException exception)
        {
            InventoryFormStatusText.Text = exception.Message;
        }
        catch (OverflowException)
        {
            InventoryFormStatusText.Text = "La cantidad excede el valor numérico que puede almacenarse en el inventario.";
        }
        finally
        {
            InventorySaveButton.IsEnabled =
                InventoryEntriesCheckBox.IsChecked == true || InventoryExitsCheckBox.IsChecked == true;
        }
    }

    private async void LoadTemplateImage_Click(object sender, RoutedEventArgs e)
    {
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
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private async void DesignSurface_Drop(object sender, DragEventArgs e)
    {
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

    private async Task LoadTemplateImageAsync(StorageFile file)
    {
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

        TemplateImage.Source = bitmap;
        EmptyCanvasPrompt.Visibility = Visibility.Collapsed;
        ModuleStatusText.Text = $"Plantilla cargada: {file.Name}. El lienzo está listo para agregar controles.";
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
        LoginErrorText.Visibility = Visibility.Collapsed;

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
            _authenticatedUsername = user.Username;
            LoginShell.Visibility = Visibility.Collapsed;
            DashboardShell.Visibility = Visibility.Visible;
            await LoadPlantNameAsync();
        }
        catch (SqlException)
        {
            ShowLoginError("No se pudo conectar a BlapaciSQL. Verifica que SQL Server esté activo y que tu cuenta de Windows tenga acceso de lectura a dbo.Password.");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginProgress.IsActive = false;
            LoginProgress.Visibility = Visibility.Collapsed;
        }
    }

    private async Task LoadPlantNameAsync()
    {
        PlantNameTextBlock.Text = "Cargando planta...";
        try
        {
            var plantName = await _companyConfigurationService.GetPlantNameAsync();
            if (string.IsNullOrWhiteSpace(plantName))
            {
                PlantNameTextBlock.Text = "Planta no configurada";
                ModuleStatusText.Text = "No se encontró un valor para Param = 'ptl' en dbo.Cia_Datos.";
                return;
            }

            PlantNameTextBlock.Text = plantName;
        }
        catch (SqlException exception)
        {
            PlantNameTextBlock.Text = "Planta no disponible";
            ModuleStatusText.Text = $"No se pudo cargar el nombre de la planta desde dbo.Cia_Datos (SQL {exception.Number}): {exception.Message}";
        }
    }

    private void ShowLoginError(string message)
    {
        LoginErrorText.Text = message;
        LoginErrorText.Visibility = Visibility.Visible;
    }

    private void ClockTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        ClockText.Text = DateTime.Now.ToString("dd/MM/yyyy  HH:mm:ss");
    }

    private static ObservableCollection<PlantValue> CreateAdjustmentRows() =>
        new()
        {
            new("GRAVA", "0"),
            new("ARENA", "0"),
            new("DESHABILITADA", "0"),
            new("DESHABILITADA", "0"),
            new("DESHABILITADA", "0"),
            new("DESHABILITADA", "0"),
            new("DESHABILITADA", "0"),
            new("DESHABILITADA", "0"),
            new("DESHABILITADA", "0"),
            new("DESHABILITADA", "0")
        };
}

public sealed class MaterialRow(string nombre, string cantidad)
{
    public string Nombre { get; } = nombre;
    public string Cantidad { get; } = cantidad;
}

public sealed class RemissionField(string titulo, string valor, bool editable)
{
    public string Titulo { get; } = titulo;
    public string Valor { get; set; } = valor;
    public bool Editable { get; } = editable;
}

public sealed class PlantValue(string nombre, string valor)
{
    public string Nombre { get; } = nombre;
    public string Valor { get; } = valor;
}
