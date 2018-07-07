using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SubtypeInduction.TypeSystemRels;

namespace SubtypeInduction
{
    public sealed class MultinomialDistribution<K>
    {
        private readonly Dictionary<K, decimal> _elements = new Dictionary<K, decimal>();
        private decimal _size = 0;

        public void Add(K element, decimal numToAdd = 1)
        {
            if (!_elements.TryGetValue(element, out var count))
            {
                count = 0;
            }
            _elements[element] = count + numToAdd;
            _size += numToAdd;
        }

        public void AddManyOnce(IEnumerable<K> elements)
        {
            foreach (var elementCounts in elements.GroupBy(k => k))
            {
                Add(elementCounts.Key, elementCounts.Count());
            }
        }

        public decimal this[K el]
        {
            get
            {
                if (!_elements.TryGetValue(el, out var count)) count = 0;
                return count;
            }
        }

        public double ProbabilityOf(K el, MultinomialDistribution<K> prior = null, double dirichletAlpha = 1)
        {
            if (prior == null || dirichletAlpha == 0)
            {
                return (double)(this[el] / _size);
            }

            double baseProb = ((double)prior[el]) / (double)prior.Count;
            return ((double)this[el] + dirichletAlpha * baseProb) / ((double)_size + dirichletAlpha);
        }

        public IEnumerable<K> Elements => _elements.Keys;

        public decimal Count { get { return _size; } }
    }

    public static class SubtokenSplitter
    {
        public static string[] SplitSubtokens(string name)
        {
            var delimiters = new char[] { '_', '.' };
            var subtokens = name.Split(delimiters)
                .SelectMany(n => Regex.Split(n, "(?<!(^|[A-Z0-9]))(?=[A-Z0-9])|(?<!(^|[^A-Z]))(?=[0-9])|(?<!(^|[^0-9]))(?=[A-Za-z])|(?<!^)(?=[A-Z][a-z])")
                    .Select(s => s.ToLower()));
            return subtokens.Where(s => s.Length > 0).ToArray();
        }
    }

    public class LRUCache<K, V>
    {
        private readonly int capacity;
        private Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>> cacheMap = new Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>>();
        private LinkedList<LRUCacheItem<K, V>> lruList = new LinkedList<LRUCacheItem<K, V>>();
        private int _accesses = 0;
        private int _hits = 0;

        public LRUCache(int capacity)
        {
            this.capacity = capacity;
        }

        public double HitRate()
        {
            try
            {
                if (_accesses == 0) return 0;
                return ((double)_hits) / _accesses;
            }
            finally
            {
                _hits = 0;
                _accesses = 0;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public V Get(K key)
        {
            _accesses++;
            if (cacheMap.TryGetValue(key, out LinkedListNode<LRUCacheItem<K, V>> node))
            {
                V value = node.Value.value;
                lruList.Remove(node);
                lruList.AddLast(node);
                _hits++;
                return value;
            }
            return default(V);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Add(K key, V val)
        {
            if (cacheMap.Count >= capacity)
            {
                RemoveFirst();
            }

            LRUCacheItem<K, V> cacheItem = new LRUCacheItem<K, V>(key, val);
            LinkedListNode<LRUCacheItem<K, V>> node = new LinkedListNode<LRUCacheItem<K, V>>(cacheItem);
            lruList.AddLast(node);
            cacheMap.Add(key, node);
        }

        private void RemoveFirst()
        {
            // Remove from LRUPriority
            LinkedListNode<LRUCacheItem<K, V>> node = lruList.First;
            lruList.RemoveFirst();

            // Remove from cache
            cacheMap.Remove(node.Value.key);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public V Get(K key, Func<V> compute)
        {
            _accesses++;
            if (cacheMap.TryGetValue(key, out var node))
            {
                V value = node.Value.value;
                lruList.Remove(node);
                lruList.AddLast(node);
                _hits++;
                return value;
            }
            V computedValue = compute();
            Add(key, computedValue);
            return computedValue;
        }
    }

    class LRUCacheItem<K, V>
    {
        public LRUCacheItem(K k, V v)
        {
            key = k;
            value = v;
        }
        public K key;
        public V value;
    }
}
