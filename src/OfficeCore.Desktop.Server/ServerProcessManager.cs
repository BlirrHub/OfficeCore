using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OfficeCore.Desktop.Server;

public enum ServerStatus
{
    Stopped,
    Starting,
    Running,
    Error
}

public class ServerProcessManager : IDisposable
{
    private Process? _serverProcess;
    private CancellationTokenSource? _healthCheckCts;
    private readonly HttpClient _httpClient = new() 
    { 
        Timeout = TimeSpan.FromSeconds(2),
        DefaultRequestHeaders = { { "X-OfficeCore-Client", "OfficeCore-Desktop-Workstation" } }
    };

    public ServerStatus CurrentStatus { get; private set; } = ServerStatus.Stopped;

    public event Action<ServerStatus>? StatusChanged;
    public event Action<string>? LogReceived;

    private void SetStatus(ServerStatus status)
    {
        CurrentStatus = status;
        StatusChanged?.Invoke(status);
    }

    public async Task<bool> TestDatabasePortAsync(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var completedTask = await Task.WhenAny(connectTask, Task.Delay(2000));
            return completedTask == connectTask && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public async Task StartServerAsync(ServerConfig config)
    {
        if (CurrentStatus == ServerStatus.Running || CurrentStatus == ServerStatus.Starting)
        {
            return;
        }

        SetStatus(ServerStatus.Starting);
        LogReceived?.Invoke("[DesktopHost] Checking port 5026 availability...");

        // Check if port 5026 is already occupied
        bool portInUse = await TestDatabasePortAsync("127.0.0.1", 5026);
        if (portInUse)
        {
            try
            {
                var checkResponse = await _httpClient.GetAsync("http://localhost:5026/");
                if (checkResponse.IsSuccessStatusCode)
                {
                    LogReceived?.Invoke("[DesktopHost] OfficeCore API is already active and healthy on http://localhost:5026.");
                    SetStatus(ServerStatus.Running);
                    return;
                }
            }
            catch
            {
                // Port occupied but not responding
            }

            LogReceived?.Invoke("[DesktopHost] Port 5026 is already in use by another process. Please close conflicting processes or click 'Stop Server' to clear them.");
            SetStatus(ServerStatus.Error);
            return;
        }

        LogReceived?.Invoke("[DesktopHost] Starting OfficeCore Server Station...");

        try
        {
            await Task.Run(() =>
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "OfficeCore.Api"));

                ProcessStartInfo startInfo;

                if (Directory.Exists(projectDir) && File.Exists(Path.Combine(projectDir, "OfficeCore.Api.csproj")))
                {
                    // Running from source in development
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "run --no-launch-profile",
                        WorkingDirectory = projectDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    // Use Development only when running from source
                    startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
                }
                else
                {
                    // Check local directories for published API binaries
                    var directExe = Path.Combine(baseDir, "OfficeCore.Api.exe");
                    var directDll = Path.Combine(baseDir, "OfficeCore.Api.dll");
                    var subApiExe = Path.Combine(baseDir, "Api", "OfficeCore.Api.exe");
                    var subApiDll = Path.Combine(baseDir, "Api", "OfficeCore.Api.dll");
                    var subServerExe = Path.Combine(baseDir, "Server", "OfficeCore.Api.exe");
                    var subStationExe = Path.Combine(baseDir, "OfficeCore-Server-Station", "OfficeCore.Api.exe");

                    if (File.Exists(directExe))
                    {
                        startInfo = new ProcessStartInfo
                        {
                            FileName = directExe,
                            WorkingDirectory = baseDir,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                    }
                    else if (File.Exists(subApiExe))
                    {
                        startInfo = new ProcessStartInfo
                        {
                            FileName = subApiExe,
                            WorkingDirectory = Path.GetDirectoryName(subApiExe)!,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                    }
                    else if (File.Exists(subApiDll))
                    {
                        startInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"\"{subApiDll}\"",
                            WorkingDirectory = Path.GetDirectoryName(subApiDll)!,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                    }
                    else if (File.Exists(subServerExe))
                    {
                        startInfo = new ProcessStartInfo
                        {
                            FileName = subServerExe,
                            WorkingDirectory = Path.GetDirectoryName(subServerExe)!,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                    }
                    else if (File.Exists(subStationExe))
                    {
                        startInfo = new ProcessStartInfo
                        {
                            FileName = subStationExe,
                            WorkingDirectory = Path.GetDirectoryName(subStationExe)!,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                    }
                    else
                    {
                        startInfo = new ProcessStartInfo
                        {
                            FileName = "dotnet",
                            Arguments = $"\"{directDll}\"",
                            WorkingDirectory = baseDir,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                    }

                    // Published builds always run in Production — prevents dev-only middleware
                    // (e.g. HTTPS redirection dev defaults) from activating for deployed clients
                    startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production";
                }

                startInfo.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = config.BuildConnectionString();
                startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = "http://0.0.0.0:5026";

                _serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                _serverProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LogReceived?.Invoke(e.Data);
                    }
                };

                _serverProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LogReceived?.Invoke($"[ERROR] {e.Data}");
                    }
                };

                _serverProcess.Exited += (s, e) =>
                {
                    LogReceived?.Invoke($"[DesktopHost] API process exited with code {_serverProcess?.ExitCode ?? 0}");
                    SetStatus(ServerStatus.Stopped);
                };

                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();
            });

            StartHealthCheckPolling();
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"[DesktopHost] Failed to launch server process: {ex.Message}");
            SetStatus(ServerStatus.Error);
        }
    }

    private void StartHealthCheckPolling()
    {
        _healthCheckCts?.Cancel();
        _healthCheckCts = new CancellationTokenSource();
        var token = _healthCheckCts.Token;

        Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            while (!token.IsCancellationRequested && stopwatch.Elapsed < TimeSpan.FromSeconds(45))
            {
                await Task.Delay(1000, token);

                try
                {
                    var response = await _httpClient.GetAsync("http://localhost:5026/", token);
                    if (response.IsSuccessStatusCode)
                    {
                        LogReceived?.Invoke("[DesktopHost] OfficeCore unified server is healthy and responding on http://localhost:5026");
                        SetStatus(ServerStatus.Running);
                        return;
                    }
                }
                catch
                {
                    // Still warming up
                }

                if (_serverProcess != null && _serverProcess.HasExited)
                {
                    SetStatus(ServerStatus.Stopped);
                    return;
                }
            }

            if (CurrentStatus == ServerStatus.Starting)
            {
                LogReceived?.Invoke("[DesktopHost] Server started, but health probe timed out. It may still be finishing EF migrations.");
                SetStatus(ServerStatus.Running);
            }
        }, token);
    }

    public void StopServer()
    {
        _healthCheckCts?.Cancel();

        try
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                LogReceived?.Invoke("[DesktopHost] Stopping server process...");
                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess.WaitForExit(3000);
            }
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"[DesktopHost] Note on stopping server: {ex.Message}");
        }
        finally
        {
            _serverProcess = null;
            SetStatus(ServerStatus.Stopped);
            LogReceived?.Invoke("[DesktopHost] Server stopped.");
        }
    }

    public void Dispose()
    {
        StopServer();
        _httpClient.Dispose();
        _healthCheckCts?.Dispose();
    }
}
