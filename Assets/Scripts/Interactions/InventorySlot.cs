using System;
using System.Collections.Generic;

namespace Sol
{
    /// <summary>
    /// One slot in the inventory list. Holds a reference to the ItemComponent and a stack count.
    /// Stacked items keep all their world-object references so each can be dropped independently.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        public Grab.ItemComponent Item;
        public int Count;

        // Extra item references added when stacking (first item stays in Item).
        private readonly List<Grab.ItemComponent> _extras = new();

        public InventorySlot(Grab.ItemComponent item, int count = 1)
        {
            Item = item;
            Count = count;
        }

        /// <summary>Store an additional item reference when stacking.</summary>
        public void PushExtra(Grab.ItemComponent item)
        {
            _extras.Add(item);
        }

        /// <summary>
        /// Pop one item reference for dropping/consuming. Returns the last extra first,
        /// then falls back to the primary Item when no extras remain.
        /// </summary>
        public Grab.ItemComponent PopItem()
        {
            if (_extras.Count > 0)
            {
                var last = _extras[_extras.Count - 1];
                _extras.RemoveAt(_extras.Count - 1);
                return last;
            }
            return Item;
        }

        public IEnumerable<Grab.ItemComponent> EnumerateItems()
        {
            if (Item != null)
                yield return Item;

            for (int i = 0; i < _extras.Count; i++)
            {
                var extra = _extras[i];
                if (extra != null)
                    yield return extra;
            }
        }
    }
}
