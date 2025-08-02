using System;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using MyPhotoHelper.Services;
using Microsoft.Extensions.DependencyInjection;

namespace MyPhotoHelper.Forms
{
    public partial class DatabaseUpdateForm : Form
    {
        private TextBox logTextBox = null!;
        private ProgressBar progressBar = null!;
        private Button updateButton = null!;
        private Button closeButton = null!;
        private Button copyLogButton = null!;
        private Label statusLabel = null!;
        private readonly IServiceProvider _serviceProvider;
        private readonly StringBuilder _logBuilder = new StringBuilder();

        public DatabaseUpdateForm(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Database Update Manager";
            this.Size = new System.Drawing.Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Status Label
            statusLabel = new Label
            {
                Text = "Click 'Update Database' to check for and apply any pending database migrations.",
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(760, 30),
                Font = new System.Drawing.Font("Segoe UI", 10F)
            };

            // Progress Bar
            progressBar = new ProgressBar
            {
                Location = new System.Drawing.Point(12, 50),
                Size = new System.Drawing.Size(760, 23),
                Style = ProgressBarStyle.Continuous
            };

            // Log TextBox
            logTextBox = new TextBox
            {
                Location = new System.Drawing.Point(12, 85),
                Size = new System.Drawing.Size(760, 420),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new System.Drawing.Font("Consolas", 9F),
                BackColor = System.Drawing.Color.Black,
                ForeColor = System.Drawing.Color.LightGreen
            };

            // Update Button
            updateButton = new Button
            {
                Text = "Update Database",
                Location = new System.Drawing.Point(12, 515),
                Size = new System.Drawing.Size(150, 35),
                Font = new System.Drawing.Font("Segoe UI", 10F)
            };
            updateButton.Click += async (s, e) => await UpdateDatabase();

            // Copy Log Button
            copyLogButton = new Button
            {
                Text = "Copy Log",
                Location = new System.Drawing.Point(172, 515),
                Size = new System.Drawing.Size(100, 35),
                Font = new System.Drawing.Font("Segoe UI", 10F),
                Enabled = false
            };
            copyLogButton.Click += (s, e) => CopyLog();

            // Close Button
            closeButton = new Button
            {
                Text = "Close",
                Location = new System.Drawing.Point(672, 515),
                Size = new System.Drawing.Size(100, 35),
                Font = new System.Drawing.Font("Segoe UI", 10F)
            };
            closeButton.Click += (s, e) => this.Close();

            // Add controls
            this.Controls.Add(statusLabel);
            this.Controls.Add(progressBar);
            this.Controls.Add(logTextBox);
            this.Controls.Add(updateButton);
            this.Controls.Add(copyLogButton);
            this.Controls.Add(closeButton);
        }

        private async Task UpdateDatabase()
        {
            updateButton.Enabled = false;
            copyLogButton.Enabled = false;
            progressBar.Value = 0;
            logTextBox.Clear();
            _logBuilder.Clear();

            try
            {
                await Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var pathService = scope.ServiceProvider.GetRequiredService<IPathService>();
                    var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();

                    // Get database path
                    var dbPath = pathService.GetDatabasePath();
                    var connectionString = $"Data Source={dbPath};Cache=Shared;";

                    LogMessage($"Database Update Process Started");
                    LogMessage($"=====================================");
                    LogMessage($"Database Path: {dbPath}");
                    LogMessage($"");

                    UpdateProgress(10, "Checking current database version...");

                    // Get current version
                    var currentVersion = await dbInitService.GetCurrentVersionAsync(connectionString);
                    LogMessage($"Current Database Version: {currentVersion}");

                    UpdateProgress(20, "Scanning for migration scripts...");

                    // Check for available scripts
                    var scriptsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database");
                    LogMessage($"Scripts Directory: {scriptsPath}");

                    if (!System.IO.Directory.Exists(scriptsPath))
                    {
                        LogMessage($"ERROR: Scripts directory not found!");
                        UpdateProgress(100, "Failed - Scripts directory not found");
                        return;
                    }

                    var scriptFiles = System.IO.Directory.GetFiles(scriptsPath, "DatabaseVersion_*.sql")
                        .Select(f => new
                        {
                            Path = f,
                            FileName = System.IO.Path.GetFileName(f),
                            Version = int.Parse(System.IO.Path.GetFileNameWithoutExtension(f).Split('_')[1])
                        })
                        .OrderBy(s => s.Version)
                        .ToList();

                    LogMessage($"Found {scriptFiles.Count} total migration scripts:");
                    foreach (var script in scriptFiles)
                    {
                        LogMessage($"  - {script.FileName} (Version {script.Version})");
                    }
                    LogMessage("");

                    var pendingMigrations = scriptFiles.Where(s => s.Version > currentVersion).ToList();
                    LogMessage($"Pending Migrations: {pendingMigrations.Count}");

                    if (pendingMigrations.Count == 0)
                    {
                        LogMessage($"Database is already up to date!");
                        UpdateProgress(100, "Database is up to date");
                        return;
                    }

                    // Apply migrations
                    var progressPerMigration = 60 / pendingMigrations.Count;
                    var currentProgress = 30;

                    foreach (var migration in pendingMigrations)
                    {
                        LogMessage($"");
                        LogMessage($"Applying Migration: {migration.FileName}");
                        UpdateProgress(currentProgress, $"Applying version {migration.Version}...");

                        var success = await dbInitService.ApplyMigrationAsync(connectionString, migration.Path);
                        
                        if (success)
                        {
                            LogMessage($"  ✓ Successfully applied version {migration.Version}");
                        }
                        else
                        {
                            LogMessage($"  ✗ FAILED to apply version {migration.Version}");
                            LogMessage($"  Check application logs for details");
                            UpdateProgress(100, "Failed - Migration error");
                            return;
                        }

                        currentProgress += progressPerMigration;
                    }

                    // Verify final version
                    UpdateProgress(90, "Verifying database version...");
                    var finalVersion = await dbInitService.GetCurrentVersionAsync(connectionString);
                    LogMessage($"");
                    LogMessage($"Final Database Version: {finalVersion}");
                    LogMessage($"");
                    LogMessage($"Database update completed successfully!");
                    UpdateProgress(100, "Update completed successfully!");
                });
            }
            catch (Exception ex)
            {
                LogMessage($"");
                LogMessage($"ERROR: {ex.Message}");
                LogMessage($"Stack Trace:");
                LogMessage(ex.StackTrace ?? "No stack trace available");
                UpdateProgress(100, "Failed with error");
            }
            finally
            {
                updateButton.Enabled = true;
                copyLogButton.Enabled = true;
            }
        }

        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logLine = $"[{timestamp}] {message}";
            _logBuilder.AppendLine(logLine);

            if (logTextBox.InvokeRequired)
            {
                logTextBox.Invoke(new Action(() =>
                {
                    logTextBox.AppendText(logLine + Environment.NewLine);
                    logTextBox.ScrollToCaret();
                }));
            }
            else
            {
                logTextBox.AppendText(logLine + Environment.NewLine);
                logTextBox.ScrollToCaret();
            }
        }

        private void UpdateProgress(int value, string status)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() =>
                {
                    progressBar.Value = Math.Min(value, 100);
                    statusLabel.Text = status;
                }));
            }
            else
            {
                progressBar.Value = Math.Min(value, 100);
                statusLabel.Text = status;
            }
        }

        private void CopyLog()
        {
            try
            {
                Clipboard.SetText(_logBuilder.ToString());
                MessageBox.Show("Log copied to clipboard!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy log: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}