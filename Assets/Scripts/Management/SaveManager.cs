using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Sol.AI;
using Sol.Grab;
using Sol.Player;
using Sol.ToD;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sol.SaveLoad
{
    public partial class SaveManager : MonoBehaviour // Singleton responsible for saving and loading game state, including player data, NPC states, world containers, and more. Uses JSON serialization to write save files to disk, and captures screenshots for save previews.
    {
        public static SaveManager Instance { get; private set; } // Singleton instance, accessible via SaveManager.Instance. Set in Awake().

        public const int MaxSlots = 25;
        public const int AutoSaveSlot = 0;

        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "saves");
#region Inspector Settings

        [Header("Player Reference")]
        [Tooltip("Inspector: tunes player root.")]
        [SerializeField] private GameObject _playerRoot;
#endregion

        private float _sessionStartTime;
    }
}



