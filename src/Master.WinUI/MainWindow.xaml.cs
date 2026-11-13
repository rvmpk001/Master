using Microsoft.Data.SqlClient;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Master_WinUI.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Master_WinUI;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Closing += AppWindow_Closing;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    private bool _allowClose;
    private bool _isHandlingClose;

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        if (_isHandlingClose)
        {
            return;
        }

        _isHandlingClose = true;
        try
        {
            if (RootFrame.Content is MainPage page)
            {
                try
                {
                    await page.CompleteAccessSessionAsync();
                }
                catch (Exception exception) when (exception is SqlException or AccessLogException)
                {
                    if (!await page.ConfirmCloseWithoutAccessLogAsync(exception))
                    {
                        return;
                    }
                }
            }

            _allowClose = true;
            Close();
        }
        finally
        {
            _isHandlingClose = false;
        }
    }
}
