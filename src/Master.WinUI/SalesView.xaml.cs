using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Master.WinUI.Data;

namespace Master.WinUI;

public sealed partial class SalesView : UserControl
{
    private const decimal MaximumSqlAmount = 9999999999999999.99m;

    private readonly SalesRepository _salesRepository;
    private readonly AuthenticatedUser _authenticatedUser;
    private readonly ObservableCollection<SaleCartLine> _lines = [];
    private SalesCatalog? _catalog;
    private SaleReceipt? _lastReceipt;
    private bool _hasLoadedCatalog;
    private bool _isLoading;
    private bool _isCompleting;
    private bool _hasCompletedSale;
    private bool _hasValidTotals = true;

    internal SalesView(
        SalesRepository salesRepository,
        AuthenticatedUser authenticatedUser)
    {
        InitializeComponent();
        _salesRepository = salesRepository;
        _authenticatedUser = authenticatedUser;
        dtpFechaVenta.Date = DateTimeOffset.Now;
        lstDetalle.ItemsSource = _lines;
        SetDraftInputsEnabled(false);
        UpdateTotals();
        ApplyResponsiveLayout(RootLayout.ActualWidth);
    }

    internal event EventHandler? BackRequested;

    internal void ActivateView()
    {
        if (!_hasLoadedCatalog && !_isLoading)
        {
            _ = LoadCatalogAsync();
        }
    }

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs e) =>
        ApplyResponsiveLayout(e.NewSize.Width);

    private void ApplyResponsiveLayout(double width)
    {
        var headerColumns = width < 540 ? 1 : width < 900 ? 2 : 4;
        ConfigureAdaptiveGrid(
            HeaderFields,
            [CustomerField, PaymentField, DateField, InvoiceField],
            headerColumns);

        var narrow = width < 850;
        ProductEntryGrid.ColumnDefinitions.Clear();
        ProductEntryGrid.RowDefinitions.Clear();
        if (narrow)
        {
            ProductEntryGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
            ProductEntryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ProductEntryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ProductEntryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ProductEntryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(ProductField, 0);
            Grid.SetColumn(ProductField, 0);
            Grid.SetRow(QuantityField, 1);
            Grid.SetColumn(QuantityField, 0);
            Grid.SetRow(btnAgregarItem, 2);
            Grid.SetColumn(btnAgregarItem, 0);
            Grid.SetRow(lblStockInfo, 3);
            Grid.SetColumn(lblStockInfo, 0);
            Grid.SetColumnSpan(lblStockInfo, 1);
        }
        else
        {
            ProductEntryGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(2, GridUnitType.Star)
            });
            ProductEntryGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
            ProductEntryGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
            ProductEntryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ProductEntryGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(ProductField, 0);
            Grid.SetColumn(ProductField, 0);
            Grid.SetRow(QuantityField, 0);
            Grid.SetColumn(QuantityField, 1);
            Grid.SetRow(btnAgregarItem, 0);
            Grid.SetColumn(btnAgregarItem, 2);
            Grid.SetRow(lblStockInfo, 1);
            Grid.SetColumn(lblStockInfo, 0);
            Grid.SetColumnSpan(lblStockInfo, 3);
        }

        var actionColumns = width < 520 ? 1 : width < 900 ? 2 : 4;
        ConfigureAdaptiveGrid(
            ActionButtons,
            [btnNuevaVenta, btnQuitarItem, btnGenerarTicket, btnRegistrarVenta],
            actionColumns);

        var compactTotals = width < 760;
        TotalsLayout.ColumnDefinitions.Clear();
        TotalsLayout.RowDefinitions.Clear();
        TotalsLayout.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });
        if (compactTotals)
        {
            TotalsLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TotalsLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(lblStockPolicy, 0);
            Grid.SetColumn(lblStockPolicy, 0);
            Grid.SetRow(TotalsPanel, 1);
            Grid.SetColumn(TotalsPanel, 0);
            TotalsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            TotalsLayout.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
            TotalsLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(lblStockPolicy, 0);
            Grid.SetColumn(lblStockPolicy, 0);
            Grid.SetRow(TotalsPanel, 0);
            Grid.SetColumn(TotalsPanel, 1);
            TotalsPanel.HorizontalAlignment = HorizontalAlignment.Right;
        }
    }

    private static void ConfigureAdaptiveGrid(
        Grid grid,
        IReadOnlyList<FrameworkElement> children,
        int columnCount)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        for (var column = 0; column < columnCount; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
        }

        var rowCount = (children.Count + columnCount - 1) / columnCount;
        for (var row = 0; row < rowCount; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 0; index < children.Count; index++)
        {
            Grid.SetRow(children[index], index / columnCount);
            Grid.SetColumn(children[index], index % columnCount);
        }
    }

    private async Task LoadCatalogAsync()
    {
        _isLoading = true;
        ShowStatus(
            InfoBarSeverity.Informational,
            "Cargando catálogo",
            "Consultando clientes, productos y configuración en SQL Server.");
        btnAgregarItem.IsEnabled = false;
        try
        {
            var catalog = await _salesRepository.GetCatalogAsync();
            _catalog = catalog;
            cmbCliente.ItemsSource = catalog.Customers;
            cmbMetodoPago.ItemsSource = catalog.PaymentMethods;
            cmbProducto.ItemsSource = catalog.Products;
            lblImpuesto.Text = $"ITBIS/IVA {catalog.TaxRate:N2}%:";
            _hasLoadedCatalog = true;

            var unavailableCatalogs = new List<string>();
            if (catalog.Customers.Count == 0)
            {
                unavailableCatalogs.Add("clientes activos");
            }

            if (catalog.Products.Count == 0)
            {
                unavailableCatalogs.Add("productos activos");
            }

            if (catalog.PaymentMethods.Count == 0)
            {
                unavailableCatalogs.Add("métodos de pago configurados");
            }

            if (unavailableCatalogs.Count > 0)
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    "Catálogo incompleto",
                    $"No se encontraron {string.Join(", ", unavailableCatalogs)} en SQL Server.");
            }
            else
            {
                ShowStatus(
                    InfoBarSeverity.Success,
                    "Catálogo cargado",
                    "Clientes, productos, métodos de pago y configuración fiscal se leyeron desde SQL Server.");
            }
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (InvalidDataException exception)
        {
            ShowStatus(InfoBarSeverity.Error, "Configuración incompleta", exception.Message);
        }
        finally
        {
            _isLoading = false;
            UpdateDraftControls();
            UpdateCompleteButton();
            UpdateTotals();
        }
    }

    private void OnBackToDashboardClick(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void OnNewSaleClick(object sender, RoutedEventArgs e) => StartNewSale();

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateCompleteButton();

    private void OnProductSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbProducto.SelectedItem is SaleProduct product && _catalog is not null)
        {
            lblStockInfo.Text =
                $"Disp: {product.Stock:N0} unid. | {_catalog.CurrencyCode} {product.UnitPrice:N2}.";
        }
        else
        {
            lblStockInfo.Text = "Seleccione un producto para consultar precio y existencia.";
        }
    }

    private void OnAddItemClick(object sender, RoutedEventArgs e)
    {
        if (_hasCompletedSale)
        {
            return;
        }

        if (cmbProducto.SelectedItem is not SaleProduct product)
        {
            ShowStatus(InfoBarSeverity.Warning, "Producto requerido", "Selecciona un producto.");
            return;
        }

        if (!double.IsFinite(nudCantidad.Value) ||
            nudCantidad.Value < 1 ||
            nudCantidad.Value > int.MaxValue ||
            nudCantidad.Value != Math.Truncate(nudCantidad.Value))
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                "Cantidad no válida",
                "La cantidad debe ser un número entero mayor que cero.");
            return;
        }

        var quantity = (int)nudCantidad.Value;
        var existingIndex = _lines
            .Select((line, index) => (line, index))
            .Where(item => item.line.ProductId == product.Id)
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();

        try
        {
            if (existingIndex >= 0)
            {
                var existing = _lines[existingIndex];
                var combinedQuantity = checked(existing.Quantity + quantity);
                var combinedSubtotal = checked(existing.UnitPrice * combinedQuantity);
                _lines[existingIndex] = existing with
                {
                    Quantity = combinedQuantity,
                    Subtotal = combinedSubtotal
                };
            }
            else
            {
                _lines.Add(new SaleCartLine(
                    product.Id,
                    product.Code,
                    product.Name,
                    quantity,
                    product.UnitPrice,
                    checked(product.UnitPrice * quantity),
                    _catalog?.CurrencyCode ?? string.Empty));
            }
        }
        catch (OverflowException)
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                "Cantidad fuera de rango",
                "La cantidad combinada con este producto excede el límite admitido.");
            return;
        }

        cmbProducto.SelectedIndex = -1;
        nudCantidad.Value = 1;
        lblStockInfo.Text = "Seleccione un producto para consultar precio y existencia.";
        salesStatus.IsOpen = false;
        UpdateCartState();
        UpdateTotals();
    }

    private void OnLineSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        btnQuitarItem.IsEnabled = lstDetalle.SelectedItem is SaleCartLine && !_hasCompletedSale;

    private void OnRemoveItemClick(object sender, RoutedEventArgs e)
    {
        if (!_hasCompletedSale && lstDetalle.SelectedItem is SaleCartLine line)
        {
            _lines.Remove(line);
            UpdateCartState();
            UpdateTotals();
        }
    }

    private async void OnCompleteSaleClick(object sender, RoutedEventArgs e)
    {
        if (_catalog is null)
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                "Catálogo no disponible",
                "No se puede completar la factura hasta cargar los datos requeridos.");
            return;
        }

        if (_isCompleting || _hasCompletedSale || !_hasValidTotals)
        {
            return;
        }

        if (cmbCliente.SelectedItem is not SaleCustomer customer)
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                "Cliente requerido",
                "Selecciona un cliente receptor para la factura.");
            return;
        }

        if (cmbMetodoPago.SelectedItem is not string paymentMethod ||
            string.IsNullOrWhiteSpace(paymentMethod))
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                "Método de pago requerido",
                "Selecciona uno de los métodos de pago existentes.");
            return;
        }

        if (_lines.Count == 0)
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                "Factura vacía",
                "Agrega al menos un producto antes de completar la factura.");
            return;
        }

        var saleDate = dtpFechaVenta.Date.DateTime.Date;
        var confirmation = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Completar factura",
            Content =
                $"Registrar la venta para {customer.DisplayName} por " +
                $"{lblValorTotal.Text}. " +
                "Esta acción insertará la venta y su detalle en SQL Server.",
            PrimaryButtonText = "Completar factura",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var draft = new SaleDraft(
            customer.Id,
            _authenticatedUser.Id,
            saleDate,
            paymentMethod,
            string.IsNullOrWhiteSpace(txtObservaciones.Text)
                ? null
                : txtObservaciones.Text.Trim(),
            _catalog.TaxRate,
            _lines.Select(line => new SaleLineDraft(
                line.ProductId,
                line.Quantity,
                line.UnitPrice)).ToArray());

        _isCompleting = true;
        UpdateDraftControls();
        UpdateCompleteButton();
        salesStatus.IsOpen = false;
        try
        {
            var receipt = await _salesRepository.CompleteSaleAsync(draft);
            _lastReceipt = receipt;
            _hasCompletedSale = true;
            txtNumeroFactura.Text = receipt.InvoiceNumber;
            SetDraftInputsEnabled(false);
            btnGenerarTicket.IsEnabled = true;
            ShowStatus(
                InfoBarSeverity.Success,
                "Factura registrada",
                $"La factura {receipt.InvoiceNumber} y sus detalles se guardaron en SQL Server. El stock no se modificó.");
            UpdateCartState();
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (InvalidDataException exception)
        {
            ShowStatus(InfoBarSeverity.Error, "Configuración inválida", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(InfoBarSeverity.Warning, "No se pudo completar la factura", exception.Message);
        }
        catch (OverflowException exception)
        {
            ShowStatus(InfoBarSeverity.Error, "Importe fuera de rango", exception.Message);
        }
        finally
        {
            _isCompleting = false;
            UpdateDraftControls();
            UpdateCompleteButton();
        }
    }

    private async void OnShowTicketClick(object sender, RoutedEventArgs e)
    {
        if (_lastReceipt is not { } receipt)
        {
            return;
        }

        var ticket = new StackPanel { Spacing = 8 };
        ticket.Children.Add(new TextBlock
        {
            Text = $"Comprobante: {receipt.InvoiceNumber}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        ticket.Children.Add(new TextBlock
        {
            Text = $"Fecha: {receipt.SaleDate.ToString("d", CultureInfo.CurrentCulture)}"
        });
        ticket.Children.Add(new TextBlock { Text = $"Cliente: {receipt.CustomerName}" });
        ticket.Children.Add(new TextBlock { Text = $"Método de pago: {receipt.PaymentMethod}" });
        foreach (var line in receipt.Lines)
        {
            ticket.Children.Add(new TextBlock
            {
                Text =
                    $"{line.ProductCode} · {line.ProductName} × {line.Quantity}: " +
                    $"{receipt.CurrencyCode} {line.Subtotal:N2}",
                TextWrapping = TextWrapping.Wrap
            });
        }

        ticket.Children.Add(new TextBlock
        {
            Text = $"Subtotal: {receipt.CurrencyCode} {receipt.Subtotal:N2}"
        });
        ticket.Children.Add(new TextBlock
        {
            Text = $"ITBIS/IVA {receipt.TaxRate:N2}%: {receipt.CurrencyCode} {receipt.Tax:N2}"
        });
        ticket.Children.Add(new TextBlock
        {
            Text = $"Total: {receipt.CurrencyCode} {receipt.Total:N2}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        ticket.Children.Add(new TextBlock
        {
            Text = "Vista previa local. La integración con impresoras no está implementada.",
            TextWrapping = TextWrapping.Wrap
        });

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Vista previa del ticket",
            Content = ticket,
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
    }

    private void StartNewSale()
    {
        _lastReceipt = null;
        _hasCompletedSale = false;
        _lines.Clear();
        cmbCliente.SelectedIndex = -1;
        cmbMetodoPago.SelectedIndex = -1;
        cmbProducto.SelectedIndex = -1;
        dtpFechaVenta.Date = DateTimeOffset.Now;
        txtNumeroFactura.Text = "Se asignará al completar";
        txtObservaciones.Text = string.Empty;
        nudCantidad.Value = 1;
        lblStockInfo.Text = "Seleccione un producto para consultar precio y existencia.";
        salesStatus.IsOpen = false;
        btnGenerarTicket.IsEnabled = false;
        SetDraftInputsEnabled(_hasLoadedCatalog);
        UpdateCartState();
        UpdateTotals();
        UpdateCompleteButton();
    }

    private void UpdateCartState()
    {
        lblEmptyCart.Visibility = _lines.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        lblConteoDetalle.Text = $"{_lines.Count} elemento(s)";
        btnQuitarItem.IsEnabled =
            lstDetalle.SelectedItem is SaleCartLine && !_hasCompletedSale;
    }

    private void UpdateTotals()
    {
        try
        {
            var subtotal = _lines.Aggregate(
                0m,
                (total, line) => checked(total + line.Subtotal));
            var taxRate = _catalog?.TaxRate ?? 0;
            var tax = decimal.Round(subtotal * taxRate / 100m, 2, MidpointRounding.AwayFromZero);
            var total = checked(subtotal + tax);
            if (subtotal > MaximumSqlAmount || tax > MaximumSqlAmount || total > MaximumSqlAmount)
            {
                throw new OverflowException();
            }

            _hasValidTotals = true;
            lblValorSubtotal.Text = FormatMoney(subtotal);
            lblValorImpuesto.Text = FormatMoney(tax);
            lblValorTotal.Text = FormatMoney(total);
            UpdateCompleteButton();
        }
        catch (OverflowException)
        {
            _hasValidTotals = false;
            lblValorSubtotal.Text = "—";
            lblValorImpuesto.Text = "—";
            lblValorTotal.Text = "—";
            ShowStatus(
                InfoBarSeverity.Warning,
                "Importe fuera de rango",
                "El total de la factura excede el límite admitido por SQL Server.");
            UpdateCompleteButton();
        }
    }

    private string FormatMoney(decimal value) =>
        _catalog is null
            ? value.ToString("N2", CultureInfo.CurrentCulture)
            : $"{_catalog.CurrencyCode} {value:N2}";

    private void UpdateCompleteButton()
    {
        btnRegistrarVenta.IsEnabled =
            _hasLoadedCatalog &&
            !_isLoading &&
            !_isCompleting &&
            !_hasCompletedSale &&
            _hasValidTotals &&
            cmbCliente.SelectedItem is SaleCustomer &&
            cmbMetodoPago.SelectedItem is string &&
            _lines.Count > 0;
    }

    private void UpdateDraftControls()
    {
        SetDraftInputsEnabled(_hasLoadedCatalog && !_isCompleting && !_hasCompletedSale);
        btnNuevaVenta.IsEnabled = !_isCompleting;
        btnQuitarItem.IsEnabled =
            !_isCompleting && !_hasCompletedSale && lstDetalle.SelectedItem is SaleCartLine;
        btnGenerarTicket.IsEnabled = !_isCompleting && _lastReceipt is not null;
    }

    private void SetDraftInputsEnabled(bool enabled)
    {
        cmbCliente.IsEnabled = enabled && cmbCliente.Items.Count > 0;
        cmbMetodoPago.IsEnabled = enabled && cmbMetodoPago.Items.Count > 0;
        dtpFechaVenta.IsEnabled = enabled;
        cmbProducto.IsEnabled = enabled && cmbProducto.Items.Count > 0;
        nudCantidad.IsEnabled = enabled;
        btnAgregarItem.IsEnabled =
            enabled && _hasLoadedCatalog && cmbProducto.Items.Count > 0;
        txtObservaciones.IsEnabled = enabled;
        lstDetalle.IsEnabled = enabled;
    }

    private void ShowDatabaseError(SqlException exception)
    {
        if (exception.Number is 2601 or 2627)
        {
            ShowStatus(
                InfoBarSeverity.Error,
                "Comprobante duplicado",
                "El número de comprobante ya existe. Inicia una factura nueva y vuelve a intentarlo.");
            return;
        }

        if (exception.Number == 547)
        {
            ShowStatus(
                InfoBarSeverity.Error,
                "Datos relacionados no disponibles",
                "Un cliente o producto cambió mientras se preparaba la factura. No se guardó la transacción.");
            return;
        }

        ShowStatus(InfoBarSeverity.Error, "No se pudo acceder a SQL Server", exception.Message);
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        salesStatus.Severity = severity;
        salesStatus.Title = title;
        salesStatus.Message = message;
        salesStatus.IsOpen = true;
    }
}
