using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyPhotoHelper.Data;

namespace MyPhotoHelper.Forms
{
    public class BasicGalleryForm : Form
    {
        public BasicGalleryForm(IServiceProvider serviceProvider)
        {
            Text = "Photo Gallery - MyPhotoHelper";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(248, 249, 250); // Light gray like Bootstrap
            
            // Add a header panel
            var headerPanel = new Panel
            {
                Height = 80,
                Dock = DockStyle.Top,
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            
            var titleLabel = new Label
            {
                Text = "📸 Photo Gallery",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };
            
            headerPanel.Controls.Add(titleLabel);
            
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(20)
            };
            
            Controls.Add(panel);
            Controls.Add(headerPanel);
            
            // Just load some images
            try
            {
                using var scope = serviceProvider.CreateScope();
                using var db = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                
                var images = db.tbl_images
                    .Include(i => i.ScanDirectory)
                    .Where(i => i.FileExists == 1)
                    .OrderByDescending(i => i.DateModified)
                    .Take(100) // Load more images
                    .ToList();
                
                // Add a status label
                var statusLabel = new Label
                {
                    Text = $"Loading {images.Count} photos...",
                    AutoSize = true,
                    Font = new Font("Segoe UI", 12),
                    Padding = new Padding(5)
                };
                panel.Controls.Add(statusLabel);
                
                foreach (var image in images)
                {
                    // Create a panel to hold the image (for better styling)
                    var imagePanel = new Panel
                    {
                        Size = new Size(210, 210),
                        BackColor = Color.White,
                        Margin = new Padding(10),
                        Padding = new Padding(5)
                    };
                    
                    // Add border effect
                    imagePanel.Paint += (s, e) =>
                    {
                        var rect = imagePanel.ClientRectangle;
                        rect.Width -= 1;
                        rect.Height -= 1;
                        e.Graphics.DrawRectangle(new Pen(Color.FromArgb(230, 230, 230), 1), rect);
                    };
                    
                    var pic = new PictureBox
                    {
                        Size = new Size(200, 200),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        BackColor = Color.FromArgb(240, 240, 240),
                        Location = new Point(5, 5),
                        Cursor = Cursors.Hand
                    };
                    
                    imagePanel.Controls.Add(pic);
                    panel.Controls.Add(imagePanel);
                    
                    // Try to load image
                    try
                    {
                        var path = Path.Combine(image.ScanDirectory.DirectoryPath, image.RelativePath);
                        if (File.Exists(path))
                        {
                            pic.ImageLocation = path;
                            
                            // Add click to open
                            pic.Click += (s, e) =>
                            {
                                try
                                {
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                    {
                                        FileName = path,
                                        UseShellExecute = true
                                    });
                                }
                                catch { }
                            };
                        }
                    }
                    catch { }
                }
                
                // Update status when done
                statusLabel.Text = $"Showing {images.Count} photos";
                panel.Controls.Remove(statusLabel);
                panel.Controls.Add(statusLabel);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }
}