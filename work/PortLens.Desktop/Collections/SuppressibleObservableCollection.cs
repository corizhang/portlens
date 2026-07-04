using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PortLens.Desktop.Collections;

public sealed class SuppressibleObservableCollection<T> : ObservableCollection<T>
{
    private int _suppressionCount;
    private bool _changed;

    public IDisposable SuppressNotifications()
    {
        _suppressionCount++;
        return new SuppressionScope(this);
    }

    public void ResetTo(IEnumerable<T> items)
    {
        using (SuppressNotifications())
        {
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
        }
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_suppressionCount > 0)
        {
            _changed = true;
            return;
        }

        base.OnCollectionChanged(e);
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_suppressionCount > 0)
        {
            return;
        }

        base.OnPropertyChanged(e);
    }

    private void Release()
    {
        if (--_suppressionCount == 0 && _changed)
        {
            _changed = false;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    private sealed class SuppressionScope : IDisposable
    {
        private readonly SuppressibleObservableCollection<T> _collection;
        private bool _disposed;

        public SuppressionScope(SuppressibleObservableCollection<T> collection)
        {
            _collection = collection;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _collection.Release();
        }
    }
}
