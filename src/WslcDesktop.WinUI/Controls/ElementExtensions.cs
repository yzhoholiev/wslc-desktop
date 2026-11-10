using Microsoft.UI.Xaml;
using WslcDesktop.Services;

namespace WslcDesktop.Controls;

public static class ElementExtensions
{
    extension(object sender)
    {
        public T TagOf<T>() => (T)((FrameworkElement)sender).Tag;
    }

    extension(FrameworkElement element)
    {
        /// <summary>
        ///     Runs <paramref name="onUpdated" /> after each monitor update while the element is in the live tree.
        /// </summary>
        public void FollowMonitor(RuntimeMonitor monitor, Action onUpdated)
        {
            element.Loaded += (_, _) => monitor.Updated += onUpdated;
            element.Unloaded += (_, _) => monitor.Updated -= onUpdated;
        }

        /// <summary>
        ///     Completes once the element is in the live tree, so dialogs can use its XamlRoot.
        /// </summary>
        public Task WhenLoadedAsync()
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
}
