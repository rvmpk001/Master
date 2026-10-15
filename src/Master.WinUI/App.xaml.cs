using Microsoft.UI.Xaml;
using Master.WinUI.Data;

namespace Master.WinUI;

public partial class App : Application
{
    private Window? _window;
    private readonly AuthenticationRepository _authenticationRepository;
    private readonly DashboardRepository _dashboardRepository;

    public App()
    {
        InitializeComponent();

        var connectionFactory = new SqlServerConnectionFactory();
        _authenticationRepository = new AuthenticationRepository(connectionFactory);
        _dashboardRepository = new DashboardRepository(connectionFactory);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow(_authenticationRepository, _dashboardRepository);
        _window.Activate();
    }
}
