using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MyPhotoHelper.Data;

namespace MyPhotoHelper.Pages
{
    public partial class Index : ComponentBase
    {
        [Inject] private NavigationManager Navigation { get; set; } = null!;
        [Inject] private MyPhotoHelperDbContext DbContext { get; set; } = null!;

        protected override async Task OnInitializedAsync()
        {
            // Check if any scan directories are configured
            var hasScanDirectories = await DbContext.tbl_scan_directory.AnyAsync();
            
            if (!hasScanDirectories)
            {
                // No directories configured, redirect to startup wizard
                Navigation.NavigateTo("/startup-wizard", replace: true);
            }
            else
            {
                // Directories exist, go to memories page
                Navigation.NavigateTo("/memories", replace: true);
            }
        }
    }
}