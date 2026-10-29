using System.Collections.ObjectModel;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Master_WinUI.Services;

namespace Master_WinUI;

public sealed partial class SecurityView : UserControl
{
    private readonly AccessConfigurationService _accessConfigurationService = new();
    private readonly UserAdministrationService _userAdministrationService = new();
    private IReadOnlyList<AccessPermissionEntry> _permissions = Array.Empty<AccessPermissionEntry>();
    private int _currentUserId;
    private string _currentUsername = string.Empty;

    public ObservableCollection<SystemUserRecord> SystemUsers { get; } = new();

    public event EventHandler? CloseRequested;

    public SecurityView()
    {
        InitializeComponent();
    }

    public async Task SetAccessAsync(
        int userId,
        string username,
        IReadOnlyList<AccessPermissionEntry> permissions)
    {
        _currentUserId = userId;
        _currentUsername = username;
        _permissions = permissions;
        UpdateActionButtons();
        await LoadUsersAsync();
    }

    private async Task<bool> LoadUsersAsync()
    {
        try
        {
            var users = await _userAdministrationService.GetUsersAsync();
            SystemUsers.Clear();
            foreach (var user in users)
            {
                SystemUsers.Add(user);
            }

            UpdateSelectionActions();
            StatusInfoBar.IsOpen = false;
            return true;
        }
        catch (SqlException exception)
        {
            SystemUsers.Clear();
            UpdateSelectionActions();
            ShowStatus($"No se pudieron cargar los usuarios (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            SystemUsers.Clear();
            UpdateSelectionActions();
            ShowStatus(exception.Message, InfoBarSeverity.Error);
            return false;
        }
    }

    private async void AddUser_Click(object sender, RoutedEventArgs e)
    {
        if (!await RefreshPermissionsAsync())
        {
            return;
        }

        if (!AccessPermissionLookup.IsActionEnabled(_permissions, "Seguridad", "Alta"))
        {
            ShowStatus("Tu perfil no tiene permiso para agregar usuarios.", InfoBarSeverity.Warning);
            return;
        }

        await ShowUserDialogAsync(null);
    }

    private async void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (!await RefreshPermissionsAsync())
        {
            return;
        }

        if (!AccessPermissionLookup.IsActionEnabled(_permissions, "Seguridad", "Editar"))
        {
            ShowStatus("Tu perfil no tiene permiso para editar usuarios.", InfoBarSeverity.Warning);
            return;
        }

        if (UsersListView.SelectedItem is SystemUserRecord user)
        {
            await ShowUserDialogAsync(user);
        }
    }

    private async void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (!await RefreshPermissionsAsync())
        {
            return;
        }

        if (!AccessPermissionLookup.IsActionEnabled(_permissions, "Seguridad", "Baja"))
        {
            ShowStatus("Tu perfil no tiene permiso para borrar usuarios.", InfoBarSeverity.Warning);
            return;
        }

        if (UsersListView.SelectedItem is not SystemUserRecord user)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Borrar usuario",
            Content = $"¿Deseas borrar el usuario «{user.Username}»?",
            PrimaryButtonText = "Borrar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await _userAdministrationService.DeleteUserAsync(user);
            if (await LoadUsersAsync())
            {
                ShowStatus($"El usuario «{user.Username}» fue eliminado.", InfoBarSeverity.Success);
            }
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudo borrar el usuario (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private async Task ShowUserDialogAsync(SystemUserRecord? user)
    {
        try
        {
            var roles = await _userAdministrationService.GetRolesAsync();
            if (roles.Count == 0)
            {
                ShowStatus("No hay tipos de usuario registrados en dbo.Password.", InfoBarSeverity.Warning);
                return;
            }

            var usernameInput = new TextBox
            {
                Header = "Usuario",
                MaxLength = 15,
                Text = user?.Username ?? string.Empty
            };
            var passwordInput = new PasswordBox
            {
                Header = user is null ? "Contraseña" : "Contraseña (dejar vacía para conservarla)",
                MaxLength = 15
            };
            var nameInput = new TextBox
            {
                Header = "Nombre",
                MaxLength = 50,
                Text = user?.Name ?? string.Empty
            };
            var roleInput = new ComboBox
            {
                Header = "Tipo",
                ItemsSource = roles,
                SelectedItem = user is null
                    ? roles[0]
                    : roles.FirstOrDefault(role => string.Equals(role, user.Role, StringComparison.OrdinalIgnoreCase))
            };
            var content = new StackPanel { Spacing = 8 };
            content.Children.Add(new TextBlock
            {
                Text = "Los permisos se asignan por separado en Configuración > Accesos.",
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(usernameInput);
            content.Children.Add(passwordInput);
            content.Children.Add(nameInput);
            content.Children.Add(roleInput);

            var dialog = new ContentDialog
            {
                Title = user is null ? "Agregar usuario" : "Editar usuario",
                Content = content,
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var username = usernameInput.Text.Trim();
            var password = passwordInput.Password;
            var name = nameInput.Text.Trim();
            var role = roleInput.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(username) || username.Length > 15)
            {
                ShowStatus("El usuario es obligatorio y admite hasta 15 caracteres.", InfoBarSeverity.Warning);
                return;
            }

            if (user is null && string.IsNullOrEmpty(password))
            {
                ShowStatus("La contraseña es obligatoria para agregar un usuario.", InfoBarSeverity.Warning);
                return;
            }

            if (password.Length > 15)
            {
                ShowStatus("La contraseña admite hasta 15 caracteres.", InfoBarSeverity.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(role) || name.Length > 50)
            {
                ShowStatus("Selecciona un tipo de usuario e ingresa un nombre de hasta 50 caracteres.", InfoBarSeverity.Warning);
                return;
            }

            if (user is null)
            {
                await _userAdministrationService.AddUserAsync(
                    username,
                    password,
                    role,
                    name,
                    _currentUsername);
            }
            else
            {
                await _userAdministrationService.UpdateUserAsync(
                    user,
                    username,
                    password,
                    role,
                    name,
                    _currentUsername);
            }

            if (await LoadUsersAsync())
            {
                ShowStatus(
                    user is null
                        ? $"El usuario «{username}» fue agregado sin accesos. Asígnalos en Configuración, Accesos."
                        : $"El usuario «{username}» fue actualizado.",
                    InfoBarSeverity.Success);
            }
        }
        catch (SqlException exception)
        {
            ShowStatus($"No se pudo guardar el usuario (SQL {exception.Number}): {exception.Message}", InfoBarSeverity.Error);
        }
        catch (InvalidOperationException exception)
        {
            ShowStatus(exception.Message, InfoBarSeverity.Error);
        }
    }

    private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateSelectionActions();

    private void UpdateSelectionActions()
    {
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        AddUserButton.IsEnabled = AccessPermissionLookup.IsActionEnabled(_permissions, "Seguridad", "Alta");
        var hasSelection = UsersListView?.SelectedItem is SystemUserRecord;
        EditUserButton.IsEnabled = hasSelection
            && AccessPermissionLookup.IsActionEnabled(_permissions, "Seguridad", "Editar");
        DeleteUserButton.IsEnabled = hasSelection
            && AccessPermissionLookup.IsActionEnabled(_permissions, "Seguridad", "Baja");
    }

    private async Task<bool> RefreshPermissionsAsync()
    {
        try
        {
            _permissions = await _accessConfigurationService.GetUserPermissionsAsync(_currentUserId);
            UpdateActionButtons();
            return true;
        }
        catch (SqlException exception)
        {
            _permissions = Array.Empty<AccessPermissionEntry>();
            UpdateActionButtons();
            ShowStatus(
                $"No se pudieron verificar los permisos del usuario (SQL {exception.Number}): {exception.Message}",
                InfoBarSeverity.Error);
            return false;
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);
}
