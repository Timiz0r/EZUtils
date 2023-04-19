namespace EZUtils
{
    using System.Collections.Generic;
    using System.Linq;

    public class MultiValueDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, List<TValue>> items = new Dictionary<TKey, List<TValue>>();

        public void Add(TKey key, TValue value)
        {
            if (items.TryGetValue(key, out List<TValue> values))
            {
                values.Add(value);
            }
            else
            {
                items[key] = new List<TValue>() { value };
            }
        }

        public bool TryGetValues(TKey key, out IReadOnlyList<TValue> values)
        {
            if (items.TryGetValue(key, out List<TValue> v))
            {
                values = v;
                return true;
            }

            values = null;
            return false;
        }

        public IEnumerable<(TKey key, TValue value)> GetValues()
            => items.SelectMany(kvp => kvp.Value.Select(v => (kvp.Key, v)));
    }
}
