using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;

namespace MyPhotoHelper.Forms
{
    public partial class StartupForm : Form
    {
        private ProgressBar progressBar = null!;
        private Label statusLabel = null!;
        private PictureBox? logoPictureBox;
        
        public StartupForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "MyPhotoHelper - Starting";
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            
            // Logo picture box
            logoPictureBox = new PictureBox
            {
                Location = new Point(150, 10),
                Size = new Size(100, 60),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            
            // Try to load the logo image
            try
            {
                string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "MyPhotoHelper Logo Transparent.png");
                if (File.Exists(logoPath))
                {
                    logoPictureBox.Image = Image.FromFile(logoPath);
                }
                else
                {
                    // Fallback to text if logo not found
                    var fallbackLabel = new Label
                    {
                        Text = "MyPhotoHelper",
                        Font = new Font("Segoe UI", 18, FontStyle.Bold),
                        ForeColor = Color.FromArgb(0, 122, 204),
                        Location = logoPictureBox.Location,
                        Size = logoPictureBox.Size,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    this.Controls.Add(fallbackLabel);
                    logoPictureBox = null;
                }
            }
            catch
            {
                // If loading fails, just continue without logo
            }
            
            // Status label
            statusLabel = new Label
            {
                Text = "Initializing...",
                Font = new Font("Segoe UI", 10),
                Location = new Point(12, 70),
                Size = new Size(360, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new Point(40, 110),
                Size = new Size(320, 23),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };
            
            // Add controls
            if (logoPictureBox != null)
            {
                this.Controls.Add(logoPictureBox);
            }
            this.Controls.Add(statusLabel);
            this.Controls.Add(progressBar);
        }
        
        public void UpdateStatus(string message, int? progress = null)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateStatus(message, progress)));
                return;
            }
            
            statusLabel.Text = message;
            
            if (progress.HasValue)
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = Math.Min(100, Math.Max(0, progress.Value));
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Marquee;
            }
        }
        
        public void SetProgress(int value)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetProgress(value)));
                return;
            }
            
            progressBar.Style = ProgressBarStyle.Blocks;
            progressBar.Value = Math.Min(100, Math.Max(0, value));
        }
    }
}