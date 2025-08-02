using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace MyPhotoHelper.Forms
{
    public partial class AboutForm : Form
    {
        private PictureBox _logoPictureBox = null!;
        private Label _versionLabel = null!;
        private Label _descriptionLabel = null!;
        private Label _copyrightLabel = null!;
        private Label _websiteLabel = null!;
        private Button _okButton = null!;

        public AboutForm()
        {
            InitializeComponent();
            LoadAboutInfo();
        }

        private void InitializeComponent()
        {
            this.Text = "About MyPhotoHelper";
            this.Size = new Size(450, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = true;
            this.ShowInTaskbar = false;

            // Set icon if available
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "tray_icon.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }

            // Main panel
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            // Logo
            _logoPictureBox = new PictureBox
            {
                Size = new Size(150, 150),
                Location = new Point((this.ClientSize.Width - 150) / 2, 20),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            // Try to load logo
            try
            {
                var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "MyPhotoHelper Logo.png");
                if (File.Exists(logoPath))
                {
                    _logoPictureBox.Image = Image.FromFile(logoPath);
                }
            }
            catch { }

            // Remove the title label since the logo already shows the name

            // Version
            _versionLabel = new Label
            {
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = false,
                Size = new Size(410, 30),
                Location = new Point(20, 185),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Description
            _descriptionLabel = new Label
            {
                Text = "AI-powered photo organization and management",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize = false,
                Size = new Size(410, 30),
                Location = new Point(20, 215),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Copyright
            _copyrightLabel = new Label
            {
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(127, 140, 141),
                AutoSize = false,
                Size = new Size(410, 25),
                Location = new Point(20, 260),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Website link
            _websiteLabel = new Label
            {
                Text = "github.com/thefrederiksen/MyPhotoHelper",
                Font = new Font("Segoe UI", 9, FontStyle.Underline),
                ForeColor = Color.FromArgb(41, 128, 185),
                AutoSize = false,
                Size = new Size(410, 25),
                Location = new Point(20, 285),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            _websiteLabel.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/thefrederiksen/MyPhotoHelper",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            // OK button
            _okButton = new Button
            {
                Text = "OK",
                Size = new Size(80, 30),
                Location = new Point((this.ClientSize.Width - 80) / 2, 320),
                UseVisualStyleBackColor = true
            };
            _okButton.Click += (s, e) => this.Close();

            // Add controls (removed _titleLabel)
            mainPanel.Controls.AddRange(new Control[]
            {
                _logoPictureBox,
                _versionLabel,
                _descriptionLabel,
                _copyrightLabel,
                _websiteLabel,
                _okButton
            });

            this.Controls.Add(mainPanel);

            // Set accept button
            this.AcceptButton = _okButton;
        }

        private void LoadAboutInfo()
        {
            try
            {
                // Get version from assembly - use ProductVersion which matches auto-updater
                var assembly = Assembly.GetExecutingAssembly();
                var fileVersionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                
                // Use ProductVersion and clean it up - remove any Git hash suffix
                var rawVersion = !string.IsNullOrEmpty(fileVersionInfo.ProductVersion) 
                    ? fileVersionInfo.ProductVersion 
                    : assembly.GetName().Version?.ToString() ?? "1.2.3";

                // Clean up version - remove Git hash if present (anything after +)
                var cleanVersion = rawVersion.Contains('+') ? rawVersion.Split('+')[0] : rawVersion;
                
                _versionLabel.Text = $"Version {cleanVersion}";

                // Get copyright from assembly attributes
                var copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
                var copyright = copyrightAttribute?.Copyright ?? "© 2025 MyPhotoHelper";
                _copyrightLabel.Text = copyright;
            }
            catch
            {
                _versionLabel.Text = "Version 1.2.3";
                _copyrightLabel.Text = "© 2025 MyPhotoHelper";
            }
        }
    }
}