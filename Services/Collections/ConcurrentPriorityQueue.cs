using System.Collections.Concurrent;

namespace WpfApp.Services.Collections
{
    public class ConcurrentPriorityQueue<TKey, TValue> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, (TValue Value, int Priority)> _items = new();

        public void Add(TKey key, TValue value, int priority)
        {
            _items.AddOrUpdate(key, (value, priority), (_, _) => (value, priority));
        }

        public bool TryRemove(TKey key)
        {
            return _items.TryRemove(key, out _);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (_items.TryGetValue(key, out var item))
            {
                value = item.Value;
                return true;
            }
            value = default!;
            return false;
        }

        public void Clear()
        {
            _items.Clear();
        }

        public int Count => _items.Count;
    }
} 