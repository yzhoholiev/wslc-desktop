using System.Collections.ObjectModel;

namespace WslcDesktop.ViewModels;

public static class CollectionExtensions
{
    /// <summary>
    ///     Brings the collection in line with <paramref name="source" /> without removing and re-adding a row whose item only changed or
    ///     moved, which a list shows as the row disappearing.
    /// </summary>
    /// <param name="target">The collection a list shows.</param>
    /// <param name="source">The items in the order to show them.</param>
    /// <param name="keyOf">The identity of an item that survives a change to its values.</param>
    public static void SyncTo<T, TKey>(this ObservableCollection<T> target, ReadOnlySpan<T> source, Func<T, TKey> keyOf)
    {
        EqualityComparer<TKey> comparer = EqualityComparer<TKey>.Default;
        for (int i = 0; i < source.Length; i++)
        {
            TKey key = keyOf(source[i]);
            int current = -1;
            for (int j = i; j < target.Count; j++)
            {
                if (comparer.Equals(keyOf(target[j]), key))
                {
                    current = j;
                    break;
                }
            }

            if (current < 0)
            {
                target.Insert(i, source[i]);
                continue;
            }

            if (current != i)
            {
                target.Move(current, i);
            }

            if (!EqualityComparer<T>.Default.Equals(target[i], source[i]))
            {
                target[i] = source[i];
            }
        }

        while (target.Count > source.Length)
        {
            target.RemoveAt(target.Count - 1);
        }
    }

    /// <summary>
    ///     Replaces the contents only when they differ, so an unchanged list does not re-render.
    /// </summary>
    public static void ReplaceWith<T>(this ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        if (items.SequenceEqual(target))
        {
            return;
        }

        target.Clear();
        foreach (T item in items)
        {
            target.Add(item);
        }
    }
}
