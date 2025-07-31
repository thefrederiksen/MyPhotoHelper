using System;
using System.Drawing;
using System.Windows.Forms;
using MyPhotoHelper.Services;

namespace MyPhotoHelper.Forms
{
    public partial class StartupErrorForm : Form
    {
        private readonly string _errorMessage;
        private readonly Exception? _exception;
        private TextBox _errorTextBox = null!;
        private Button _copyButton = null!;
        private Button _openLogButton = null!;
        private Button _exitButton = null!;

        public StartupErrorForm(string errorMessage, Exception? exception = null)
        {
            _errorMessage = errorMessage;
            _exception = exception;
            InitializeComponent();
            LoadErrorDetails();
        }

        private void InitializeComponent()
        {
            this.Text = "MyPhotoHelper - Startup Error";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Icon = SystemIcons.Error;

            // Main panel
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20)
            };

            // Error icon and title
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80
            };

            var iconPictureBox = new PictureBox
            {
                Image = SystemIcons.Error.ToBitmap(),
                Size = new Size(48, 48),
                Location = new Point(0, 0),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            var titleLabel = new Label
            {
                Text = "MyPhotoHelper Failed to Start",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(60, 0),
                AutoSize = true
            };

            var subtitleLabel = new Label
            {
                Text = "An error occurred during application startup. Please review the details below:",
                Font = new Font("Segoe UI", 10),
                Location = new Point(60, 30),
                AutoSize = true,
                ForeColor = Color.FromArgb(64, 64, 64)
            };

            headerPanel.Controls.AddRange(new Control[] { iconPictureBox, titleLabel, subtitleLabel });

            // Error details text box
            _errorTextBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9),
                BackColor = Color.FromArgb(250, 250, 250),
                Dock = DockStyle.Fill,
                WordWrap = false
            };

            // Button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            _copyButton = new Button
            {
                Text = "📋 Copy Error Details",
                Size = new Size(150, 35),
                Location = new Point(0, 8),
                FlatStyle = FlatStyle.System
            };
            _copyButton.Click += CopyButton_Click;

            _openLogButton = new Button
            {
                Text = "📁 Open Log File",
                Size = new Size(150, 35),
                Location = new Point(160, 8),
                FlatStyle = FlatStyle.System
            };
            _openLogButton.Click += OpenLogButton_Click;

            _exitButton = new Button
            {
                Text = "Exit",
                Size = new Size(100, 35),
                Anchor = AnchorStyles.Right,
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.System
            };
            _exitButton.Location = new Point(buttonPanel.Width - _exitButton.Width - 20, 8);

            buttonPanel.Controls.AddRange(new Control[] { _copyButton, _openLogButton, _exitButton });

            // Layout
            mainPanel.Controls.Add(_errorTextBox);
            mainPanel.Controls.Add(buttonPanel);
            mainPanel.Controls.Add(headerPanel);

            this.Controls.Add(mainPanel);

            // Handle resize
            buttonPanel.Resize += (s, e) => 
            {
                _exitButton.Location = new Point(buttonPanel.Width - _exitButton.Width - 20, 8);
            };
        }

        private void LoadErrorDetails()
        {
            var details = new System.Text.StringBuilder();
            
            details.AppendLine("=== STARTUP ERROR DETAILS ===");
            details.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            details.AppendLine($"Machine: {Environment.MachineName}");
            details.AppendLine($"OS: {Environment.OSVersion}");
            details.AppendLine($".NET Version: {Environment.Version}");
            details.AppendLine();
            
            details.AppendLine("=== ERROR MESSAGE ===");
            details.AppendLine(_errorMessage);
            details.AppendLine();

            if (_exception != null)
            {
                details.AppendLine("=== EXCEPTION DETAILS ===");
                details.AppendLine($"Type: {_exception.GetType().FullName}");
                details.AppendLine($"Message: {_exception.Message}");
                details.AppendLine($"Source: {_exception.Source}");
                details.AppendLine();
                details.AppendLine("Stack Trace:");
                details.AppendLine(_exception.StackTrace);

                var inner = _exception.InnerException;
                int level = 1;
                while (inner != null)
                {
                    details.AppendLine();
                    details.AppendLine($"=== INNER EXCEPTION {level} ===");
                    details.AppendLine($"Type: {inner.GetType().FullName}");
                    details.AppendLine($"Message: {inner.Message}");
                    details.AppendLine($"Source: {inner.Source}");
                    details.AppendLine();
                    details.AppendLine("Stack Trace:");
                    details.AppendLine(inner.StackTrace);
                    
                    inner = inner.InnerException;
                    level++;
                }
            }

            details.AppendLine();
            details.AppendLine("=== LOG FILE ===");
            details.AppendLine($"Location: {StartupErrorLogger.GetLogPath()}");
            
            _errorTextBox.Text = details.ToString();
        }

        private void CopyButton_Click(object? sender, EventArgs e)
        {
            try
            {
                Clipboard.SetText(_errorTextBox.Text);
                MessageBox.Show("Error details copied to clipboard!", "Copy Successful", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Copy Failed", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenLogButton_Click(object? sender, EventArgs e)
        {
            try
            {
                var logPath = StartupErrorLogger.GetLogPath();
                if (File.Exists(logPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{logPath}\"");
                }
                else
                {
                    var logDir = Path.GetDirectoryName(logPath);
                    if (Directory.Exists(logDir))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", logDir);
                    }
                    else
                    {
                        MessageBox.Show("Log file location not found.", "Log File", 
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open log location: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}