using Microsoft.Web.WebView2.Core;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OfficeCore.DesktopClient;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        InitializeAsync();
    }

    async void InitializeAsync()
    {
        await webView.EnsureCoreWebView2Async(null);
        
        webView.NavigationCompleted += WebView_NavigationCompleted;

        // Load saved IP if any
        if (System.IO.File.Exists("server_ip.txt"))
        {
            IpAddressInput.Text = System.IO.File.ReadAllText("server_ip.txt");
        }
    }

    private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            // Successfully connected, hide the input and show the web view
            InputPanel.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Visible;
        }
        else
        {
            // Failed to connect, show the input panel again
            MessageBox.Show("Failed to connect to the server. Please check the IP address.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            InputPanel.Visibility = Visibility.Visible;
            webView.Visibility = Visibility.Collapsed;
        }
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        LoadServer();
    }

    private void LoadServer()
    {
        string ip = IpAddressInput.Text.Trim();
        if (string.IsNullOrEmpty(ip)) return;

        // Ensure http scheme
        if (!ip.StartsWith("http://") && !ip.StartsWith("https://"))
        {
            ip = "http://" + ip;
        }

        // Save for next launch
        System.IO.File.WriteAllText("server_ip.txt", IpAddressInput.Text.Trim());

        if (webView != null && webView.CoreWebView2 != null)
        {
            // Hide input immediately to show it's loading, it will revert if it fails
            InputPanel.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Visible;
            webView.CoreWebView2.Navigate(ip);
        }
    }
}