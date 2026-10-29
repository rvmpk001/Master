using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Master_WinUI.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Master_WinUI;

public sealed partial class MachineConfigurationView : UserControl
{
    private const string MaterialEditorOption = "AGREGAR / BORRAR";
    private static readonly IReadOnlyDictionary<string, string> CategoryTitles = new Dictionary<string, string>
    {
        ["AGREGADOS"] = "Agregados",
        ["CEMENTOS"] = "Cementos",
        ["ADITIVOS"] = "Aditivos",
        ["AGUA"] = "Agua",
        ["FIBRAS"] = "Fibras"
    };

    private static readonly IReadOnlyDictionary<string, string> CategoryColors = new Dictionary<string, string>
    {
        ["AGREGADOS"] = "#9BB52A",
        ["CEMENTOS"] = "#4D7EFF",
        ["ADITIVOS"] = "#FF641C",
        ["AGUA"] = "#22B7FF",
        ["FIBRAS"] = "#24B64B"
    };
    private static readonly IReadOnlyDictionary<string, string> LightThemeColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["#9BB52A"] = "#657B00",
        ["#4D7EFF"] = "#315BCC",
        ["#FF641C"] = "#C94300",
        ["#22B7FF"] = "#007CAB",
        ["#24B64B"] = "#16803A",
        ["#162033"] = "#FFFFFF",
        ["#E6EDF7"] = "#253244",
        ["#A9B8CC"] = "#586779",
        ["#40516A"] = "#B3C0CE",
        ["#FF8A80"] = "#B42318",
        ["#72D49A"] = "#137A45"
    };

    private readonly MachineConfigurationService _service = new();
    private readonly Dictionary<string, SolidColorBrush> _themeBrushes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ComboBox> _materialPickers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _selectedMaterials = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NumberBox> _humidityInputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NumberBox> _absorptionInputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _textInputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _checkInputs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingMaterialAssignments = new(StringComparer.Ordinal);
    private bool _isLoading;
    private bool _isEditing;
    private ElementTheme _theme = ElementTheme.Dark;

    public event EventHandler? CloseRequested;
    public event EventHandler<string>? MachineNameSaved;
    public event Action<MachineDashboardOptions>? DashboardOptionsChanged;
    public Func<string, Task<string?>>? LoadMachineImageToDesignCanvasAsync { get; set; }
    public Func<string, bool>? HasPermission { get; set; }

    public MachineConfigurationView()
    {
        InitializeComponent();
        BuildForm();
    }

    public void ApplyTheme(ElementTheme theme)
    {
        _theme = theme;
        RequestedTheme = theme;
        foreach (var (color, brush) in _themeBrushes)
        {
            brush.Color = ParseColor(ResolveThemeColor(color, theme));
        }
    }

    public async Task LoadAsync()
    {
        SetEditMode(false);
        SetStatus("Cargando configuración de la máquina desde BlapaciSQL...", InfoBarSeverity.Informational);
        SetInputsEnabled(FormContent, false);
        try
        {
            var snapshot = await _service.GetConfigurationAsync();
            _isLoading = true;
            _missingMaterialAssignments.Clear();
            foreach (var category in CategoryTitles.Keys)
            {
                var options = snapshot.MaterialOptions[category];
                foreach (var picker in _materialPickers.Where(pair => pair.Key.StartsWith($"{category}:", StringComparison.Ordinal)).Select(pair => pair.Value))
                {
                    var parameter = ((MaterialPickerTag)picker.Tag).Parameter;
                    var selected = snapshot.Values.GetValueOrDefault(parameter, string.Empty);
                    _selectedMaterials[parameter] = selected;
                    picker.Items.Clear();
                    foreach (var option in options)
                    {
                        picker.Items.Add(option);
                    }

                    picker.Items.Add(MaterialEditorOption);
                    var matchingOption = options.FirstOrDefault(option => option.Equals(selected, StringComparison.OrdinalIgnoreCase));
                    picker.SelectedItem = matchingOption;
                    if (!string.IsNullOrWhiteSpace(selected) && matchingOption is null)
                    {
                        _missingMaterialAssignments.Add(parameter);
                    }
                }

            }

            foreach (var (parameter, input) in _humidityInputs)
            {
                input.Value = ParseNumber(snapshot.Values.GetValueOrDefault(parameter, "0"));
            }

            foreach (var (parameter, input) in _absorptionInputs)
            {
                input.Value = ParseNumber(snapshot.Values.GetValueOrDefault(parameter, "0"));
            }

            foreach (var (parameter, input) in _textInputs)
            {
                input.Text = snapshot.Values.GetValueOrDefault(parameter, string.Empty);
            }

            foreach (var (parameter, input) in _checkInputs)
            {
                input.IsChecked = snapshot.Values.GetValueOrDefault(parameter, "0") == "1";
            }

            PublishDashboardOptions();
            SetEditMode(false);
            SetStatus(
                _missingMaterialAssignments.Count == 0
                    ? $"Configuración cargada. Catálogos: {snapshot.MaterialOptions.Values.Sum(options => options.Count)} materiales."
                    : $"Configuración cargada, pero hay {_missingMaterialAssignments.Count} asignaciones que no existen en sus catálogos. Selecciónalas de nuevo antes de guardar.",
                _missingMaterialAssignments.Count == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
        }
        catch (SqlException exception)
        {
            SetStatus($"No se pudo cargar la configuración desde BlapaciSQL (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            _isLoading = false;
            SetInputsEnabled(FormContent, _isEditing);
        }
    }

    public void RefreshPermissions()
    {
        EditButton.IsEnabled = HasPermission?.Invoke("Editar") == true;
        SaveButton.IsEnabled = _isEditing && HasPermission?.Invoke("Editar") == true;
    }

    private void BuildForm()
    {
        var groups = new Grid { ColumnSpacing = 16, MinWidth = 940 };
        for (var index = 0; index < CategoryTitles.Count; index++)
        {
            groups.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var slots = new[]
        {
            ("AGREGADOS", Enumerable.Range(1, 8).Select(index => ($"A{index}", $"A{index}")).ToArray()),
            ("CEMENTOS", new[] { ("Principal", "C1"), ("Auxiliar 1", "C2"), ("Auxiliar 2", "C3"), ("Auxiliar 3", "C4"), ("Auxiliar 4", "C5") }),
            ("ADITIVOS", Enumerable.Range(1, 6).Select(index => ($"Aditivo {index}", $"D{index}")).ToArray()),
            ("AGUA", Enumerable.Range(1, 3).Select(index => ($"Agua {index}", $"G{index}")).ToArray()),
            ("FIBRAS", Enumerable.Range(1, 3).Select(index => ($"Fibra {index}", $"FI{index}")).ToArray())
        };

        for (var column = 0; column < slots.Length; column++)
        {
            var (category, entries) = slots[column];
            var group = new StackPanel { Spacing = 8, MinWidth = 160 };
            group.Children.Add(new TextBlock
            {
                Text = CategoryTitles[category],
                Foreground = Brush(CategoryColors[category]),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            foreach (var (label, parameter) in entries)
            {
                var row = new StackPanel { Spacing = 3 };
                var labelBlock = new TextBlock
                {
                    Text = label,
                    Foreground = Brush(CategoryColors[category]),
                    FontSize = 12
                };
                var pickerRow = new Grid { ColumnSpacing = 6 };
                pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                pickerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var icon = new Border
                {
                    Width = 30,
                    Height = 22,
                    Background = Brush(CategoryColors[category]),
                    CornerRadius = new CornerRadius(2),
                    Child = new TextBlock
                    {
                        Text = "▼",
                        FontSize = 10,
                        Foreground = Brush("#162033"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                var picker = new ComboBox
                {
                    MinWidth = 110,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = new MaterialPickerTag(category, parameter)
                };
                picker.SelectionChanged += MaterialPicker_SelectionChanged;
                Grid.SetColumn(icon, 0);
                Grid.SetColumn(picker, 1);
                pickerRow.Children.Add(icon);
                pickerRow.Children.Add(picker);
                row.Children.Add(labelBlock);
                row.Children.Add(pickerRow);
                group.Children.Add(row);
                _materialPickers.Add($"{category}:{parameter}", picker);
            }

            Grid.SetColumn(group, column);
            groups.Children.Add(group);
        }

        FormContent.Children.Add(groups);
        FormContent.Children.Add(BuildAdjustmentGroup("Ajuste de Humedad", "H", _humidityInputs));
        FormContent.Children.Add(BuildAdjustmentGroup("Ajuste de Absorción", "O", _absorptionInputs));
        FormContent.Children.Add(BuildMachineGroup());
    }

    private Border BuildAdjustmentGroup(string title, string parameterPrefix, Dictionary<string, NumberBox> inputs)
    {
        var layout = new Grid { ColumnSpacing = 16, RowSpacing = 8 };
        for (var column = 0; column < 4; column++)
        {
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var index = 1; index <= 8; index++)
        {
            var parameter = $"{parameterPrefix}{index}";
            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = $"Agregado {index}:",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("#E6EDF7")
            });
            var number = new NumberBox
            {
                Value = 0,
                Minimum = 0,
                SmallChange = 0.1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinWidth = 72
            };
            Grid.SetColumn(number, 1);
            var percent = new TextBlock { Text = "%", VerticalAlignment = VerticalAlignment.Center, Foreground = Brush("#E6EDF7") };
            Grid.SetColumn(percent, 2);
            row.Children.Add(number);
            row.Children.Add(percent);
            var cell = new Border { Child = row, Padding = new Thickness(4, 2, 4, 2) };
            Grid.SetRow(cell, (index - 1) / 4);
            Grid.SetColumn(cell, (index - 1) % 4);
            layout.Children.Add(cell);
            inputs.Add(parameter, number);
        }

        var section = new StackPanel { Spacing = 4 };
        section.Children.Add(new TextBlock { Text = title, Foreground = Brush("#E6EDF7"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        section.Children.Add(new Border
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("#40516A"),
            Child = layout
        });
        return new Border { Child = section };
    }

    private UIElement BuildMachineGroup()
    {
        var content = new StackPanel { Spacing = 8 };
        var machineName = new TextBox { Header = "Nombre de la Máquina", MaxLength = 255 };
        _textInputs.Add("Mq", machineName);
        content.Children.Add(machineName);

        var options = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        AddBooleanOption(options, "Habilitar Humedades", "MU");
        AddBooleanOption(options, "Habilitar Absorciones", "OS");
        AddBooleanOption(options, "Habilitar Botón Carga/Descarga", "DC");
        content.Children.Add(options);

        var imagePath = new TextBox { Header = "Archivo de imagen", MaxLength = 255 };
        _textInputs.Add("IM", imagePath);
        var pathRow = new Grid { ColumnSpacing = 8 };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var browseButton = new Button { Content = "Examinar...", Tag = imagePath };
        browseButton.Click += BrowseImage_Click;
        Grid.SetColumn(browseButton, 1);
        pathRow.Children.Add(imagePath);
        pathRow.Children.Add(browseButton);
        content.Children.Add(pathRow);

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = "Tipo de Máquina", Foreground = Brush("#E6EDF7"), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        panel.Children.Add(new Border
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Brush("#40516A"),
            Child = content
        });
        return panel;
    }

    private void AddBooleanOption(Panel parent, string label, string parameter)
    {
        var checkBox = new CheckBox { Content = label };
        _checkInputs.Add(parameter, checkBox);
        parent.Children.Add(checkBox);
    }

    private void MaterialPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || !_isEditing || sender is not ComboBox { Tag: MaterialPickerTag tag } picker)
        {
            return;
        }

        if (picker.SelectedItem is string selected && selected.Equals(MaterialEditorOption, StringComparison.Ordinal))
        {
            picker.SelectedItem = _selectedMaterials.GetValueOrDefault(tag.Parameter);
            _ = ShowMaterialEditorAsync(tag.Category);
            return;
        }

        _selectedMaterials[tag.Parameter] = picker.SelectedItem as string ?? string.Empty;
        _missingMaterialAssignments.Remove(tag.Parameter);
    }

    private async Task ShowMaterialEditorAsync(string category)
    {
        if (HasPermission?.Invoke("Alta") != true && HasPermission?.Invoke("Baja") != true)
        {
            SetStatus("Tu perfil no tiene permiso para agregar ni borrar materiales.", InfoBarSeverity.Error);
            return;
        }

        try
        {
            var snapshot = await _service.GetConfigurationAsync();
            var names = new ObservableCollection<string>(snapshot.MaterialOptions[category]);
            var list = new ListView
            {
                ItemsSource = names,
                SelectionMode = ListViewSelectionMode.Single,
                MinHeight = 150,
                MaxHeight = 280
            };
            var input = new TextBox { MaxLength = 50, PlaceholderText = "Nombre del material" };
            var confirmDelete = new CheckBox { Content = "Confirmo borrar el material seleccionado", IsEnabled = false };
            var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brush("#A9B8CC") };
            var content = new StackPanel { Spacing = 8, MinWidth = 300 };
            content.Children.Add(new TextBlock { Text = CategoryTitles[category], FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            content.Children.Add(list);
            content.Children.Add(input);
            content.Children.Add(confirmDelete);
            content.Children.Add(message);

            var dialog = new ContentDialog
            {
                Title = "Edición de Textos",
                Content = content,
                PrimaryButtonText = "Agregar",
                SecondaryButtonText = "Borrar",
                CloseButtonText = "Cerrar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
                MinWidth = 320
            };
            input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled =
                HasPermission?.Invoke("Alta") == true && !string.IsNullOrWhiteSpace(input.Text);
            dialog.IsPrimaryButtonEnabled = false;
            list.SelectionChanged += (_, _) =>
            {
                confirmDelete.IsChecked = false;
                confirmDelete.IsEnabled = list.SelectedItem is string selected
                    && HasPermission?.Invoke("Baja") == true
                    && !selected.Equals("DESHABILITADA", StringComparison.OrdinalIgnoreCase);
                dialog.IsSecondaryButtonEnabled = false;
            };
            confirmDelete.Checked += (_, _) => dialog.IsSecondaryButtonEnabled =
                HasPermission?.Invoke("Baja") == true && list.SelectedItem is not null;
            confirmDelete.Unchecked += (_, _) => dialog.IsSecondaryButtonEnabled = false;

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                args.Cancel = true;
                var deferral = args.GetDeferral();
                try
                {
                    if (HasPermission?.Invoke("Alta") != true)
                    {
                        message.Text = "Tu perfil no tiene permiso para agregar materiales.";
                        message.Foreground = Brush("#FF8A80");
                        return;
                    }

                    await _service.AddMaterialAsync(category, input.Text);
                    await RefreshCategoryAsync(category, names);
                    input.Text = string.Empty;
                    message.Text = "Material agregado a la base de datos con existencia inicial cero.";
                    message.Foreground = Brush("#72D49A");
                }
                catch (SqlException exception)
                {
                    message.Text = $"No se pudo agregar el material (SQL {exception.Number}): {exception.Message}";
                    message.Foreground = Brush("#FF8A80");
                }
                catch (InvalidOperationException exception)
                {
                    message.Text = exception.Message;
                    message.Foreground = Brush("#FF8A80");
                }
                finally
                {
                    deferral.Complete();
                }
            };
            dialog.SecondaryButtonClick += async (_, args) =>
            {
                args.Cancel = true;
                var deferral = args.GetDeferral();
                try
                {
                    if (HasPermission?.Invoke("Baja") != true)
                    {
                        message.Text = "Tu perfil no tiene permiso para borrar materiales.";
                        message.Foreground = Brush("#FF8A80");
                        return;
                    }

                    var selected = list.SelectedItem as string;
                    if (selected is null)
                    {
                        message.Text = "Selecciona un material para borrar.";
                        return;
                    }

                    await _service.DeleteMaterialAsync(category, selected);
                    await RefreshCategoryAsync(category, names);
                    message.Text = "Material borrado.";
                    message.Foreground = Brush("#72D49A");
                    confirmDelete.IsChecked = false;
                    confirmDelete.IsEnabled = false;
                    dialog.IsSecondaryButtonEnabled = false;
                }
                catch (SqlException exception)
                {
                    message.Text = $"No se pudo borrar el material (SQL {exception.Number}): {exception.Message}";
                    message.Foreground = Brush("#FF8A80");
                }
                catch (InvalidOperationException exception)
                {
                    message.Text = exception.Message;
                    message.Foreground = Brush("#FF8A80");
                }
                finally
                {
                    deferral.Complete();
                }
            };

            await dialog.ShowAsync();
        }
        catch (SqlException exception)
        {
            SetStatus($"No se pudo abrir el editor de {CategoryTitles[category]} (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task RefreshCategoryAsync(string category, ObservableCollection<string> names)
    {
        var snapshot = await _service.GetConfigurationAsync();
        names.Clear();
        foreach (var name in snapshot.MaterialOptions[category])
        {
            names.Add(name);
        }

        _isLoading = true;
        try
        {
            foreach (var (key, picker) in _materialPickers.Where(pair => pair.Key.StartsWith($"{category}:", StringComparison.Ordinal)))
            {
                var parameter = ((MaterialPickerTag)picker.Tag).Parameter;
                var selected = _selectedMaterials.GetValueOrDefault(parameter, string.Empty);
                picker.Items.Clear();
                foreach (var name in snapshot.MaterialOptions[category])
                {
                    picker.Items.Add(name);
                }

                picker.Items.Add(MaterialEditorOption);
                picker.SelectedItem = snapshot.MaterialOptions[category]
                    .FirstOrDefault(name => name.Equals(selected, StringComparison.OrdinalIgnoreCase));
                if (picker.SelectedItem is null)
                {
                    _selectedMaterials[parameter] = string.Empty;
                }
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (HasPermission?.Invoke("Editar") != true)
        {
            SetStatus("Tu perfil no tiene permiso para editar la configuración de la máquina.", InfoBarSeverity.Error);
            return;
        }

        if (_missingMaterialAssignments.Count > 0)
        {
            SetStatus("Hay asignaciones de materiales que no existen en sus catálogos. Selecciona una opción válida antes de guardar.", InfoBarSeverity.Warning);
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (parameter, picker) in _materialPickers.Values
                         .Select(picker => (((MaterialPickerTag)picker.Tag).Parameter, picker)))
            {
                values[parameter] = picker.SelectedItem as string ?? string.Empty;
            }

            foreach (var (parameter, number) in _humidityInputs)
            {
                values[parameter] = FormatNumber(number.Value);
            }

            foreach (var (parameter, number) in _absorptionInputs)
            {
                values[parameter] = FormatNumber(number.Value);
            }

            foreach (var (parameter, input) in _textInputs)
            {
                values[parameter] = input.Text.Trim();
            }

            foreach (var (parameter, checkBox) in _checkInputs)
            {
                values[parameter] = checkBox.IsChecked == true ? "1" : "0";
            }

            await _service.SaveConfigurationAsync(values);
            SetEditMode(false);
            var imagePath = _textInputs["IM"].Text.Trim();
            MachineNameSaved?.Invoke(this, _textInputs["Mq"].Text.Trim());
            PublishDashboardOptions();
            if (!string.IsNullOrWhiteSpace(imagePath) && LoadMachineImageToDesignCanvasAsync is not null)
            {
                var imageLoadError = await LoadMachineImageToDesignCanvasAsync(imagePath);
                if (imageLoadError is not null)
                {
                    SetStatus($"Configuración guardada, pero no se pudo cargar la imagen en el lienzo: {imageLoadError}", InfoBarSeverity.Warning);
                    return;
                }

                SetStatus("Configuración guardada y la imagen se cargó en el lienzo de diseño.", InfoBarSeverity.Success);
                return;
            }

            SetStatus("Configuración de la máquina guardada en BlapaciSQL.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            SetStatus($"No se pudo guardar la configuración (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            SetStatus(exception.Message, InfoBarSeverity.Error);
        }
        finally
        {
            SaveButton.IsEnabled = _isEditing && HasPermission?.Invoke("Editar") == true;
        }
    }

    private void PublishDashboardOptions()
    {
        DashboardOptionsChanged?.Invoke(new MachineDashboardOptions(
            _checkInputs.GetValueOrDefault("MU")?.IsChecked == true,
            _checkInputs.GetValueOrDefault("OS")?.IsChecked == true,
            _checkInputs.GetValueOrDefault("DC")?.IsChecked == true));
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (HasPermission?.Invoke("Editar") != true)
        {
            SetStatus("Tu perfil no tiene permiso para editar la configuración de la máquina.", InfoBarSeverity.Error);
            return;
        }

        SetEditMode(true);
        SetStatus("Edición activada. Guarda los cambios cuando termines; cerrar el formulario descarta los cambios no guardados.", InfoBarSeverity.Informational);
    }

    private void SetEditMode(bool isEditing)
    {
        _isEditing = isEditing;
        SetInputsEnabled(FormContent, isEditing);
        SaveButton.IsEnabled = isEditing && HasPermission?.Invoke("Editar") == true;
        EditButton.IsEnabled = !isEditing && HasPermission?.Invoke("Editar") == true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private async void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TextBox textBox })
        {
            return;
        }

        if (Application.Current is not App { MainWindow: { } window })
        {
            SetStatus("No se pudo abrir el selector de archivos porque la ventana principal no está disponible.", InfoBarSeverity.Error);
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
                textBox.Text = file.Path;
            }
        }
        catch (COMException)
        {
            SetStatus("No se pudo abrir el selector de imágenes. Inténtalo de nuevo.", InfoBarSeverity.Error);
        }
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private static double ParseNumber(string text) =>
        double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
        || double.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            ? value
            : 0;

    private static string FormatNumber(double value) =>
        double.IsFinite(value) ? value.ToString("0.##", CultureInfo.InvariantCulture) : "0";

    private SolidColorBrush Brush(string color)
    {
        if (!_themeBrushes.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush(ParseColor(ResolveThemeColor(color, _theme)));
            _themeBrushes.Add(color, brush);
        }

        return brush;
    }

    private static string ResolveThemeColor(string color, ElementTheme theme) =>
        theme == ElementTheme.Light && LightThemeColors.TryGetValue(color, out var lightColor)
            ? lightColor
            : color;

    private static Windows.UI.Color ParseColor(string color) =>
        Microsoft.UI.ColorHelper.FromArgb(
            255,
            Convert.ToByte(color[1..3], 16),
            Convert.ToByte(color[3..5], 16),
            Convert.ToByte(color[5..7], 16));

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
        else if (element is Border { Child: { } child })
        {
            SetInputsEnabled(child, isEnabled);
        }
        else if (element is ContentControl { Content: UIElement content })
        {
            SetInputsEnabled(content, isEnabled);
        }
    }

    private sealed record MaterialPickerTag(string Category, string Parameter);
}
