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
        /// <summary>
        /// Unique code if this slot is a caught fish (for example, FSH12345). Otherwise null.
        /// This enables lookup in the runtime FishRegistry for instance-unique, non-stackable inventory.
        /// </summary>
        public string FishCode;

        // Extra item references added when stacking (first item stays in Item).
        private readonly List<Grab.ItemComponent> _extras = new();

        public InventorySlot(Grab.ItemComponent item, int count = 1)
        {
            Item = item;
            Count = count;
            FishCode = null;
        }

        /// <summary>
        /// Create an inventory slot for a uniquely-caught fish.
        /// </summary>
        /// <param name="item">Root item template for caught fish (if any).</param>
        /// <param name="fishCode">Unique registry code (FSH12345, etc)</param>
        public InventorySlot(Grab.ItemComponent item, string fishCode)
        {
            Item = item;
            Count = 1;
            FishCode = fishCode;
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

        /// <summary>
        /// Remove a specific item reference from this slot without changing Count.
        /// If the primary item is removed and extras exist, promotes the last extra to primary.
        /// </summary>
        public bool TryRemoveItemReference(Grab.ItemComponent item)
        {
            if (item == null)
                return false;

            if (ReferenceEquals(Item, item))
            {
                if (_extras.Count > 0)
                {
                    Item = _extras[_extras.Count - 1];
                    _extras.RemoveAt(_extras.Count - 1);
                }
                else
                {
                    Item = null;
                }

                return true;
            }

            for (int i = _extras.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_extras[i], item))
                    continue;

                _extras.RemoveAt(i);
                return true;
            }

            return false;
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
