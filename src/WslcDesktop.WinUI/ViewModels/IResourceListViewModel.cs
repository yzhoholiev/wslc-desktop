using System.ComponentModel;
using WslcDesktop.Models;

namespace WslcDesktop.ViewModels;

/// <summary>
///     What a resource list page drives, whatever the item type.
/// </summary>
public interface IResourceListViewModel : INotifyPropertyChanged
{
    object? SelectedItem { get; set; }

    /// <summary>
    ///     The rows, as an observable collection of the list's item type.
    /// </summary>
    object Items { get; }

    string Summary { get; }

    string SearchText { get; set; }

    bool IsBusy { get; }

    bool IsEmpty { get; }

    EmptyText Empty { get; }

    bool HasLoadError { get; }

    string? LoadError { get; }

    bool IsDetailsOpen { get; }

    string DetailsTitle { get; }

    string DetailsSubtitle { get; }

    string InspectText { get; }

    Task LoadAsync();

    void OnRuntimeUpdated(RuntimeSnapshot snapshot);

    Task RunEmptyActionAsync();

    void CloseDetails();

    /// <summary>
    ///     The containers that use the item, listed by its usage chip.
    /// </summary>
    ContainerDto[] ContainersUsing(object item);
}
