using Microsoft.UI.Xaml;
using Master.WinUI.Data;

namespace Master.WinUI;

public partial class App : Application
{
    private Window? _window;
    private readonly AuthenticationRepository _authenticationRepository;
    private readonly DashboardRepository _dashboardRepository;
    private readonly CustomerRepository _customerRepository;
    private readonly ProductRepository _productRepository;

    public App()
    {
        InitializeComponent();

        var connectionFactory = new SqlServerConnectionFactory();
        _authenticationRepository = new AuthenticationRepository(connectionFactory);
        _dashboardRepository = new DashboardRepository(connectionFactory);
        _customerRepository = new CustomerRepository(connectionFactory);
        _productRepository = new ProductRepository(connectionFactory);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ShowLogin();
    }

    internal void ShowLogin()
    {
        _window = new MainWindow(
            _authenticationRepository,
            _dashboardRepository,
            _customerRepository,
            _productRepository);
        _window.Activate();
    }
}
