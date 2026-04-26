using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sol.Audio
{
    [CreateAssetMenu(menuName = "Sol/Audio/Audio Cue Library", fileName = "AudioCueLibrary")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class AudioCue
        {
            [SerializeField] private AudioEvent _event;
            [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
            [SerializeField] private bool _spatialized = true;
            [SerializeField, Range(0f, 1f)] private float _baseVolume = 1f;
            [SerializeField, Min(0f)] private float _cooldownSeconds = 0f;
            [SerializeField] private Vector2 _volumeRange = new(0.95f, 1f);
            [SerializeField] private Vector2 _pitchRange = new(0.98f, 1.02f);

            public AudioEvent Event => _event;
            public IReadOnlyList<AudioClip> Clips => _clips;
            public bool Spatialized => _spatialized;
            public float BaseVolume => Mathf.Clamp01(_baseVolume);
            public float CooldownSeconds => Mathf.Max(0f, _cooldownSeconds);
            public Vector2 VolumeRange => NormalizeRange(_volumeRange, 0f, 2f, new Vector2(1f, 1f));
            public Vector2 PitchRange => NormalizeRange(_pitchRange, 0.1f, 3f, new Vector2(1f, 1f));

            public void SetEvent(AudioEvent audioEvent)
            {
                _event = audioEvent;
            }

            private static Vector2 NormalizeRange(Vector2 value, float min, float max, Vector2 fallback)
            {
                if (value.x > value.y)
                    value = new Vector2(value.y, value.x);

                float low = Mathf.Clamp(value.x, min, max);
                float high = Mathf.Clamp(value.y, min, max);
                if (high < low)
                    return fallback;

                return new Vector2(low, high);
            }
        }

        [Serializable]
        public sealed class LoopCue
        {
            [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
            [SerializeField, Range(0f, 1f)] private float _baseVolume = 0.75f;
            [SerializeField] private Vector2 _pitchRange = new(1f, 1f);

            public IReadOnlyList<AudioClip> Clips => _clips;
            public float BaseVolume => Mathf.Clamp01(_baseVolume);
            public Vector2 PitchRange
            {
                get
                {
                    float min = Mathf.Clamp(Mathf.Min(_pitchRange.x, _pitchRange.y), 0.1f, 3f);
                    float max = Mathf.Clamp(Mathf.Max(_pitchRange.x, _pitchRange.y), 0.1f, 3f);
                    return new Vector2(min, max);
                }
            }
        }

        [SerializeField] private List<AudioCue> _cues = new();

        [Header("Ambient Loops")]
        [SerializeField] private LoopCue _worldAmbientLoop = new();
        [SerializeField] private LoopCue _windLoop = new();
        [SerializeField] private LoopCue _waveLoop = new();
        [SerializeField] private LoopCue _underwaterLoop = new();
        [SerializeField, Range(0f, 1f)] private float _underwaterAmbientDuck = 0.35f;

        private static readonly AudioEvent[] RequiredEvents =
        {
            AudioEvent.ItemPickup,
            AudioEvent.ItemConsume,
            AudioEvent.GoldGained,
            AudioEvent.GoldSpent,
            AudioEvent.GoldWorldPickup,
            AudioEvent.FishingCast,
            AudioEvent.FishingSplash,
            AudioEvent.FishingReelLoopStart,
            AudioEvent.FishingReelLoopStop,
            AudioEvent.UnderwaterEnter,
            AudioEvent.UnderwaterExit,
            AudioEvent.UnderwaterLoop
        };

        public LoopCue WorldAmbientLoop => _worldAmbientLoop;
        public LoopCue WindLoop => _windLoop;
        public LoopCue WaveLoop => _waveLoop;
        public LoopCue UnderwaterLoop => _underwaterLoop;
        public float UnderwaterAmbientDuck => Mathf.Clamp01(_underwaterAmbientDuck);

        public bool TryGetCue(AudioEvent audioEvent, out AudioCue cue)
        {
            for (int i = 0; i < _cues.Count; i++)
            {
                AudioCue candidate = _cues[i];
                if (candidate != null && candidate.Event == audioEvent)
                {
                    cue = candidate;
                    return true;
                }
            }

            cue = null;
            return false;
        }

        public bool TryGetRandomClip(AudioEvent audioEvent, out AudioCue cue, out AudioClip clip)
        {
            cue = null;
            clip = null;

            if (!TryGetCue(audioEvent, out AudioCue resolvedCue) || resolvedCue.Clips == null)
                return false;

            int validClipCount = 0;
            for (int i = 0; i < resolvedCue.Clips.Count; i++)
            {
                if (resolvedCue.Clips[i] != null)
                    validClipCount++;
            }

            if (validClipCount <= 0)
                return false;

            int targetIndex = UnityEngine.Random.Range(0, validClipCount);
            int running = 0;
            for (int i = 0; i < resolvedCue.Clips.Count; i++)
            {
                AudioClip candidate = resolvedCue.Clips[i];
                if (candidate == null)
                    continue;

                if (running == targetIndex)
                {
                    cue = resolvedCue;
                    clip = candidate;
                    return true;
                }

                running++;
            }

            return false;
        }

        public bool TryGetRandomLoopClip(LoopCue loopCue, out AudioClip clip, out float pitch)
        {
            clip = null;
            pitch = 1f;

            if (loopCue == null || loopCue.Clips == null)
                return false;

            int validClipCount = 0;
            for (int i = 0; i < loopCue.Clips.Count; i++)
            {
                if (loopCue.Clips[i] != null)
                    validClipCount++;
            }

            if (validClipCount <= 0)
                return false;

            int targetIndex = UnityEngine.Random.Range(0, validClipCount);
            int running = 0;
            for (int i = 0; i < loopCue.Clips.Count; i++)
            {
                AudioClip candidate = loopCue.Clips[i];
                if (candidate == null)
                    continue;

                if (running == targetIndex)
                {
                    clip = candidate;
                    Vector2 pitchRange = loopCue.PitchRange;
                    pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
                    return true;
                }

                running++;
            }

            return false;
        }

        public bool ValidateForRuntime(out string warning)
        {
            EnsureRequiredEvents();

            List<string> missing = new();
            for (int i = 0; i < RequiredEvents.Length; i++)
            {
                AudioEvent required = RequiredEvents[i];
                if (!TryGetCue(required, out AudioCue cue) || !HasAnyClip(cue.Clips))
                    missing.Add(required.ToString());
            }

            if (!HasAnyClip(_worldAmbientLoop?.Clips))
                missing.Add("WorldAmbientLoop");
            if (!HasAnyClip(_windLoop?.Clips))
                missing.Add("WindLoop");
            if (!HasAnyClip(_waveLoop?.Clips))
                missing.Add("WaveLoop");

            warning = missing.Count > 0
                ? $"Missing audio clips for: {string.Join(", ", missing)}"
                : string.Empty;
            return missing.Count == 0;
        }

        private void Reset()
        {
            EnsureRequiredEvents();
        }

        private void OnValidate()
        {
            EnsureRequiredEvents();
        }

        private void EnsureRequiredEvents()
        {
            if (_cues == null)
                _cues = new List<AudioCue>();

            for (int i = 0; i < RequiredEvents.Length; i++)
            {
                AudioEvent required = RequiredEvents[i];
                bool found = false;
                for (int j = 0; j < _cues.Count; j++)
                {
                    AudioCue cue = _cues[j];
                    if (cue != null && cue.Event == required)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    AudioCue cue = new();
                    cue.SetEvent(required);
                    _cues.Add(cue);
                }
            }
        }

        private static bool HasAnyClip(IReadOnlyList<AudioClip> clips)
        {
            if (clips == null)
                return false;

            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i] != null)
                    return true;
            }

            return false;
        }
    }
}
