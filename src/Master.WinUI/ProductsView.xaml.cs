using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Master.WinUI.Data;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace Master.WinUI;

public sealed partial class ProductsView : UserControl
{
    private readonly ProductRepository _productRepository;
    private readonly IntPtr _windowHandle;
    private List<Product> _allProducts = [];
    private List<Product> _visibleProducts = [];
    private List<ProductCategory> _categories = [];
    private Product? _selectedProduct;
    private string? _imageBase64;
    private bool _isNewProduct = true;
    private bool _suppressSelectionChanged;
    private bool _hasLoadedProducts;
    private bool _isLoading;

    internal ProductsView(ProductRepository productRepository, IntPtr windowHandle)
    {
        InitializeComponent();
        _productRepository = productRepository;
        _windowHandle = windowHandle;
        btnEliminar.IsEnabled = false;
        btnQuitarImagen.IsEnabled = false;
    }

    internal event EventHandler? BackRequested;

    internal void ActivateView()
    {
        if (_hasLoadedProducts || _isLoading)
        {
            return;
        }

        _ = ReloadProductsAsync();
    }

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 900;
        ContentLayout.ColumnDefinitions[1].Width = compact
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(pnlFormulario, compact ? 0 : 1);
        Grid.SetRow(pnlFormulario, compact ? 1 : 0);
        ContentLayout.RowDefinitions[0].Height = compact
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);
        ContentLayout.RowDefinitions[1].Height = compact
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);

        var narrow = e.NewSize.Width < 760;
        if (narrow && FilterBar.RowDefinitions.Count == 1)
        {
            FilterBar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FilterBar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumnSpan(txtBuscar, 4);
            Grid.SetRow(cmbFiltroCategoria, 1);
            Grid.SetColumn(cmbFiltroCategoria, 0);
            Grid.SetColumnSpan(cmbFiltroCategoria, 4);
            Grid.SetRow(btnBuscar, 2);
            Grid.SetColumn(btnBuscar, 0);
            Grid.SetRow(btnLimpiarFiltro, 2);
            Grid.SetColumn(btnLimpiarFiltro, 1);
        }
        else if (!narrow && FilterBar.RowDefinitions.Count > 1)
        {
            FilterBar.RowDefinitions.RemoveAt(2);
            FilterBar.RowDefinitions.RemoveAt(1);
            Grid.SetColumnSpan(txtBuscar, 1);
            Grid.SetRow(cmbFiltroCategoria, 0);
            Grid.SetColumn(cmbFiltroCategoria, 1);
            Grid.SetColumnSpan(cmbFiltroCategoria, 1);
            Grid.SetRow(btnBuscar, 0);
            Grid.SetColumn(btnBuscar, 2);
            Grid.SetRow(btnLimpiarFiltro, 0);
            Grid.SetColumn(btnLimpiarFiltro, 3);
        }
    }

    private void OnFilterClick(object sender, RoutedEventArgs e) => ApplyFilters();

    private void OnClearFilterClick(object sender, RoutedEventArgs e)
    {
        txtBuscar.Text = string.Empty;
        cmbFiltroCategoria.SelectedIndex = 0;
        ApplyFilters();
    }

    private void OnProductSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged || lstProductos.SelectedItem is not Product product)
        {
            return;
        }

        _selectedProduct = product;
        _isNewProduct = false;
        PopulateForm(product);
        btnGuardar.Content = "Actualizar";
        btnEliminar.IsEnabled = true;
        _ = ShowImageAsync(product.ImageBase64);
    }

    private void OnNewClick(object sender, RoutedEventArgs e) => StartNewProduct();

    private void OnBackToDashboardClick(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct is not null)
        {
            PopulateForm(_selectedProduct);
            _ = ShowImageAsync(_selectedProduct.ImageBase64);
            return;
        }

        StartNewProduct();
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var draft = ReadDraft(out var validationMessage);
        if (draft is null)
        {
            ShowStatus(InfoBarSeverity.Warning, "Revisa los datos", validationMessage);
            return;
        }

        btnGuardar.IsEnabled = false;
        try
        {
            int? savedId = null;
            if (_isNewProduct)
            {
                savedId = await _productRepository.InsertAsync(draft);
            }
            else if (_selectedProduct is not null)
            {
                savedId = _selectedProduct.Id;
                await _productRepository.UpdateAsync(_selectedProduct.Id, draft);
            }
            else
            {
                throw new InvalidOperationException(
                    "Selecciona un producto antes de intentar actualizarlo.");
            }

            ShowStatus(
                InfoBarSeverity.Success,
                _isNewProduct ? "Producto guardado" : "Producto actualizado",
                "Los datos del producto se guardaron en SQL Server.");
            await ReloadProductsAsync(savedId);
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(InfoBarSeverity.Warning, "No se pudo actualizar el producto", exception.Message);
        }
        finally
        {
            btnGuardar.IsEnabled = true;
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_selectedProduct is null)
        {
            return;
        }

        var confirmation = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Eliminar producto",
            Content = $"¿Eliminar «{_selectedProduct.Name}»? Esta acción no se puede deshacer.",
            PrimaryButtonText = "Eliminar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        btnEliminar.IsEnabled = false;
        try
        {
            await _productRepository.DeleteAsync(_selectedProduct.Id);
            StartNewProduct();
            ShowStatus(InfoBarSeverity.Success, "Producto eliminado", "El registro se eliminó de SQL Server.");
            await ReloadProductsAsync();
        }
        catch (SqlException exception)
        {
            if (exception.Number == 547)
            {
                ShowStatus(
                    InfoBarSeverity.Error,
                    "No se puede eliminar el producto",
                    "El producto está relacionado con otros registros y no se eliminó.");
            }
            else
            {
                ShowDatabaseError(exception);
            }
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(InfoBarSeverity.Warning, "No se pudo eliminar el producto", exception.Message);
        }
        finally
        {
            btnEliminar.IsEnabled = _selectedProduct is not null;
        }
    }

    private async void OnLoadImageClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        InitializeWithWindow.Initialize(picker, _windowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var buffer = await FileIO.ReadBufferAsync(file);
            var imageBytes = new byte[checked((int)buffer.Length)];
            using (var reader = DataReader.FromBuffer(buffer))
            {
                reader.ReadBytes(imageBytes);
            }

            await SetImagePreviewAsync(imageBytes);
            _imageBase64 = Convert.ToBase64String(imageBytes);
            btnQuitarImagen.IsEnabled = true;
            lblImagen.Text = file.Name;
            ShowStatus(
                InfoBarSeverity.Informational,
                "Imagen seleccionada",
                "La imagen se guardará en SQL Server al guardar el producto.");
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or
                System.Runtime.InteropServices.COMException)
        {
            ShowStatus(InfoBarSeverity.Error, "No se pudo cargar la imagen", exception.Message);
        }
    }

    private void OnRemoveImageClick(object sender, RoutedEventArgs e)
    {
        _imageBase64 = null;
        imgProducto.Source = null;
        lblImagen.Text = "Sin imagen seleccionada.";
        btnQuitarImagen.IsEnabled = false;
        ShowStatus(
            InfoBarSeverity.Informational,
            "Imagen quitada",
            "El cambio se aplicará en SQL Server al guardar el producto.");
    }

    private async Task ReloadProductsAsync(int? selectedProductId = null)
    {
        if (_isLoading)
        {
            return;
        }

        _isLoading = true;
        btnBuscar.IsEnabled = false;
        try
        {
            var selectedId = selectedProductId ?? _selectedProduct?.Id;
            _categories = (await _productRepository.GetCategoriesAsync()).ToList();
            _allProducts = (await _productRepository.GetAllAsync()).ToList();
            _hasLoadedProducts = true;
            RefreshCategoryOptions();
            ApplyFilters();

            if (selectedId is not null)
            {
                lstProductos.SelectedItem = _visibleProducts.FirstOrDefault(
                    product => product.Id == selectedId);
            }

            if (!productStatus.IsOpen)
            {
                ShowStatus(
                    InfoBarSeverity.Success,
                    "Productos actualizados",
                    $"Se cargaron {_allProducts.Count} registros desde SQL Server.");
            }
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        finally
        {
            btnBuscar.IsEnabled = true;
            _isLoading = false;
        }
    }

    private void RefreshCategoryOptions()
    {
        var selectedCategoryId = (cmbFiltroCategoria.SelectedItem as CategoryFilterOption)?.Id;
        var options = new List<CategoryFilterOption>
        {
            new(null, "Todas las categorías")
        };
        options.AddRange(_categories.Select(category =>
            new CategoryFilterOption(category.Id, category.Name)));
        cmbFiltroCategoria.ItemsSource = options;
        cmbFiltroCategoria.SelectedItem = options.FirstOrDefault(
            option => option.Id == selectedCategoryId) ?? options[0];
        cmbCategoria.ItemsSource = _categories;
    }

    private void ApplyFilters()
    {
        var search = txtBuscar.Text.Trim();
        var categoryId = (cmbFiltroCategoria.SelectedItem as CategoryFilterOption)?.Id;
        _visibleProducts = _allProducts
            .Where(product =>
                (categoryId is null || product.CategoryId == categoryId) &&
                (string.IsNullOrEmpty(search) ||
                 product.Code.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 product.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 (product.Description?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)))
            .ToList();

        _suppressSelectionChanged = true;
        lstProductos.ItemsSource = _visibleProducts;
        if (_selectedProduct is not null &&
            _visibleProducts.FirstOrDefault(product => product.Id == _selectedProduct.Id) is { } selected)
        {
            _selectedProduct = selected;
            lstProductos.SelectedItem = selected;
        }
        else
        {
            lstProductos.SelectedItem = null;
            _selectedProduct = null;
            _isNewProduct = true;
            ClearForm();
            btnGuardar.Content = "Guardar";
            btnEliminar.IsEnabled = false;
        }

        _suppressSelectionChanged = false;
        lblConteo.Text = $"{_visibleProducts.Count} de {_allProducts.Count}";
    }

    private void StartNewProduct()
    {
        _suppressSelectionChanged = true;
        lstProductos.SelectedItem = null;
        _suppressSelectionChanged = false;
        _selectedProduct = null;
        _isNewProduct = true;
        ClearForm();
        btnGuardar.Content = "Guardar";
        btnEliminar.IsEnabled = false;
    }

    private void ClearForm()
    {
        txtCodigo.Text = string.Empty;
        txtNombre.Text = string.Empty;
        txtDescripcion.Text = string.Empty;
        cmbCategoria.SelectedIndex = -1;
        nudPrecio.Value = 0;
        nudStock.Value = 0;
        chkActivo.IsChecked = true;
        _imageBase64 = null;
        imgProducto.Source = null;
        lblImagen.Text = "Sin imagen seleccionada.";
        btnQuitarImagen.IsEnabled = false;
    }

    private void PopulateForm(Product product)
    {
        txtCodigo.Text = product.Code;
        txtNombre.Text = product.Name;
        txtDescripcion.Text = product.Description ?? string.Empty;
        cmbCategoria.SelectedItem = _categories.FirstOrDefault(
            category => category.Id == product.CategoryId);
        nudPrecio.Value = (double)product.UnitPrice;
        nudStock.Value = product.Stock;
        chkActivo.IsChecked = product.IsActive;
        _imageBase64 = product.ImageBase64;
        btnQuitarImagen.IsEnabled = !string.IsNullOrEmpty(product.ImageBase64);
    }

    private ProductDraft? ReadDraft(out string validationMessage)
    {
        var code = txtCodigo.Text.Trim();
        var name = txtNombre.Text.Trim();
        var description = txtDescripcion.Text.Trim();

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            validationMessage = "El código y el nombre del producto son obligatorios.";
            return null;
        }

        if (cmbCategoria.SelectedItem is not ProductCategory category)
        {
            validationMessage = "Selecciona una categoría existente.";
            return null;
        }

        if (!double.IsFinite(nudPrecio.Value) ||
            !decimal.TryParse(
                nudPrecio.Value.ToString("R", CultureInfo.InvariantCulture),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var price) ||
            price < 0 ||
            price > 9999999999999999.99m ||
            decimal.Round(price, 2) != price)
        {
            validationMessage = "El precio debe ser un número no negativo con hasta dos decimales.";
            return null;
        }

        if (!double.IsFinite(nudStock.Value) ||
            nudStock.Value < 0 ||
            nudStock.Value > int.MaxValue ||
            nudStock.Value != Math.Truncate(nudStock.Value))
        {
            validationMessage = "El stock debe ser un entero no negativo.";
            return null;
        }

        validationMessage = string.Empty;
        return new ProductDraft(
            category.Id,
            code,
            name,
            string.IsNullOrEmpty(description) ? null : description,
            price,
            (int)nudStock.Value,
            _imageBase64,
            chkActivo.IsChecked == true);
    }

    private async Task ShowImageAsync(string? imageBase64)
    {
        _imageBase64 = imageBase64;
        if (string.IsNullOrWhiteSpace(imageBase64))
        {
            imgProducto.Source = null;
            lblImagen.Text = "Sin imagen seleccionada.";
            btnQuitarImagen.IsEnabled = false;
            return;
        }

        try
        {
            var imageBytes = Convert.FromBase64String(imageBase64);
            await SetImagePreviewAsync(imageBytes);
            lblImagen.Text = "Imagen guardada.";
            btnQuitarImagen.IsEnabled = true;
        }
        catch (FormatException exception)
        {
            imgProducto.Source = null;
            lblImagen.Text = "Imagen guardada no válida.";
            btnQuitarImagen.IsEnabled = true;
            ShowStatus(InfoBarSeverity.Warning, "Imagen no válida", exception.Message);
        }
        catch (Exception exception) when (
            exception is IOException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            imgProducto.Source = null;
            lblImagen.Text = "No se pudo mostrar la imagen guardada.";
            btnQuitarImagen.IsEnabled = true;
            ShowStatus(InfoBarSeverity.Warning, "No se pudo mostrar la imagen", exception.Message);
        }
    }

    private async Task SetImagePreviewAsync(byte[] imageBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(imageBytes);
            await writer.StoreAsync();
            _ = writer.DetachStream();
        }

        stream.Seek(0);
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        imgProducto.Source = bitmap;
    }

    private void ShowDatabaseError(SqlException exception)
    {
        if (exception.Number is 2601 or 2627)
        {
            ShowStatus(
                InfoBarSeverity.Error,
                "Código duplicado",
                "Ya existe un producto con ese código.");
            return;
        }

        ShowStatus(InfoBarSeverity.Error, "No se pudo acceder a SQL Server", exception.Message);
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        productStatus.Severity = severity;
        productStatus.Title = title;
        productStatus.Message = message;
        productStatus.IsOpen = true;
    }

    private sealed record CategoryFilterOption(int? Id, string Name);
}
