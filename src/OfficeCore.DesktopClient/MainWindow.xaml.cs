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
        // Load saved IP if any
        if (System.IO.File.Exists("server_ip.txt"))
        {
            IpAddressInput.Text = System.IO.File.ReadAllText("server_ip.txt");
            LoadServer();
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
            webView.CoreWebView2.Navigate(ip);
        }
    }
}