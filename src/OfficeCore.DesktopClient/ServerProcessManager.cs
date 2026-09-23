using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OfficeCore.DesktopClient;

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
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

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

    public async Task StartServerAsync(DesktopConfig config)
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
                // Port occupied but not responding to HTTP
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
                // Locate OfficeCore.Api project root or binary
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var projectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "OfficeCore.Api"));

                ProcessStartInfo startInfo;

                if (Directory.Exists(projectDir) && File.Exists(Path.Combine(projectDir, "OfficeCore.Api.csproj")))
                {
                    // Running from source repository in development
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
                }
                else
                {
                    // Running from published output
                    var apiDll = Path.Combine(baseDir, "OfficeCore.Api.dll");
                    var apiExe = Path.Combine(baseDir, "OfficeCore.Api.exe");

                    if (File.Exists(apiExe))
                    {
                        startInfo = new ProcessStartInfo
                        {
                            FileName = apiExe,
                            WorkingDirectory = baseDir,
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
                            Arguments = $"\"{apiDll}\"",
                            WorkingDirectory = baseDir,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                    }
                }

                // Inject the configured PostgreSQL connection string and bind to 0.0.0.0:5026
                startInfo.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = config.BuildConnectionString();
                startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = "http://0.0.0.0:5026";
                startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";

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

            LogReceived?.Invoke($"[DesktopHost] Process started (PID: {_serverProcess?.Id}). Monitoring endpoint http://localhost:5026...");

            // Start health check polling to detect when the API is ready
            _healthCheckCts?.Cancel();
            _healthCheckCts = new CancellationTokenSource();
            _ = PollHealthAsync(_healthCheckCts.Token);
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"[DesktopHost] Failed to launch server process: {ex.Message}");
            SetStatus(ServerStatus.Error);
        }
    }

    private async Task PollHealthAsync(CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var maxWait = TimeSpan.FromSeconds(45);

        while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow - startTime < maxWait)
        {
            if (_serverProcess != null && _serverProcess.HasExited)
            {
                LogReceived?.Invoke($"[DesktopHost] API process exited unexpectedly (Exit code: {_serverProcess.ExitCode}).");
                SetStatus(ServerStatus.Error);
                return;
            }

            try
            {
                var response = await _httpClient.GetAsync("http://localhost:5026/openapi/v1.json", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    SetStatus(ServerStatus.Running);
                    LogReceived?.Invoke("[DesktopHost] OfficeCore API is LIVE and healthy on http://0.0.0.0:5026");
                    return;
                }
            }
            catch
            {
                // Still spinning up
            }

            await Task.Delay(1000, cancellationToken);
        }

        if (CurrentStatus == ServerStatus.Starting)
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                LogReceived?.Invoke("[DesktopHost] Warning: Health check timed out, but server process is still running.");
                SetStatus(ServerStatus.Running);
            }
            else
            {
                SetStatus(ServerStatus.Error);
            }
        }
    }

    public void StopServer()
    {
        _healthCheckCts?.Cancel();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                LogReceived?.Invoke("[DesktopHost] Stopping server process...");
                _serverProcess.Kill(entireProcessTree: true);
                _serverProcess.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[DesktopHost] Error stopping process: {ex.Message}");
            }
            finally
            {
                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }
        else
        {
            // Terminate any external running OfficeCore.Api processes
            try
            {
                var processes = Process.GetProcessesByName("OfficeCore.Api");
                foreach (var p in processes)
                {
                    LogReceived?.Invoke($"[DesktopHost] Terminating external OfficeCore.Api process (PID: {p.Id})...");
                    p.Kill(entireProcessTree: true);
                }
            }
            catch { }
        }

        SetStatus(ServerStatus.Stopped);
        LogReceived?.Invoke("[DesktopHost] Server is stopped.");
    }

    public void Dispose()
    {
        StopServer();
        _httpClient.Dispose();
    }
}
