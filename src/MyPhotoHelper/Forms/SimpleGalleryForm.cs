using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyPhotoHelper.Data;
using MyPhotoHelper.Services;

namespace MyPhotoHelper.Forms
{
    public class SimpleGalleryForm : Form
    {
        private readonly IServiceProvider _serviceProvider;
        private FlowLayoutPanel _photoPanel;
        private Label _statusLabel;
        private ProgressBar _progressBar;
        private Button _loadMoreButton;
        private int _currentPage = 0;
        private const int _pageSize = 50;
        private readonly ConcurrentDictionary<string, byte[]> _thumbnailCache = new();
        private CancellationTokenSource? _loadingCts;

        public SimpleGalleryForm(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            
            // Basic form setup
            Text = "Photo Gallery";
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
            
            // Top panel with status and progress
            var topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.WhiteSmoke
            };
            
            // Status label
            _statusLabel = new Label
            {
                Text = "Loading photos...",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 12)
            };
            
            // Progress bar
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            
            topPanel.Controls.Add(_statusLabel);
            topPanel.Controls.Add(_progressBar);
            
            // Create a simple panel for photos
            _photoPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(10)
            };
            
            // Load more button at bottom
            _loadMoreButton = new Button
            {
                Text = "Load More Photos",
                Dock = DockStyle.Bottom,
                Height = 40,
                Visible = false,
                Font = new Font("Arial", 11),
                BackColor = Color.LightBlue
            };
            _loadMoreButton.Click += async (s, e) => await LoadMorePhotos();
            
            Controls.Add(_photoPanel);
            Controls.Add(_loadMoreButton);
            Controls.Add(topPanel);
            
            // Load photos when form loads
            Load += async (s, e) => await LoadMorePhotos();
            
            // Cancel loading when form closes
            FormClosing += (s, e) =>
            {
                _loadingCts?.Cancel();
            };
        }
        
        private async Task LoadMorePhotos()
        {
            if (_loadingCts != null && !_loadingCts.IsCancellationRequested)
                return;
                
            _loadingCts = new CancellationTokenSource();
            var cancellationToken = _loadingCts.Token;
            
            _loadMoreButton.Enabled = false;
            _progressBar.Visible = true;
            
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<MyPhotoHelperDbContext>();
                var thumbnailService = scope.ServiceProvider.GetRequiredService<IThumbnailService>();
                
                // Get total count
                var totalCount = await dbContext.tbl_images
                    .Where(i => i.FileExists == 1 && i.IsDeleted == 0)
                    .CountAsync(cancellationToken);
                
                // Get next page of photos
                var images = await dbContext.tbl_images
                    .Include(i => i.ScanDirectory)
                    .Where(i => i.FileExists == 1 && i.IsDeleted == 0)
                    .OrderByDescending(i => i.DateModified)
                    .Skip(_currentPage * _pageSize)
                    .Take(_pageSize)
                    .ToListAsync(cancellationToken);
                
                if (images.Count == 0)
                {
                    _statusLabel.Text = $"All {_photoPanel.Controls.Count} photos loaded";
                    _loadMoreButton.Visible = false;
                    _progressBar.Visible = false;
                    return;
                }
                
                _progressBar.Maximum = images.Count;
                _progressBar.Value = 0;
                
                // Create placeholder picture boxes first
                var pictureBoxes = new PictureBox[images.Count];
                for (int i = 0; i < images.Count; i++)
                {
                    var pictureBox = new PictureBox
                    {
                        Size = new Size(150, 150),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        BackColor = Color.LightGray,
                        Margin = new Padding(5),
                        Tag = images[i]
                    };
                    
                    // Show loading placeholder
                    var placeholder = new Bitmap(150, 150);
                    using (var g = Graphics.FromImage(placeholder))
                    {
                        g.Clear(Color.LightGray);
                        g.DrawString("Loading...", new Font("Arial", 10), Brushes.Gray, 40, 65);
                    }
                    pictureBox.Image = placeholder;
                    
                    pictureBoxes[i] = pictureBox;
                    _photoPanel.Controls.Add(pictureBox);
                }
                
                _statusLabel.Text = $"Loading {images.Count} photos... Total: {_photoPanel.Controls.Count}/{totalCount}";
                
                // Load thumbnails in parallel with limited concurrency
                var semaphore = new SemaphoreSlim(4); // Load 4 at a time
                var loadedCount = 0;
                
                var tasks = images.Select(async (image, index) =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var imagePath = Path.Combine(image.ScanDirectory.DirectoryPath, image.RelativePath);
                        
                        // Check cache first
                        byte[]? bytes;
                        if (!_thumbnailCache.TryGetValue(imagePath, out bytes!))
                        {
                            bytes = await thumbnailService.GetThumbnailAsync(imagePath);
                            if (bytes.Length > 0 && bytes.Length < 500000) // Cache if less than 500KB
                            {
                                _thumbnailCache.TryAdd(imagePath, bytes);
                            }
                        }
                        
                        if (bytes.Length > 0 && !cancellationToken.IsCancellationRequested)
                        {
                            using (var ms = new MemoryStream(bytes))
                            {
                                var img = Image.FromStream(ms);
                                
                                Invoke((Action)(() =>
                                {
                                    if (!cancellationToken.IsCancellationRequested && index < pictureBoxes.Length)
                                    {
                                        pictureBoxes[index].Image?.Dispose();
                                        pictureBoxes[index].Image = img;
                                        
                                        // Add click handler
                                        pictureBoxes[index].Click += (s, e) =>
                                        {
                                            try
                                            {
                                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                                {
                                                    FileName = imagePath,
                                                    UseShellExecute = true
                                                });
                                            }
                                            catch { }
                                        };
                                        
                                        _progressBar.Value = Math.Min(++loadedCount, _progressBar.Maximum);
                                        _statusLabel.Text = $"Loaded {_photoPanel.Controls.Count} photos ({loadedCount}/{images.Count} on this page)";
                                    }
                                }));
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        semaphore.Release();
                    }
                });
                
                await Task.WhenAll(tasks);
                
                _currentPage++;
                _loadMoreButton.Visible = (_currentPage * _pageSize) < totalCount;
                _loadMoreButton.Text = $"Load More Photos ({totalCount - _photoPanel.Controls.Count} remaining)";
                _statusLabel.Text = $"Showing {_photoPanel.Controls.Count} of {totalCount} photos";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _progressBar.Visible = false;
                _loadMoreButton.Enabled = true;
                _loadingCts = null;
            }
        }
    }
}