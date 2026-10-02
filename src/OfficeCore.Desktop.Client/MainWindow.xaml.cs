using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;

namespace OfficeCore.Desktop.Client;

public partial class MainWindow : Window
{
    private ClientConfig _config;
    private readonly HttpClient _httpClient = new() 
    { 
        Timeout = TimeSpan.FromSeconds(2.5),
        DefaultRequestHeaders = { { "X-OfficeCore-Client", "OfficeCore-Desktop-Workstation" } }
    };

    public MainWindow()
    {
        InitializeComponent();
        _config = ClientConfigService.Current;
        InitializeView();
    }

    private async void InitializeView()
    {
        try
        {
            var udataDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OfficeCore", "Client_WebView2");
            System.IO.Directory.CreateDirectory(udataDir);
            var env = await CoreWebView2Environment.CreateAsync(null, udataDir);

            await webView.EnsureCoreWebView2Async(env);

            // ── Production Security: disable right-click menu and DevTools (F12) ──
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;


            webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            webView.CoreWebView2.WebResourceRequested += (s, args) =>
            {
                args.Request.Headers.SetHeader("X-OfficeCore-Client", "OfficeCore-Desktop-Workstation");
            };
            webView.NavigationCompleted += WebView_NavigationCompleted;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}", "WebView Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        ServerUrlInput.Text = _config.ServerUrl;
        AutoConnectCheckBox.IsChecked = _config.AutoConnect;

        if (_config.AutoConnect && !string.IsNullOrWhiteSpace(_config.ServerUrl))
        {
            _ = AttemptAutoConnectAsync(_config.ServerUrl);
        }
    }

    private async Task AttemptAutoConnectAsync(string url)
    {
        StatusFeedbackText.Text = "Auto-connecting to server...";
        StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));
        ConnectButton.IsEnabled = false;

        bool isReachable = await PingServerAsync(url);
        ConnectButton.IsEnabled = true;

        if (isReachable)
        {
            ConnectToServer(url);
        }
        else
        {
            StatusFeedbackText.Text = $"✗ Server offline or unreachable at {url}.";
            StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
            // Keep user on the Connect screen so they can edit URL or test ping, rather than freezing WebView
        }
    }

    private string NormalizeUrl(string input)
    {
        var url = input.Trim();
        if (string.IsNullOrEmpty(url)) return string.Empty;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        return url.TrimEnd('/');
    }

    private async Task<bool> PingServerAsync(string url)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        var rawUrl = ServerUrlInput.Text;
        var url = NormalizeUrl(rawUrl);

        if (string.IsNullOrEmpty(url))
        {
            StatusFeedbackText.Text = "Please enter a valid server URL.";
            StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
            return;
        }

        StatusFeedbackText.Text = "Testing connection to server...";
        StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FACC15"));
        TestButton.IsEnabled = false;

        var isOnline = await PingServerAsync(url);
        TestButton.IsEnabled = true;

        if (isOnline)
        {
            StatusFeedbackText.Text = "✓ OfficeCore Server Station is online and reachable!";
            StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#34D399"));
        }
        else
        {
            StatusFeedbackText.Text = "✗ Cannot connect to server station. Please check the IP and port.";
            StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
        }
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var url = NormalizeUrl(ServerUrlInput.Text);
        if (string.IsNullOrEmpty(url))
        {
            StatusFeedbackText.Text = "Please specify a server address.";
            StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));
            return;
        }

        _config.ServerUrl = url;
        _config.AutoConnect = AutoConnectCheckBox.IsChecked == true;
        ClientConfigService.Save(_config);

        ConnectToServer(url);
    }

    private void ServerUrlInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ConnectButton_Click(sender, e);
        }
    }

    private void ConnectToServer(string url)
    {
        CurrentConnectedUrlText.Text = url;
        ConnectPanel.Visibility = Visibility.Collapsed;
        WebViewContainer.Visibility = Visibility.Visible;
        WebFullToolbar.Visibility = Visibility.Visible;
        WebMiniBar.Visibility = Visibility.Collapsed;

        webView.CoreWebView2?.Navigate(url);
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (webView.CoreWebView2 != null && webView.Source != null && webView.Source.ToString() != "about:blank")
        {
            webView.CoreWebView2.Navigate("about:blank");
        }
        WebViewContainer.Visibility = Visibility.Collapsed;
        ConnectPanel.Visibility = Visibility.Visible;
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        webView.CoreWebView2?.Reload();
    }

    private void GoBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (webView.CoreWebView2 != null && webView.CoreWebView2.CanGoBack)
        {
            webView.CoreWebView2.GoBack();
        }
    }

    private void GoForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (webView.CoreWebView2 != null && webView.CoreWebView2.CanGoForward)
        {
            webView.CoreWebView2.GoForward();
        }
    }

    private void ToggleToolbar_Click(object sender, RoutedEventArgs e)
    {
        if (WebFullToolbar.Visibility == Visibility.Visible)
        {
            WebFullToolbar.Visibility = Visibility.Collapsed;
            WebMiniBar.Visibility = Visibility.Visible;
        }
        else
        {
            WebFullToolbar.Visibility = Visibility.Visible;
            WebMiniBar.Visibility = Visibility.Collapsed;
        }
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            var uriStr = webView.Source?.ToString();
            if (!string.IsNullOrEmpty(uriStr) && !uriStr.Equals("about:blank", StringComparison.OrdinalIgnoreCase))
            {
                DisconnectButton_Click(this, new RoutedEventArgs());
                StatusFeedbackText.Text = $"✗ Could not connect to {uriStr}. Server station might be offline.";
                StatusFeedbackText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F87171"));

                MessageBox.Show(
                    $"Failed to connect to the server at:\n{uriStr}\n\nMake sure the OfficeCore Server Station is running on the host machine and your device is on the same network.",
                    "Server Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
