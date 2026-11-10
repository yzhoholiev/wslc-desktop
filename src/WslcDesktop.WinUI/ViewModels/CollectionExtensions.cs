using System.Collections.ObjectModel;

namespace WslcDesktop.ViewModels;

public static class CollectionExtensions
{
    /// <summary>
    ///     Updates to a collection a list shows.
    /// </summary>
    /// <param name="target">The collection a list shows.</param>
    extension<T>(ObservableCollection<T> target)
    {
        /// <summary>
        ///     Brings the collection in line with <paramref name="source" /> without removing and re-adding a row whose item only changed or
        ///     moved, which a list shows as the row disappearing.
        /// </summary>
        /// <param name="source">The items in the order to show them.</param>
        /// <param name="keyOf">The identity of an item that survives a change to its values.</param>
        public void SyncTo<TKey>(ReadOnlySpan<T> source, Func<T, TKey> keyOf)
        {
            EqualityComparer<TKey> comparer = EqualityComparer<TKey>.Default;
        nextItem:
            for (int i = 0; i < source.Length; i++)
            {
                TKey key = keyOf(source[i]);
                for (int j = i; j < target.Count; j++)
                {
                    if (!comparer.Equals(keyOf(target[j]), key))
                    {
                        continue;
                    }

                    if (j != i)
                    {
                        target.Move(j, i);
                    }

                    if (!EqualityComparer<T>.Default.Equals(target[i], source[i]))
                    {
                        target[i] = source[i];
                    }

                    continue nextItem;
                }

                target.Insert(i, source[i]);
            }

            while (target.Count > source.Length)
            {
                target.RemoveAt(target.Count - 1);
            }
        }

        /// <summary>
        ///     Replaces the contents only when they differ, so an unchanged list does not re-render.
        /// </summary>
        public void ReplaceWith(IReadOnlyList<T> items)
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
}
