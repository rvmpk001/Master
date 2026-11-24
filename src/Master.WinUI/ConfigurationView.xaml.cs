using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using Master_WinUI.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Master_WinUI;

public sealed partial class ConfigurationView : UserControl
{
    private static readonly HashSet<string> AccessModulesWithoutViewActions = new(StringComparer.Ordinal)
    {
        "ADICIONALES",
        "ABSORCION",
        "AJUSTEABSORCION",
        "AJUSTEHUMEDADES",
        "AJUSTEREMISION",
        "CARGAMANUAL",
        "CONFIGURARPEDIDO",
        "DATOSCOMPANIA",
        "ENTRADAS",
        "ENTRADASMATERIAL",
        "EXISTENCIASR",
        "FORMULAS",
        "GENERAL",
        "HUMEDADES",
        "MAQUINA",
        "PEDIDOS",
        "PLC",
        "PRODUCCIONGENERAL",
        "PRODUCCIONREAL",
        "PROVEEDORES",
        "PUERTO",
        "RESPALDARDB",
        "REMISIONANTERIOR",
        "REMISIONSALIDA",
        "RELACION",
        "RELACIONDEPRODUCCION",
        "RESPALDARREPORTES",
        "REGISTROS",
        "SALIDAS",
        "SALIDASMATERIAL",
        "TURNOS"
    };

    private static readonly HashSet<string> AccessModulesWithViewAction = new(StringComparer.Ordinal)
    {
        "FORMULAS",
        "PEDIDOS",
        "PROVEEDORES"
    };

    private static readonly HashSet<string> AccessModulesWithoutCreateAction = new(StringComparer.Ordinal)
    {
        "AJUSTEREMISION",
        "CONFIGURACION",
        "EXISTENCIAS",
        "PLC",
        "PUERTO",
        "RESPALDARDB",
        "RESPALDARREPORTES"
    };

    private static readonly HashSet<string> AccessModulesHiddenFromTree = new(StringComparer.Ordinal)
    {
        "ABSORCION",
        "AJUSTEABSORCION",
        "AJUSTEHUMEDADES",
        "HUMEDADES"
    };

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
    private readonly AccessConfigurationService _accessConfigurationService = new();
    private readonly ThemeBrushSet _themeBrushes = new();
    private readonly Dictionary<string, ComboBox> _additionalSelectors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ComboBox, string> _additionalSelectedValues = new();
    private readonly AdditionalConfigurationService _additionalConfigurationService = new();
    private readonly OrderService _orderService = new();
    private readonly OrderConfigurationService _orderConfigurationService = new();
    private readonly Dictionary<string, CheckBox> _remissionOptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _remissionTextFields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComboBox> _remissionSelectors = new(StringComparer.Ordinal);
    private readonly RemissionConfigurationService _remissionConfigurationService = new();
    private readonly List<Button> _databaseActionButtons = new();
    private readonly Dictionary<TreeViewNode, PlcTagRecord> _plcTagsByNode = new();
    private readonly StackPanel _orderScheduleRows = new();
    private readonly List<OrderScheduleFields> _orderScheduleFieldControls = new();
    private readonly List<OrderScheduleRecord> _orderSchedules = new();
    private readonly OrderScheduleConfigurationService _orderScheduleConfigurationService = new();
    private ComboBox? _orderDischargeTypeSelector;
    private ComboBox? _orderProductSelector;
    private ComboBox? _orderUseSelector;
    private ListView? _orderScheduleList;
    private TextBox? _orderConditionsField;
    private int _selectedOrderScheduleIndex = -1;
    private bool _updatingOrderScheduleList;
    private TextBox? _orderNumberField;
    private CheckBox? _orderIncludeCompanyName;
    private CheckBox? _orderPrintLinesAndTitles;
    private CheckBox? _orderOccasionalOrders;
    private readonly Dictionary<string, TextBox> _orderTitleFields = new(StringComparer.Ordinal);
    private int? _orderConfigurationId;
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
    private int? _remissionSettingsId;
    private ListView? _accessProfilesList;
    private TreeView? _accessPermissionsTree;
    private Grid? _accessLayoutGrid;
    private FrameworkElement? _accessUsersSection;
    private FrameworkElement? _accessPermissionsSection;
    private bool _loadingAccessProfiles;
    private int _accessProfileLoadVersion;
    private readonly List<AccessPermissionItem> _accessPermissionItems = new();
    private StackPanel? _remissionGeneralHost;
    private StackPanel? _remissionNumberingHost;
    private StackPanel? _remissionPrintFormatHost;
    private StackPanel? _databaseBackupsHost;
    private StackPanel? _orderConfigurationHost;
    private StackPanel? _orderSchedulesHost;
    private bool _backupToolsOnly;

    private static readonly AdditionalOptionEditorItem AdditionalOptionEditor = new();

    public event EventHandler? CloseRequested;
    public event EventHandler? AccessPermissionsSaved;
    public event EventHandler? AdditionalConfigurationSaved;
    public event EventHandler<string>? PlantNameSaved;
    public event Action<IReadOnlyCollection<MessageConfigurationUpdate>>? MessagesSaved;
    public Func<int, bool>? CanSaveConfigurationTab { get; set; }
    public Func<bool>? CanEditDesignerCanvas { get; set; }
    public Func<string, bool>? HasProfileModuleAccess { get; set; }
    public Func<string, string, bool>? HasProfileAction { get; set; }

    public ConfigurationView()
    {
        InitializeComponent();
        BuildTabs();
    }

    public void ApplyTheme(ElementTheme theme)
    {
        RequestedTheme = theme;
        _themeBrushes.ApplyTheme(theme);
        StatusInfoBarStyler.Apply(StatusInfoBar, theme);
    }

    public void ShowBackupToolsOnly(bool backupToolsOnly)
    {
        _backupToolsOnly = backupToolsOnly;
        if (_remissionGeneralHost is null
            || _remissionNumberingHost is null
            || _remissionPrintFormatHost is null
            || _databaseBackupsHost is null)
        {
            return;
        }

        RefreshProfilePermissions();
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

    public async Task LoadAccessProfilesAsync()
    {
        if (_accessProfilesList is null)
        {
            throw new InvalidOperationException("La lista de perfiles no está inicializada.");
        }

        try
        {
            var profiles = await _accessConfigurationService.GetProfilesAsync();
            _loadingAccessProfiles = true;
            try
            {
                _accessProfilesList.ItemsSource = profiles;
                _accessProfilesList.SelectedIndex = profiles.Count > 0 ? 0 : -1;
            }
            finally
            {
                _loadingAccessProfiles = false;
            }

            if (_accessProfilesList.SelectedItem is AccessProfile selectedProfile)
            {
                await LoadAccessPermissionsAsync(selectedProfile);
            }
            else
            {
                _accessPermissionsTree?.RootNodes.Clear();
                _accessPermissionItems.Clear();
                ShowStatus("No hay perfiles registrados en dbo.Perfiles.", InfoBarSeverity.Warning);
            }
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar los perfiles (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar los perfiles: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private async void AccessProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingAccessProfiles && _accessProfilesList?.SelectedItem is AccessProfile profile)
        {
            _accessPermissionsTree?.RootNodes.Clear();
            _accessPermissionItems.Clear();
            await LoadAccessPermissionsAsync(profile);
        }
    }

    private async Task LoadAccessPermissionsAsync(AccessProfile profile)
    {
        if (_accessPermissionsTree is null)
        {
            throw new InvalidOperationException("El árbol de accesos no está inicializado.");
        }

        var loadVersion = ++_accessProfileLoadVersion;
        ShowStatus($"Cargando los accesos de {profile.Name}...", InfoBarSeverity.Informational);
        try
        {
            var entries = await _accessConfigurationService.GetPermissionEntriesAsync(profile.Id);
            if (_accessProfilesList?.SelectedItem is not AccessProfile selectedProfile
                || selectedProfile.Id != profile.Id
                || loadVersion != _accessProfileLoadVersion)
            {
                return;
            }

            BuildAccessPermissionTree(entries);
            ShowStatus(
                entries.Count == 0
                    ? $"No hay accesos registrados en dbo.PerfilDetalle para {profile.Name}."
                    : entries.All(entry => entry.DetailId == 0)
                        ? $"El perfil {profile.Name} no tiene accesos propios; se muestran opciones desmarcadas para asignar."
                        : $"Accesos de {profile.Name} cargados desde dbo.PerfilDetalle.",
                entries.Count == 0 || entries.All(entry => entry.DetailId == 0)
                    ? InfoBarSeverity.Warning
                    : InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            if (loadVersion != _accessProfileLoadVersion)
            {
                return;
            }

            _accessPermissionsTree.RootNodes.Clear();
            _accessPermissionItems.Clear();
            ShowStatus($"No se pudieron cargar los accesos (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            if (loadVersion != _accessProfileLoadVersion)
            {
                return;
            }

            _accessPermissionsTree.RootNodes.Clear();
            _accessPermissionItems.Clear();
            ShowStatus($"No se pudieron cargar los accesos: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private void BuildAccessPermissionTree(IReadOnlyList<AccessPermissionEntry> entries)
    {
        _accessPermissionsTree!.RootNodes.Clear();
        _accessPermissionItems.Clear();
        var sortedEntries = entries
            .OrderBy(GetAccessPermissionTitle, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(entry => entry.DetailId)
            .ToArray();
        var hiddenOptions = new HashSet<string>(AccessModulesHiddenFromTree, StringComparer.Ordinal);
        bool addedHiddenOption;
        do
        {
            addedHiddenOption = false;
            foreach (var entry in sortedEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.ParentOption)
                    || !hiddenOptions.Contains(NormalizeAccessModule(entry.ParentOption)))
                {
                    continue;
                }

                addedHiddenOption |= AddHiddenOption(entry.Option, hiddenOptions);
                addedHiddenOption |= AddHiddenOption(entry.Title, hiddenOptions);
            }
        }
        while (addedHiddenOption);

        var visibleEntries = sortedEntries
            .Where(entry =>
                !hiddenOptions.Contains(NormalizeAccessModule(entry.Option))
                && !hiddenOptions.Contains(NormalizeAccessModule(entry.Title)))
            .ToArray();

        var nodesByOption = new Dictionary<string, TreeViewNode>(StringComparer.OrdinalIgnoreCase);
        var nodesByEntryId = new Dictionary<int, TreeViewNode>();
        var reportCenterNodesByOption = new Dictionary<string, TreeViewNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in visibleEntries)
        {
            var entryId = entry.TemplateDetailId ?? entry.DetailId;
            var node = new TreeViewNode { IsExpanded = true };
            node.Content = CreateAccessPermissionItem(
                GetAccessPermissionTitle(entry),
                entry.DetailId,
                "Estatus",
                entry.IsEnabled,
                entry.TemplateProfileId,
                entry.TemplateDetailId);
            if (!string.IsNullOrWhiteSpace(entry.Option))
            {
                nodesByOption.TryAdd(entry.Option, node);
            }
            nodesByEntryId.Add(entryId, node);

            var isReportsEntry = IsReportsEntry(entry);
            var actions = entry.Actions;
            if (isReportsEntry)
            {
                var legacyCenterAction = actions.FirstOrDefault(action =>
                    string.Equals(action.Column, "Alta", StringComparison.OrdinalIgnoreCase));
                var viewAction = actions.FirstOrDefault(action =>
                    string.Equals(action.Column, "Visualizar", StringComparison.OrdinalIgnoreCase));
                actions = actions
                    .Where(action =>
                        !string.Equals(action.Column, "Alta", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(action.Column, "Visualizar", StringComparison.OrdinalIgnoreCase))
                    .Append(new AccessPermissionAction(
                        "Visualizar",
                        "Centro de reportes",
                        viewAction?.IsEnabled ?? legacyCenterAction?.IsEnabled ?? false))
                    .ToArray();
            }

            if (string.Equals(
                    NormalizeAccessModule(entry.Option),
                    "MAQUINA",
                    StringComparison.Ordinal)
                && !actions.Any(action =>
                    string.Equals(action.Column, "Editar", StringComparison.OrdinalIgnoreCase)))
            {
                actions = actions.Append(new AccessPermissionAction("Editar", "Editar", false)).ToArray();
            }

            foreach (var action in actions.OrderBy(
                         action => action.Title,
                         StringComparer.CurrentCultureIgnoreCase))
            {
                if (ShouldHideAccessAction(entry, action.Column))
                {
                    if (IsConfigurationOrdersEntry(entry)
                        && action.Column is "Alta" or "Visualizar")
                    {
                        CreateAccessPermissionItem(
                            action.Title,
                            entry.DetailId,
                            action.Column,
                            action.IsEnabled,
                            entry.TemplateProfileId,
                            entry.TemplateDetailId);
                    }

                    continue;
                }

                var actionNode = new TreeViewNode();
                actionNode.Content = CreateAccessPermissionItem(
                    isReportsEntry
                        && string.Equals(action.Column, "Visualizar", StringComparison.OrdinalIgnoreCase)
                            ? "Centro de reportes"
                            : action.Title,
                    entry.DetailId,
                    action.Column,
                    action.IsEnabled,
                    entry.TemplateProfileId,
                    entry.TemplateDetailId);
                node.Children.Add(actionNode);
                if (isReportsEntry
                    && string.Equals(action.Column, "Visualizar", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(entry.Option))
                    {
                        reportCenterNodesByOption.Add(entry.Option, actionNode);
                    }
                }
            }
        }

        foreach (var entry in visibleEntries)
        {
            var node = nodesByEntryId[entry.TemplateDetailId ?? entry.DetailId];
            if (!string.IsNullOrWhiteSpace(entry.ParentOption)
                && nodesByOption.TryGetValue(entry.ParentOption, out var parentNode)
                && !ReferenceEquals(parentNode, node))
            {
                if (string.Equals(
                        NormalizeAccessModule(entry.ParentOption),
                        "REPORTES",
                        StringComparison.Ordinal)
                    && reportCenterNodesByOption.TryGetValue(entry.ParentOption, out var reportCenterNode))
                {
                    reportCenterNode.Children.Add(node);
                }
                else
                {
                    parentNode.Children.Add(node);
                }
            }
            else
            {
                _accessPermissionsTree!.RootNodes.Add(node);
            }
        }
    }

    private static bool IsReportsEntry(AccessPermissionEntry entry) =>
        string.Equals(NormalizeAccessModule(entry.Option), "REPORTES", StringComparison.Ordinal)
        || string.Equals(NormalizeAccessModule(entry.Title), "REPORTES", StringComparison.Ordinal);

    private static bool AddHiddenOption(string? option, HashSet<string> hiddenOptions) =>
        !string.IsNullOrWhiteSpace(option)
        && hiddenOptions.Add(NormalizeAccessModule(option));

    private static bool IsConfigurationOrdersEntry(AccessPermissionEntry entry)
    {
        var isConfigurationChild = string.Equals(
            NormalizeAccessModule(entry.ParentOption),
            "CONFIGURACION",
            StringComparison.Ordinal);
        var isOrdersModule = string.Equals(NormalizeAccessModule(entry.Option), "PEDIDOS", StringComparison.Ordinal)
            || string.Equals(NormalizeAccessModule(entry.Option), "PEDIDOSC", StringComparison.Ordinal)
            || string.Equals(NormalizeAccessModule(entry.Title), "PEDIDOS", StringComparison.Ordinal);

        return isConfigurationChild && isOrdersModule;
    }

    private static bool ShouldHideAccessAction(AccessPermissionEntry entry, string actionColumn)
    {
        if (IsConfigurationOrdersEntry(entry)
            && actionColumn is "Alta" or "Visualizar")
        {
            return true;
        }

        var module = NormalizeAccessModule(entry.Option);
        var title = NormalizeAccessModule(entry.Title);
        if (module == "CAMIONES" || title == "CAMIONES")
        {
            return actionColumn is "Exportar" or "Imprimir";
        }

        if (module == "CLIENTES" || title == "CLIENTES")
        {
            return actionColumn is "Exportar" or "Imprimir";
        }

        if (string.Equals(actionColumn, "Alta", StringComparison.OrdinalIgnoreCase)
            && (AccessModulesWithoutCreateAction.Contains(module)
                || AccessModulesWithoutCreateAction.Contains(title)))
        {
            return true;
        }

        if (actionColumn is not ("Exportar" or "Imprimir" or "Visualizar"))
        {
            return false;
        }

        if (string.Equals(actionColumn, "Visualizar", StringComparison.OrdinalIgnoreCase)
            && (AccessModulesWithViewAction.Contains(module)
                || AccessModulesWithViewAction.Contains(NormalizeAccessModule(entry.Title))))
        {
            return false;
        }

        return AccessModulesWithoutViewActions.Contains(module)
            || AccessModulesWithoutViewActions.Contains(title);
    }

    private static string NormalizeAccessModule(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark
                && char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToUpperInvariant(character));
            }
        }

        return normalized.ToString();
    }

    private static string GetAccessPermissionTitle(AccessPermissionEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Title))
        {
            return entry.Title;
        }

        return string.IsNullOrWhiteSpace(entry.Option)
            ? $"Detalle {entry.DetailId}"
            : entry.Option;
    }

    private AccessPermissionItem CreateAccessPermissionItem(
        string title,
        int detailId,
        string column,
        bool isEnabled,
        int? templateProfileId,
        int? templateDetailId)
    {
        var permissionItem = new AccessPermissionItem(
            title,
            isEnabled,
            detailId,
            column,
            templateProfileId,
            templateDetailId);
        _accessPermissionItems.Add(permissionItem);
        return permissionItem;
    }

    public async Task LoadRemissionConfigurationAsync()
    {
        _remissionSettingsId = null;
        try
        {
            var settings = await _remissionConfigurationService.GetSettingsAsync();
            foreach (var (column, field) in _remissionOptions)
            {
                field.IsChecked = settings.Options[column];
            }

            foreach (var (column, field) in _remissionTextFields)
            {
                field.Text = settings.TextValues[column];
            }

            foreach (var (column, field) in _remissionSelectors)
            {
                var code = settings.AlignmentValues[column];
                var option = field.Items
                    .OfType<RemissionAlignmentOption>()
                    .FirstOrDefault(candidate => candidate.Code == code);
                if (option is null)
                {
                    option = new RemissionAlignmentOption($"Código {code}", code);
                    field.Items.Add(option);
                }

                field.SelectedItem = option;
            }

            _remissionSettingsId = settings.Id;
            ShowStatus("Ajuste de remisión cargado desde dbo.AjusteRemision.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudo cargar el ajuste de remisión (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudo cargar el ajuste de remisión: {exception.Message}", InfoBarSeverity.Error);
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

    public void ReflectMessageText(string control, string text)
    {
        if (!_messageFields.TryGetValue(control, out var fields))
        {
            return;
        }

        fields.Message.Text = text;
        ShowStatus($"{control} actualizado desde el lienzo.", InfoBarSeverity.Success);
    }

    public async Task LoadAdditionalConfigurationAsync()
    {
        try
        {
            var configuration = await _additionalConfigurationService.GetConfigurationAsync();
            ApplyAdditionalConfiguration(configuration);

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

    private void ApplyAdditionalConfiguration(AdditionalConfigurationSnapshot configuration)
    {
        foreach (var (parameter, selector) in _additionalSelectors)
        {
            if (!configuration.Selections.TryGetValue(parameter, out var selectedValue))
            {
                throw new InvalidOperationException($"Falta el parámetro '{parameter}' en dbo.ConfigMaq.");
            }

            var option = configuration.Options.FirstOrDefault(candidate =>
                string.Equals(candidate, selectedValue, StringComparison.OrdinalIgnoreCase));
            if (option is null)
            {
                throw new InvalidOperationException(
                    $"El valor '{selectedValue}' de '{parameter}' no existe en dbo.Adicionales.");
            }

            SetAdditionalSelectorOptions(selector, configuration.Options, option);
        }
    }

    private void ApplyAdditionalOptions(IReadOnlyList<string> options)
    {
        foreach (var selector in _additionalSelectors.Values)
        {
            var selectedValue = selector.SelectedItem as string
                ?? (_additionalSelectedValues.TryGetValue(selector, out var previousValue)
                    ? previousValue
                    : null);
            var option = options.FirstOrDefault(candidate =>
                string.Equals(candidate, selectedValue, StringComparison.OrdinalIgnoreCase));
            if (option is null)
            {
                throw new InvalidOperationException(
                    $"La opción seleccionada ya no existe en dbo.Adicionales. Selecciona otra opción.");
            }

            SetAdditionalSelectorOptions(selector, options, option);
        }
    }

    private void SetAdditionalSelectorOptions(
        ComboBox selector,
        IReadOnlyList<string> options,
        string selectedOption)
    {
        selector.SelectionChanged -= AdditionalSelector_SelectionChanged;
        selector.ItemsSource = options.Cast<object>().Append(AdditionalOptionEditor).ToArray();
        selector.SelectedItem = selectedOption;
        selector.IsEnabled = true;
        _additionalSelectedValues[selector] = selectedOption;
        selector.SelectionChanged += AdditionalSelector_SelectionChanged;
    }

    private bool CanDeleteAdditionalOption() =>
        HasProfileAction?.Invoke("Adicionales", "Baja") == true
        || CanSaveConfigurationTab?.Invoke(8) == true;

    private async void AdditionalSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox selector)
        {
            return;
        }

        if (ReferenceEquals(selector.SelectedItem, AdditionalOptionEditor))
        {
            if (_additionalSelectedValues.TryGetValue(selector, out var previousValue))
            {
                selector.SelectedItem = previousValue;
            }

            await ShowAdditionalOptionsEditorAsync();
            return;
        }

        if (selector.SelectedItem is string selectedValue)
        {
            _additionalSelectedValues[selector] = selectedValue;
        }
    }

    private async Task ShowAdditionalOptionsEditorAsync()
    {
        var canAdd = HasProfileAction?.Invoke("Adicionales", "Alta") == true;
        var canDelete = CanDeleteAdditionalOption();
        if (!canAdd && !canDelete)
        {
            ShowStatus("Tu perfil no tiene permiso para agregar ni borrar opciones de Adicionales.", InfoBarSeverity.Error);
            return;
        }

        try
        {
            var configuration = await _additionalConfigurationService.GetConfigurationAsync();
            var options = configuration.Options.ToList();
            var list = new ListView
            {
                ItemsSource = options,
                SelectionMode = ListViewSelectionMode.Single,
                MinHeight = 150,
                MaxHeight = 280
            };
            var input = new TextBox { MaxLength = 50, PlaceholderText = "Nombre de la opción" };
            var confirmDelete = new CheckBox
            {
                Content = "Confirmo borrar la opción seleccionada",
                IsEnabled = false
            };
            var message = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = _themeBrushes.GetBrush("#A9B8CC")
            };
            var content = new StackPanel { Spacing = 8, MinWidth = 300 };
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
                MinWidth = 320,
                IsPrimaryButtonEnabled = false,
                IsSecondaryButtonEnabled = false
            };
            input.TextChanged += (_, _) =>
                dialog.IsPrimaryButtonEnabled =
                    HasProfileAction?.Invoke("Adicionales", "Alta") == true
                    && !string.IsNullOrWhiteSpace(input.Text);
            list.SelectionChanged += (_, _) =>
            {
                confirmDelete.IsChecked = false;
                confirmDelete.IsEnabled = list.SelectedItem is string
                    && CanDeleteAdditionalOption();
                dialog.IsSecondaryButtonEnabled = false;
            };
            confirmDelete.Checked += (_, _) =>
                dialog.IsSecondaryButtonEnabled =
                    CanDeleteAdditionalOption()
                    && list.SelectedItem is string;
            confirmDelete.Unchecked += (_, _) => dialog.IsSecondaryButtonEnabled = false;

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                args.Cancel = true;
                var deferral = args.GetDeferral();
                try
                {
                    if (HasProfileAction?.Invoke("Adicionales", "Alta") != true)
                    {
                        message.Text = "Tu perfil no tiene permiso para agregar opciones.";
                        message.Foreground = _themeBrushes.GetBrush("#FF8A80");
                        return;
                    }

                    await _additionalConfigurationService.AddOptionAsync(input.Text);
                    configuration = await _additionalConfigurationService.GetConfigurationAsync();
                    options = configuration.Options.ToList();
                    list.ItemsSource = options;
                    ApplyAdditionalOptions(configuration.Options);
                    input.Text = string.Empty;
                    message.Text = "Opción agregada a dbo.Adicionales.";
                    message.Foreground = _themeBrushes.GetBrush("#72D49A");
                }
                catch (SqlException exception)
                {
                    message.Text = $"No se pudo agregar la opción (SQL {exception.Number}): {exception.Message}";
                    message.Foreground = _themeBrushes.GetBrush("#FF8A80");
                }
                catch (InvalidOperationException exception)
                {
                    message.Text = exception.Message;
                    message.Foreground = _themeBrushes.GetBrush("#FF8A80");
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
                    if (!CanDeleteAdditionalOption())
                    {
                        message.Text = "Tu perfil no tiene permiso para borrar opciones.";
                        message.Foreground = _themeBrushes.GetBrush("#FF8A80");
                        return;
                    }

                    if (list.SelectedItem is not string selected)
                    {
                        message.Text = "Selecciona una opción para borrar.";
                        message.Foreground = _themeBrushes.GetBrush("#FF8A80");
                        return;
                    }

                    if (_additionalSelectors.Values.Any(selector =>
                        string.Equals(
                            selector.SelectedItem as string,
                            selected,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        message.Text =
                            "No se puede borrar una opción seleccionada en la configuración actual.";
                        message.Foreground = _themeBrushes.GetBrush("#FF8A80");
                        return;
                    }

                    await _additionalConfigurationService.DeleteOptionAsync(selected);
                    configuration = await _additionalConfigurationService.GetConfigurationAsync();
                    options = configuration.Options.ToList();
                    list.ItemsSource = options;
                    ApplyAdditionalOptions(configuration.Options);
                    message.Text = "Opción borrada.";
                    message.Foreground = _themeBrushes.GetBrush("#72D49A");
                    confirmDelete.IsChecked = false;
                    confirmDelete.IsEnabled = false;
                    dialog.IsSecondaryButtonEnabled = false;
                }
                catch (SqlException exception)
                {
                    message.Text = $"No se pudo borrar la opción (SQL {exception.Number}): {exception.Message}";
                    message.Foreground = _themeBrushes.GetBrush("#FF8A80");
                }
                catch (InvalidOperationException exception)
                {
                    message.Text = exception.Message;
                    message.Foreground = _themeBrushes.GetBrush("#FF8A80");
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
            ShowStatus($"No se pudo abrir la edición de Adicionales (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudo abrir la edición de Adicionales: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public async Task LoadOrderOptionsAsync()
    {
        try
        {
            var optionLists = await Task.WhenAll(
                _orderService.GetDischargeTypesAsync(),
                _orderService.GetProductsAsync(),
                _orderService.GetPaymentConditionsAsync());

            SetOrderOptions(_orderDischargeTypeSelector!, optionLists[0], "BOMBEADO", "Tipo Descarga");
            SetOrderOptions(_orderProductSelector!, optionLists[1], "CONCRETO", "Producto Solicitado");
            SetOrderOptions(_orderUseSelector!, optionLists[2], "BANQUETAS", "Uso");
            ShowStatus(
                "Tipos de descarga (T), productos (P) y usos (U) cargados desde dbo.OpcionesPedido.",
                InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar las opciones de pedido (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar las opciones de pedido: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public async Task LoadOrderSchedulesAsync()
    {
        try
        {
            var storedSchedules = await _orderScheduleConfigurationService.GetSchedulesAsync();
            var storedShiftSchedules = storedSchedules
                .Where(schedule => schedule.Position.HasValue)
                .ToArray();
            var schedules = new List<OrderScheduleRecord>();
            var legacyScheduleCounts = storedShiftSchedules
                .GroupBy(schedule => (schedule.Position, schedule.StartTime, schedule.EndTime))
                .ToDictionary(group => group.Key, group => group.Count());
            var notesFromStandaloneRecords = storedSchedules
                .Where(schedule => !schedule.Position.HasValue && !string.IsNullOrWhiteSpace(schedule.Description))
                .Select(schedule => schedule.Description)
                .ToArray();
            var unmatchedLegacySchedules = new List<OrderScheduleRecord>();
            foreach (var storedSchedule in storedShiftSchedules)
            {
                var rowNotes = new List<string>();
                foreach (var descriptionLine in storedSchedule.Description
                    .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
                {
                    if (!TryParseLegacyOrderSchedule(descriptionLine, out var legacySchedule))
                    {
                        if (!string.IsNullOrWhiteSpace(descriptionLine))
                        {
                            rowNotes.Add(descriptionLine);
                        }

                        continue;
                    }

                    var key = (legacySchedule.Position, legacySchedule.StartTime, legacySchedule.EndTime);
                    if (legacyScheduleCounts.TryGetValue(key, out var count) && count > 0)
                    {
                        legacyScheduleCounts[key] = count - 1;
                    }
                    else
                    {
                        unmatchedLegacySchedules.Add(legacySchedule);
                    }
                }

                var rowDescription = string.Join(Environment.NewLine, rowNotes).Trim();
                if (notesFromStandaloneRecords.Length > 0)
                {
                    var standaloneDescription = string.Join(Environment.NewLine, notesFromStandaloneRecords).Trim();
                    if (!string.IsNullOrWhiteSpace(standaloneDescription)
                        && !string.Equals(rowDescription, standaloneDescription, StringComparison.Ordinal))
                    {
                        rowDescription = string.IsNullOrWhiteSpace(rowDescription)
                            ? standaloneDescription
                            : $"{standaloneDescription}{Environment.NewLine}{rowDescription}";
                    }
                }

                schedules.Add(storedSchedule with { Description = rowDescription });
            }

            var fallbackDescription = schedules
                .Select(schedule => schedule.Description)
                .FirstOrDefault(description => !string.IsNullOrWhiteSpace(description))
                ?? string.Join(Environment.NewLine, notesFromStandaloneRecords).Trim();
            schedules.AddRange(unmatchedLegacySchedules.Select(schedule =>
                schedule with { Description = fallbackDescription }));

            if (schedules.Count == 0)
            {
                schedules.Add(new OrderScheduleRecord(
                    1,
                    string.Join(Environment.NewLine, notesFromStandaloneRecords).Trim(),
                    new TimeSpan(13, 20, 0),
                    new TimeSpan(13, 20, 0)));
            }

            _orderConditionsField!.Text = schedules[0].Description;
            SetOrderSchedules(schedules, 0);
            ShowStatus(
                schedules.Count > 1
                    ? $"Se cargaron {schedules.Count} turnos desde dbo.Horarios."
                    : storedSchedules.Count == 0
                        ? "dbo.Horarios no tiene turnos configurados."
                        : "Turnos, horas y observaciones cargados desde dbo.Horarios.",
            storedSchedules.Count == 0
                    ? InfoBarSeverity.Warning
                    : InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudieron cargar los horarios (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudieron cargar los horarios: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    public async Task LoadOrderConfigurationAsync()
    {
        _orderConfigurationId = null;
        try
        {
            var settings = await _orderConfigurationService.GetSettingsAsync();
            _orderConfigurationId = settings.Id;
            _orderNumberField!.Text = settings.OrderNumber;
            _orderIncludeCompanyName!.IsChecked = settings.IncludeCompanyName;
            _orderPrintLinesAndTitles!.IsChecked = settings.PrintLinesAndTitles;
            _orderOccasionalOrders!.IsChecked = settings.OccasionalOrders;
            _orderTitleFields["Tipo"].Text = settings.DischargeTitle;
            _orderTitleFields["Producto"].Text = settings.ProductTitle;
            _orderTitleFields["Uso"].Text = settings.UseTitle;
            ShowStatus("Número, opciones y títulos cargados desde dbo.AjustePedido.", InfoBarSeverity.Success);
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudo cargar la configuración de pedido (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus($"No se pudo cargar la configuración de pedido: {exception.Message}", InfoBarSeverity.Error);
        }
    }

    private static void SetOrderOptions(
        ComboBox selector,
        IReadOnlyList<string> options,
        string preferredSelection,
        string label)
    {
        selector.ItemsSource = options;
        selector.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option, preferredSelection, StringComparison.OrdinalIgnoreCase))
            ?? options.FirstOrDefault();
        selector.IsEnabled = options.Count > 0;
        if (options.Count == 0)
        {
            throw new InvalidOperationException($"No se encontraron opciones para '{label}' en dbo.OpcionesPedido.");
        }
    }

    public void ShowTab(int index, bool onlySelectedTab)
    {
        if (index < 0 || index >= ConfigurationTabs.TabItems.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _selectedConfigurationTab = index;
        SaveButton.IsEnabled = !_backupToolsOnly && CanSaveConfigurationTab?.Invoke(index) == true;
        RefreshProfilePermissions();
        PlcContentHost.Visibility = Visibility.Collapsed;
        CompanyContentHost.Visibility = Visibility.Collapsed;
        RemissionContentHost.Visibility = Visibility.Collapsed;
        ImagesOnContentHost.Visibility = Visibility.Collapsed;
        ImagesOffContentHost.Visibility = Visibility.Collapsed;
        ImageSizesContentHost.Visibility = Visibility.Collapsed;
        MessagesContentHost.Visibility = Visibility.Collapsed;
        AdditionalContentHost.Visibility = Visibility.Collapsed;
        OrdersContentHost.Visibility = Visibility.Collapsed;
        AccessContentHost.Visibility = Visibility.Collapsed;

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

        if (index == 9 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            OrdersContentHost.Visibility = Visibility.Visible;
            return;
        }

        if (index == 10 && onlySelectedTab)
        {
            ConfigurationTabs.Visibility = Visibility.Collapsed;
            AccessContentHost.Visibility = Visibility.Visible;
            return;
        }

        ConfigurationTabs.Visibility = Visibility.Visible;

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
        var compactImageConfiguration = index is 4 or 5 or 6;
        var content = new StackPanel
        {
            Padding = new Thickness(compactImageConfiguration ? 10 : 16),
            Spacing = compactImageConfiguration ? 8 : 14,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        build(content);
        if (compactImageConfiguration)
        {
            var imageHost = index switch
            {
                4 => ImagesOnContentHost,
                5 => ImagesOffContentHost,
                _ => ImageSizesContentHost
            };
            imageHost.Content = content;
            return;
        }

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

        if (index == 9)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            OrdersContentHost.Content = scrollViewer;
            return;
        }

        if (index == 10)
        {
            ((TabViewItem)ConfigurationTabs.TabItems[index]).Visibility = Visibility.Collapsed;
            AccessContentHost.Content = scrollViewer;
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
            Enumerable.Range(0, 128).Select(value => value.ToString()),
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
        _remissionGeneralHost = generalHost;
        var general = AddSection(generalHost, "Opciones generales");
        AddRemissionCheckBox(general, "NomCia", "Colocar nombre de compañía en remisión");
        AddRemissionCheckBox(general, "ImpLineas", "Imprimir líneas y títulos en remisión");
        AddRemissionCheckBox(general, "ImpDesCa", "Imprimir remisión después de cargar");
        AddRemissionCheckBox(general, "NumAutomatico", "Número de remisión automático");
        AddRemissionCheckBox(general, "NumSAutomatico", "Número de salida automático");
        AddRemissionCheckBox(general, "LoP", "Leyenda por parámetro");
        AddRemissionTextField(general, "NumSalida", "Número de salida");
        AddRemissionTextField(general, "NumCopias", "Número de copias");

        var numberingHost = new StackPanel();
        _remissionNumberingHost = numberingHost;
        var numbering = AddSection(numberingHost, "Folio y leyendas");
        AddRemissionTextField(numbering, "Leyendas", "Leyenda copia");
        AddRemissionTextField(numbering, "Subfijo", "Sufijo remisión");
        AddRemissionTextField(numbering, "Numremision", "Número remisión");

        var printFormatHost = new StackPanel();
        _remissionPrintFormatHost = printFormatHost;
        var printFormat = AddSection(printFormatHost, "Formato de impresión");
        AddRemissionTextField(printFormat, "Opacidad", "Opacidad");
        AddRemissionTextField(printFormat, "Tamaño", "Tamaño");
        AddRemissionTextField(printFormat, "Rotacion", "Rotación");
        AddRemissionAlignmentSelector(
            printFormat,
            "AlineacionH",
            "Alineación horizontal",
            new RemissionAlignmentOption("Izquierda", 0),
            new RemissionAlignmentOption("Centro", 1),
            new RemissionAlignmentOption("Derecha", 2));
        AddRemissionAlignmentSelector(
            printFormat,
            "AlineacionV",
            "Alineación vertical",
            new RemissionAlignmentOption("Arriba", 4),
            new RemissionAlignmentOption("Centro", 5),
            new RemissionAlignmentOption("Abajo", 6));

        Grid.SetColumn(generalHost, 0);
        layout.Children.Add(generalHost);
        Grid.SetColumn(numberingHost, 1);
        layout.Children.Add(numberingHost);
        Grid.SetColumn(printFormatHost, 2);
        layout.Children.Add(printFormatHost);

        var backupsHost = new StackPanel { Spacing = 12 };
        _databaseBackupsHost = backupsHost;
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
            "Respaldar Reportes",
            path => _databaseBackupService.ExportReportsAsync(path),
            "Respaldo de dbo.Reportes guardado en");
    }

    private async void ExportDatabase_Click(object sender, RoutedEventArgs e)
    {
        await ExportSqlBackupAsync(
            "Respaldo-BlapaciSQL",
            "Respaldar DB",
            path => _databaseBackupService.ExportDatabaseAsync(path),
            "Respaldo de estructura de tablas y datos guardado en");
    }

    private async void RestoreReports_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginDatabaseOperation("Respaldar Reportes"))
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
        if (!TryBeginDatabaseOperation("Respaldar DB"))
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

    private async Task ExportSqlBackupAsync(
        string suggestedName,
        string permissionModule,
        Func<string, Task> export,
        string successMessage)
    {
        if (!TryBeginDatabaseOperation(permissionModule))
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
        var permissionModule = text.Contains("Reporte", StringComparison.OrdinalIgnoreCase)
            ? "Respaldar Reportes"
            : "Respaldar DB";
        var button = new Button { Content = text, MinWidth = 180, Tag = permissionModule };
        button.Click += handler;
        _databaseActionButtons.Add(button);
        return button;
    }

    public void RefreshProfilePermissions()
    {
        foreach (var button in _databaseActionButtons)
        {
            button.IsEnabled = !_databaseOperationInProgress
                && button.Tag is string module
                && HasProfileModuleAccess?.Invoke(module) == true;
        }

        var remissionSettingsVisible = !_backupToolsOnly
            && HasProfileModuleAccess?.Invoke("Datos Remision") == true;
        var settingsVisibility = remissionSettingsVisible ? Visibility.Visible : Visibility.Collapsed;
        if (_remissionGeneralHost is not null)
        {
            _remissionGeneralHost.Visibility = settingsVisibility;
        }

        if (_remissionNumberingHost is not null)
        {
            _remissionNumberingHost.Visibility = settingsVisibility;
        }

        if (_remissionPrintFormatHost is not null)
        {
            _remissionPrintFormatHost.Visibility = settingsVisibility;
        }

        if (_databaseBackupsHost is not null)
        {
            _databaseBackupsHost.Visibility = Visibility.Visible;
        }

        if (_orderConfigurationHost is not null)
        {
            _orderConfigurationHost.Visibility =
                HasProfileModuleAccess?.Invoke("Configurar Pedido") == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (_orderSchedulesHost is not null)
        {
            _orderSchedulesHost.Visibility =
                HasProfileModuleAccess?.Invoke("Turnos") == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        SaveButton.Visibility = _backupToolsOnly ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = !_backupToolsOnly
            && CanSaveConfigurationTab?.Invoke(_selectedConfigurationTab) == true;
    }

    private bool CanSaveProfileModule(string module) =>
        HasProfileModuleAccess?.Invoke(module) == true
        && (HasProfileAction?.Invoke(module, "Editar") == true
            || HasProfileAction?.Invoke(module, "Alta") == true);

    private bool TryBeginDatabaseOperation(string permissionModule)
    {
        if (HasProfileModuleAccess?.Invoke(permissionModule) != true)
        {
            ShowStatus($"Tu perfil no tiene acceso a {permissionModule}.", InfoBarSeverity.Error);
            return false;
        }

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
        RefreshProfilePermissions();
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
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        var columns = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 4; index++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var columnPanels = Enumerable.Range(0, 4)
            .Select(_ => new StackPanel { Spacing = 10 })
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
        var section = AddSection(host, title, compact: true);
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
            ColumnSpacing = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelControl = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12
        };
        ToolTipService.SetToolTip(labelControl, label);
        var path = new TextBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0,
            FontSize = 13,
            Height = 40
        };
        fields.Add(control, path);
        var browse = new Button
        {
            Content = "...",
            Width = 36,
            Height = 36,
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
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });

        var columns = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 4; index++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            columns.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var columnPanels = Enumerable.Range(0, 4)
            .Select(_ => new StackPanel { Spacing = 8 })
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
        var fields = AddSection(host, title, compact: true);
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
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 13
        };
        var widthLabel = new TextBlock { Text = "W:", VerticalAlignment = VerticalAlignment.Center };
        var width = new TextBox { MinWidth = 0, Width = 64, Height = 40, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Stretch };
        var heightLabel = new TextBlock { Text = "H:", VerticalAlignment = VerticalAlignment.Center };
        var height = new TextBox { MinWidth = 0, Width = 64, Height = 40, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Stretch };

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
            Foreground = _themeBrushes.GetBrush("#A9B8CC")
        };
        var formatButton = new Button
        {
            Content = "A",
            Width = 36,
            Padding = new Thickness(0),
            Tag = control,
            Visibility = Visibility.Collapsed
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
                Foreground = _themeBrushes.GetBrush("#A9B8CC")
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
        var configurationHost = new StackPanel { Spacing = 18 };
        _orderConfigurationHost = configurationHost;

        var options = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        options.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        options.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _orderNumberField = new TextBox
        {
            Header = "Número Pedido",
            Text = "2",
            Width = 140,
            MaxLength = 10
        };
        Grid.SetRow(_orderNumberField, 0);
        Grid.SetColumn(_orderNumberField, 0);
        options.Children.Add(_orderNumberField);

        var flags = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _orderIncludeCompanyName = new CheckBox
        {
            Content = "Colocar nombre de compañía en Pedido",
            IsChecked = true
        };
        flags.Children.Add(_orderIncludeCompanyName);
        _orderPrintLinesAndTitles = new CheckBox
        {
            Content = "Imprimir líneas y títulos en Pedido",
            IsChecked = true
        };
        flags.Children.Add(_orderPrintLinesAndTitles);
        _orderOccasionalOrders = new CheckBox
        {
            Content = "Pedidos Ocasionales",
            IsChecked = true
        };
        flags.Children.Add(_orderOccasionalOrders);
        flags.SizeChanged += (_, args) =>
        {
            flags.Orientation = args.NewSize.Width >= 620
                ? Orientation.Horizontal
                : Orientation.Vertical;
        };
        Grid.SetColumn(flags, 1);
        Grid.SetColumnSpan(flags, 3);
        options.Children.Add(flags);
        configurationHost.Children.Add(options);

        var fields = new Grid
        {
            ColumnSpacing = 18,
            RowSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (var index = 0; index < 3; index++)
        {
            fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        _orderDischargeTypeSelector = AddOrderField(fields, 0, "Tipo Descarga", "Tipo descarga");
        _orderProductSelector = AddOrderField(fields, 1, "Producto Solicitado", "Producto");
        _orderUseSelector = AddOrderField(fields, 2, "Uso", "Condiciones pago");
        fields.SizeChanged += (_, args) =>
        {
            var columnCount = args.NewSize.Width >= 780 ? 3 : args.NewSize.Width >= 520 ? 2 : 1;
            for (var index = 0; index < fields.Children.Count; index++)
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
        configurationHost.Children.Add(fields);
        host.Children.Add(configurationHost);

        var schedulesHost = new StackPanel { Spacing = 8 };
        _orderSchedulesHost = schedulesHost;
        _orderScheduleRows.Children.Clear();
        _orderScheduleRows.Spacing = 8;
        _orderScheduleRows.HorizontalAlignment = HorizontalAlignment.Stretch;
        _orderScheduleFieldControls.Clear();
        _orderScheduleList = new ListView
        {
            Header = "Turnos agregados",
            SelectionMode = ListViewSelectionMode.Single,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 76,
            MaxHeight = 180
        };
        _orderScheduleList.SelectionChanged += OrderScheduleList_SelectionChanged;
        schedulesHost.Children.Add(_orderScheduleList);
        AddOrderScheduleRow(new OrderScheduleRecord(
            1,
            string.Empty,
            new TimeSpan(13, 20, 0),
            new TimeSpan(13, 20, 0)));
        schedulesHost.Children.Add(_orderScheduleRows);

        _orderConditionsField = new TextBox
        {
            Header = "Condiciones y observaciones",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 120,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        schedulesHost.Children.Add(_orderConditionsField);
        SetOrderSchedules(
            new[]
            {
                new OrderScheduleRecord(
                    1,
                    string.Empty,
                    new TimeSpan(13, 20, 0),
                    new TimeSpan(13, 20, 0))
            },
            0);
        host.Children.Add(schedulesHost);
    }

    private ComboBox AddOrderField(
        Grid parent,
        int index,
        string label,
        string title)
    {
        var selector = new ComboBox
        {
            Header = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 160
        };
        var titleField = new TextBox
        {
            Header = "Título",
            Text = title,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 160,
            MaxLength = 250
        };
        _orderTitleFields.Add(
            label switch
            {
                "Tipo Descarga" => "Tipo",
                "Producto Solicitado" => "Producto",
                "Uso" => "Uso",
                _ => throw new InvalidOperationException($"No se reconoce el título de '{label}'.")
            },
            titleField);

        var field = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        field.Children.Add(selector);
        field.Children.Add(titleField);
        Grid.SetRow(field, index / 3);
        Grid.SetColumn(field, index % 3);
        parent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        parent.Children.Add(field);
        return selector;
    }

    private OrderScheduleFields CreateOrderScheduleRow(OrderScheduleRecord schedule)
    {
        var row = new Grid
        {
            ColumnSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var index = 0; index < 5; index++)
        {
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var shift = new TextBox
        {
            Header = "Turno",
            Text = schedule.Position?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            PlaceholderText = "Posición",
            MaxLength = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0
        };
        Grid.SetColumn(shift, 0);
        row.Children.Add(shift);

        var start = new TimePicker
        {
            Header = "Inicio",
            Time = schedule.StartTime,
            ClockIdentifier = "12HourClock",
            MinuteIncrement = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0
        };
        Grid.SetColumn(start, 1);
        row.Children.Add(start);

        var end = new TimePicker
        {
            Header = "Término",
            Time = schedule.EndTime,
            ClockIdentifier = "12HourClock",
            MinuteIncrement = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 0
        };
        Grid.SetColumn(end, 2);
        row.Children.Add(end);

        var add = new Button
        {
            Content = new SymbolIcon(Symbol.Add),
            Tag = row,
            Width = 44,
            Height = 38,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 4)
        };
        ToolTipService.SetToolTip(add, "Agregar un turno");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(add, "Agregar turno");
        add.Click += AddOrderScheduleRow_Click;
        Grid.SetColumn(add, 3);
        row.Children.Add(add);
        var remove = new Button
        {
            Content = new SymbolIcon(Symbol.Remove),
            Tag = row,
            Width = 44,
            Height = 38,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 4)
        };
        ToolTipService.SetToolTip(remove, "Eliminar el turno seleccionado");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, "Eliminar turno seleccionado");
        remove.Click += RemoveOrderScheduleEntry_Click;
        Grid.SetColumn(remove, 4);
        row.Children.Add(remove);
        row.SizeChanged += (_, args) =>
        {
            var columnCount = args.NewSize.Width >= 900 ? 5 : args.NewSize.Width >= 650 ? 3 : args.NewSize.Width >= 420 ? 2 : 1;
            for (var index = 0; index < row.Children.Count; index++)
            {
                if (row.Children[index] is FrameworkElement field)
                {
                    Grid.SetRow(field, index / columnCount);
                    Grid.SetColumn(field, index % columnCount);
                }
            }

            for (var index = 0; index < row.ColumnDefinitions.Count; index++)
            {
                if (index >= columnCount)
                {
                    row.ColumnDefinitions[index].Width = new GridLength(0);
                }
                else if (columnCount == 5 && index >= 3)
                {
                    row.ColumnDefinitions[index].Width = GridLength.Auto;
                }
                else
                {
                    row.ColumnDefinitions[index].Width = new GridLength(
                        index == 0 ? 1.5 : 1,
                        GridUnitType.Star);
                }
            }
        };
        return new OrderScheduleFields(row, shift, start, end);
    }

    private void AddOrderScheduleRow_Click(object sender, RoutedEventArgs e)
    {
        if (_orderScheduleFieldControls.Count != 1)
        {
            ShowStatus("No se pudo agregar el turno.", InfoBarSeverity.Error);
            return;
        }

        try
        {
            var fields = _orderScheduleFieldControls.Single();
            var position = ParseOrderPosition(fields.Position.Text, _orderSchedules.Count + 1);
            _orderSchedules.Add(new OrderScheduleRecord(
                position,
                _orderConditionsField?.Text.Trim() ?? string.Empty,
                fields.StartTime.Time,
                fields.EndTime.Time));
            RefreshOrderScheduleList(_orderSchedules.Count - 1);
            ShowStatus(
                $"Turno agregado. Hay {_orderSchedules.Count} turnos; pulsa Guardar para conservarlos.",
                InfoBarSeverity.Success);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void RemoveOrderScheduleEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedOrderScheduleIndex < 0 || _selectedOrderScheduleIndex >= _orderSchedules.Count)
        {
            ShowStatus("Selecciona el turno que deseas eliminar.", InfoBarSeverity.Warning);
            return;
        }

        if (_orderSchedules.Count <= 1)
        {
            ShowStatus("Debe quedar al menos un turno configurado.", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            CommitSelectedOrderSchedule();
            var removedPosition = _orderSchedules[_selectedOrderScheduleIndex].Position;
            _orderSchedules.RemoveAt(_selectedOrderScheduleIndex);
            var selectedIndex = Math.Min(_selectedOrderScheduleIndex, _orderSchedules.Count - 1);
            RefreshOrderScheduleList(selectedIndex);
            ShowStatus(
                $"Se eliminó el turno {removedPosition}. Quedan {_orderSchedules.Count}; pulsa Guardar para conservar el cambio.",
                InfoBarSeverity.Success);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void OrderScheduleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingOrderScheduleList || _orderScheduleList is null)
        {
            return;
        }

        var newIndex = _orderScheduleList.SelectedIndex;
        try
        {
            CommitSelectedOrderSchedule();
        }
        catch (InvalidOperationException exception)
        {
            _updatingOrderScheduleList = true;
            _orderScheduleList.SelectedIndex = _selectedOrderScheduleIndex;
            _updatingOrderScheduleList = false;
            ShowStatus(exception.Message, InfoBarSeverity.Error);
            return;
        }

        if (_selectedOrderScheduleIndex >= 0)
        {
            _orderScheduleList.Items[_selectedOrderScheduleIndex] = FormatOrderScheduleLine(
                _orderSchedules[_selectedOrderScheduleIndex].Position ?? 0,
                _orderSchedules[_selectedOrderScheduleIndex].StartTime,
                _orderSchedules[_selectedOrderScheduleIndex].EndTime);
        }

        _selectedOrderScheduleIndex = newIndex;
        LoadSelectedOrderScheduleIntoFields();
    }

    private void SetOrderSchedules(IEnumerable<OrderScheduleRecord> schedules, int selectedIndex)
    {
        _orderSchedules.Clear();
        _orderSchedules.AddRange(schedules);
        RefreshOrderScheduleList(selectedIndex);
    }

    private void RefreshOrderScheduleList(int selectedIndex)
    {
        if (_orderScheduleList is null)
        {
            return;
        }

        _updatingOrderScheduleList = true;
        _orderScheduleList.Items.Clear();
        foreach (var schedule in _orderSchedules)
        {
            _orderScheduleList.Items.Add(FormatOrderScheduleLine(
                schedule.Position ?? 0,
                schedule.StartTime,
                schedule.EndTime));
        }

        _selectedOrderScheduleIndex = _orderSchedules.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, _orderSchedules.Count - 1);
        _orderScheduleList.SelectedIndex = _selectedOrderScheduleIndex;
        _updatingOrderScheduleList = false;
        LoadSelectedOrderScheduleIntoFields();
    }

    private void LoadSelectedOrderScheduleIntoFields()
    {
        if (_selectedOrderScheduleIndex < 0 || _selectedOrderScheduleIndex >= _orderSchedules.Count)
        {
            return;
        }

        var schedule = _orderSchedules[_selectedOrderScheduleIndex];
        var fields = _orderScheduleFieldControls.Single();
        fields.Position.Text = schedule.Position?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        fields.StartTime.Time = schedule.StartTime;
        fields.EndTime.Time = schedule.EndTime;
        if (_orderConditionsField is not null)
        {
            _orderConditionsField.Text = schedule.Description;
        }
    }

    private void CommitSelectedOrderSchedule()
    {
        if (_selectedOrderScheduleIndex < 0 || _selectedOrderScheduleIndex >= _orderSchedules.Count)
        {
            return;
        }

        var fields = _orderScheduleFieldControls.Single();
        var position = ParseOrderPosition(fields.Position.Text, _selectedOrderScheduleIndex + 1);
        _orderSchedules[_selectedOrderScheduleIndex] = new OrderScheduleRecord(
            position,
            _orderConditionsField?.Text.Trim() ?? string.Empty,
            fields.StartTime.Time,
            fields.EndTime.Time);
    }

    private void AddOrderScheduleRow(OrderScheduleRecord schedule)
    {
        var fields = CreateOrderScheduleRow(schedule);
        _orderScheduleFieldControls.Add(fields);
        _orderScheduleRows.Children.Add(fields.Container);
    }

    private void BuildAccesses(StackPanel host)
    {
        _accessLayoutGrid = new Grid
        {
            ColumnSpacing = 16,
            MinHeight = 460,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _accessLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.32, GridUnitType.Star) });
        _accessLayoutGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.68, GridUnitType.Star) });
        _accessLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _accessLayoutGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _accessLayoutGrid.SizeChanged += AccessLayoutGrid_SizeChanged;
        host.Children.Add(_accessLayoutGrid);

        var usersSection = CreateSection("Usuarios registrados");
        _accessUsersSection = usersSection.Root;
        _accessLayoutGrid.Children.Add(usersSection.Root);
        _accessProfilesList = new ListView
        {
            DisplayMemberPath = nameof(AccessProfile.Name),
            SelectionMode = ListViewSelectionMode.Single,
            Height = 440,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _accessProfilesList.SelectionChanged += AccessProfilesList_SelectionChanged;
        usersSection.Content.Children.Add(_accessProfilesList);

        var permissionsSection = CreateSection("Accesos");
        _accessPermissionsSection = permissionsSection.Root;
        _accessLayoutGrid.Children.Add(permissionsSection.Root);
        _accessPermissionsTree = new TreeView
        {
            ItemTemplate = (DataTemplate)Resources["AccessTreeItemTemplate"],
            Height = 480,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        permissionsSection.Content.Children.Add(_accessPermissionsTree);
        Grid.SetColumn(usersSection.Root, 0);
        Grid.SetColumn(permissionsSection.Root, 1);
    }

    private void AccessLayoutGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_accessLayoutGrid is null
            || _accessUsersSection is null
            || _accessPermissionsSection is null
            || _accessProfilesList is null
            || _accessPermissionsTree is null)
        {
            return;
        }

        var isCompact = e.NewSize.Width < 760;
        _accessLayoutGrid.ColumnSpacing = isCompact ? 0 : 16;
        _accessLayoutGrid.ColumnDefinitions[0].Width = new GridLength(
            isCompact ? 1 : 0.32,
            GridUnitType.Star);
        _accessLayoutGrid.ColumnDefinitions[1].Width = new GridLength(
            isCompact ? 1 : 0.68,
            GridUnitType.Star);

        Grid.SetRow(_accessUsersSection, 0);
        Grid.SetColumn(_accessUsersSection, 0);
        Grid.SetRow(_accessPermissionsSection, isCompact ? 1 : 0);
        Grid.SetColumn(_accessPermissionsSection, isCompact ? 0 : 1);
        _accessProfilesList.Height = isCompact ? 150 : 440;
        _accessPermissionsTree.Height = isCompact ? 420 : 480;
        _accessLayoutGrid.MinHeight = isCompact ? 520 : 460;
    }

    private StackPanel AddSection(Panel host, string title, bool compact = false)
    {
        var section = CreateSection(title, compact);
        host.Children.Add(section.Root);
        return section.Content;
    }

    private AccessSection CreateSection(string title, bool compact = false)
    {
        var content = new StackPanel { Spacing = compact ? 3 : 10 };
        var section = new StackPanel { Spacing = compact ? 5 : 8 };
        section.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = compact ? 14 : 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        var border = new Border
        {
            Padding = new Thickness(compact ? 8 : 12),
            Background = _themeBrushes.GetBrush("#172337"),
            BorderBrush = _themeBrushes.GetBrush("#40516A"),
            BorderThickness = new Thickness(1),
            Child = content
        };
        section.Children.Add(border);
        return new AccessSection(section, content);
    }

    private void AddSelectorGroup(StackPanel host, string title, (string Label, string[] Options, string Selected)[] fields)
    {
        var section = AddSection(host, title);
        foreach (var (label, options, selected) in fields)
        {
            AddSelector(section, label, options, selected);
        }
    }

    private void AddRemissionCheckBox(StackPanel parent, string column, string label)
    {
        var field = new CheckBox { Content = label };
        _remissionOptions.Add(column, field);
        parent.Children.Add(field);
    }

    private void AddRemissionTextField(StackPanel parent, string column, string label)
    {
        var field = new TextBox
        {
            Header = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 180
        };
        _remissionTextFields.Add(column, field);
        parent.Children.Add(field);
    }

    private void AddRemissionAlignmentSelector(
        StackPanel parent,
        string column,
        string label,
        params RemissionAlignmentOption[] options)
    {
        var selector = new ComboBox
        {
            Header = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 180,
            DisplayMemberPath = nameof(RemissionAlignmentOption.Label)
        };
        foreach (var option in options)
        {
            selector.Items.Add(option);
        }

        _remissionSelectors.Add(column, selector);
        parent.Children.Add(selector);
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
        field.Children.Add(new TextBlock { Text = label, Foreground = _themeBrushes.GetBrush("#E6EDF7") });
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
        if (_backupToolsOnly || CanSaveConfigurationTab?.Invoke(_selectedConfigurationTab) != true)
        {
            ShowStatus("Tu perfil no tiene permiso para guardar esta configuración.", InfoBarSeverity.Error);
            return;
        }

        if (_selectedConfigurationTab is >= 4 and <= 8
            && CanEditDesignerCanvas?.Invoke() != true)
        {
            ShowStatus(
                "El lienzo está bloqueado. Desbloquéalo con el candado antes de guardar cambios que afectan a sus elementos.",
                InfoBarSeverity.Warning);
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            if (_selectedConfigurationTab == 3)
            {
                if (_remissionSettingsId is not int id)
                {
                    throw new InvalidOperationException(
                        "Carga el ajuste de remisión desde dbo.AjusteRemision antes de guardarlo.");
                }

                var settings = new RemissionConfigurationUpdate(
                    id,
                    _remissionOptions.ToDictionary(
                        option => option.Key,
                        option => option.Value.IsChecked == true,
                        StringComparer.Ordinal),
                    _remissionTextFields.ToDictionary(
                        field => field.Key,
                        field => field.Value.Text.Trim(),
                        StringComparer.Ordinal),
                    _remissionSelectors.ToDictionary(
                        selector => selector.Key,
                        selector => (selector.Value.SelectedItem as RemissionAlignmentOption
                            ?? throw new InvalidOperationException(
                                $"Selecciona un valor para '{selector.Value.Header}' antes de guardar.")).Code,
                        StringComparer.Ordinal));

                await _remissionConfigurationService.SaveSettingsAsync(settings);
                ShowStatus("Ajuste de remisión guardado en dbo.AjusteRemision.", InfoBarSeverity.Success);
            }
            else if (_selectedConfigurationTab == 2)
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
                PlantNameSaved?.Invoke(this, companySettings.PlantName);
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
                MessagesSaved?.Invoke(updates);
            }
            else if (_selectedConfigurationTab == 8)
            {
                var selections = _additionalSelectors.Select(selector =>
                {
                    var value = selector.Value.SelectedItem as string;
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new InvalidOperationException(
                            $"Selecciona un valor para '{selector.Key}' antes de guardar.");
                    }

                    return new AdditionalConfigurationSelection(selector.Key, value);
                }).ToArray();

                await _additionalConfigurationService.SaveConfigurationAsync(selections);
                ShowStatus("Adicionales guardados en dbo.ConfigMaq.", InfoBarSeverity.Success);
                AdditionalConfigurationSaved?.Invoke(this, EventArgs.Empty);
            }
            else if (_selectedConfigurationTab == 9)
            {
                var canSaveOrderSettings = HasProfileModuleAccess?.Invoke("PedidosC") == true
                    && CanSaveProfileModule("Configurar Pedido");
                var canSaveSchedules = HasProfileModuleAccess?.Invoke("PedidosC") == true
                    && CanSaveProfileModule("Turnos");
                if (!canSaveOrderSettings && !canSaveSchedules)
                {
                    throw new InvalidOperationException(
                        "Tu perfil no tiene permiso para guardar la configuración de pedidos ni los turnos.");
                }

                var savedSections = new List<string>();
                if (canSaveOrderSettings)
                {
                    if (_orderConfigurationId is not int orderConfigurationId)
                    {
                        throw new InvalidOperationException(
                            "Carga la configuración desde dbo.AjustePedido antes de guardarla.");
                    }

                    var orderSettings = new OrderConfigurationUpdate(
                        orderConfigurationId,
                        _orderNumberField!.Text.Trim(),
                        _orderIncludeCompanyName!.IsChecked == true,
                        _orderPrintLinesAndTitles!.IsChecked == true,
                        _orderOccasionalOrders!.IsChecked == true,
                        _orderTitleFields["Tipo"].Text,
                        _orderTitleFields["Producto"].Text,
                        _orderTitleFields["Uso"].Text);
                    await _orderConfigurationService.SaveSettingsAsync(orderSettings);
                    savedSections.Add("configuración de pedidos guardada en dbo.AjustePedido");
                }

                if (canSaveSchedules)
                {
                    var schedules = BuildOrderSchedulesForSave();
                    await _orderScheduleConfigurationService.SaveSchedulesAsync(schedules);
                    savedSections.Add(
                        $"{schedules.Count(schedule => schedule.Position.HasValue)} turnos y observaciones guardados en dbo.Horarios");
                }

                ShowStatus(string.Join("; ", savedSections), InfoBarSeverity.Success);
            }
            else if (_selectedConfigurationTab == 10)
            {
                if (_accessProfilesList?.SelectedItem is not AccessProfile profile)
                {
                    throw new InvalidOperationException("Selecciona un perfil antes de guardar sus accesos.");
                }

                var templateProfileIds = _accessPermissionItems
                    .Select(control => control.TemplateProfileId)
                    .Where(templateProfileId => templateProfileId.HasValue)
                    .Select(templateProfileId => templateProfileId!.Value)
                    .Distinct()
                    .ToArray();
                if (templateProfileIds.Length > 1)
                {
                    throw new InvalidOperationException("Los accesos mostrados no comparten un perfil de referencia.");
                }

                if (_accessPermissionItems.Count == 0)
                {
                    throw new InvalidOperationException(
                        "No hay definiciones de accesos para mostrar o guardar en dbo.PerfilDetalle.");
                }

                var changes = _accessPermissionItems
                    .Select(control => new AccessPermissionChange(
                        control.DetailId,
                        control.Column,
                        control.IsEnabled,
                        control.TemplateDetailId))
                    .ToArray();
                await _accessConfigurationService.SavePermissionChangesAsync(
                    profile.Id,
                    templateProfileIds.Length == 0 ? null : templateProfileIds[0],
                    changes);
                await LoadAccessPermissionsAsync(profile);
                ShowStatus($"Accesos de {profile.Name} guardados en dbo.PerfilDetalle.", InfoBarSeverity.Success);
                AccessPermissionsSaved?.Invoke(this, EventArgs.Empty);
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
            SaveButton.IsEnabled = !_backupToolsOnly
                && CanSaveConfigurationTab?.Invoke(_selectedConfigurationTab) == true;
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

    private static int ParseOrderPosition(string value, int rowNumber)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var position)
            || position < 0)
        {
            throw new InvalidOperationException(
                $"Turno {rowNumber} debe ser un número entero mayor o igual a 0.");
        }

        return position;
    }

    private static bool TryParseLegacyOrderSchedule(string value, out OrderScheduleRecord schedule)
    {
        schedule = new OrderScheduleRecord(null, string.Empty, TimeSpan.Zero, TimeSpan.Zero);
        var line = value.Trim();
        const string prefix = "Turno ";
        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var positionEnd = line.IndexOf(':');
        if (positionEnd <= prefix.Length
            || !int.TryParse(
                line.Substring(prefix.Length, positionEnd - prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var position))
        {
            return false;
        }

        var timeRange = line[(positionEnd + 1)..].Trim();
        var separator = timeRange.IndexOf(" - ", StringComparison.Ordinal);
        if (separator < 0
            || !DateTime.TryParse(
                timeRange[..separator],
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var start)
            || !DateTime.TryParse(
                timeRange[(separator + 3)..],
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var end))
        {
            return false;
        }

        schedule = new OrderScheduleRecord(position, string.Empty, start.TimeOfDay, end.TimeOfDay);
        return true;
    }

    private static string FormatOrderScheduleLine(int position, TimeSpan startTime, TimeSpan endTime)
    {
        var start = DateTime.Today.Add(startTime).ToString("t", CultureInfo.CurrentCulture);
        var end = DateTime.Today.Add(endTime).ToString("t", CultureInfo.CurrentCulture);
        return $"Turno {position}: {start} - {end}";
    }

    private OrderScheduleRecord[] BuildOrderSchedulesForSave()
    {
        CommitSelectedOrderSchedule();
        RefreshOrderScheduleList(_selectedOrderScheduleIndex);
        if (_orderSchedules.Count == 0)
        {
            throw new InvalidOperationException("La configuración debe contener al menos un turno.");
        }

        return _orderSchedules
            .Select((schedule, index) =>
            {
                var position = schedule.Position
                    ?? throw new InvalidOperationException($"Turno {index + 1} debe tener una posición.");
                ParseOrderPosition(position.ToString(CultureInfo.InvariantCulture), index + 1);
                return schedule with
                {
                    Position = position,
                    Description = schedule.Description.Trim()
                };
            })
            .ToArray();
    }

    private sealed record RemissionAlignmentOption(string Label, int Code)
    {
        public override string ToString() => Label;
    }

    private sealed record OrderScheduleFields(
        Grid Container,
        TextBox Position,
        TimePicker StartTime,
        TimePicker EndTime);

    private sealed class AccessPermissionItem(
        string title,
        bool isEnabled,
        int detailId,
        string column,
        int? templateProfileId,
        int? templateDetailId)
    {
        public string Title { get; } = title;
        public bool IsEnabled { get; set; } = isEnabled;
        public int DetailId { get; } = detailId;
        public string Column { get; } = column;
        public int? TemplateProfileId { get; } = templateProfileId;
        public int? TemplateDetailId { get; } = templateDetailId;
    }

    private sealed record AccessSection(StackPanel Root, StackPanel Content);

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

    private sealed record AdditionalOptionEditorItem
    {
        public override string ToString() => "AGREGAR / BORRAR";
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
        StatusInfoBarStyler.Apply(StatusInfoBar, RequestedTheme);
        StatusInfoBar.IsOpen = true;
    }
}
