using FaceVault.Services;
using CSnakes.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using CSnakes.Runtime.PackageManagement;

namespace FaceVault.Tests;

/// <summary>
/// Simplified screenshot detection tests that are more reliable in Visual Studio
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class SimpleScreenshotTests
{
    // Internal static fields so other test classes can access the shared Python environment
    internal static ServiceProvider? _serviceProvider;
    internal static IScreenshotDetectionService? _screenshotService;
    private static string _screenshotsDirectory = string.Empty;

    [AssemblyInitialize]
    public static void AssemblySetup(TestContext context)
    {
        try
        {
            var startTime = DateTime.Now;
            Console.WriteLine($"[{startTime:HH:mm:ss.fff}] Starting Python environment initialization for FaceVault tests...");
            
            // Get the directory where test images are located
            var testProjectDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var testImagesDirectory = Path.Combine(testProjectDir!, "Images");
            _screenshotsDirectory = Path.Combine(testImagesDirectory, "screenshots");
            
            Console.WriteLine($"Screenshots directory: {_screenshotsDirectory}");
            
            // Set up Python environment
            var services = new ServiceCollection();
            
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddConsole();
                builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning); // Reduce logging noise
            });

            // Python files are now automatically linked from FaceVault via project file
            var testBinDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var pythonHome = Path.Join(testBinDir, "Python");
            
            Console.WriteLine($"Python Home: {pythonHome}");

            services
                .WithPython()
                .WithHome(pythonHome)
                .FromRedistributable("3.12");

            services.AddScoped<IScreenshotDetectionService, ScreenshotDetectionService>();

            _serviceProvider = services.BuildServiceProvider();
            
            // Initialize Python environment
            Console.WriteLine("Initializing Python environment...");
            var pythonEnv = _serviceProvider.GetRequiredService<IPythonEnvironment>();
            Console.WriteLine("Python environment created successfully");
            
            // For tests, ensure minimal packages are installed
            try
            {
                dynamic result = pythonEnv.Screenshots().CheckLibraries();
                var libCheckResult = result?.ToString() ?? "null";
                Console.WriteLine($"Library check result: {libCheckResult}");
                
                if (libCheckResult.Contains("'has_image_libs': False"))
                {
                    Console.WriteLine("Warning: Python image libraries not available for testing");
                    Console.WriteLine("Tests will use filename-based detection only");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not check Python libraries: {ex.Message}");
            }
            
            _screenshotService = _serviceProvider.GetRequiredService<IScreenshotDetectionService>();
            
            var endTime = DateTime.Now;
            var duration = endTime - startTime;
            Console.WriteLine($"[{endTime:HH:mm:ss.fff}] Python environment initialization completed");
            Console.WriteLine($"Total initialization time: {duration.TotalSeconds:F2} seconds ({duration.TotalMilliseconds:F0}ms)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to setup test environment: {ex.Message}");
            throw;
        }
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Cleaning up Python environment for FaceVault tests...");
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        _screenshotService = null;
    }

    [TestMethod]
    [Timeout(15000)]
    public void TestEnvironment_IsInitialized()
    {
        Assert.IsNotNull(_screenshotService, "Screenshot detection service should be initialized");
        Assert.IsTrue(Directory.Exists(_screenshotsDirectory), $"Screenshots directory should exist: {_screenshotsDirectory}");
        
        var imageFiles = Directory.GetFiles(_screenshotsDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => IsImageFile(file))
            .ToArray();
            
        Assert.IsTrue(imageFiles.Length > 0, "Screenshots directory should contain at least one image file");
        Console.WriteLine($"Test environment verified: {imageFiles.Length} image files found");
    }

    [TestMethod]
    [Timeout(30000)]
    public async Task AllScreenshotsInDirectory_AreDetectedCorrectly()
    {
        Assert.IsNotNull(_screenshotService, "Screenshot service not initialized");
        
        var imageFiles = Directory.GetFiles(_screenshotsDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => IsImageFile(file))
            .ToArray();
            
        Assert.IsTrue(imageFiles.Length > 0, "No image files found for testing");
        
        Console.WriteLine($"\nTesting {imageFiles.Length} screenshot files in directory: {_screenshotsDirectory}");
        Console.WriteLine(new string('-', 80));
        
        int successCount = 0;
        var failedFiles = new List<string>();
        
        foreach (var testFile in imageFiles)
        {
            var fileName = Path.GetFileName(testFile);
            Console.Write($"Testing '{fileName}'... ");
            
            try
            {
                var result = await _screenshotService.DetectScreenshotAsync(testFile);
                
                Assert.IsNotNull(result, "Detection result should not be null");
                
                if (result.IsScreenshot)
                {
                    Console.WriteLine($"✓ DETECTED as screenshot (confidence: {result.Confidence:F2})");
                    successCount++;
                }
                else
                {
                    Console.WriteLine($"✗ NOT detected as screenshot (confidence: {result.Confidence:F2})");
                    failedFiles.Add($"{fileName} - Confidence: {result.Confidence:F2}");
                    
                    // Print debug info for failed detections
                    if (result.Analysis != null && result.Analysis.Count > 0)
                    {
                        Console.WriteLine("  Analysis details:");
                        foreach (var kvp in result.Analysis.Take(5))
                        {
                            Console.WriteLine($"    - {kvp.Key}: {kvp.Value}");
                        }
                    }
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Console.WriteLine($"  Error: {result.Error}");
                    }
                }
                
                // Check if image libraries are available
                if (result.Analysis != null && result.Analysis.ContainsKey("error"))
                {
                    var error = result.Analysis["error"]?.ToString() ?? "";
                    if (error.Contains("Image analysis libraries not available"))
                    {
                        Assert.Fail($"Image analysis libraries (PIL/numpy) are not available! " +
                                   $"These are REQUIRED for screenshot detection. " +
                                   $"Error: {error}");
                    }
                }
                
                // ALL images in the screenshots directory MUST be detected as screenshots
                Assert.IsTrue(result.IsScreenshot, 
                    $"File '{fileName}' should be detected as a screenshot. " +
                    $"Confidence: {result.Confidence:F2}, Error: {result.Error ?? "None"}");
                Assert.IsTrue(result.Confidence > 0, "Confidence should be greater than 0");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAILED with error: {ex.Message}");
                failedFiles.Add($"{fileName} - Error: {ex.Message}");
                Assert.Fail($"Screenshot detection failed for '{fileName}': {ex.Message}");
            }
        }
        
        Console.WriteLine(new string('-', 80));
        Console.WriteLine($"Summary: {successCount}/{imageFiles.Length} screenshots detected successfully");
        
        if (failedFiles.Any())
        {
            Console.WriteLine("\nFailed detections:");
            foreach (var failure in failedFiles)
            {
                Console.WriteLine($"  - {failure}");
            }
        }
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task BooleanDetection_WorksCorrectly()
    {
        Assert.IsNotNull(_screenshotService, "Screenshot service not initialized");
        
        var imageFiles = Directory.GetFiles(_screenshotsDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => IsImageFile(file))
            .ToArray();
            
        var testFile = imageFiles.First();
        
        try
        {
            var isScreenshot = await _screenshotService.IsScreenshotAsync(testFile);
            
            Assert.IsTrue(isScreenshot, $"File '{Path.GetFileName(testFile)}' should be detected as a screenshot");
            Console.WriteLine($"✓ Boolean detection successful for: {Path.GetFileName(testFile)}");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Boolean screenshot detection failed: {ex.Message}");
        }
    }

    private static bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tiff" or ".webp";
    }
}