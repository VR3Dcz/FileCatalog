using System.Threading.Tasks;

namespace FileCatalog.Services.UI;

/// <summary>
/// Provides UI dialog interactions abstractly so the ViewModel remains independent of the View.
/// </summary>
public interface IDialogService
{
    Task<string?> ShowFolderPickerDialogAsync();

    /// <summary>
    /// Opens a standard file picker to open an existing catalog.
    /// </summary>
    Task<string?> ShowOpenFileDialogAsync();

    /// <summary>
    /// Opens a standard file saver to save the catalog to a new location.
    /// </summary>
    Task<string?> ShowSaveFileDialogAsync();
}