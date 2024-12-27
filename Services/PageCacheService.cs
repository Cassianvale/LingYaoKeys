using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace WpfApp.Services
{
    public class PageCacheService
    {
        private static readonly Dictionary<Type, Page> _pageCache = new Dictionary<Type, Page>();
        private static readonly object _lock = new object();

        public static T GetPage<T>() where T : Page, new()
        {
            lock (_lock)
            {
                Type pageType = typeof(T);
                if (!_pageCache.ContainsKey(pageType))
                {
                    _pageCache[pageType] = new T();
                }
                return (T)_pageCache[pageType];
            }
        }

        public static void ClearCache()
        {
            lock (_lock)
            {
                foreach (var page in _pageCache.Values)
                {
                    if (page is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                _pageCache.Clear();
            }
        }

        public static void RemoveFromCache<T>() where T : Page
        {
            lock (_lock)
            {
                Type pageType = typeof(T);
                if (_pageCache.ContainsKey(pageType))
                {
                    if (_pageCache[pageType] is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    _pageCache.Remove(pageType);
                }
            }
        }
    }
} 