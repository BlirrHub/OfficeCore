using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OfficeCore.DesktopClient;

public static class NetworkHelper
{
    /// <summary>
    /// Discovers the local primary IPv4 address on the office LAN/Wi-Fi network.
    /// Filters out loopback, virtual (Hyper-V/Docker/WSL), and link-local addresses.
    /// </summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                              && (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                                  || nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                              && !nic.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                              && !nic.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                              && !nic.Description.Contains("WSL", StringComparison.OrdinalIgnoreCase)
                              && !nic.Description.Contains("Docker", StringComparison.OrdinalIgnoreCase));

            foreach (var nic in interfaces)
            {
                var ipProps = nic.GetIPProperties();
                var ipv4 = ipProps.UnicastAddresses
                    .FirstOrDefault(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork
                                            && !IPAddress.IsLoopback(addr.Address)
                                            && !addr.Address.ToString().StartsWith("169.254."));

                if (ipv4 != null)
                {
                    return ipv4.Address.ToString();
                }
            }

            // Fallback to general host lookup
            var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
            var fallback = hostEntry.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork 
                                      && !IPAddress.IsLoopback(ip)
                                      && !ip.ToString().StartsWith("169.254."));

            if (fallback != null)
            {
                return fallback.ToString();
            }
        }
        catch (Exception)
        {
            // Fall through
        }

        return "127.0.0.1";
    }

    public static string GetServerShareUrl(int port = 5026)
    {
        var ip = GetLocalIpAddress();
        return $"http://{ip}:{port}";
    }
}
