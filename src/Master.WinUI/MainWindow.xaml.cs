using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using Windows.Graphics;
using WinRT.Interop;
using Master.WinUI.Data;

namespace Master.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly AuthenticationRepository _authenticationRepository;
    private readonly DashboardRepository _dashboardRepository;
    private DashboardWindow? _dashboardPreview;
    private bool _dashboardHasDataAccess;

    internal MainWindow(
        AuthenticationRepository authenticationRepository,
        DashboardRepository dashboardRepository)
    {
        InitializeComponent();
        _authenticationRepository = authenticationRepository;
        _dashboardRepository = dashboardRepository;

        var windowHandle = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow.GetFromWindowId(windowId).Resize(new SizeInt32(420, 600));
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

            ShowLoginStatus(
                InfoBarSeverity.Success,
                "Autenticación correcta",
                $"Sesión iniciada para {user.FullName} ({user.Role}).");
            txtPassword.Password = string.Empty;
            txtPasswordVisible.Text = string.Empty;
            mtxtPin.Text = string.Empty;
            OpenDashboard(allowDataAccess: true);
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

    private void OnDashboardPreviewClick(object sender, RoutedEventArgs e)
    {
        OpenDashboard(allowDataAccess: false);
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        var keepUser = chkRecordar.IsChecked == true;
        var rememberedUser = keepUser ? txtUsuario.Text : string.Empty;

        txtUsuario.Text = rememberedUser;
        if (chkVerPassword.IsChecked == true)
        {
            chkVerPassword.IsChecked = false;
        }

        txtPassword.Password = string.Empty;
        txtPasswordVisible.Text = string.Empty;
        mtxtPin.Text = string.Empty;
        chkRecordar.IsChecked = false;
        txtPassword.Visibility = Visibility.Visible;
        txtPasswordVisible.Visibility = Visibility.Collapsed;

        formStatus.Severity = InfoBarSeverity.Informational;
        formStatus.Title = "Formulario cancelado";
        formStatus.Message = keepUser
            ? "Se limpiaron la contraseña y el PIN. El usuario se conserva solo en esta ventana; no se guardó ni envió información."
            : "Se limpiaron los campos de esta ventana; no se guardó ni envió información.";
        formStatus.IsOpen = true;
    }

    private void OpenDashboard(bool allowDataAccess)
    {
        if (_dashboardPreview is not null)
        {
            if (!allowDataAccess || _dashboardHasDataAccess)
            {
                _dashboardPreview.Activate();
                return;
            }

            _dashboardPreview.Close();
        }

        _dashboardPreview = new DashboardWindow(_dashboardRepository, allowDataAccess);
        _dashboardHasDataAccess = allowDataAccess;
        _dashboardPreview.Closed += (_, _) =>
        {
            _dashboardPreview = null;
            _dashboardHasDataAccess = false;
        };
        _dashboardPreview.Activate();
    }

    private void ShowLoginStatus(InfoBarSeverity severity, string title, string message)
    {
        formStatus.Severity = severity;
        formStatus.Title = title;
        formStatus.Message = message;
        formStatus.IsOpen = true;
    }
}
