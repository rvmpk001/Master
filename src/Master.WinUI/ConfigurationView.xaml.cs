using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using Master_WinUI.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Master_WinUI;

public sealed partial class ConfigurationView : UserControl
{
    private readonly PlcCommunicationService _plcCommunicationService = new();
    private readonly PlcTagService _plcTagService = new();
    private readonly CompanyConfigurationService _companyConfigurationService = new();
    private readonly DatabaseBackupService _databaseBackupService = new();
    private readonly ImageConfigurationService _imageConfigurationService = new();
    private readonly Dictionary<string, ComboBox> _plcSelectors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _plcTextFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _companyTextFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _imageOnPathFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBox> _imageOffPathFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _imageIdsByControl = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (TextBox Width, TextBox Height)> _imageSizeFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MessageFields> _messageFields = new(StringComparer.OrdinalIgnoreCase);
    private readonly MessageConfigurationService _messageConfigurationService = new();
    private readonly Dictionary<string, ComboBox> _additionalSelectors = new(StringComparer.OrdinalIgnoreCase);
    private readonly AdditionalConfigurationService _additionalConfigurationService = new();
    private readonly List<Button> _databaseActionButtons = new();
    private readonly Dictionary<TreeViewNode, PlcTagRecord> _plcTagsByNode = new();
    private TreeView? _plcTagsTree;
    private ComboBox? _plcConnectionType;
    private StackPanel? _plcSerialOptions;
    private StackPanel? _plcEthernetOptions;
    private CheckBox? _plcConfigurationMode;
    private CheckBox? _plcTagScanType;
    private CheckBox? _plcTagEnabled;
    private TextBox? _plcTagAddressType;
    private TextBox? _plcTagPlcAddress;
    private TextBox? _plcTagInternalAddress;
    private TextBox? _plcTagMultiplier;
    private TextBlock? _plcTagName;
    private TextBlock? _plcTagDescription;
    private Button? _savePlcTagButton;
    private bool _databaseOperationInProgress;
    private int _selectedConfigurationTab;

    public event EventHandler? CloseRequested;

    public ConfigurationView()
    {
        InitializeComponent();
        BuildTabs();
    }

    public async Task LoadImageConfigurationAsync(bool isOn)
    {
        var fields = isOn ? _imageOnPathFields : _imageOffPathFields;
        var columnName = isOn ? "ImagenOn" : "ImagenOff";
        _imageIdsByControl.Clear();
        try
        {
            var images = await _imageConfigurationService.GetImagePathsAsync();
            var missingControls = fields.Keys
                .Where(control => !images.ContainsKey(control))
                .OrderBy(control => control, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingControls.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Faltan controles en dbo.Imagenes: {string.Join(", ", missingControls)}.");
            }

            foreach (var (control, field) in fields)
            {
                var image = images[control];
                field.Text = isOn ? image.ImageOn : image.ImageOff;
                _imageIdsByControl[control] = image.IdImagen;
            }

            ShowStatus($"Rutas {columnName} cargadas desde dbo.Imagenes.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar las imágenes (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar las imágenes: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public async Task LoadImageSizesAsync()
    {
        _imageIdsByControl.Clear();
        try
        {
            var images = await _imageConfigurationService.GetImageSizesAsync();
            var missingControls = _imageSizeFields.Keys
                .Where(control => !images.ContainsKey(control))
                .OrderBy(control => control, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingControls.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Faltan controles en dbo.Imagenes: {string.Join(", ", missingControls)}.");
            }

            foreach (var (control, fields) in _imageSizeFields)
            {
                var image = images[control];
                fields.Width.Text = image.Width?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                fields.Height.Text = image.Height?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                _imageIdsByControl[control] = image.IdImagen;
            }

            ShowStatus("Tamaños cargados desde dbo.Imagenes.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar los tamaños (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar los tamaños: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public async Task LoadMessagesAsync()
    {
        try
        {
            var messages = await _messageConfigurationService.GetMessagesAsync();
            var missingControls = _messageFields.Keys
                .Where(control => !messages.ContainsKey(control))
                .OrderBy(control => control, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missingControls.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Faltan controles en dbo.Mensajes: {string.Join(", ", missingControls)}.");
            }

            foreach (var (control, fields) in _messageFields)
            {
                var message = messages[control];
                fields.Message.Text = message.Text;
                fields.IdMensaje = message.IdMensaje;
                fields.Format = message.Format;
                fields.FormatText.Text = message.Format;
            }

            ShowStatus("Mensajes cargados desde dbo.Mensajes.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar los mensajes (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar los mensajes: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public async Task LoadAdditionalConfigurationAsync()
    {
        try
        {
            var configuration = await _additionalConfigurationService.GetConfigurationAsync();
            foreach (var (parameter, selector) in _additionalSelectors)
            {
                selector.ItemsSource = configuration.Options;
                if (!configuration.Selections.TryGetValue(parameter, out var selectedValue))
                {
                    throw new InvalidOperationException(
                        $"Falta el parámetro '{parameter}' en dbo.ConfigMaq.");
                }

                if (!configuration.Options.Contains(selectedValue, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"El valor '{selectedValue}' de '{parameter}' no existe en dbo.Adicionales.");
                }

                selector.SelectedItem = configuration.Options.First(option =>
                    string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
                selector.IsEnabled = true;
            }

            ShowStatus("Opciones cargadas desde dbo.Adicionales y dbo.ConfigMaq.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar los adicionales (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar los adicionales: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public void ShowTab(int index, bool onlySelectedTab)
    {
        if (index < 0 || index >= ConfigurationTabs.TabItems.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _selectedConfigurationTab = index;
        CompanyContentHost.Visibility = Visibility.Collapsed;
        RemissionContentHost.Visibility = Visibility.Collapsed;
        ImagesOnContentHost.Visibility = Visibility.Collapsed;
        ImagesOffContentHost.Visibility = Visibility.Collapsed;
        ImageSizesContentHost.Visibility = Visibility.Collapsed;
        MessagesContentHost.Visibility = Visibility.Collapsed;
        AdditionalContentHost.Visibility = Visibility.Collapsed;

        if (index == 1 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 2 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Collapsed;
            CompanyContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 3 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Collapsed;
            RemissionContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 4 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Collapsed;
            ImagesOnContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 5 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Collapsed;
            ImagesOffContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 6 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Collapsed;
            ImageSizesContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 7 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Collapsed;
            MessagesContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 8 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Collapsed;
            AdditionalContentHost.Visibility = Visibility.Visible;
            return;
        }

        ConfigurationTabs.Visibility = Visibility.Visible;
        PlcContentHost.Visibility = Visibility.Collapsed;

        for (var tabIndex = 0; tabIndex < ConfigurationTabs.TabItems.Count; tabIndex++)
        {
            if (ConfigurationTabs.TabItems[tabIndex] is TabViewItem tab)
            {
                tab.Visibility = tabIndex != 1 && (!onlySelectedTab || tabIndex == index)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        ConfigurationTabs.SelectedIndex = index;
    }

    public async Task LoadCompanyConfigurationAsync()
    {
        try
        {
            var settings = await _companyConfigurationService.GetCompanyConfigurationAsync();
            SetCompanyText("Cia", settings.CompanyName);
            SetCompanyText("Ptl", settings.PlantName);
            SetCompanyText("UbiPtl", settings.PlantLocation);
            SetCompanyText("CalleNum", settings.StreetAndNumber);
            SetCompanyText("Col", settings.Neighborhood);
            SetCompanyText("Munp", settings.Municipality);
            SetCompanyText("CP", settings.PostalCode);
            SetCompanyText("Edo", settings.State);
            ShowStatus("Datos generales de la compañía cargados desde dbo.Cia_Datos.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar los datos de la compañía (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar los datos de la compañía: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public async Task LoadPlcConfigurationAsync()
    {
        var messages = new List<string>();
        var severity = InfoBarSeverity.Success;
        var connectionType = string.Empty;

        try
        {
            var settings = await _plcCommunicationService.GetSettingsAsync();
            SelectPlcOption("DirPLC", settings.PlcAddress);
            SelectPlcOption("Port", settings.Port);
            SelectPlcOption("Velocity", settings.Velocity);
            SelectPlcOption("DataBits", settings.DataBits);
            SelectPlcOption("Parity", settings.Parity);
            SelectPlcOption("StopBits", settings.StopBits);
            SelectPlcOption("Tipo", settings.ConnectionType);
            SetPlcText("TimeOut", settings.Timeout);
            SetPlcText("Retry", settings.Retry);
            SetPlcText("Salidas", settings.Outputs);
            _plcConfigurationMode!.IsChecked = settings.ConfigurationMode == "1";
            UpdateConnectionFields(settings.ConnectionType);
            connectionType = settings.ConnectionType;
            messages.Add("Configuración de comunicación cargada.");
        }
        catch (SqlException exception)
        {
            messages.Add($"Error SQL al cargar comunicación ({exception.Number}): {exception.Message}");
            severity = InfoBarSeverity.Error;
        }
        catch (InvalidOperationException exception)
        {
            messages.Add($"Error al cargar comunicación: {exception.Message}");
            severity = InfoBarSeverity.Error;
        }

        try
        {
            var tags = await _plcTagService.GetTagsAsync();
            BuildPlcTagTree(tags);
            messages.Add($"{tags.Count} variables cargadas desde dbo.Tags.");
        }
        catch (SqlException exception)
        {
            messages.Add($"Error SQL al cargar variables ({exception.Number}): {exception.Message}");
            severity = InfoBarSeverity.Error;
        }
        catch (InvalidOperationException exception)
        {
            messages.Add($"Error al cargar variables PLC: {exception.Message}");
            severity = InfoBarSeverity.Error;
        }

        if (connectionType.Equals("ETHERNET", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("Dirección IP y Password no están disponibles en el esquema actual de la base de datos.");
            if (severity == InfoBarSeverity.Success)
            {
                severity = InfoBarSeverity.Warning;
            }
        }

        ShowStatus(string.Join(" ", messages), severity);
    }

    private void BuildTabs()
    {
        BuildTab(0, BuildMachineConfiguration);
        BuildTab(1, BuildPlcConfiguration);
        BuildTab(2, BuildCompanyData);
        BuildTab(3, BuildRemissionSettings);
        BuildTab(4, panel => BuildImagePaths(panel, isOn: true));
        BuildTab(5, panel => BuildImagePaths(panel, isOn: false));
        BuildTab(6, BuildImageSizes);
        BuildTab(7, BuildMessages);
        BuildTab(8, BuildAdditionalFields);
        BuildTab(9, BuildOrders);
        BuildTab(10, BuildAccesses);
    }

    private void BuildTab(int index, Action<StackPanel> build)
    {
        var content = new StackPanel
        {
            Padding = new Thickness(16),
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        build(content);
        var scrollViewer = new ScrollViewer
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        if (index == 1)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            PlcContentHost.Content = scrollViewer;
            return;
        }

        if (index == 2)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            CompanyContentHost.Content = scrollViewer;
            return;
        }

        if (index == 3)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            RemissionContentHost.Content = scrollViewer;
            return;
        }

        if (index == 4)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            ImagesOnContentHost.Content = scrollViewer;
            return;
        }

        if (index == 5)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            ImagesOffContentHost.Content = scrollViewer;
            return;
        }

        if (index == 6)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            ImageSizesContentHost.Content = scrollViewer;
            return;
        }

        if (index == 7)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            MessagesContentHost.Content = scrollViewer;
            return;
        }

        if (index == 8)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            AdditionalContentHost.Content = scrollViewer;
            return;
        }

        ConfigurationTabs.TabItems[index] = new TabViewItem
        {
            Header = ((TabViewItem)ConfigurationTabs.TabItems[index]).Header,
            IsClosable = false,
            Content = scrollViewer
        };
    }

    private void BuildMachineConfiguration(StackPanel host)
    {
        AddSelectorGroup(host, "Agregados", new[]
        {
            ("A1", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "GRAVA"),
            ("A2", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "ARENA"),
            ("A3", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "DESHABILITADA"),
            ("A4", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "DESHABILITADA"),
            ("A5", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "DESHABILITADA"),
            ("A6", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "DESHABILITADA"),
            ("A7", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "DESHABILITADA"),
            ("A8", new[] { "GRAVA", "ARENA", "DESHABILITADA" }, "DESHABILITADA")
        });
        AddSelectorGroup(host, "Cementos", new[]
        {
            ("Principal", new[] { "CEMENTO", "DESHABILITADA" }, "CEMENTO"),
            ("Auxiliar 1", new[] { "CEMENTO", "DESHABILITADA" }, "DESHABILITADA"),
            ("Auxiliar 2", new[] { "CEMENTO", "DESHABILITADA" }, "DESHABILITADA"),
            ("Auxiliar 3", new[] { "CEMENTO", "DESHABILITADA" }, "DESHABILITADA"),
            ("Auxiliar 4", new[] { "CEMENTO", "DESHABILITADA" }, "DESHABILITADA")
        });
        AddSelectorGroup(host, "Aditivos", Enumerable.Range(1, 6)
            .Select(index => ($"Aditivo {index}", new[] { $"ADITIVO {index}", "DESHABILITADA" }, index == 1 ? $"ADITIVO {index}" : "DESHABILITADA"))
            .ToArray());
        AddSelectorGroup(host, "Agua", Enumerable.Range(1, 3)
            .Select(index => ($"Agua {index}", new[] { $"AGUA {index}", "DESHABILITADA" }, index == 1 ? $"AGUA {index}" : "DESHABILITADA"))
            .ToArray());
        AddSelectorGroup(host, "Fibras", Enumerable.Range(1, 3)
            .Select(index => ($"Fibra {index}", new[] { $"FIBRA {index}", "DESHABILITADA" }, "DESHABILITADA"))
            .ToArray());

        AddNumericGroup(host, "Ajuste de Humedad", Enumerable.Range(1, 8).Select(index => $"Agregado {index}").ToArray());
        AddNumericGroup(host, "Ajuste de Absorción", Enumerable.Range(1, 8).Select(index => $"Agregado {index}").ToArray());

        var machine = AddSection(host, "Tipo de Máquina");
        AddTextField(machine, "Nombre de la Máquina", "PLANTA B-40");
        AddCheckBox(machine, "Habilitar Humedades", true);
        AddCheckBox(machine, "Habilitar Absorciones", true);
        AddCheckBox(machine, "Habilitar Botón Carga/Descarga", true);
        AddPathField(machine, "Archivo de imagen");
    }

    private void BuildPlcConfiguration(StackPanel host)
    {
        var layout = new Grid
        {
            ColumnSpacing = 24,
            RowSpacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0
        };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var communicationColumn = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var memoryColumn = new StackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        layout.Children.Add(communicationColumn);
        layout.Children.Add(memoryColumn);
        Grid.SetColumn(memoryColumn, 1);
        layout.SizeChanged += (_, args) =>
        {
            var useSingleColumn = args.NewSize.Width < 760;
            layout.ColumnDefinitions[1].Width = useSingleColumn
                ? new GridLength(0)
                : new GridLength(0.9, GridUnitType.Star);
            Grid.SetRow(memoryColumn, useSingleColumn ? 1 : 0);
            Grid.SetColumn(memoryColumn, useSingleColumn ? 0 : 1);
        };
        host.Children.Add(layout);

        var communication = AddSection(communicationColumn, "Comunicación");
        RegisterPlcSelector("DirPLC", AddPlcSelector(
            communication,
            "Dirección PLC",
            Enumerable.Range(0, 65).Select(value => value.ToString()),
            "1"));

        var connection = AddSection(communication, "Preferencias de Conexión");
        _plcConnectionType = AddPlcSelector(connection, "T. Conexión", new[] { "COM", "ETHERNET" }, "COM");
        RegisterPlcSelector("Tipo", _plcConnectionType);
        RegisterPlcSelector("Port", AddPlcSelector(connection, "Puerto", Enumerable.Range(1, 16)
            .Select(port => $"COM{port}")
            .Concat(new[] { "TCP", "UDP" }), "COM3"));
        RegisterPlcSelector("Velocity", AddPlcSelector(connection, "Velocidad", new[]
        {
            "110", "300", "600", "1200", "2400", "4800", "9600", "19200",
            "38400", "57600", "115200", "20256"
        }, "57600"));

        _plcSerialOptions = new StackPanel { Spacing = 10 };
        RegisterPlcSelector("DataBits", AddPlcSelector(_plcSerialOptions, "Bits de Datos", new[] { "7", "8" }, "8"));
        RegisterPlcSelector("Parity", AddPlcSelector(_plcSerialOptions, "Paridad", new[] { "None", "Odd", "Even" }, "None"));
        RegisterPlcSelector("StopBits", AddPlcSelector(_plcSerialOptions, "Bits de Paro", new[] { "1", "2" }, "1"));
        connection.Children.Add(_plcSerialOptions);

        _plcEthernetOptions = new StackPanel { Spacing = 10, Visibility = Visibility.Collapsed };
        AddPlcTextField(_plcEthernetOptions, "Dirección IP", string.Empty);
        AddPlcField(_plcEthernetOptions, "Password", new PasswordBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 140
        });
        connection.Children.Add(_plcEthernetOptions);

        _plcConnectionType.SelectionChanged += (_, _) =>
        {
            UpdateConnectionFields(_plcConnectionType.SelectedItem?.ToString() ?? string.Empty);
        };

        var times = AddSection(communicationColumn, "Tiempos");
        RegisterPlcTextField("TimeOut", AddPlcTextField(times, "Tiempo de Desconexión (ms)", "750"));
        RegisterPlcTextField("Retry", AddPlcTextField(times, "Reintentos", "2"));

        var outputs = AddSection(communicationColumn, "Salidas");
        RegisterPlcTextField("Salidas", AddPlcTextField(outputs, "Número de Salidas", "150"));

        _plcConfigurationMode = new CheckBox
        {
            Content = "Modo Configuración",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _plcConfigurationMode.IsChecked = false;
        _plcConfigurationMode.Checked += (_, _) => SetPlcTagEditorEnabled(true);
        _plcConfigurationMode.Unchecked += (_, _) => SetPlcTagEditorEnabled(false);

        var memory = AddSection(memoryColumn, "Direcciones de Memoria (PLC)");
        _plcTagsTree = new TreeView
        {
            MinHeight = 260,
            Height = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectionMode = TreeViewSelectionMode.Single
        };
        var memoryLayout = new Grid { ColumnSpacing = 16 };
        memoryLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        memoryLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        var variableList = new StackPanel { Spacing = 8 };
        variableList.Children.Add(new TextBlock
        {
            Text = "Variables",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        variableList.Children.Add(_plcTagsTree);

        var tagDetails = new StackPanel { Spacing = 10 };
        tagDetails.Children.Add(new TextBlock
        {
            Text = "Mapeo de variable",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        _plcTagName = new TextBlock { Text = "Selecciona una variable", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _plcTagDescription = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8
        };
        tagDetails.Children.Add(_plcTagName);
        tagDetails.Children.Add(_plcTagDescription);
        _plcTagAddressType = AddPlcTextField(tagDetails, "Tipo de dirección", string.Empty);
        _plcTagPlcAddress = AddPlcTextField(tagDetails, "Dirección PLC", string.Empty);
        _plcTagInternalAddress = AddPlcTextField(tagDetails, "Dirección interna", string.Empty);
        _plcTagMultiplier = AddPlcTextField(tagDetails, "Multiplicador", string.Empty);
        _plcTagScanType = new CheckBox { Content = "Tipo Scan" };
        _plcTagEnabled = new CheckBox { Content = "Habilitada" };
        tagDetails.Children.Add(_plcTagScanType);
        tagDetails.Children.Add(_plcTagEnabled);
        _savePlcTagButton = new Button
        {
            Content = "Guardar mapeo",
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled = false
        };
        _savePlcTagButton.Click += SavePlcTag_Click;
        tagDetails.Children.Add(_savePlcTagButton);

        Grid.SetColumn(variableList, 0);
        Grid.SetColumn(tagDetails, 1);
        memoryLayout.Children.Add(variableList);
        memoryLayout.Children.Add(tagDetails);
        memory.Children.Add(memoryLayout);
        _plcTagsTree.SelectionChanged += (_, _) =>
        {
            if (_plcTagsTree.SelectedItem is TreeViewNode selectedNode
                && _plcTagsByNode.TryGetValue(selectedNode, out var selectedTag))
            {
                ShowPlcTag(selectedTag);
            }
        };
        SetPlcTagEditorEnabled(false);
        memoryColumn.Children.Add(_plcConfigurationMode);
    }

    private void BuildPlcTagTree(IReadOnlyList<PlcTagRecord> tags)
    {
        if (_plcTagsTree is null)
        {
            throw new InvalidOperationException("No se encontró el árbol de variables PLC.");
        }

        _plcTagsTree.RootNodes.Clear();
        _plcTagsByNode.Clear();
        var variables = new TreeViewNode { Content = "Variables", IsExpanded = true };
        foreach (var tag in tags)
        {
            var node = new TreeViewNode { Content = tag.Name };
            variables.Children.Add(node);
            _plcTagsByNode.Add(node, tag);
        }

        _plcTagsTree.RootNodes.Add(variables);
        _plcTagsTree.SelectedItem = null;
        if (tags.Count == 0)
        {
            ShowPlcTag(null);
        }
    }

    private void ShowPlcTag(PlcTagRecord? tag)
    {
        if (tag is null)
        {
            _plcTagName!.Text = "No hay variables PLC";
            _plcTagDescription!.Text = string.Empty;
            _plcTagAddressType!.Text = string.Empty;
            _plcTagPlcAddress!.Text = string.Empty;
            _plcTagInternalAddress!.Text = string.Empty;
            _plcTagMultiplier!.Text = string.Empty;
            _plcTagScanType!.IsChecked = false;
            _plcTagEnabled!.IsChecked = false;
            _savePlcTagButton!.IsEnabled = false;
            return;
        }

        _plcTagName!.Text = $"{tag.Name} (No. {tag.Id})";
        _plcTagDescription!.Text = tag.Description;
        _plcTagAddressType!.Text = FormatNullable(tag.AddressType);
        _plcTagPlcAddress!.Text = FormatNullable(tag.PlcAddress);
        _plcTagInternalAddress!.Text = FormatNullable(tag.InternalAddress);
        _plcTagMultiplier!.Text = FormatNullable(tag.Multiplier);
        _plcTagScanType!.IsChecked = tag.ScanType;
        _plcTagEnabled!.IsChecked = tag.IsEnabled;
        _savePlcTagButton!.IsEnabled = _plcConfigurationMode!.IsChecked == true;
    }

    private void SetPlcTagEditorEnabled(bool isEnabled)
    {
        _plcTagAddressType!.IsEnabled = isEnabled;
        _plcTagPlcAddress!.IsEnabled = isEnabled;
        _plcTagInternalAddress!.IsEnabled = isEnabled;
        _plcTagMultiplier!.IsEnabled = isEnabled;
        _plcTagScanType!.IsEnabled = isEnabled;
        _plcTagEnabled!.IsEnabled = isEnabled;
        var hasSelectedTag = _plcTagsTree?.SelectedItem is TreeViewNode selectedNode
            && _plcTagsByNode.ContainsKey(selectedNode);
        _savePlcTagButton!.IsEnabled = isEnabled && hasSelectedTag;
    }

    private async void SavePlcTag_Click(object sender, RoutedEventArgs e)
    {
        if (_plcTagsTree?.SelectedItem is not TreeViewNode selectedNode
            || !_plcTagsByNode.TryGetValue(selectedNode, out var selectedTag))
        {
            ShowStatus("Selecciona una variable PLC antes de guardar su mapeo.", InfoBarSeverity.Warning);
            return;
        }

        if (!TryParseOptionalSingle(_plcTagAddressType!.Text, out var addressType)
            || !TryParseOptionalInt16(_plcTagPlcAddress!.Text, out var plcAddress)
            || !TryParseOptionalInt16(_plcTagInternalAddress!.Text, out var internalAddress)
            || !TryParseOptionalDouble(_plcTagMultiplier!.Text, out var multiplier))
        {
            ShowStatus("Revisa los valores numéricos del mapeo; deben ser válidos y caber en los tipos de la base de datos.", InfoBarSeverity.Warning);
            return;
        }

        _savePlcTagButton!.IsEnabled = false;
        var updatedTag = selectedTag with
        {
            AddressType = addressType,
            PlcAddress = plcAddress,
            InternalAddress = internalAddress,
            Multiplier = multiplier,
            ScanType = _plcTagScanType!.IsChecked == true,
            IsEnabled = _plcTagEnabled!.IsChecked == true
        };

        try
        {
            await _plcTagService.UpdateTagAsync(updatedTag);
            _plcTagsByNode[selectedNode] = updatedTag;
            ShowStatus($"Mapeo de '{updatedTag.Name}' guardado en dbo.Tags.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudo guardar el mapeo (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudo guardar el mapeo: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            _savePlcTagButton.IsEnabled = _plcConfigurationMode!.IsChecked == true;
        }
    }

    private static string FormatNullable<T>(T? value) where T : struct =>
        value is { } currentValue ? Convert.ToString(currentValue, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

    private static bool TryParseOptionalInt16(string value, out short? parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }

        if (short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            parsed = result;
            return true;
        }

        parsed = null;
        return false;
    }

    private static bool TryParseOptionalSingle(string value, out float? parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }

        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && float.IsFinite(result))
        {
            parsed = result;
            return true;
        }

        parsed = null;
        return false;
    }

    private static bool TryParseOptionalDouble(string value, out double? parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            && double.IsFinite(result))
        {
            parsed = result;
            return true;
        }

        parsed = null;
        return false;
    }

    private void RegisterPlcSelector(string parameter, ComboBox selector)
    {
        _plcSelectors.Add(parameter, selector);
    }

    private void RegisterPlcTextField(string parameter, TextBox field)
    {
        _plcTextFields.Add(parameter, field);
    }

    private void SelectPlcOption(string parameter, string value)
    {
        var selector = _plcSelectors[parameter];
        var matchingOption = selector.Items
            .OfType<string>()
            .FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));
        if (matchingOption is null)
        {
            throw new InvalidOperationException(
                $"El valor '{value}' para '{parameter}' no está entre las opciones disponibles.");
        }

        selector.SelectedItem = matchingOption;
    }

    private void SetPlcText(string parameter, string value)
    {
        _plcTextFields[parameter].Text = value;
    }

    private static ComboBox AddPlcSelector(StackPanel parent, string label, IEnumerable<string> options, string selected)
    {
        var comboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 140
        };
        foreach (var option in options)
        {
            comboBox.Items.Add(option);
        }

        comboBox.SelectedItem = selected;
        AddPlcField(parent, label, comboBox);
        return comboBox;
    }

    private static TextBox AddPlcTextField(StackPanel parent, string label, string value)
    {
        var textBox = new TextBox
        {
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 140
        };
        AddPlcField(parent, label, textBox);
        return textBox;
    }

    private static void AddPlcField(StackPanel parent, string label, Control input)
    {
        var row = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(new TextBlock
        {
            Text = label,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        parent.Children.Add(row);
    }

    private void BuildCompanyData(StackPanel host)
    {
        var form = new Grid
        {
            MaxWidth = 1200,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = 24
        };
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var companyColumn = new StackPanel { Spacing = 14 };
        var company = AddSection(companyColumn, "Datos para la emisión de remisiones");
        AddCompanyTextField(company, "Cia", "Nombre de la Compañía", "CRETTA");
        AddCompanyTextField(company, "Ptl", "Nombre de la Planta", "PLANTA B-40 SAN JUAN DE LOS LAGOS");
        AddCompanyTextField(company, "UbiPtl", "Ubicación de la Planta", "SAN JUAN DE LOS LAGOS");

        var addressColumn = new StackPanel { Spacing = 14 };
        var address = AddSection(addressColumn, "Dirección");
        AddCompanyTextField(address, "CalleNum", "Calle y Número");
        AddCompanyTextField(address, "Col", "Colonia");
        AddCompanyTextField(address, "Munp", "Delegación o Municipio", "SAN JUAN DE LOS LAGOS");
        AddCompanyTextField(address, "CP", "Código Postal", "4700");
        AddCompanyTextField(address, "Edo", "Estado o Ciudad", "JALISCO");

        Grid.SetColumn(companyColumn, 0);
        form.Children.Add(companyColumn);
        form.SizeChanged += (_, args) =>
        {
            var useSingleColumn = args.NewSize.Width < 800;
            form.ColumnDefinitions[1].Width = useSingleColumn
                ? new GridLength(0)
                : new GridLength(1, GridUnitType.Star);
            Grid.SetRow(addressColumn, useSingleColumn ? 1 : 0);
            Grid.SetColumn(addressColumn, useSingleColumn ? 0 : 1);
        };
        Grid.SetColumn(addressColumn, 1);
        form.Children.Add(addressColumn);
        host.Children.Add(form);
    }

    private void AddCompanyTextField(StackPanel parent, string parameter, string label, string value = "")
    {
        var field = AddPlcTextField(parent, label, value);
        _companyTextFields.Add(parameter, field);
    }

    private void SetCompanyText(string parameter, string value) =>
        _companyTextFields[parameter].Text = value;

    private void BuildRemissionSettings(StackPanel host)
    {
        var layout = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 3; index++)
        {
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var generalHost = new StackPanel();
        var general = AddSection(generalHost, "Opciones generales");
        AddCheckBox(general, "Colocar nombre de compañía en remisión", true);
        AddCheckBox(general, "Imprimir líneas y títulos en remisión", true);
        AddCheckBox(general, "Imprimir remisión después de cargar", true);
        AddCheckBox(general, "Número de remisión automático", true);
        AddCheckBox(general, "Número de salida automático", true);
        AddCheckBox(general, "Leyenda por parámetro", true);
        AddTextField(general, "Número de salida", "1");
        AddTextField(general, "Número de copias", "1");

        var numberingHost = new StackPanel();
        var numbering = AddSection(numberingHost, "Folio y leyendas");
        AddTextField(numbering, "Leyenda copia");
        AddTextField(numbering, "Sufijo remisión", "+");
        AddTextField(numbering, "Número remisión", "320");
        AddTextField(numbering, "Leyenda");

        var printFormatHost = new StackPanel();
        var printFormat = AddSection(printFormatHost, "Formato de impresión");
        AddTextField(printFormat, "Opacidad", "100");
        AddTextField(printFormat, "Tamaño", "10");
        AddTextField(printFormat, "Rotación", "0");
        AddSelector(printFormat, "Alineación horizontal", new[] { "Izquierda", "Centro", "Derecha" }, "Izquierda");
        AddSelector(printFormat, "Alineación vertical", new[] { "Arriba", "Centro", "Abajo" }, "Arriba");

        Grid.SetColumn(generalHost, 0);
        layout.Children.Add(generalHost);
        Grid.SetColumn(numberingHost, 1);
        layout.Children.Add(numberingHost);
        Grid.SetColumn(printFormatHost, 2);
        layout.Children.Add(printFormatHost);

        var backupsHost = new StackPanel { Spacing = 12 };
        var reportsSection = AddSection(backupsHost, "Respaldo y carga de reportes");
        reportsSection.Orientation = Orientation.Horizontal;
        reportsSection.Spacing = 12;
        var exportReportsButton = CreateDatabaseActionButton("Respaldar Reportes", ExportReports_Click);
        reportsSection.Children.Add(exportReportsButton);

        var restoreReportsButton = CreateDatabaseActionButton("Cargar Reportes", RestoreReports_Click);
        reportsSection.Children.Add(restoreReportsButton);

        var databaseSection = AddSection(backupsHost, "Respaldo y restauración de la base de datos");
        databaseSection.Orientation = Orientation.Horizontal;
        databaseSection.Spacing = 12;
        var exportDatabaseButton = CreateDatabaseActionButton("Respaldar BD", ExportDatabase_Click);
        databaseSection.Children.Add(exportDatabaseButton);

        var restoreDatabaseButton = CreateDatabaseActionButton("Restaurar BD", RestoreDatabase_Click);
        databaseSection.Children.Add(restoreDatabaseButton);
        Grid.SetColumnSpan(backupsHost, 3);
        Grid.SetRow(backupsHost, 1);
        layout.Children.Add(backupsHost);

        layout.SizeChanged += (_, args) =>
        {
            var columnCount = args.NewSize.Width >= 1120 ? 3 : args.NewSize.Width >= 720 ? 2 : 1;
            for (var column = 0; column < layout.ColumnDefinitions.Count; column++)
            {
                layout.ColumnDefinitions[column].Width = column < columnCount
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
            }

            var rowCount = (3 + columnCount - 1) / columnCount;
            for (var row = 0; row < 3; row++)
            {
                layout.RowDefinitions[row].Height = row < rowCount
                    ? GridLength.Auto
                    : new GridLength(0);
            }
            layout.RowDefinitions[rowCount].Height = GridLength.Auto;
            Grid.SetRow(generalHost, 0);
            Grid.SetColumn(generalHost, 0);
            Grid.SetRow(numberingHost, 1 / columnCount);
            Grid.SetColumn(numberingHost, 1 % columnCount);
            Grid.SetRow(printFormatHost, 2 / columnCount);
            Grid.SetColumn(printFormatHost, 2 % columnCount);
            Grid.SetRow(backupsHost, rowCount);
            Grid.SetColumn(backupsHost, 0);
        };
        host.Children.Add(layout);
    }

    private async void ExportReports_Click(object sender, RoutedEventArgs e)
    {
        await ExportSqlBackupAsync(
            "Respaldo-Reportes",
            path => _databaseBackupService.ExportReportsAsync(path),
            "Respaldo de dbo.Reportes guardado en");
    }

    private async void ExportDatabase_Click(object sender, RoutedEventArgs e)
    {
        await ExportSqlBackupAsync(
            "Respaldo-BlapaciSQL",
            path => _databaseBackupService.ExportDatabaseAsync(path),
            "Respaldo de estructura de tablas y datos guardado en");
    }

    private async void RestoreReports_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginDatabaseOperation())
        {
            return;
        }

        try
        {
            var path = await PickSqlFileAsync(save: false, "Reportes-Blapaci");
            if (path is null)
            {
                return;
            }

            if (!await ConfirmRestoreAsync(
                    "Reemplazar reportes",
                    "Se eliminarán todos los registros actuales de dbo.Reportes y se cargarán los del archivo seleccionado. Esta acción no se puede deshacer. ¿Deseas continuar?"))
            {
                return;
            }

            await _databaseBackupService.RestoreReportsAsync(path);
            ShowStatus("Los registros de dbo.Reportes se reemplazaron correctamente.", InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is SqlException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus($"No se pudieron restaurar los reportes: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            EndDatabaseOperation();
        }
    }

    private async void RestoreDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginDatabaseOperation())
        {
            return;
        }

        try
        {
            var path = await PickSqlFileAsync(save: false, "Respaldo-BlapaciSQL");
            if (path is null)
            {
                return;
            }

            if (!await ConfirmRestoreAsync(
                    "Reemplazar datos de la base de datos",
                    "Se borrarán los registros actuales de todas las tablas de usuario y se cargarán los del respaldo. La estructura existente debe coincidir; no se ejecutará el DDL del archivo. Esta acción no se puede deshacer. ¿Deseas continuar?"))
            {
                return;
            }

            await _databaseBackupService.RestoreDatabaseAsync(path);
            ShowStatus("Los datos de todas las tablas se restauraron correctamente.", InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is SqlException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus($"No se pudo restaurar la base de datos: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            EndDatabaseOperation();
        }
    }

    private async Task ExportSqlBackupAsync(string suggestedName, Func<string, Task> export, string successMessage)
    {
        if (!TryBeginDatabaseOperation())
        {
            return;
        }

        try
        {
            var path = await PickSqlFileAsync(save: true, suggestedName);
            if (path is null)
            {
                return;
            }

            await export(path);
            ShowStatus($"{successMessage} {path}", InfoBarSeverity.Success);
        }
        catch (Exception exception) when (exception is SqlException or InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowStatus($"No se pudo generar el respaldo: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            EndDatabaseOperation();
        }
    }

    private Button CreateDatabaseActionButton(string text, RoutedEventHandler handler)
    {
        var button = new Button { Content = text, MinWidth = 180 };
        button.Click += handler;
        _databaseActionButtons.Add(button);
        return button;
    }

    private bool TryBeginDatabaseOperation()
    {
        if (_databaseOperationInProgress)
        {
            return false;
        }

        _databaseOperationInProgress = true;
        foreach (var button in _databaseActionButtons)
        {
            button.IsEnabled = false;
        }
        return true;
    }

    private void EndDatabaseOperation()
    {
        _databaseOperationInProgress = false;
        foreach (var button in _databaseActionButtons)
        {
            button.IsEnabled = true;
        }
    }

    private async Task<string?> PickSqlFileAsync(bool save, string suggestedName)
    {
        if (Application.Current is not App { MainWindow: { } window })
        {
            ShowStatus("No se pudo abrir el selector porque la ventana principal no está disponible.", InfoBarSeverity.Error);
            return null;
        }

        try
        {
            var windowHandle = WindowNative.GetWindowHandle(window);
            if (save)
            {
                var picker = new FileSavePicker();
                picker.FileTypeChoices.Add("Script SQL", new List<string> { ".sql" });
                picker.SuggestedFileName = suggestedName;
                InitializeWithWindow.Initialize(picker, windowHandle);
                var file = await picker.PickSaveFileAsync();
                return file?.Path;
            }

            var openPicker = new FileOpenPicker();
            openPicker.FileTypeFilter.Add(".sql");
            InitializeWithWindow.Initialize(openPicker, windowHandle);
            var selectedFile = await openPicker.PickSingleFileAsync();
            return selectedFile?.Path;
        }
        catch (COMException exception)
        {
            ShowStatus($"No se pudo abrir el selector de archivos: {exception.Message}", InfoBarSeverity.Error);
            return null;
        }
    }

    private async Task<bool> ConfirmRestoreAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "Restaurar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void BuildImagePaths(StackPanel host, bool isOn)
    {
        var fields = isOn ? _imageOnPathFields : _imageOffPathFields;
        host.Children.Add(new TextBlock
        {
            Text = isOn ? "Imágenes On" : "Imágenes Off",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        var columns = new Grid
        {
            ColumnSpacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 4; index++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var columnPanels = Enumerable.Range(0, 4)
            .Select(_ => new StackPanel { Spacing = 12 })
            .ToArray();

        AddImagePathSection(columnPanels[0], "Válvulas", fields,
            ("Ag 1", "V_Ag1"), ("Ag 2", "V_Ag2"), ("Ag 3", "V_Ag3"), ("Ag 4", "V_Ag4"),
            ("Ag Adc", "V_Ag"), ("Cm 1", "V_C1"), ("Cm 2", "V_C2"), ("Cm 3", "V_C3"),
            ("Ag 1", "V_Agua"), ("Ag 2", "V_Agua2"));
        AddImagePathSection(columnPanels[0], "Bombas", fields,
            ("Adt 1", "B_Ad1"), ("Adt 2", "B_Ad2"), ("Adt 3", "B_Ad3"),
            ("Ag 1 (bomba)", "B_Agua"), ("Ag 2 (bomba)", "B_Agua2"));

        AddImagePathSection(columnPanels[1], "Válvulas", fields,
            ("V_Ad 1", "V_Adic1"), ("V_Ad 2", "V_Adic2"), ("V_Ad 3", "V_Adic3"),
            ("Ad 1", "Adic1"), ("Ad 2", "Adic2"), ("Ad 3", "Adic3"), ("Ad 4", "Adic4"), ("Ad 5", "Adic5"));
        AddImagePathSection(columnPanels[1], "Bombas", fields,
            ("B_Ad 1", "B_Adic1"), ("B_Ad 2", "B_Adic2"));
        AddImagePathSection(columnPanels[1], "Adicionales", fields,
            ("Adl1", "AdI1"), ("Adl2", "AdI2"), ("Adl3", "AdI3"));

        AddImagePathSection(columnPanels[2], "Adicionales", fields,
            Enumerable.Range(4, 15).Select(index => ($"Adl{index}", $"AdI{index}")).ToArray());
        AddImagePathSection(columnPanels[3], "Adicionales", fields,
            Enumerable.Range(19, 7).Select(index => ($"Adl{index}", $"AdI{index}")).ToArray());

        for (var index = 0; index < columnPanels.Length; index++)
        {
            Grid.SetColumn(columnPanels[index], index);
            columns.Children.Add(columnPanels[index]);
        }

        columns.SizeChanged += (_, args) =>
        {
            var columnCount = args.NewSize.Width >= 1180 ? 4 : args.NewSize.Width >= 620 ? 2 : 1;
            for (var index = 0; index < columns.ColumnDefinitions.Count; index++)
            {
                columns.ColumnDefinitions[index].Width = index < columnCount
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
                Grid.SetRow(columnPanels[index], index / columnCount);
                Grid.SetColumn(columnPanels[index], index % columnCount);
            }
        };
        host.Children.Add(columns);
    }

    private void AddImagePathSection(
        StackPanel host,
        string title,
        Dictionary<string, TextBox> fields,
        params (string Label, string Control)[] imageFields)
    {
        var section = AddSection(host, title);
        foreach (var (label, control) in imageFields)
        {
            AddCompactPathField(section, label, control, fields);
        }
    }

    private void AddCompactPathField(
        StackPanel parent,
        string label,
        string control,
        Dictionary<string, TextBox> fields)
    {
        var row = new Grid
        {
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 2, 0, 2)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelControl = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ToolTipService.SetToolTip(labelControl, label);
        var path = new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0
        };
        fields.Add(control, path);
        var browse = new Button
        {
            Content = "...",
            Width = 36,
            Padding = new Thickness(0),
            Tag = path
        };
        ToolTipService.SetToolTip(browse, $"Seleccionar archivo para {label}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(browse, $"Examinar {label}");
        browse.Click += BrowseImage_Click;
        Grid.SetColumn(path, 1);
        Grid.SetColumn(browse, 2);
        row.Children.Add(labelControl);
        row.Children.Add(path);
        row.Children.Add(browse);
        parent.Children.Add(row);
    }

    private void BuildImageSizes(StackPanel host)
    {
        host.Children.Add(new TextBlock
        {
            Text = "Tamaño Imagen",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var columns = new Grid
        {
            ColumnSpacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 4; index++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var columnPanels = Enumerable.Range(0, 4)
            .Select(_ => new StackPanel { Spacing = 12 })
            .ToArray();

        AddImageSizeSection(columnPanels[0], "Válvulas",
            ("Ag 1", "V_Ag1"), ("Ag 2", "V_Ag2"), ("Ag 3", "V_Ag3"), ("Ag 4", "V_Ag4"),
            ("Ag A", "V_Ag"), ("Cm 1", "V_C1"), ("Cm 2", "V_C2"), ("Cm 3", "V_C3"),
            ("Ag 1", "V_Agua"), ("Ag 2", "V_Agua2"));
        AddImageSizeSection(columnPanels[0], "Bombas",
            ("Ad 1", "B_Ad1"), ("Ad 2", "B_Ad2"), ("Ad 3", "B_Ad3"),
            ("Ag 1", "B_Agua"), ("Ag 2", "B_Agua2"));

        AddImageSizeSection(columnPanels[1], "Válvulas",
            ("V_Ad 1", "V_Adic1"), ("V_Ad 2", "V_Adic2"), ("V_Ad 3", "V_Adic3"),
            ("Ad 1", "Adic1"), ("Ad 2", "Adic2"), ("Ad 3", "Adic3"), ("Ad 4", "Adic4"), ("Ad 5", "Adic5"));
        AddImageSizeSection(columnPanels[1], "Bombas",
            ("B_Ad 1", "B_Adic1"), ("B_Ad 2", "B_Adic2"));
        AddImageSizeSection(columnPanels[1], "Adicionales",
            Enumerable.Range(1, 4).Select(index => ($"Ad {index}", $"AdI{index}")).ToArray());

        AddImageSizeSection(columnPanels[2], "Adicionales",
            Enumerable.Range(5, 15).Select(index => ($"Ad {index}", $"AdI{index}")).ToArray());
        AddImageSizeSection(columnPanels[3], "Adicionales",
            Enumerable.Range(20, 6).Select(index => ($"Ad {index}", $"AdI{index}")).ToArray());

        for (var index = 0; index < columnPanels.Length; index++)
        {
            Grid.SetColumn(columnPanels[index], index);
            columns.Children.Add(columnPanels[index]);
        }

        columns.SizeChanged += (_, args) =>
        {
            var columnCount = args.NewSize.Width >= 1180 ? 4 : args.NewSize.Width >= 620 ? 2 : 1;
            for (var index = 0; index < columns.ColumnDefinitions.Count; index++)
            {
                columns.ColumnDefinitions[index].Width = index < columnCount
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
                Grid.SetRow(columnPanels[index], index / columnCount);
                Grid.SetColumn(columnPanels[index], index % columnCount);
            }
        };
        host.Children.Add(columns);
    }

    private void AddImageSizeSection(
        StackPanel host,
        string title,
        params (string Label, string Control)[] imageFields)
    {
        var fields = AddSection(host, title);
        foreach (var (label, control) in imageFields)
        {
            AddImageSizeField(fields, label, control);
        }
    }

    private void AddImageSizeField(StackPanel parent, string label, string control)
    {
        var row = new Grid
        {
            ColumnSpacing = 5,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 1, 0, 1)
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelControl = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var widthLabel = new TextBlock { Text = "W:", VerticalAlignment = VerticalAlignment.Center };
        var width = new TextBox { MinWidth = 0, Width = 58, HorizontalAlignment = HorizontalAlignment.Stretch };
        var heightLabel = new TextBlock { Text = "H:", VerticalAlignment = VerticalAlignment.Center };
        var height = new TextBox { MinWidth = 0, Width = 58, HorizontalAlignment = HorizontalAlignment.Stretch };

        Grid.SetColumn(widthLabel, 1);
        Grid.SetColumn(width, 2);
        Grid.SetColumn(heightLabel, 3);
        Grid.SetColumn(height, 4);
        row.Children.Add(labelControl);
        row.Children.Add(widthLabel);
        row.Children.Add(width);
        row.Children.Add(heightLabel);
        row.Children.Add(height);
        parent.Children.Add(row);
        _imageSizeFields.Add(control, (width, height));
    }

    private void BuildMessages(StackPanel host)
    {
        host.Children.Add(new TextBlock
        {
            Text = "Válvulas",
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 16)
        });

        var columns = new Grid
        {
            ColumnSpacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var columnIndex = 0; columnIndex < 2; columnIndex++)
        {
            var column = new StackPanel { Spacing = 12 };
            for (var index = columnIndex * 5 + 1; index <= columnIndex * 5 + 5; index++)
            {
                AddMessageField(column, index);
            }

            var formats = new StackPanel { Spacing = 12, Margin = new Thickness(0, 10, 0, 0) };
            for (var index = columnIndex * 5 + 1; index <= columnIndex * 5 + 5; index++)
            {
                var formatText = _messageFields[$"Mensaje{index}"].FormatText;
                formatText.Margin = new Thickness(0, 2, 0, 2);
                formatText.TextTrimming = TextTrimming.CharacterEllipsis;
                ToolTipService.SetToolTip(formatText, formatText.Text);
                formats.Children.Add(formatText);
            }

            column.Children.Add(formats);
            Grid.SetColumn(column, columnIndex);
            columns.Children.Add(column);
        }

        host.Children.Add(columns);
    }

    private void AddMessageField(StackPanel parent, int messageNumber)
    {
        var control = $"Mensaje{messageNumber}";
        var row = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = $"Mensaje {messageNumber}:",
            VerticalAlignment = VerticalAlignment.Center
        };
        var message = new TextBox
        {
            MaxLength = 100,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var formatText = new TextBlock
        {
            Text = string.Empty,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 169, 184, 204))
        };
        var formatButton = new Button
        {
            Content = "A",
            Width = 36,
            Padding = new Thickness(0),
            Tag = control
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(formatButton, $"Editar formato de Mensaje {messageNumber}");
        ToolTipService.SetToolTip(formatButton, $"Editar formato de Mensaje {messageNumber}");
        formatButton.Click += EditMessageFormat_Click;

        Grid.SetColumn(message, 1);
        Grid.SetColumn(formatButton, 2);
        row.Children.Add(label);
        row.Children.Add(message);
        row.Children.Add(formatButton);
        parent.Children.Add(row);
        _messageFields.Add(control, new MessageFields(message, formatText));
    }

    private void BuildAdditionalFields(StackPanel host)
    {
        var fields = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 4; index++)
        {
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        for (var index = 0; index < 16; index++)
        {
            fields.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var index = 1; index <= 16; index++)
        {
            var parameter = $"I{index}";
            var row = new Grid
            {
                ColumnSpacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = $"Txt{index}:",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 169, 184, 204))
            };
            var selector = new ComboBox
            {
                MinWidth = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = false
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(selector, $"Txt{index}");
            _additionalSelectors.Add(parameter, selector);
            Grid.SetColumn(selector, 1);
            row.Children.Add(label);
            row.Children.Add(selector);
            Grid.SetRow(row, (index - 1) / 4);
            Grid.SetColumn(row, (index - 1) % 4);
            fields.Children.Add(row);
        }

        fields.SizeChanged += (_, args) =>
        {
            var columnCount = args.NewSize.Width >= 900 ? 4 : args.NewSize.Width >= 580 ? 2 : 1;
            for (var index = 0; index < 16; index++)
            {
                if (fields.Children[index] is FrameworkElement field)
                {
                    Grid.SetRow(field, index / columnCount);
                    Grid.SetColumn(field, index % columnCount);
                }
            }

            for (var index = 0; index < fields.ColumnDefinitions.Count; index++)
            {
                fields.ColumnDefinitions[index].Width = index < columnCount
                    ? new GridLength(1, GridUnitType.Star)
                    : new GridLength(0);
            }
        };
        host.Children.Add(fields);
    }

    private void BuildOrders(StackPanel host)
    {
        var section = AddSection(host, "Configuración de pedidos");
        AddTextField(section, "Número Pedido", "2");
        AddCheckBox(section, "Colocar nombre de compañía en Pedido", true);
        AddCheckBox(section, "Imprimir líneas y títulos en Pedido", true);
        AddCheckBox(section, "Pedidos Ocasionales", true);
        AddSelector(section, "Tipo Descarga", new[] { "BOMBEADO" }, "BOMBEADO");
        AddSelector(section, "Producto Solicitado", new[] { "CONCRETO" }, "CONCRETO");
        AddSelector(section, "Uso", new[] { "BANQUETAS" }, "BANQUETAS");
        AddTextField(section, "Título tipo descarga", "Tipo descarga");
        AddTextField(section, "Título producto", "Producto");
        AddTextField(section, "Título condiciones pago", "Condiciones pago");
        AddTextField(section, "Turno");
        AddTextField(section, "Inicio", "07:16 a. m.");
        AddTextField(section, "Término", "07:16 a. m.");
        AddTextField(section, "Condiciones y observaciones");
    }

    private void BuildAccesses(StackPanel host)
    {
        var users = AddSection(host, "Usuarios registrados");
        users.Children.Add(new TextBlock
        {
            Text = "Los usuarios no se cargan aquí porque la aplicación aún no cuenta con un servicio de administración de accesos.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 169, 184, 204))
        });
        AddCheckBox(users, "Accesos");
        AddCheckBox(users, "Asignar");

        var permissions = AddSection(host, "Permisos");
        foreach (var section in new[]
        {
            "Camiones", "Carga Manual", "Clientes", "Configuración", "Ajuste Remisión",
            "Datos Remisión y Copias", "Respaldar BD", "Respaldar Reportes", "Adicionales",
            "General", "Datos Compañía", "Máquina", "Absorción", "Ajuste Absorción",
            "Ajuste Humedades", "Humedades", "Pedidos", "Configurar Pedido", "Turnos", "PLC", "Puerto"
        })
        {
            AddCheckBox(permissions, section);
        }
    }

    private StackPanel AddSection(StackPanel host, string title)
    {
        var content = new StackPanel { Spacing = 10 };
        var section = new StackPanel { Spacing = 8 };
        section.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        section.Children.Add(new Border
        {
            Padding = new Thickness(12),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 23, 35, 55)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 64, 81, 106)),
            BorderThickness = new Thickness(1),
            Child = content
        });
        host.Children.Add(section);
        return content;
    }

    private void AddSelectorGroup(StackPanel host, string title, (string Label, string[] Options, string Selected)[] fields)
    {
        var section = AddSection(host, title);
        foreach (var (label, options, selected) in fields)
        {
            AddSelector(section, label, options, selected);
        }
    }

    private void AddNumericGroup(StackPanel host, string title, string[] fields)
    {
        var section = AddSection(host, title);
        foreach (var label in fields)
        {
            AddTextField(section, label, "0.00");
        }
    }

    private static void AddTextField(StackPanel parent, string label, string value = "")
    {
        parent.Children.Add(new TextBox
        {
            Header = label,
            Text = value,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 180
        });
    }

    private static void AddSelector(StackPanel parent, string label, IEnumerable<string> options, string selected)
    {
        var comboBox = new ComboBox
        {
            Header = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 180
        };
        foreach (var option in options)
        {
            comboBox.Items.Add(option);
        }

        comboBox.SelectedItem = selected;
        parent.Children.Add(comboBox);
    }

    private static void AddCheckBox(StackPanel parent, string label, bool isChecked = false)
    {
        parent.Children.Add(new CheckBox { Content = label, IsChecked = isChecked });
    }

    private void AddPathField(StackPanel parent, string label)
    {
        var field = new StackPanel { Spacing = 4 };
        field.Children.Add(new TextBlock { Text = label, Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 237, 247)) });
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var path = new TextBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 180 };
        var browse = new Button { Content = "Examinar...", Tag = path };
        browse.Click += BrowseImage_Click;
        Grid.SetColumn(browse, 1);
        row.Children.Add(path);
        row.Children.Add(browse);
        field.Children.Add(row);
        parent.Children.Add(field);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveButton.IsEnabled = false;
        try
        {
            if (_selectedConfigurationTab == 2)
            {
                var companySettings = new CompanyConfigurationSettings(
                    GetCompanyText("Cia"),
                    GetCompanyText("Ptl"),
                    GetCompanyText("UbiPtl"),
                    GetCompanyText("CalleNum"),
                    GetCompanyText("Col"),
                    GetCompanyText("Munp"),
                    GetCompanyText("CP"),
                    GetCompanyText("Edo"));
                await _companyConfigurationService.SaveCompanyConfigurationAsync(companySettings);
                ShowStatus("Datos generales de la compañía guardados en dbo.Cia_Datos.", InfoBarSeverity.Success);
            }
            else if (_selectedConfigurationTab == 1)
            {
                var settings = new PlcCommunicationSettings(
                    GetPlcSelectedValue("DirPLC"),
                    GetPlcSelectedValue("Port"),
                    GetPlcSelectedValue("Velocity"),
                    GetPlcSelectedValue("DataBits"),
                    GetPlcSelectedValue("Parity"),
                    GetPlcSelectedValue("StopBits"),
                    GetPlcText("TimeOut"),
                    GetPlcText("Retry"),
                    GetPlcText("Salidas"),
                    GetPlcSelectedValue("Tipo"),
                    _plcConfigurationMode!.IsChecked == true ? "1" : "0");

                await _plcCommunicationService.SaveSettingsAsync(settings);
                ShowStatus(
                    settings.ConnectionType == "ETHERNET"
                        ? "Configuración compatible guardada en dbo.Comunicacion. La tabla actual no contiene parámetros para guardar Dirección IP ni Password."
                        : "Configuración PLC guardada en dbo.Comunicacion.",
                    settings.ConnectionType == "ETHERNET" ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
            }
            else if (_selectedConfigurationTab is 4 or 5)
            {
                var isOn = _selectedConfigurationTab == 4;
                var fields = isOn ? _imageOnPathFields : _imageOffPathFields;
                var updates = fields.Select(field =>
                {
                    if (!_imageIdsByControl.TryGetValue(field.Key, out var idImagen))
                    {
                        throw new InvalidOperationException(
                            $"No se ha cargado el identificador de '{field.Key}' desde dbo.Imagenes.");
                    }

                    return new ImagePathUpdate(idImagen, field.Key, field.Value.Text);
                }).ToArray();

                await _imageConfigurationService.SaveImagePathsAsync(updates, isOn);
                ShowStatus(
                    $"Rutas de Imágenes {(isOn ? "On" : "Off")} guardadas en dbo.Imagenes.{(isOn ? "ImagenOn" : "ImagenOff")}.",
                    InfoBarSeverity.Success);
            }
            else if (_selectedConfigurationTab == 6)
            {
                var updates = _imageSizeFields.Select(field =>
                {
                    if (!_imageIdsByControl.TryGetValue(field.Key, out var idImagen))
                    {
                        throw new InvalidOperationException(
                            $"No se ha cargado el identificador de '{field.Key}' desde dbo.Imagenes.");
                    }

                    var width = ParseImageDimension(field.Key, "W", field.Value.Width.Text);
                    var height = ParseImageDimension(field.Key, "H", field.Value.Height.Text);
                    return new ImageSizeUpdate(idImagen, field.Key, width, height);
                }).ToArray();

                await _imageConfigurationService.SaveImageSizesAsync(updates);
                ShowStatus("Tamaños guardados en dbo.Imagenes (Ancho y Alto).", InfoBarSeverity.Success);
            }
            else if (_selectedConfigurationTab == 7)
            {
                var updates = _messageFields.Select(field =>
                    new MessageConfigurationUpdate(
                        field.Value.IdMensaje,
                        field.Key,
                        field.Value.Message.Text,
                        field.Value.Format)).ToArray();
                await _messageConfigurationService.SaveMessagesAsync(updates);
                ShowStatus("Mensajes y formatos guardados en dbo.Mensajes.", InfoBarSeverity.Success);
            }
            else if (_selectedConfigurationTab == 8)
            {
                var selections = _additionalSelectors.Select(selector =>
                {
                    var value = selector.Value.SelectedItem?.ToString();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new InvalidOperationException(
                            $"Selecciona un valor para '{selector.Key}' antes de guardar.");
                    }

                    return new AdditionalConfigurationSelection(selector.Key, value);
                }).ToArray();

                await _additionalConfigurationService.SaveConfigurationAsync(selections);
                ShowStatus("Adicionales guardados en dbo.ConfigMaq.", InfoBarSeverity.Success);
            }
            else
            {
                ShowStatus("El guardado de esta pestaña todavía no está disponible.", InfoBarSeverity.Warning);
            }
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudo guardar la configuración en la base de datos (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudo guardar la configuración: {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private string GetPlcSelectedValue(string parameter) =>
        _plcSelectors[parameter].SelectedItem?.ToString()
        ?? throw new InvalidOperationException($"Selecciona un valor para '{parameter}' antes de guardar.");

    private string GetPlcText(string parameter) => _plcTextFields[parameter].Text.Trim();

    private string GetCompanyText(string parameter) => _companyTextFields[parameter].Text.Trim();

    private static int ParseImageDimension(string control, string dimension, string value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            throw new InvalidOperationException(
                $"El tamaño {dimension} de '{control}' debe ser un número entero mayor o igual a 0.");
        }

        return parsed;
    }

    private async void EditMessageFormat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string control } || !_messageFields.TryGetValue(control, out var fields))
        {
            return;
        }

        var formatInput = new TextBox
        {
            Text = fields.Format,
            MaxLength = 800,
            PlaceholderText = "Formato de fuente"
        };
        var dialog = new ContentDialog
        {
            Title = $"Formato de {control}",
            Content = formatInput,
            PrimaryButtonText = "Aceptar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            fields.Format = formatInput.Text.Trim();
            fields.FormatText.Text = fields.Format;
            ToolTipService.SetToolTip(fields.FormatText, fields.Format);
        }
    }

    private void UpdateConnectionFields(string connectionType)
    {
        var useEthernet = string.Equals(connectionType, "ETHERNET", StringComparison.OrdinalIgnoreCase);
        _plcSerialOptions!.Visibility = useEthernet ? Visibility.Collapsed : Visibility.Visible;
        _plcEthernetOptions!.Visibility = useEthernet ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private sealed class MessageFields(TextBox message, TextBlock formatText)
    {
        public TextBox Message { get; } = message;
        public TextBlock FormatText { get; } = formatText;
        public string Format { get; set; } = string.Empty;
        public int IdMensaje { get; set; }
    }

    private async void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TextBox path })
        {
            return;
        }

        if (Application.Current is not App { MainWindow: { } window })
        {
            ShowStatus("No se pudo abrir el selector porque la ventana principal no está disponible.", InfoBarSeverity.Error);
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
                path.Text = file.Path;
            }
        }
        catch (COMException)
        {
            ShowStatus("No se pudo abrir el selector de archivos. Inténtalo de nuevo.", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }
}
