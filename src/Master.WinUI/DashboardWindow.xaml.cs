using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System.Globalization;
using WinRT.Interop;
using Master.WinUI.Data;

namespace Master.WinUI;

public sealed partial class DashboardWindow : Window
{
    private readonly Border[] _kpiCards;
    private readonly DashboardRepository _dashboardRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ProductRepository _productRepository;
    private readonly AuthenticatedUser _authenticatedUser;
    private readonly CustomersView _customersView;
    private readonly ProductsView _productsView;
    private bool _hasLoadedMetrics;

    internal event EventHandler? LogoutRequested;

    internal DashboardWindow(
        DashboardRepository dashboardRepository,
        CustomerRepository customerRepository,
        ProductRepository productRepository,
        AuthenticatedUser authenticatedUser)
    {
        InitializeComponent();
        _dashboardRepository = dashboardRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _authenticatedUser = authenticatedUser;
        lblDataMode.Text = "CONECTANDO SQL";
        lblSubtitulo.Text = $"Sesión: {authenticatedUser.FullName} · {authenticatedUser.Role}.";

        _kpiCards =
        [
            pnlKpiClientes,
            pnlKpiProductos,
            pnlKpiVentas,
            pnlKpiIngresos
        ];

        var windowHandle = WindowNative.GetWindowHandle(this);
        _customersView = new CustomersView(
            _customerRepository,
            canAccessDatabase: true,
            windowHandle: windowHandle);
        _customersView.BackRequested += (_, _) => ShowDashboard();
        customersContentHost.Children.Add(_customersView);
        _productsView = new ProductsView(_productRepository, windowHandle);
        _productsView.BackRequested += (_, _) => ShowDashboard();
        customersContentHost.Children.Add(_productsView);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        if (AppWindow.GetFromWindowId(windowId).Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
        ApplyKpiLayout(DashboardContent.ActualWidth);
    }

    private void OnDashboardContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyKpiLayout(e.NewSize.Width);
    }

    private void ApplyKpiLayout(double availableWidth)
    {
        var compact = availableWidth < 620;
        flpTarjetas.ColumnDefinitions[1].Width = compact
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);

        for (var index = 0; index < _kpiCards.Length; index++)
        {
            Grid.SetColumn(_kpiCards[index], compact ? 0 : index % 2);
            Grid.SetRow(_kpiCards[index], compact ? index : index / 2);
        }
    }

    private void OnNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item)
        {
            return;
        }

        switch (item.Tag?.ToString())
        {
            case "dashboard":
                ShowDashboard();
                break;
            case "backToLogin":
                LogoutRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "Clientes":
                ShowCustomers();
                break;
            case "Productos":
                ShowProducts();
                break;
            default:
                ShowDashboard();
                ShowUnavailableModule(item.Content?.ToString() ?? "Este módulo");
                break;
        }
    }

    private void OnModuleToolbarClick(object sender, RoutedEventArgs e)
    {
        if (sender is AppBarButton button)
        {
            if (button.Tag?.ToString() == "Clientes")
            {
                ShowCustomers();
            }
            else if (button.Tag?.ToString() == "Productos")
            {
                ShowProducts();
            }
            else
            {
                ShowUnavailableModule(button.Tag?.ToString() ?? button.Label);
            }
        }
    }

    private void ShowDashboard()
    {
        customersContentHost.Visibility = Visibility.Collapsed;
        _customersView.Visibility = Visibility.Collapsed;
        _productsView.Visibility = Visibility.Collapsed;
        DashboardPage.Visibility = Visibility.Visible;
        dashboardStatus.IsOpen = false;
        DashboardNavigation.SelectedItem = navDashboard;
    }

    private void ShowCustomers()
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        customersContentHost.Visibility = Visibility.Visible;
        _productsView.Visibility = Visibility.Collapsed;
        _customersView.Visibility = Visibility.Visible;
        _customersView.ActivateView();
        DashboardNavigation.SelectedItem = navClientes;
    }

    private void ShowProducts()
    {
        DashboardPage.Visibility = Visibility.Collapsed;
        customersContentHost.Visibility = Visibility.Visible;
        _customersView.Visibility = Visibility.Collapsed;
        _productsView.Visibility = Visibility.Visible;
        _productsView.ActivateView();
        DashboardNavigation.SelectedItem = navProductos;
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        _ = LoadMetricsAsync();
    }

    private void OnDashboardLoaded(object sender, RoutedEventArgs e)
    {
        if (!_hasLoadedMetrics)
        {
            _ = LoadMetricsAsync();
        }
    }

    private async Task LoadMetricsAsync()
    {
        btnActualizar.IsEnabled = false;
        dashboardStatus.IsOpen = false;

        try
        {
            var metrics = await _dashboardRepository.GetMetricsAsync();
            lblValorClientes.Text = metrics.CustomerCount.ToString("N0", CultureInfo.CurrentCulture);
            lblValorProductos.Text = metrics.ProductCount.ToString("N0", CultureInfo.CurrentCulture);
            lblValorVentas.Text = metrics.SalesCount.ToString("N0", CultureInfo.CurrentCulture);
            lblValorIngresos.Text = metrics.TotalRevenue.ToString("N2", CultureInfo.CurrentCulture);
            lblSubtitulo.Text =
                $"Sesión: {_authenticatedUser.FullName} · {_authenticatedUser.Role}. Indicadores consultados en SQL Server.";
            RenderMonthlyRevenue(metrics.MonthlyRevenue);
            lblDataMode.Text = "SQL SERVER ACTUALIZADO";
            dashboardStatus.Severity = InfoBarSeverity.Success;
            dashboardStatus.Title = "Datos actualizados";
            dashboardStatus.Message = "Los indicadores se leyeron desde SQL Server.";
            dashboardStatus.IsOpen = true;
            _hasLoadedMetrics = true;
        }
        catch (SqlException exception)
        {
            lblDataMode.Text = _hasLoadedMetrics ? "DATOS ANTERIORES" : "ERROR SQL";
            if (!_hasLoadedMetrics)
            {
                salesChart.ItemsSource = null;
                chartEmptyState.Text = "No se pudieron consultar las ventas en SQL Server.";
                chartEmptyState.Visibility = Visibility.Visible;
            }

            dashboardStatus.Severity = InfoBarSeverity.Error;
            dashboardStatus.Title = "No se pudo leer SQL Server";
            dashboardStatus.Message = exception.Message;
            dashboardStatus.IsOpen = true;
        }
        finally
        {
            btnActualizar.IsEnabled = true;
        }
    }

    private void RenderMonthlyRevenue(IReadOnlyList<MonthlyRevenue> monthlyRevenue)
    {
        var maximum = monthlyRevenue.Max(item => item.Total);
        salesChart.ItemsSource = monthlyRevenue
            .Select(item => new RevenueChartBar(
                item.MonthStart.ToString("MMM", CultureInfo.CurrentCulture)
                    .ToUpper(CultureInfo.CurrentCulture),
                maximum > 0
                    ? Math.Max(3, (double)(item.Total / maximum * 118))
                    : 3,
                $"{item.MonthStart.ToString("MMMM yyyy", CultureInfo.CurrentCulture)}: {item.Total:N2}"))
            .ToList();
        chartEmptyState.Visibility = monthlyRevenue.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ShowUnavailableModule(string moduleName)
    {
        dashboardStatus.Severity = InfoBarSeverity.Informational;
        dashboardStatus.Title = "Módulo no migrado";
        dashboardStatus.Message = $"“{moduleName}” está listado en la aplicación original, pero su pantalla y comportamiento no se han migrado.";
        dashboardStatus.IsOpen = true;
    }

    private sealed record RevenueChartBar(string MonthLabel, double BarHeight, string Description);
}
