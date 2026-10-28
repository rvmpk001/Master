using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Master_WinUI;

public sealed partial class ConfigurationView : UserControl
{
    private readonly List<StackPanel> _tabContents = new();
    private bool _isEditing;

    public event EventHandler? CloseRequested;

    public ConfigurationView()
    {
        InitializeComponent();
        BuildTabs();
        SetEditMode(false);
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
            Spacing = 14
        };
        build(content);
        _tabContents.Add(content);
        ConfigurationTabs.TabItems[index] = new TabViewItem
        {
            Header = ((TabViewItem)ConfigurationTabs.TabItems[index]).Header,
            Content = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
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
        var communication = AddSection(host, "Comunicación PLC");
        AddSelector(communication, "Dirección PLC", new[] { "1" }, "1");
        AddSelector(communication, "Tipo de conexión", new[] { "COM" }, "COM");
        AddSelector(communication, "Puerto", new[] { "COM3" }, "COM3");
        AddSelector(communication, "Velocidad", new[] { "57600" }, "57600");
        AddSelector(communication, "Bits de datos", new[] { "8" }, "8");
        AddSelector(communication, "Paridad", new[] { "None" }, "None");
        AddSelector(communication, "Bits de paro", new[] { "1" }, "1");

        var times = AddSection(host, "Tiempos");
        AddTextField(times, "Tiempo de desconexión (ms)", "750");
        AddTextField(times, "Reintentos", "2");
        var outputs = AddSection(host, "Salidas");
        AddTextField(outputs, "Número de salidas", "150");
        AddCheckBox(host, "Modo Configuración");

        var memory = AddSection(host, "Direcciones de Memoria (PLC)");
        var tree = new TreeView { MinHeight = 260, MaxHeight = 420 };
        var variables = new TreeViewNode { Content = "Variables", IsExpanded = true };
        foreach (var name in new[]
        {
            "Agua", "drAgregado1", "drAgregado2", "drAgregado3", "drAgregado4",
            "drCemento1", "drAgua", "bitInicioCarga", "bitInicioDescarga", "bitPausa",
            "bitCancelar", "MIConfig", "AnalogEspera2", "AnalogEspera3", "AnalogEspera4",
            "AnalogEspera5", "BoolEspera5", "dbAgregado1"
        })
        {
            variables.Children.Add(new TreeViewNode { Content = name });
        }

        tree.RootNodes.Add(variables);
        memory.Children.Add(tree);
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

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        SetEditMode(!_isEditing);
        ShowStatus(
            _isEditing
                ? "Edición local activada. Los cambios aún no están conectados a almacenamiento."
                : "Edición local finalizada. Los valores siguen disponibles mientras esta ventana permanezca abierta.",
            InfoBarSeverity.Informational);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ShowStatus(
            "No se guardaron los cambios: todavía no existe una integración de persistencia para esta configuración.",
            InfoBarSeverity.Warning);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void SetEditMode(bool isEditing)
    {
        _isEditing = isEditing;
        foreach (var content in _tabContents)
        {
            SetInputsEnabled(content, isEditing);
        }

        SaveButton.IsEnabled = isEditing;
        EditButton.Content = isEditing ? "Terminar edición" : "Editar";
    }

    private static void SetInputsEnabled(UIElement element, bool isEnabled)
    {
        if (element is Control control)
        {
            control.IsEnabled = isEnabled;
        }

        if (element is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                SetInputsEnabled(child, isEnabled);
            }
        }
        else if (element is Border { Child: { } borderChild })
        {
            SetInputsEnabled(borderChild, isEnabled);
        }
        else if (element is ContentControl { Content: UIElement content })
        {
            SetInputsEnabled(content, isEnabled);
        }
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
