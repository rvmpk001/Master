using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualBasic.FileIO;
using Master.WinUI.Data;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using WinRT.Interop;

namespace Master.WinUI;

public sealed partial class CustomersView : UserControl
{
    private readonly CustomerRepository _customerRepository;
    private readonly bool _canAccessDatabase;
    private readonly IntPtr _windowHandle;
    private List<Customer> _allCustomers = [];
    private List<Customer> _visibleCustomers = [];
    private Customer? _selectedCustomer;
    private bool _isNewCustomer = true;
    private bool _suppressSelectionChanged;
    private bool _hasBeenActivated;

    internal CustomersView(
        CustomerRepository customerRepository,
        bool canAccessDatabase,
        IntPtr windowHandle)
    {
        InitializeComponent();
        _customerRepository = customerRepository;
        _canAccessDatabase = canAccessDatabase;
        _windowHandle = windowHandle;
        cmbFiltroCiudad.Items.Add("Todas las ciudades");
        cmbFiltroCiudad.SelectedIndex = 0;
        btnGuardar.IsEnabled = canAccessDatabase;
        btnExportarCsv.IsEnabled = canAccessDatabase;
        btnImportarCsv.IsEnabled = canAccessDatabase;
        btnEliminar.IsEnabled = false;
    }

    internal event EventHandler? BackRequested;

    internal void ActivateView()
    {
        if (_hasBeenActivated)
        {
            return;
        }

        _hasBeenActivated = true;
        if (!_canAccessDatabase)
        {
            ShowStatus(
                InfoBarSeverity.Informational,
                "Vista previa sin acceso a datos",
                "Inicia sesión correctamente para consultar, guardar, importar o exportar clientes.");
            return;
        }

        _ = ReloadCustomersAsync();
    }

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 900;
        ContentLayout.ColumnDefinitions[1].Width = compact
            ? new GridLength(0)
            : new GridLength(1.2, GridUnitType.Star);
        Grid.SetColumn(pnlFormulario, compact ? 0 : 1);
        Grid.SetRow(pnlFormulario, compact ? 1 : 0);
        ContentLayout.RowDefinitions[0].Height = compact
            ? new GridLength(1.1, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);
        ContentLayout.RowDefinitions[1].Height = compact
            ? new GridLength(1.5, GridUnitType.Star)
            : new GridLength(0);

        var narrow = e.NewSize.Width < 760;
        if (narrow && FilterBar.RowDefinitions.Count == 1)
        {
            FilterBar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumnSpan(txtBuscar, 3);
            Grid.SetColumn(cmbFiltroCiudad, 3);
            Grid.SetColumnSpan(cmbFiltroCiudad, 3);
            Grid.SetRow(btnBuscar, 1);
            Grid.SetColumn(btnBuscar, 0);
            Grid.SetRow(btnLimpiarFiltro, 1);
            Grid.SetColumn(btnLimpiarFiltro, 1);
            Grid.SetRow(btnExportarCsv, 1);
            Grid.SetColumn(btnExportarCsv, 2);
            Grid.SetRow(btnImportarCsv, 1);
            Grid.SetColumn(btnImportarCsv, 3);
        }
        else if (!narrow && FilterBar.RowDefinitions.Count > 1)
        {
            FilterBar.RowDefinitions.RemoveAt(1);
            Grid.SetColumnSpan(txtBuscar, 1);
            Grid.SetColumn(cmbFiltroCiudad, 1);
            Grid.SetColumnSpan(cmbFiltroCiudad, 1);
            Grid.SetRow(btnBuscar, 0);
            Grid.SetColumn(btnBuscar, 2);
            Grid.SetRow(btnLimpiarFiltro, 0);
            Grid.SetColumn(btnLimpiarFiltro, 3);
            Grid.SetRow(btnExportarCsv, 0);
            Grid.SetColumn(btnExportarCsv, 4);
            Grid.SetRow(btnImportarCsv, 0);
            Grid.SetColumn(btnImportarCsv, 5);
        }
    }

    private void OnFilterClick(object sender, RoutedEventArgs e) => ApplyFilters();

    private void OnClearFilterClick(object sender, RoutedEventArgs e)
    {
        txtBuscar.Text = string.Empty;
        cmbFiltroCiudad.SelectedIndex = 0;
        ApplyFilters();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        await ReloadCustomersAsync();
    }

    private void OnCustomerSelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged || lstClientes.SelectedItem is not Customer customer)
        {
            return;
        }

        _selectedCustomer = customer;
        _isNewCustomer = false;
        PopulateForm(customer);
        btnGuardar.Content = "Actualizar";
        btnGuardar.IsEnabled = _canAccessDatabase;
        btnEliminar.IsEnabled = _canAccessDatabase;
    }

    private void OnNewClick(object sender, RoutedEventArgs e)
    {
        StartNewCustomer();
    }

    private void OnBackToDashboardClick(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_selectedCustomer is not null)
        {
            PopulateForm(_selectedCustomer);
            return;
        }

        StartNewCustomer();
    }

    private void OnIncludeDateChanged(object sender, RoutedEventArgs e)
    {
        if (dtpFechaNacimiento is not null && chkIncluirFecha is not null)
        {
            dtpFechaNacimiento.IsEnabled = chkIncluirFecha.IsChecked == true;
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!_canAccessDatabase)
        {
            ShowPreviewRestriction();
            return;
        }

        var draft = ReadDraft(out var validationMessage);
        if (draft is null)
        {
            ShowStatus(InfoBarSeverity.Warning, "Revisa los datos", validationMessage);
            return;
        }

        btnGuardar.IsEnabled = false;
        try
        {
            var isNewCustomer = _isNewCustomer;
            if (isNewCustomer)
            {
                await _customerRepository.InsertAsync(draft);
            }
            else if (_selectedCustomer is not null)
            {
                await _customerRepository.UpdateAsync(_selectedCustomer.Id, draft);
            }
            else
            {
                throw new InvalidOperationException(
                    "Selecciona un cliente antes de intentar actualizarlo.");
            }

            ShowStatus(
                InfoBarSeverity.Success,
                isNewCustomer ? "Cliente guardado" : "Cliente actualizado",
                "Los datos del cliente se guardaron en SQL Server.");
            if (isNewCustomer)
            {
                StartNewCustomer();
            }

            await ReloadCustomersAsync();
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(InfoBarSeverity.Warning, "No se pudo actualizar el cliente", exception.Message);
        }
        finally
        {
            btnGuardar.IsEnabled = _canAccessDatabase;
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (!_canAccessDatabase || _selectedCustomer is null)
        {
            return;
        }

        var confirmation = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Eliminar cliente",
            Content = $"¿Eliminar a {_selectedCustomer.FullName}? Esta acción no se puede deshacer.",
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
            await _customerRepository.DeleteAsync(_selectedCustomer.Id);
            StartNewCustomer();
            ShowStatus(InfoBarSeverity.Success, "Cliente eliminado", "El registro se eliminó de SQL Server.");
            await ReloadCustomersAsync();
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(InfoBarSeverity.Warning, "No se pudo eliminar el cliente", exception.Message);
        }
        finally
        {
            btnEliminar.IsEnabled = _canAccessDatabase && _selectedCustomer is not null;
        }
    }

    private async void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        if (!_canAccessDatabase)
        {
            ShowPreviewRestriction();
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "clientes"
        };
        picker.FileTypeChoices.Add("CSV", [".csv"]);
        InitializeWithWindow.Initialize(picker, _windowHandle);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            await FileIO.WriteTextAsync(file, CustomerCsv.Export(_visibleCustomers));
            ShowStatus(
                InfoBarSeverity.Success,
                "Exportación completada",
                $"Se exportaron {_visibleCustomers.Count} clientes visibles a {file.Name}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowStatus(InfoBarSeverity.Error, "No se pudo exportar el CSV", exception.Message);
        }
    }

    private async void OnImportCsvClick(object sender, RoutedEventArgs e)
    {
        if (!_canAccessDatabase)
        {
            ShowPreviewRestriction();
            return;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, _windowHandle);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        IReadOnlyList<CustomerDraft> imported;
        try
        {
            imported = CustomerCsv.Import(await FileIO.ReadTextAsync(file));
        }
        catch (Exception exception) when (
            exception is InvalidDataException or MalformedLineException or IOException)
        {
            ShowStatus(InfoBarSeverity.Error, "CSV no válido", exception.Message);
            return;
        }

        if (imported.Count == 0)
        {
            ShowStatus(InfoBarSeverity.Warning, "CSV vacío", "El archivo no contiene registros de clientes.");
            return;
        }

        for (var index = 0; index < imported.Count; index++)
        {
            if (ValidateDraft(imported[index]) is { } validationMessage)
            {
                ShowStatus(
                    InfoBarSeverity.Warning,
                    "Datos CSV no válidos",
                    $"Fila {index + 2}: {validationMessage}");
                return;
            }
        }

        var confirmation = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Importar clientes",
            Content = $"Se agregarán {imported.Count} registros. Si un documento ya existe, se cancelará toda la importación.",
            PrimaryButtonText = "Importar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirmation.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        btnImportarCsv.IsEnabled = false;
        try
        {
            await _customerRepository.InsertManyAsync(imported);
            ShowStatus(
                InfoBarSeverity.Success,
                "Importación completada",
                $"Se importaron {imported.Count} clientes. Cada fila se insertó dentro de una única transacción.");
            await ReloadCustomersAsync();
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        finally
        {
            btnImportarCsv.IsEnabled = _canAccessDatabase;
        }
    }

    private async Task ReloadCustomersAsync()
    {
        if (!_canAccessDatabase)
        {
            ShowPreviewRestriction();
            return;
        }

        btnBuscar.IsEnabled = false;
        try
        {
            var selectedId = _selectedCustomer?.Id;
            _allCustomers = (await _customerRepository.GetAllAsync()).ToList();
            RefreshCityOptions();
            ApplyFilters();

            if (selectedId is not null)
            {
                lstClientes.SelectedItem = _visibleCustomers.FirstOrDefault(
                    customer => customer.Id == selectedId);
            }

            if (!customerStatus.IsOpen)
            {
                ShowStatus(
                    InfoBarSeverity.Success,
                    "Clientes actualizados",
                    $"Se cargaron {_allCustomers.Count} registros desde SQL Server.");
            }
        }
        catch (SqlException exception)
        {
            ShowDatabaseError(exception);
        }
        finally
        {
            btnBuscar.IsEnabled = true;
        }
    }

    private void RefreshCityOptions()
    {
        var selectedCity = cmbFiltroCiudad.SelectedItem?.ToString();
        cmbFiltroCiudad.Items.Clear();
        cmbFiltroCiudad.Items.Add("Todas las ciudades");
        foreach (var city in _allCustomers
                     .Select(customer => customer.City)
                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                     .OrderBy(city => city, StringComparer.CurrentCultureIgnoreCase))
        {
            cmbFiltroCiudad.Items.Add(city);
        }

        cmbFiltroCiudad.SelectedItem = cmbFiltroCiudad.Items
            .Cast<object>()
            .FirstOrDefault(item => string.Equals(
                item.ToString(),
                selectedCity,
                StringComparison.CurrentCultureIgnoreCase))
            ?? cmbFiltroCiudad.Items[0];
    }

    private void ApplyFilters()
    {
        var search = txtBuscar.Text.Trim();
        var city = cmbFiltroCiudad.SelectedIndex > 0
            ? cmbFiltroCiudad.SelectedItem?.ToString()
            : null;

        _visibleCustomers = _allCustomers
            .Where(customer =>
                (city is null || string.Equals(
                    customer.City,
                    city,
                    StringComparison.CurrentCultureIgnoreCase)) &&
                (string.IsNullOrEmpty(search) ||
                 customer.Document.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 customer.FullName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                 (customer.Phone?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                 (customer.Email?.Contains(search, StringComparison.CurrentCultureIgnoreCase) ?? false)))
            .ToList();

        _suppressSelectionChanged = true;
        lstClientes.ItemsSource = _visibleCustomers;
        if (_selectedCustomer is not null &&
            _visibleCustomers.Any(customer => customer.Id == _selectedCustomer.Id))
        {
            lstClientes.SelectedItem = _visibleCustomers.First(
                customer => customer.Id == _selectedCustomer.Id);
        }
        else
        {
            lstClientes.SelectedItem = null;
            _selectedCustomer = null;
            _isNewCustomer = true;
            ClearForm();
            btnGuardar.Content = "Guardar";
            btnGuardar.IsEnabled = _canAccessDatabase;
            btnEliminar.IsEnabled = false;
        }

        _suppressSelectionChanged = false;
        lblConteo.Text = $"{_visibleCustomers.Count} de {_allCustomers.Count}";
    }

    private void StartNewCustomer()
    {
        _suppressSelectionChanged = true;
        lstClientes.SelectedItem = null;
        _suppressSelectionChanged = false;
        _selectedCustomer = null;
        _isNewCustomer = true;
        ClearForm();
        btnGuardar.Content = "Guardar";
        btnGuardar.IsEnabled = _canAccessDatabase;
        btnEliminar.IsEnabled = false;
        lblPersistencia.Text =
            "En clientes existentes, los valores VIP o tipo de persona vacíos indican que aún no se han clasificado.";
    }

    private void ClearForm()
    {
        txtDocumento.Text = string.Empty;
        txtNombres.Text = string.Empty;
        txtApellidos.Text = string.Empty;
        txtTelefono.Text = string.Empty;
        txtEmail.Text = string.Empty;
        txtCiudad.Text = string.Empty;
        txtLimiteCredito.Text = 5000m.ToString("0.00", CultureInfo.CurrentCulture);
        txtDireccion.Text = string.Empty;
        chkIncluirFecha.IsChecked = false;
        dtpFechaNacimiento.Date = DateTimeOffset.Now;
        chkActivo.IsChecked = true;
        chkClienteVip.IsChecked = false;
        rbPersonaFisica.IsChecked = false;
        rbPersonaJuridica.IsChecked = false;
        SetPreferences([]);
    }

    private void PopulateForm(Customer customer)
    {
        txtDocumento.Text = customer.Document;
        txtNombres.Text = customer.FirstName;
        txtApellidos.Text = customer.LastName;
        txtTelefono.Text = customer.Phone ?? string.Empty;
        txtEmail.Text = customer.Email ?? string.Empty;
        txtCiudad.Text = customer.City;
        txtLimiteCredito.Text = customer.CreditLimit.ToString(
            "0.00",
            CultureInfo.CurrentCulture);
        txtDireccion.Text = customer.Address ?? string.Empty;
        chkIncluirFecha.IsChecked = customer.BirthDate is not null;
        dtpFechaNacimiento.Date = customer.BirthDate is { } date
            ? new DateTimeOffset(date.Date)
            : DateTimeOffset.Now;
        chkActivo.IsChecked = customer.IsActive;
        chkClienteVip.IsChecked = customer.IsVip;
        rbPersonaFisica.IsChecked = customer.PersonType == CustomerPersonType.Physical;
        rbPersonaJuridica.IsChecked = customer.PersonType == CustomerPersonType.Legal;
        SetPreferences((customer.Preferences ?? string.Empty)
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    private CustomerDraft? ReadDraft(out string validationMessage)
    {
        if (!decimal.TryParse(
                txtLimiteCredito.Text,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out var creditLimit))
        {
            validationMessage = "El límite de crédito debe ser un número válido.";
            return null;
        }

        var preferences = GetPreferences();
        if (preferences.Length > 250)
        {
            validationMessage = "Las preferencias superan los 250 caracteres permitidos.";
            return null;
        }

        var draft = new CustomerDraft(
            txtDocumento.Text.Trim(),
            txtNombres.Text.Trim(),
            txtApellidos.Text.Trim(),
            EmptyToNull(txtTelefono.Text.Trim()),
            EmptyToNull(txtEmail.Text.Trim()),
            EmptyToNull(txtDireccion.Text.Trim()),
            txtCiudad.Text.Trim(),
            EmptyToNull(preferences),
            creditLimit,
            chkIncluirFecha.IsChecked == true ? dtpFechaNacimiento.Date.DateTime.Date : null,
            chkActivo.IsChecked == true,
            chkClienteVip.IsChecked,
            GetPersonType());

        validationMessage = ValidateDraft(draft) ?? string.Empty;
        return string.IsNullOrEmpty(validationMessage) ? draft : null;
    }

    private static string? ValidateDraft(CustomerDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Document) ||
            string.IsNullOrWhiteSpace(draft.FirstName) ||
            string.IsNullOrWhiteSpace(draft.LastName) ||
            string.IsNullOrWhiteSpace(draft.City))
        {
            return "Documento, nombres, apellidos y ciudad son obligatorios.";
        }

        if (draft.Document.Length > 20 ||
            draft.FirstName.Length > 100 ||
            draft.LastName.Length > 100 ||
            (draft.Phone?.Length > 30) ||
            (draft.Email?.Length > 100) ||
            (draft.Address?.Length > 200) ||
            draft.City.Length > 80 ||
            (draft.Preferences?.Length > 250))
        {
            return "Uno o más campos superan los límites del esquema de dbo.Clientes.";
        }

        if (draft.CreditLimit < -9999999999999999.99m ||
            draft.CreditLimit > 9999999999999999.99m ||
            decimal.Round(draft.CreditLimit, 2) != draft.CreditLimit)
        {
            return "El límite debe caber en DECIMAL(18,2) y tener hasta dos decimales.";
        }

        return null;
    }

    private string GetPreferences()
    {
        var selected = PreferenceControls()
            .Where(item => item.Control.IsChecked == true)
            .Select(item => item.Label);
        return string.Join("; ", selected);
    }

    private CustomerPersonType? GetPersonType() =>
        rbPersonaFisica.IsChecked == true
            ? CustomerPersonType.Physical
            : rbPersonaJuridica.IsChecked == true
                ? CustomerPersonType.Legal
                : null;

    private void SetPreferences(IEnumerable<string> selectedPreferences)
    {
        var selected = new HashSet<string>(selectedPreferences, StringComparer.CurrentCultureIgnoreCase);
        foreach (var item in PreferenceControls())
        {
            item.Control.IsChecked = selected.Contains(item.Label);
        }
    }

    private (string Label, CheckBox Control)[] PreferenceControls() =>
    [
        ("Laptops", chkPrefLaptops),
        ("Monitores", chkPrefMonitores),
        ("Componentes", chkPrefComponentes),
        ("Almacenamiento", chkPrefAlmacenamiento),
        ("Audio", chkPrefAudio),
        ("Redes", chkPrefRedes)
    ];

    private void ShowDatabaseError(SqlException exception)
    {
        var message = exception.Number is 2601 or 2627
            ? "Ya existe un cliente con ese documento."
            : exception.Message;
        ShowStatus(InfoBarSeverity.Error, "Error de SQL Server", message);
    }

    private void ShowPreviewRestriction()
    {
        ShowStatus(
            InfoBarSeverity.Informational,
            "Vista previa",
            "Inicia sesión correctamente para consultar o modificar clientes.");
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        customerStatus.Severity = severity;
        customerStatus.Title = title;
        customerStatus.Message = message;
        customerStatus.IsOpen = true;
    }

    private static string? EmptyToNull(string value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
