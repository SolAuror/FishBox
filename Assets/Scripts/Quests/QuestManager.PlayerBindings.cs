using System;
using System.Collections.Generic;
using UnityEngine;
using Sol.AI;
using Sol.Actions;
using Sol.Grab;
using Sol.HUD;
using Sol.ToD;
using System.Collections;

namespace Sol.Quests
{
    public partial class QuestManager : MonoBehaviour
    {

        private void ResolvePlayerRefs()
        {
            if (_inventory != null && _equipment != null)
                return;

            GameObject root = _playerRoot;
            if (root == null)
            {
                Sol.Player.PlayerSoul soul = FindFirstObjectByType<Sol.Player.PlayerSoul>();
                if (soul != null)
                    root = soul.gameObject;
            }

            if (root == null)
                return;

            _inventory = root.GetComponentInChildren<Inventory>(true);
            _equipment = root.GetComponentInChildren<Equipment>(true);
            _calendar = FindFirstObjectByType<Calendar>();
        }


        private void SubscribeToPlayer()
        {
            if (_inventory != null)
                _inventory.OnChanged += HandleInventoryChanged;
            if (_equipment != null)
                _equipment.OnChanged += HandleEquipmentChanged;
        }


        private void UnsubscribeFromPlayer()
        {
            if (_inventory != null)
                _inventory.OnChanged -= HandleInventoryChanged;
            if (_equipment != null)
                _equipment.OnChanged -= HandleEquipmentChanged;
        }
    }
}
