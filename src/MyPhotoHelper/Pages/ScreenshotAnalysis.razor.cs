using Microsoft.AspNetCore.Components;
using MyPhotoHelper.Models;
using MyPhotoHelper.Services;

namespace MyPhotoHelper.Pages
{
    public partial class ScreenshotAnalysis : ComponentBase
    {
        [Inject] private IScreenshotAnalysisService AnalysisService { get; set; } = null!;
        [Inject] private IFastImageCategorizationService FastCategorizationService { get; set; } = null!;

        private List<ScreenshotPattern>? patterns;
        private List<ResolutionStats>? resolutionStats;
        private CameraDataStats? cameraStats;
        private bool isLoading = true;
        private bool isRunningDetection = false;
        private string? errorMessage;
        private string? detectionResult;

        protected override async Task OnInitializedAsync()
        {
            await RefreshAnalysis();
        }

        private async Task RefreshAnalysis()
        {
            isLoading = true;
            errorMessage = null;
            StateHasChanged();

            try
            {
                patterns = await AnalysisService.AnalyzeExistingPatternsAsync();
                resolutionStats = await AnalysisService.GetResolutionStatsAsync();
                cameraStats = await AnalysisService.GetCameraDataStatsAsync();
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        private async Task RunFastDetection()
        {
            isRunningDetection = true;
            detectionResult = null;
            StateHasChanged();

            try
            {
                var progress = new Progress<PhaseProgress>(p =>
                {
                    detectionResult = $"Progress: {p.CurrentItem} ({p.ProcessedItems}/{p.TotalItems})";
                    InvokeAsync(StateHasChanged);
                });

                await FastCategorizationService.CategorizeImagesAsync(progress);
                detectionResult = "✅ Fast screenshot detection completed! Refresh the analysis to see results.";
            }
            catch (Exception ex)
            {
                detectionResult = $"❌ Error: {ex.Message}";
            }
            finally
            {
                isRunningDetection = false;
                StateHasChanged();
            }
        }
    }
}