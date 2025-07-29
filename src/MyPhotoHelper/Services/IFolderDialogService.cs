namespace MyPhotoHelper.Services
{
    public interface IFolderDialogService
    {
        Task<string?> OpenFolderDialogAsync(string? initialDirectory = null);
    }
}