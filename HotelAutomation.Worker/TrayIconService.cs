#if WINDOWS
using System.Diagnostics;
using System.Windows.Forms;

namespace HotelAutomation.Worker;

/// <summary>
/// Hosted service that shows a Windows system tray icon for the hotel automation app.
/// Runs on a dedicated STA thread (required by Windows Forms).
/// Double-click or right-click → Open Dashboard to launch the browser.
/// Right-click → Stop Application to gracefully shut down the server.
/// </summary>
public sealed class TrayIconService : IHostedService, IDisposable
{
    private static TrayIconService? _instance;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TrayIconService> _logger;
    private Thread? _uiThread;
    private NotifyIcon? _trayIcon;

    public TrayIconService(IHostApplicationLifetime lifetime, ILogger<TrayIconService> logger)
    {
        _lifetime = lifetime;
        _logger = logger;
        _instance = this;
    }

    public static void ShowNotification(string title, string text)
    {
        _instance?.ShowNotificationInternal(title, text);
    }

    private void ShowNotificationInternal(string title, string text)
    {
        try
        {
            if (_trayIcon != null)
            {
                _trayIcon.BalloonTipTitle = title;
                _trayIcon.BalloonTipText = text;
                _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(5000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show balloon tip notification.");
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _uiThread = new Thread(RunMessageLoop)
        {
            Name = "TrayIconThread",
            IsBackground = true
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        return Task.CompletedTask;
    }

    private void RunMessageLoop()
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Load icon: prefer hotel.ico in exe directory, fall back to system icon
            Icon appIcon = LoadAppIcon();

            _trayIcon = new NotifyIcon
            {
                Icon = appIcon,
                Text = "TheSankalpRetreat",
                Visible = true,
                BalloonTipTitle = "TheSankalpRetreat",
                BalloonTipText = "Running on http://localhost:8000\nDouble-click to open Dashboard.",
                BalloonTipIcon = ToolTipIcon.Info
            };

            // Show startup notification
            _trayIcon.ShowBalloonTip(4000);

            // Build context menu
            var menu = new ContextMenuStrip();
            menu.Font = new System.Drawing.Font("Segoe UI", 9f);

            var headerItem = new ToolStripMenuItem("TheSankalpRetreat")
            {
                Enabled = false,
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold)
            };
            menu.Items.Add(headerItem);
            menu.Items.Add(new ToolStripSeparator());

            var openItem = new ToolStripMenuItem("Open Dashboard");
            openItem.Click += (_, _) => OpenDashboard();
            menu.Items.Add(openItem);

            menu.Items.Add(new ToolStripSeparator());

            var stopItem = new ToolStripMenuItem("Stop Application");
            stopItem.Click += (_, _) =>
            {
                if (_trayIcon != null) _trayIcon.Visible = false;
                _lifetime.StopApplication();
            };
            menu.Items.Add(stopItem);

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (_, _) => OpenDashboard();

            // Gracefully hide tray icon when app stops
            _lifetime.ApplicationStopping.Register(() =>
            {
                if (_trayIcon != null) _trayIcon.Visible = false;
                Application.ExitThread();
            });

            Application.Run();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TrayIconService message loop failed.");
        }
    }

    private static Icon LoadAppIcon()
    {
        // Try to load hotel.ico bundled alongside the exe
        var iconPath = Path.Combine(AppContext.BaseDirectory, "hotel.ico");
        if (File.Exists(iconPath))
        {
            try { return new Icon(iconPath); }
            catch { /* fall through to system icon */ }
        }
        return SystemIcons.Application;
    }

    private static void OpenDashboard()
    {
        try
        {
            Process.Start(new ProcessStartInfo("http://localhost:8000")
            {
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Ignore — browser might not be installed or URL failed
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
    }
}
#else
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace HotelAutomation.Worker;

public sealed class TrayIconService : IHostedService, IDisposable
{
    public TrayIconService(IHostApplicationLifetime lifetime, ILogger<TrayIconService> logger)
    {
    }

    public static void ShowNotification(string title, string text)
    {
        // On macOS: fire a native Notification Center alert via osascript (stdin avoids quoting issues)
        try
        {
            var safeTitle = title.Replace("\"", "'");
            var safeText  = text .Replace("\"", "'");
            var psi = new ProcessStartInfo("osascript")
            {
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute = false,
                CreateNoWindow  = true
            };
            var proc = Process.Start(psi);
            if (proc != null)
            {
                proc.StandardInput.WriteLine($"display notification \"{safeText}\" with title \"{safeTitle}\"");
                proc.StandardInput.Close();
            }
        }
        catch { /* notifications are non-critical */ }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
#endif
