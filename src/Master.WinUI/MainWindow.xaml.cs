using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using WinRT.Interop;
using Master.WinUI.Data;

namespace Master.WinUI;

public sealed partial class MainWindow : Window
{
    private static readonly string RememberedUsernamePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Master.WinUI",
        "remembered-user.txt");
    private readonly AuthenticationRepository _authenticationRepository;
    private readonly DashboardRepository _dashboardRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ProductRepository _productRepository;
    private readonly SalesRepository _salesRepository;
    private DashboardWindow? _dashboardWindow;
    private bool _loadingRememberedUsername;

    internal MainWindow(
        AuthenticationRepository authenticationRepository,
        DashboardRepository dashboardRepository,
        CustomerRepository customerRepository,
        ProductRepository productRepository,
        SalesRepository salesRepository)
    {
        InitializeComponent();
        _authenticationRepository = authenticationRepository;
        _dashboardRepository = dashboardRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _salesRepository = salesRepository;
        LoadRememberedUsername();

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        if (AppWindow.GetFromWindowId(windowId).Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private void OnPasswordVisibilityChanged(object sender, RoutedEventArgs e)
    {
        var showPassword = chkVerPassword.IsChecked == true;

        if (showPassword)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPassword.Password = string.Empty;
            txtPassword.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Visibility = Visibility.Visible;
        }
        else
        {
            txtPassword.Password = txtPasswordVisible.Text;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Text = string.Empty;
            txtPassword.Visibility = Visibility.Visible;
        }
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        var username = txtUsuario.Text.Trim();
        var password = chkVerPassword.IsChecked == true
            ? txtPasswordVisible.Text
            : txtPassword.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            ShowLoginStatus(
                InfoBarSeverity.Warning,
                "Datos requeridos",
                "Escribe el usuario y la contraseña.");
            return;
        }

        if (username.Length > 50)
        {
            ShowLoginStatus(
                InfoBarSeverity.Warning,
                "Usuario demasiado largo",
                "El usuario no puede superar los 50 caracteres definidos por la base de datos.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(mtxtPin.Text))
        {
            ShowLoginStatus(
                InfoBarSeverity.Warning,
                "PIN no compatible",
                "El esquema actual no almacena ni valida PIN o información de segundo factor.");
            return;
        }

        btnIngresar.IsEnabled = false;
        try
        {
            var user = await _authenticationRepository.AuthenticateAsync(username, password);
            if (user is null)
            {
                ShowLoginStatus(
                    InfoBarSeverity.Error,
                    "Acceso denegado",
                    "Usuario o contraseña incorrectos, o la cuenta está inactiva.");
                return;
            }

            var rememberError = UpdateRememberedUsername(
                chkRecordar.IsChecked == true,
                username);
            ShowLoginStatus(
                rememberError is null ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                rememberError is null ? "Autenticación correcta" : "Sesión iniciada",
                rememberError is null
                    ? $"Sesión iniciada para {user.FullName} ({user.Role})."
                    : $"Sesión iniciada para {user.FullName} ({user.Role}), pero no se pudo guardar la preferencia: {rememberError}");
            txtPassword.Password = string.Empty;
            txtPasswordVisible.Text = string.Empty;
            mtxtPin.Text = string.Empty;
            OpenDashboard(user);
            Close();
        }
        catch (SqlException exception)
        {
            ShowLoginStatus(
                InfoBarSeverity.Error,
                "No se pudo acceder a SQL Server",
                exception.Message);
        }
        finally
        {
            btnIngresar.IsEnabled = true;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        var keepUser = chkRecordar.IsChecked == true;
        var rememberedUser = keepUser ? txtUsuario.Text : string.Empty;
        var rememberError = UpdateRememberedUsername(keepUser, rememberedUser);

        txtUsuario.Text = rememberedUser;
        if (chkVerPassword.IsChecked == true)
        {
            chkVerPassword.IsChecked = false;
        }

        txtPassword.Password = string.Empty;
        txtPasswordVisible.Text = string.Empty;
        mtxtPin.Text = string.Empty;
        txtPassword.Visibility = Visibility.Visible;
        txtPasswordVisible.Visibility = Visibility.Collapsed;

        formStatus.Severity = InfoBarSeverity.Informational;
        formStatus.Title = "Formulario cancelado";
        formStatus.Message = rememberError is not null
            ? $"Se limpiaron la contraseña y el PIN, pero no se pudo actualizar el usuario guardado: {rememberError}"
            : keepUser
                ? "Se limpiaron la contraseña y el PIN. El usuario se guardó localmente; no se guardó ni envió información a SQL Server."
                : "Se limpiaron los campos y se eliminó el usuario guardado localmente; no se guardó ni envió información a SQL Server.";
        formStatus.IsOpen = true;
    }

    private void OnRememberUserChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingRememberedUsername)
        {
            return;
        }

        if (chkRecordar.IsChecked == true && string.IsNullOrWhiteSpace(txtUsuario.Text))
        {
            return;
        }

        var error = UpdateRememberedUsername(
            chkRecordar.IsChecked == true,
            txtUsuario.Text);
        if (error is not null)
        {
            ShowLoginStatus(InfoBarSeverity.Error, "No se pudo guardar la preferencia", error);
        }
    }

    private void LoadRememberedUsername()
    {
        _loadingRememberedUsername = true;
        try
        {
            if (File.Exists(RememberedUsernamePath))
            {
                var username = File.ReadAllText(RememberedUsernamePath);
                username = username.Trim();
                if (username.Length <= 50)
                {
                    txtUsuario.Text = username;
                    chkRecordar.IsChecked = true;
                }
                else
                {
                    formStatus.Severity = InfoBarSeverity.Warning;
                    formStatus.Title = "Preferencia ignorada";
                    formStatus.Message = "El usuario guardado supera el límite admitido y no se cargó.";
                    formStatus.IsOpen = true;
                }
            }
        }
        catch (IOException exception)
        {
            ShowLoginStatus(InfoBarSeverity.Error, "No se pudo leer la preferencia", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowLoginStatus(InfoBarSeverity.Error, "No se pudo leer la preferencia", exception.Message);
        }
        finally
        {
            _loadingRememberedUsername = false;
        }
    }

    private static string? UpdateRememberedUsername(bool remember, string username)
    {
        try
        {
            if (!remember)
            {
                if (File.Exists(RememberedUsernamePath))
                {
                    File.Delete(RememberedUsernamePath);
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(username))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RememberedUsernamePath)!);
                File.WriteAllText(RememberedUsernamePath, username.Trim());
            }

            return null;
        }
        catch (IOException exception)
        {
            return exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            return exception.Message;
        }
    }

    private void OnUsernameLostFocus(object sender, RoutedEventArgs e)
    {
        if (chkRecordar.IsChecked != true || string.IsNullOrWhiteSpace(txtUsuario.Text))
        {
            return;
        }

        var error = UpdateRememberedUsername(remember: true, txtUsuario.Text);
        if (error is not null)
        {
            ShowLoginStatus(InfoBarSeverity.Error, "No se pudo guardar la preferencia", error);
        }
    }

    private void OpenDashboard(AuthenticatedUser authenticatedUser)
    {
        if (_dashboardWindow is not null)
        {
            _dashboardWindow.Activate();
            return;
        }

        var dashboardWindow = new DashboardWindow(
            _dashboardRepository,
            _customerRepository,
            _productRepository,
            _salesRepository,
            authenticatedUser);
        _dashboardWindow = dashboardWindow;
        dashboardWindow.LogoutRequested += (_, _) =>
        {
            ((App)Application.Current).ShowLogin();
            dashboardWindow.Close();
        };
        dashboardWindow.Closed += (_, _) =>
        {
            if (ReferenceEquals(_dashboardWindow, dashboardWindow))
            {
                _dashboardWindow = null;
            }
        };
        dashboardWindow.Activate();
    }

    private void ShowLoginStatus(InfoBarSeverity severity, string title, string message)
    {
        formStatus.Severity = severity;
        formStatus.Title = title;
        formStatus.Message = message;
        formStatus.IsOpen = true;
    }
}
