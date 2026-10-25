using Microsoft.UI.Xaml;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

public static class ElementExtensions
{
    public static T TagOf<T>(this object sender) => (T)((FrameworkElement)sender).Tag;

    /// <summary>
    ///     Runs <paramref name="onUpdated" /> after each monitor update while the element is in the live tree.
    /// </summary>
    public static void FollowMonitor(this FrameworkElement element, RuntimeMonitor monitor, Action onUpdated)
    {
        element.Loaded += (_, _) => monitor.Updated += onUpdated;
        element.Unloaded += (_, _) => monitor.Updated -= onUpdated;
    }

    /// <summary>
    ///     Completes once the element is in the live tree, so dialogs can use its XamlRoot.
    /// </summary>
    public static Task WhenLoadedAsync(this FrameworkElement element)
    {
        if (element.IsLoaded)
        {
            return Task.CompletedTask;
        }

        var loaded = new TaskCompletionSource();

        element.Loaded += OnLoaded;
        return loaded.Task;

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            element.Loaded -= OnLoaded;
            loaded.SetResult();
        }
    }
}
