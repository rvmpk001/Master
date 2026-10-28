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
    private readonly Dictionary<string, ComboBox> _plcSelectors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _plcTextFields = new(StringComparer.Ordinal);
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

    public event EventHandler? CloseRequested;

    public ConfigurationView()
    {
        InitializeComponent();
        BuildTabs();
    }

    public void ShowTab(int index, bool onlySelectedTab)
    {
        if (index < 0 || index >= ConfigurationTabs.TabItems.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        if (index == 1 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            PlcContentHost.Visibility = Visibility.Visible;
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
        var company = AddSection(host, "Datos para la emisión de remisiones");
        AddTextField(company, "Nombre de la Compañía", "CRETTA");
        AddTextField(company, "Nombre de la Planta", "PLANTA B-40 SAN JUAN DE LOS LAGOS");
        AddTextField(company, "Ubicación de la Planta", "SAN JUAN DE LOS LAGOS");

        var address = AddSection(host, "Dirección");
        AddTextField(address, "Calle y Número");
        AddTextField(address, "Colonia");
        AddTextField(address, "Delegación o Municipio", "SAN JUAN DE LOS LAGOS");
        AddTextField(address, "Código Postal", "4700");
        AddTextField(address, "Estado o Ciudad", "JALISCO");
        AddPathField(address, "Logotipo");
        AddPathField(address, "Imagen");
    }

    private void BuildRemissionSettings(StackPanel host)
    {
        var options = AddSection(host, "Ajustes de remisión");
        AddCheckBox(options, "Colocar nombre de compañía en remisión", true);
        AddCheckBox(options, "Imprimir líneas y títulos en remisión", true);
        AddCheckBox(options, "Imprimir remisión después de cargar", true);
        AddCheckBox(options, "Número de remisión automático", true);
        AddCheckBox(options, "Número de salida automático", true);
        AddCheckBox(options, "Leyenda por parámetro", true);
        AddTextField(options, "Número de salida", "1");
        AddTextField(options, "Número de copias", "1");
        AddTextField(options, "Leyenda copia");
        AddTextField(options, "Sufijo remisión", "+");
        AddTextField(options, "Número remisión", "320");
        AddTextField(options, "Leyenda");
        AddTextField(options, "Opacidad", "100");
        AddTextField(options, "Tamaño", "10");
        AddTextField(options, "Rotación", "0");
        AddSelector(options, "Alineación horizontal", new[] { "Izquierda", "Centro", "Derecha" }, "Izquierda");
        AddSelector(options, "Alineación vertical", new[] { "Arriba", "Centro", "Abajo" }, "Arriba");

        var unsupported = AddSection(host, "Respaldo y reportes");
        unsupported.Children.Add(new TextBlock
        {
            Text = "El respaldo de reportes y de base de datos requiere una integración de almacenamiento que aún no está configurada.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 169, 184, 204))
        });
    }

    private void BuildImagePaths(StackPanel host, bool isOn)
    {
        var title = isOn ? "Imágenes On" : "Imágenes Off";
        var section = AddSection(host, $"Rutas de imágenes - {title}");
        var names = new[]
        {
            "Ag 1", "Ag 2", "Ag 3", "Ag 4", "Ag Adc", "Cm 1", "Cm 2", "Cm 3",
            "Ag 1 (bomba)", "Ag 2 (bomba)", "Adt 1", "Adt 2", "Adt 3",
            "V_Ad 1", "V_Ad 2", "Ad 1", "Ad 2", "Ad 3", "Ad 4", "Ad 5",
            "B_Ad 1", "B_Ad 2", "Adicional 1", "Adicional 2", "Adicional 3",
            "Adicional 4", "Adicional 5", "Adicional 6", "Adicional 7", "Adicional 8",
            "Adicional 9", "Adicional 10", "Adicional 11", "Adicional 12", "Adicional 13",
            "Adicional 14", "Adicional 15", "Adicional 16", "Adicional 17", "Adicional 18",
            "Adicional 19", "Adicional 20", "Adicional 21", "Adicional 22", "Adicional 23",
            "Adicional 24", "Adicional 25"
        };
        foreach (var name in names)
        {
            AddPathField(section, name);
        }
    }

    private void BuildImageSizes(StackPanel host)
    {
        var section = AddSection(host, "Tamaño de imágenes");
        var names = new[]
        {
            "Ag 1", "Ag 2", "Ag 3", "Ag 4", "Ag A", "Cm 1", "Cm 2", "Cm 3", "Ag 1 (bomba)",
            "Ag 2 (bomba)", "Adt 1", "Adt 2", "Adt 3", "V_Ad 1", "V_Ad 2", "Ad 1", "Ad 2",
            "Ad 3", "Ad 4", "Ad 5", "B_Ad 1", "B_Ad 2", "Adicional 1", "Adicional 2",
            "Adicional 3", "Adicional 4", "Adicional 5", "Adicional 6", "Adicional 7",
            "Adicional 8", "Adicional 9", "Adicional 10", "Adicional 11", "Adicional 12",
            "Adicional 13", "Adicional 14", "Adicional 15", "Adicional 16", "Adicional 17",
            "Adicional 18", "Adicional 19", "Adicional 20", "Adicional 21", "Adicional 22",
            "Adicional 23", "Adicional 24", "Adicional 25"
        };
        foreach (var name in names)
        {
            var dimensions = AddSection(section, name);
            AddTextField(dimensions, "W", "25");
            AddTextField(dimensions, "H", "25");
        }
    }

    private void BuildMessages(StackPanel host)
    {
        var section = AddSection(host, "Mensajes de válvulas");
        var initialMessages = new[]
        {
            "CARGANDO ARENA (FOCO BLANCO)", "CARGANDO GRAVA (FOCO AZUL)", "CHICHARRA SONANDO",
            "FIN DE CARGA", "FIN DESCARGA", "GUSANO ACTIVADO", "BANDA ARRANCADA",
            "BOMBA DE AGUA ACTIVADA", "COMPUERTA DE AGREGADOS ABIERTA", ""
        };
        for (var index = 0; index < initialMessages.Length; index++)
        {
            AddTextField(section, $"Mensaje {index + 1}", initialMessages[index]);
        }
    }

    private void BuildAdditionalFields(StackPanel host)
    {
        var section = AddSection(host, "Textos adicionales");
        for (var index = 1; index <= 16; index++)
        {
            AddSelector(section, $"Txt{index}", new[]
            {
                $"ADICIONAL{index}", $"ADICIONAL {index}", "EN SILO CEMENTO"
            }, index == 1 ? "EN SILO CEMENTO" : $"ADICIONAL{index}");
        }
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
        catch (SqlException exception)
        {
            ShowStatus($"No se pudo guardar la configuración PLC en la base de datos (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudo guardar la configuración PLC: {exception.Message}", InfoBarSeverity.Error);
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

    private void UpdateConnectionFields(string connectionType)
    {
        var useEthernet = string.Equals(connectionType, "ETHERNET", StringComparison.OrdinalIgnoreCase);
        _plcSerialOptions!.Visibility = useEthernet ? Visibility.Collapsed : Visibility.Visible;
        _plcEthernetOptions!.Visibility = useEthernet ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

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
