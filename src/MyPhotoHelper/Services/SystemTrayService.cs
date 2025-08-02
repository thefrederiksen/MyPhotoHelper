using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;
using MyPhotoHelper.Models;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace MyPhotoHelper.Services
{
    public class SystemTrayService : IDisposable
    {
        // No console management needed with OutputType=WinExe

        private NotifyIcon? _trayIcon;
        private readonly ILogger<SystemTrayService> _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IScanStatusService _scanStatusService;
        private readonly IServiceProvider _serviceProvider;
        private volatile bool _disposed = false;
        private ToolStripMenuItem? _scanStatusMenuItem;
        private ToolStripMenuItem? _scanProgressMenuItem;
        private System.Windows.Forms.Timer? _statusUpdateTimer;
        private Icon? _defaultIcon;
        private Icon? _scanningIcon;
        private bool _lastScanningState = false;
        private string _lastTooltipText = "MyPhotoHelper - Ready";
        private ScanPhase _lastPhase = ScanPhase.None;
        private readonly object _updateLock = new object();

        public SystemTrayService(
            ILogger<SystemTrayService> logger,
            IHostApplicationLifetime applicationLifetime,
            IScanStatusService scanStatusService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _applicationLifetime = applicationLifetime;
            _scanStatusService = scanStatusService;
            _serviceProvider = serviceProvider;
        }

        public void Initialize()
        {
            try
            {
                _logger.LogInformation("System tray service initializing");

                // Don't add to startup automatically anymore - let user control via settings
                // AddToStartup();

                // Create system tray icon on the main UI thread
                // Since we're called from the main WinForms app, we're already on the UI thread
                CreateSystemTrayIcon();
                SetupScanStatusMonitoring();

                _logger.LogInformation("System tray service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize system tray service");
            }
        }

        public void EnableWindowsStartup()
        {
            try
            {
                _logger.LogInformation("Enabling Windows startup");
                AddToStartup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enable Windows startup");
                throw;
            }
        }

        public void DisableWindowsStartup()
        {
            try
            {
                _logger.LogInformation("Disabling Windows startup");
                
                // Remove from Startup folder
                var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcutPath = Path.Combine(startupPath, "MyPhotoHelper.lnk");
                
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                    _logger.LogInformation("Removed shortcut from startup folder");
                }

                // Remove from Registry
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null && key.GetValue("MyPhotoHelper") != null)
                {
                    key.DeleteValue("MyPhotoHelper", false);
                    _logger.LogInformation("Removed MyPhotoHelper from registry startup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable Windows startup");
                throw;
            }
        }

        public bool IsWindowsStartupEnabled()
        {
            try
            {
                // Check Startup folder
                var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcutPath = Path.Combine(startupPath, "MyPhotoHelper.lnk");
                
                if (File.Exists(shortcutPath))
                {
                    return true;
                }

                // Check Registry
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                if (key != null && key.GetValue("MyPhotoHelper") != null)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check Windows startup status");
                return false;
            }
        }

        private void AddToStartup()
        {
            try
            {
                // Method 1: Add to user's Startup folder (most reliable)
                var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                
                if (!string.IsNullOrEmpty(exePath))
                {
                    var shortcutPath = Path.Combine(startupPath, "MyPhotoHelper.lnk");
                    
                    // Create shortcut using Windows Script Host
                    CreateShortcut(shortcutPath, exePath);
                    
                    _logger.LogInformation("Added MyPhotoHelper to startup folder: {Path}", shortcutPath);
                }

                // Method 2: Also add to registry as backup
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key != null && !string.IsNullOrEmpty(exePath))
                {
                    // Add with --minimized flag
                    key.SetValue("MyPhotoHelper", $"\"{exePath}\" --minimized");
                    _logger.LogInformation("Added MyPhotoHelper to registry startup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add application to startup");
            }
        }

        private void CreateShortcut(string shortcutPath, string targetPath)
        {
            try
            {
                // Use Windows Script Host to create shortcut
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    var shell = Activator.CreateInstance(shellType);
                    if (shell != null)
                    {
                        dynamic? shortcut = shell.GetType().InvokeMember("CreateShortcut", 
                            System.Reflection.BindingFlags.InvokeMethod, 
                            null, shell, new object[] { shortcutPath });
                        
                        if (shortcut != null)
                        {
                            shortcut.TargetPath = targetPath;
                            shortcut.Arguments = "--minimized";
                            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                            shortcut.IconLocation = targetPath;
                            shortcut.Description = "MyPhotoHelper - AI-powered photo organization";
                            shortcut.Save();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create shortcut");
            }
        }

        private void CreateSystemTrayIcon()
        {
            try
            {
                Icon? icon = null;
                
                // Try to load icon from resources first
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "tray_icon.ico");
                if (File.Exists(iconPath))
                {
                    try
                    {
                        icon = new Icon(iconPath);
                        _logger.LogInformation($"Loaded icon from file: {iconPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to load icon from file: {ex.Message}");
                    }
                }
                
                // If no icon file or loading failed, create programmatically
                if (icon == null)
                {
                    icon = CreateDefaultIcon();
                    _logger.LogInformation("Using programmatically created icon");
                }
                
                _defaultIcon = icon;
                _scanningIcon = CreateScanningIcon();
                
                _trayIcon = new NotifyIcon
                {
                    Icon = icon,
                    Visible = true,
                    Text = "MyPhotoHelper - AI-powered photo organization",
                    BalloonTipTitle = "MyPhotoHelper",
                    BalloonTipText = "MyPhotoHelper is running in the background",
                    BalloonTipIcon = ToolTipIcon.Info
                };
                
                _logger.LogInformation("Creating system tray icon with context menu");

                // Create context menu with simplified options
                var contextMenu = new ContextMenuStrip();
                
                // Open FaceVault - Opens browser
                var openMenuItem = new ToolStripMenuItem("Open MyPhotoHelper");
                openMenuItem.Click += (s, e) => OpenApplication();
                openMenuItem.Font = new Font(openMenuItem.Font, FontStyle.Bold); // Make default action bold
                contextMenu.Items.Add(openMenuItem);
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                // Scan Status
                _scanStatusMenuItem = new ToolStripMenuItem("Scan Status: Idle");
                _scanStatusMenuItem.Enabled = false;
                contextMenu.Items.Add(_scanStatusMenuItem);
                
                // Scan Progress (submenu)
                _scanProgressMenuItem = new ToolStripMenuItem("Scan Progress");
                UpdateScanProgressMenu();
                contextMenu.Items.Add(_scanProgressMenuItem);
                
                // Manual Scan Trigger
                var scanNowMenuItem = new ToolStripMenuItem("Start Scan Now");
                scanNowMenuItem.Click += async (s, e) => await TriggerManualScan();
                contextMenu.Items.Add(scanNowMenuItem);
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                // View Logs - Opens log file
                var logsMenuItem = new ToolStripMenuItem("View Logs");
                logsMenuItem.Click += (s, e) => OpenLogFile();
                contextMenu.Items.Add(logsMenuItem);
                
                // Open Data Directory - Opens file explorer in app data directory
                var dataMenuItem = new ToolStripMenuItem("Open Data Directory");
                dataMenuItem.Click += (s, e) => OpenDataDirectory();
                contextMenu.Items.Add(dataMenuItem);
                
                contextMenu.Items.Add(new ToolStripSeparator());
                
                // Exit application
                var exitMenuItem = new ToolStripMenuItem("Exit");
                exitMenuItem.Click += (s, e) => ExitApplication();
                contextMenu.Items.Add(exitMenuItem);

                _trayIcon.ContextMenuStrip = contextMenu;
                
                // Handle left-click to show menu (Windows 11 style)
                _trayIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        // Show context menu on left click
                        var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        mi?.Invoke(_trayIcon, null);
                    }
                };
                
                // Double-click to open application
                _trayIcon.DoubleClick += (s, e) => OpenApplication();

                // Show balloon tip on first run
                _trayIcon.ShowBalloonTip(3000);
                
                _logger.LogInformation($"System tray icon created successfully. Icon visible: {_trayIcon.Visible}, Has context menu: {_trayIcon.ContextMenuStrip != null}, Menu items: {_trayIcon.ContextMenuStrip?.Items.Count ?? 0}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create system tray icon");
            }
        }

        private Icon CreateDefaultIcon()
        {
            // Create a clean, modern photo icon for system tray
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                // Enable antialiasing for smoother edges
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Main photo frame - slightly rounded rectangle
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddRectangle(new Rectangle(2, 3, 12, 10));
                    
                    // Fill with gradient for depth
                    using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Point(2, 3), new Point(14, 13),
                        Color.FromArgb(41, 128, 185),  // Nice blue
                        Color.FromArgb(52, 152, 219))) // Lighter blue
                    {
                        g.FillPath(brush, path);
                    }
                }
                
                // Inner photo area (white)
                g.FillRectangle(Brushes.White, 3, 4, 10, 8);
                
                // Mountain/landscape icon inside photo
                using (var mountainPath = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    mountainPath.AddPolygon(new Point[] {
                        new Point(3, 12),
                        new Point(7, 7),
                        new Point(9, 9),
                        new Point(11, 6),
                        new Point(13, 12)
                    });
                    g.FillPath(new SolidBrush(Color.FromArgb(46, 204, 113)), mountainPath); // Green
                }
                
                // Sun/circle in corner
                g.FillEllipse(new SolidBrush(Color.FromArgb(241, 196, 15)), 9, 5, 3, 3); // Yellow
            }
            
            var hIcon = bitmap.GetHicon();
            var icon = Icon.FromHandle(hIcon);
            
            // Clone the icon to avoid GDI+ errors
            var clonedIcon = (Icon)icon.Clone();
            
            // Clean up
            DestroyIcon(hIcon);
            bitmap.Dispose();
            
            return clonedIcon;
        }

        private void OpenLogFile()
        {
            try
            {
                // Open the log file in default text editor (logs are in AppData, not LocalAppData)
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPhotoHelper", "Logs");
                
                if (Directory.Exists(logDir))
                {
                    var latestLog = Directory.GetFiles(logDir, "*.log")
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .FirstOrDefault();
                        
                    if (!string.IsNullOrEmpty(latestLog))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = latestLog,
                            UseShellExecute = true
                        });
                    }
                    else
                    {
                        _logger.LogWarning("No log files found");
                    }
                }
                else
                {
                    _logger.LogWarning($"Log directory not found: {logDir}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening log file");
            }
        }

        private void OpenDataDirectory()
        {
            try
            {
                // Open the application data directory in File Explorer (where database is stored)
                var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MyPhotoHelper");
                
                // Ensure directory exists
                Directory.CreateDirectory(dataDir);
                
                // Open in File Explorer
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = dataDir,
                    UseShellExecute = true
                });
                
                _logger.LogInformation($"Opened data directory: {dataDir}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening data directory");
            }
        }

        private void OpenApplication()
        {
            try
            {
                // Open the default browser with our URL (use correct port from launchSettings)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:5113",
                    UseShellExecute = true
                });
                _logger.LogInformation("Opening MyPhotoHelper in browser");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open browser");
            }
        }


        private void ExitApplication()
        {
            try
            {
                _logger.LogInformation("Exiting MyPhotoHelper application...");
                
                // Mark as disposed to prevent further updates
                _disposed = true;
                
                // Hide and dispose tray icon first
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
                
                // Stop the ASP.NET Core application
                _applicationLifetime.StopApplication();
                
                // Exit the Windows Forms application loop (main message pump)
                Application.Exit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during application exit");
                // Force exit if graceful shutdown fails
                Environment.Exit(0);
            }
        }

        public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            _trayIcon?.ShowBalloonTip(3000, title, text, icon);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                // Unsubscribe event handlers
                _scanStatusService.StatusChanged -= OnScanStatusChanged;
                _scanStatusService.PhasedStatusChanged -= OnPhasedStatusChanged;
                
                // Stop and dispose timer
                _statusUpdateTimer?.Stop();
                _statusUpdateTimer?.Dispose();
                _statusUpdateTimer = null;
                
                // Dispose icons
                _defaultIcon?.Dispose();
                _defaultIcon = null;
                _scanningIcon?.Dispose();
                _scanningIcon = null;
                
                // Dispose tray icon
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
            }
        }
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
        
        private Icon CreateScanningIcon()
        {
            // Create animated scanning icon (with progress indicator)
            var bitmap = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Photo frame
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddRectangle(new Rectangle(2, 3, 12, 10));
                    
                    // Orange gradient for scanning
                    using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                        new Point(2, 3), new Point(14, 13),
                        Color.FromArgb(230, 126, 34),  // Orange
                        Color.FromArgb(241, 196, 15))) // Yellow
                    {
                        g.FillPath(brush, path);
                    }
                }
                
                // Inner photo area (white)
                g.FillRectangle(Brushes.White, 3, 4, 10, 8);
                
                // Scanning line animation (we'll update position in timer)
                using (var pen = new Pen(Color.FromArgb(200, 52, 152, 219), 2))
                {
                    g.DrawLine(pen, 4, 8, 12, 8);
                }
            }
            
            var hIcon = bitmap.GetHicon();
            var icon = Icon.FromHandle(hIcon);
            var clonedIcon = (Icon)icon.Clone();
            DestroyIcon(hIcon);
            bitmap.Dispose();
            
            return clonedIcon;
        }
        
        private void SetupScanStatusMonitoring()
        {
            // Subscribe to scan status changes
            _scanStatusService.StatusChanged += OnScanStatusChanged;
            _scanStatusService.PhasedStatusChanged += OnPhasedStatusChanged;
            
            // Setup timer for UI updates - ONLY for progress menu, not the icon
            _statusUpdateTimer = new System.Windows.Forms.Timer();
            _statusUpdateTimer.Interval = 5000; // Update every 5 seconds instead of 1
            _statusUpdateTimer.Tick += (s, e) => 
            {
                lock (_updateLock)
                {
                    if (!_disposed && _trayIcon != null)
                    {
                        UpdateScanProgressMenu();
                    }
                }
            };
            _statusUpdateTimer.Start();
        }
        
        private void OnScanStatusChanged(object? sender, EventArgs e)
        {
            if (_disposed || _trayIcon == null) return;
            
            try
            {
                // Always use BeginInvoke to avoid deadlocks
                if (_trayIcon.ContextMenuStrip?.IsHandleCreated == true)
                {
                    _trayIcon.ContextMenuStrip.BeginInvoke(new Action(() =>
                    {
                        lock (_updateLock)
                        {
                            if (!_disposed && _trayIcon != null)
                            {
                                UpdateScanDisplay();
                                
                                // Only show balloon tip when scan starts (not on every status update)
                                if (_scanStatusService.IsScanning && !_lastScanningState)
                                {
                                    ShowBalloonTip("Photo Scan Started", "MyPhotoHelper is scanning for new photos...", ToolTipIcon.Info);
                                }
                            }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnScanStatusChanged");
            }
        }
        
        private void OnPhasedStatusChanged(object? sender, PhasedScanProgress progress)
        {
            if (_disposed || _trayIcon == null) return;
            
            try
            {
                if (_trayIcon.ContextMenuStrip?.IsHandleCreated == true)
                {
                    _trayIcon.ContextMenuStrip.BeginInvoke(new Action(() =>
                    {
                        lock (_updateLock)
                        {
                            if (!_disposed && _trayIcon != null)
                            {
                                UpdateScanDisplay();
                            }
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnPhasedStatusChanged");
            }
        }
        
        private void UpdateScanDisplay()
        {
            if (_trayIcon == null) return;
            
            var isScanning = _scanStatusService.IsScanning;
            var phasedProgress = _scanStatusService.CurrentPhasedProgress;
            var currentPhase = phasedProgress?.CurrentPhase ?? ScanPhase.None;
            
            // Only update icon if state changed
            if (isScanning != _lastScanningState)
            {
                _trayIcon.Icon = isScanning ? _scanningIcon : _defaultIcon;
                _lastScanningState = isScanning;
            }
            
            // Generate tooltip text
            string newTooltipText;
            if (isScanning && phasedProgress != null)
            {
                var phaseText = phasedProgress.CurrentPhase switch
                {
                    ScanPhase.Phase1_Discovery => "Finding new photos",
                    ScanPhase.Phase2_Metadata => "Reading photo details",
                    ScanPhase.Phase3_ScreenshotDetection => "Filtering screenshots",
                    ScanPhase.Phase4_Hashing => "Checking for duplicates",
                    _ => "Scanning"
                };
                newTooltipText = $"MyPhotoHelper - {phaseText}...";
            }
            else
            {
                newTooltipText = "MyPhotoHelper - Ready";
            }
            
            // Only update tooltip if text changed
            if (newTooltipText != _lastTooltipText)
            {
                _trayIcon.Text = newTooltipText;
                _lastTooltipText = newTooltipText;
            }
            
            // Only update menu items if phase changed
            if (currentPhase != _lastPhase || isScanning != _lastScanningState)
            {
                _lastPhase = currentPhase;
                
                // Update menu items
                if (_scanStatusMenuItem != null)
                {
                    _scanStatusMenuItem.Text = isScanning ? "Scan Status: Running" : "Scan Status: Idle";
                    _scanStatusMenuItem.ForeColor = isScanning ? Color.Green : Color.Gray;
                }
                
                UpdateScanProgressMenu();
            }
        }
        
        private void UpdateScanProgressMenu()
        {
            if (_scanProgressMenuItem == null) return;
            
            _scanProgressMenuItem.DropDownItems.Clear();
            
            var isScanning = _scanStatusService.IsScanning;
            var phasedProgress = _scanStatusService.CurrentPhasedProgress;
            
            if (!isScanning || phasedProgress == null)
            {
                _scanProgressMenuItem.DropDownItems.Add(new ToolStripMenuItem("No scan in progress") { Enabled = false });
                return;
            }
            
            // Current phase
            var phaseItem = new ToolStripMenuItem($"Current Phase: {phasedProgress.CurrentPhase}") { Enabled = false };
            phaseItem.Font = new Font(phaseItem.Font, FontStyle.Bold);
            _scanProgressMenuItem.DropDownItems.Add(phaseItem);
            
            _scanProgressMenuItem.DropDownItems.Add(new ToolStripSeparator());
            
            // Phase details
            foreach (var phase in phasedProgress.PhaseProgress)
            {
                var progress = phase.Value;
                var statusText = progress.IsComplete ? "✓" : (progress.ProcessedItems > 0 ? "→" : "○");
                var phaseText = $"{statusText} {phase.Key}: {progress.ProcessedItems}/{progress.TotalItems}";
                
                var item = new ToolStripMenuItem(phaseText) { Enabled = false };
                if (phase.Key == phasedProgress.CurrentPhase)
                {
                    item.ForeColor = Color.Blue;
                }
                else if (progress.IsComplete)
                {
                    item.ForeColor = Color.Green;
                }
                
                _scanProgressMenuItem.DropDownItems.Add(item);
            }
            
            // Scan duration
            if (phasedProgress.StartTime != default)
            {
                var duration = (phasedProgress.EndTime ?? DateTime.UtcNow) - phasedProgress.StartTime;
                _scanProgressMenuItem.DropDownItems.Add(new ToolStripSeparator());
                _scanProgressMenuItem.DropDownItems.Add(new ToolStripMenuItem($"Duration: {duration:mm\\:ss}") { Enabled = false });
            }
        }
        
        private async Task TriggerManualScan()
        {
            try
            {
                _logger.LogInformation("Manual scan triggered from system tray");
                
                // Get the background task service and trigger scan
                var hostedServices = _serviceProvider.GetServices<IHostedService>();
                var backgroundService = hostedServices.OfType<BackgroundTaskService>().FirstOrDefault();
                    
                if (backgroundService != null)
                {
                    await backgroundService.TriggerScanAsync();
                    ShowBalloonTip("Scan Started", "Manual photo scan has been triggered.", ToolTipIcon.Info);
                }
                else
                {
                    _logger.LogError("Could not find BackgroundTaskService");
                    ShowBalloonTip("Scan Failed", "Could not start scan. Check logs for details.", ToolTipIcon.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering manual scan");
                ShowBalloonTip("Scan Error", ex.Message, ToolTipIcon.Error);
            }
        }
    }
}