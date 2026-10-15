using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System.Globalization;
using Windows.Graphics;
using WinRT.Interop;
using Master.WinUI.Data;

namespace Master.WinUI;

public sealed partial class DashboardWindow : Window
{
    private readonly Border[] _kpiCards;
    private readonly DashboardRepository _dashboardRepository;
    private readonly bool _canReadDatabase;
    private bool _hasLoadedMetrics;

    internal DashboardWindow(DashboardRepository dashboardRepository, bool canReadDatabase)
    {
        InitializeComponent();
        _dashboardRepository = dashboardRepository;
        _canReadDatabase = canReadDatabase;

        _kpiCards =
        [
            pnlKpiClientes,
            pnlKpiProductos,
            pnlKpiVentas,
            pnlKpiIngresos
        ];

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow.GetFromWindowId(windowId).Resize(new SizeInt32(1100, 700));
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
                dashboardStatus.IsOpen = false;
                break;
            case "backToLogin":
                Close();
                break;
            default:
                ShowUnavailableModule(item.Content?.ToString() ?? "Este módulo");
                break;
        }
    }

    private void OnModuleToolbarClick(object sender, RoutedEventArgs e)
    {
        if (sender is AppBarButton button)
        {
            ShowUnavailableModule(button.Tag?.ToString() ?? button.Label);
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (!_canReadDatabase)
        {
            dashboardStatus.Severity = InfoBarSeverity.Informational;
            dashboardStatus.Title = "Vista previa";
            dashboardStatus.Message = "Inicia sesión para leer indicadores de SQL Server.";
            dashboardStatus.IsOpen = true;
            return;
        }

        _ = LoadMetricsAsync();
    }

    private void OnDashboardLoaded(object sender, RoutedEventArgs e)
    {
        if (!_canReadDatabase)
        {
            dashboardStatus.Severity = InfoBarSeverity.Informational;
            dashboardStatus.Title = "Vista previa sin datos";
            dashboardStatus.Message = "Inicia sesión para leer indicadores de SQL Server.";
            dashboardStatus.IsOpen = true;
        }
        else if (!_hasLoadedMetrics)
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
            lblSubtitulo.Text = "Indicadores consultados en la base de datos local.";
            dashboardStatus.Severity = InfoBarSeverity.Success;
            dashboardStatus.Title = "Datos actualizados";
            dashboardStatus.Message = "Los indicadores se leyeron desde SQL Server.";
            dashboardStatus.IsOpen = true;
            _hasLoadedMetrics = true;
        }
        catch (SqlException exception)
        {
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

    private void ShowUnavailableModule(string moduleName)
    {
        dashboardStatus.Severity = InfoBarSeverity.Informational;
        dashboardStatus.Title = "Módulo no migrado";
        dashboardStatus.Message = $"“{moduleName}” está listado en la aplicación original, pero su pantalla y comportamiento no se han migrado.";
        dashboardStatus.IsOpen = true;
    }
}
