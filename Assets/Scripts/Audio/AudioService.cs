using System.Collections.Generic;
using Sol.Settings;
using UnityEngine;

namespace Sol.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        private const string DefaultCueLibraryPath = "Audio/AudioCueLibrary";

        public static AudioService Instance { get; private set; }

        [Header("References")]
        [SerializeField] private AudioCueLibrary _cueLibrary;

        [Header("Defaults")]
        [SerializeField, Range(0f, 1f)] private float _defaultUiVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _defaultSfxVolume = 1f;

        [Header("Wave Proximity")]
        [SerializeField, Min(0f)] private float _waveNearDistance = 0f;
        [SerializeField, Min(0.1f)] private float _waveFarDistance = 80f;
        [SerializeField, Range(0f, 1f)] private float _waveFarVolumeScale = 0.2f;
        [SerializeField, Min(0.1f)] private float _waveProximityExponent = 1f;

        private readonly Dictionary<AudioEvent, float> _lastPlayedTimes = new();
        private readonly HashSet<AudioEvent> _missingCueWarnings = new();
        private AudioSource _uiSource;
        private AudioSource _reelLoopSource;
        private AudioSource _worldAmbientSource;
        private AudioSource _windLoopSource;
        private AudioSource _waveLoopSource;
        private AudioSource _underwaterLoopSource;
        private bool _isUnderwater;
        private bool _validatedCueLibrary;
        private Transform _cachedListenerTransform;
        private float _nextListenerRefreshTime;

        public void PlaySfx(AudioEvent audioEvent, Vector3 worldPos, float volumeScale = 1f)
        {
            if (!EnsureCueLibrary())
                return;

            if (audioEvent == AudioEvent.FishingReelLoopStart)
            {
                StartFishingReelLoop();
                return;
            }

            if (audioEvent == AudioEvent.FishingReelLoopStop)
            {
                StopFishingReelLoop();
                return;
            }

            if (!_cueLibrary.TryGetRandomClip(audioEvent, out AudioCueLibrary.AudioCue cue, out AudioClip clip))
            {
                WarnMissingCue(audioEvent);
                return;
            }

            if (IsOnCooldown(audioEvent, cue.CooldownSeconds))
                return;

            float finalVolume = ResolveSfxVolume() * _defaultSfxVolume * cue.BaseVolume * Random.Range(cue.VolumeRange.x, cue.VolumeRange.y) * Mathf.Max(0f, volumeScale);
            float pitch = Random.Range(cue.PitchRange.x, cue.PitchRange.y);

            if (finalVolume <= 0.0001f)
                return;

            if (cue.Spatialized)
                PlayWorldOneShot(clip, worldPos, finalVolume, pitch);
            else
                PlayUiOneShot(clip, finalVolume, pitch);

            _lastPlayedTimes[audioEvent] = Time.time;
        }

        public void PlayUi(AudioEvent audioEvent)
        {
            if (!EnsureCueLibrary())
                return;

            if (!_cueLibrary.TryGetRandomClip(audioEvent, out AudioCueLibrary.AudioCue cue, out AudioClip clip))
            {
                WarnMissingCue(audioEvent);
                return;
            }

            if (IsOnCooldown(audioEvent, cue.CooldownSeconds))
                return;

            float finalVolume = ResolveSfxVolume() * _defaultUiVolume * cue.BaseVolume * Random.Range(cue.VolumeRange.x, cue.VolumeRange.y);
            float pitch = Random.Range(cue.PitchRange.x, cue.PitchRange.y);
            if (finalVolume <= 0.0001f)
                return;

            PlayUiOneShot(clip, finalVolume, pitch);
            _lastPlayedTimes[audioEvent] = Time.time;
        }

        public void SetUnderwater(bool isUnderwater)
        {
            if (!EnsureCueLibrary() || _isUnderwater == isUnderwater)
                return;

            _isUnderwater = isUnderwater;
            if (_isUnderwater)
            {
                PlayUi(AudioEvent.UnderwaterEnter);
                TryStartLoop(_underwaterLoopSource, _cueLibrary.UnderwaterLoop);
            }
            else
            {
                PlayUi(AudioEvent.UnderwaterExit);
                StopLoop(_underwaterLoopSource);
            }

            RefreshLoopVolumes();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _uiSource = CreateChildSource("UI_OneShot", loop: false, spatialBlend: 0f);
            _reelLoopSource = CreateChildSource("Fishing_ReelLoop", loop: true, spatialBlend: 0f);
            _worldAmbientSource = CreateChildSource("Ambient_World", loop: true, spatialBlend: 0f);
            _windLoopSource = CreateChildSource("Ambient_Wind", loop: true, spatialBlend: 0f);
            _waveLoopSource = CreateChildSource("Ambient_Waves", loop: true, spatialBlend: 0f);
            _underwaterLoopSource = CreateChildSource("Ambient_Underwater", loop: true, spatialBlend: 0f);

            EnsureCueLibrary();
        }

        private void Start()
        {
            TryStartLoop(_worldAmbientSource, _cueLibrary != null ? _cueLibrary.WorldAmbientLoop : null);
            TryStartLoop(_windLoopSource, _cueLibrary != null ? _cueLibrary.WindLoop : null);
            TryStartLoop(_waveLoopSource, _cueLibrary != null ? _cueLibrary.WaveLoop : null);
            RefreshLoopVolumes();
        }

        private void Update()
        {
            RefreshLoopVolumes();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private bool EnsureCueLibrary()
        {
            if (_cueLibrary == null)
                _cueLibrary = Resources.Load<AudioCueLibrary>(DefaultCueLibraryPath);

            if (_cueLibrary == null)
                return false;

            if (!_validatedCueLibrary)
            {
                _validatedCueLibrary = true;
                if (!_cueLibrary.ValidateForRuntime(out string warning) && !string.IsNullOrWhiteSpace(warning))
                    Debug.LogWarning($"[AudioService] {warning}", this);
            }

            return true;
        }

        private bool IsOnCooldown(AudioEvent audioEvent, float cooldownSeconds)
        {
            float cooldown = Mathf.Max(0f, cooldownSeconds);
            if (audioEvent == AudioEvent.ItemPickup || audioEvent == AudioEvent.GoldWorldPickup || audioEvent == AudioEvent.GoldGained || audioEvent == AudioEvent.GoldSpent)
                cooldown = Mathf.Max(cooldown, 0.06f);

            if (cooldown <= 0f)
                return false;

            return _lastPlayedTimes.TryGetValue(audioEvent, out float lastTime)
                && Time.time < lastTime + cooldown;
        }

        private void WarnMissingCue(AudioEvent audioEvent)
        {
            if (_missingCueWarnings.Contains(audioEvent))
                return;

            _missingCueWarnings.Add(audioEvent);
            Debug.LogWarning($"[AudioService] No clip configured for {audioEvent}.", this);
        }

        private static void PlayWorldOneShot(AudioClip clip, Vector3 worldPos, float volume, float pitch)
        {
            if (clip == null || volume <= 0f)
                return;

            GameObject temp = new($"[AudioOneShot]_{clip.name}");
            temp.transform.position = worldPos;

            AudioSource source = temp.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.minDistance = 1f;
            source.maxDistance = 25f;
            source.volume = volume;
            source.pitch = pitch;
            source.clip = clip;
            source.Play();

            Destroy(temp, Mathf.Max(clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch)), 0.1f) + 0.1f);
        }

        private void PlayUiOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _uiSource == null || volume <= 0f)
                return;

            _uiSource.pitch = pitch;
            _uiSource.PlayOneShot(clip, volume);
        }

        private void StartFishingReelLoop()
        {
            if (_reelLoopSource == null || !EnsureCueLibrary())
                return;

            if (_reelLoopSource.isPlaying)
                return;

            if (!_cueLibrary.TryGetRandomClip(AudioEvent.FishingReelLoopStart, out _, out AudioClip clip))
            {
                WarnMissingCue(AudioEvent.FishingReelLoopStart);
                return;
            }

            _reelLoopSource.clip = clip;
            _reelLoopSource.pitch = 1f;
            _reelLoopSource.volume = ResolveSfxVolume();
            _reelLoopSource.Play();
        }

        private void StopFishingReelLoop()
        {
            if (_reelLoopSource != null && _reelLoopSource.isPlaying)
                _reelLoopSource.Stop();
        }

        private void TryStartLoop(AudioSource source, AudioCueLibrary.LoopCue loopCue)
        {
            if (source == null || loopCue == null)
                return;

            if (!_cueLibrary.TryGetRandomLoopClip(loopCue, out AudioClip clip, out float pitch) || clip == null)
                return;

            source.clip = clip;
            source.pitch = pitch;
            source.Play();
        }

        private static void StopLoop(AudioSource source)
        {
            if (source != null && source.isPlaying)
                source.Stop();
        }

        private void RefreshLoopVolumes()
        {
            float ambientVolume = ResolveAmbientVolume();
            float sfxVolume = ResolveSfxVolume();
            float duck = _isUnderwater && _cueLibrary != null ? _cueLibrary.UnderwaterAmbientDuck : 1f;

            if (_worldAmbientSource != null)
                _worldAmbientSource.volume = ambientVolume * (_cueLibrary != null ? _cueLibrary.WorldAmbientLoop.BaseVolume : 1f) * duck;

            if (_windLoopSource != null)
                _windLoopSource.volume = ambientVolume * (_cueLibrary != null ? _cueLibrary.WindLoop.BaseVolume : 1f) * duck;

            if (_waveLoopSource != null)
            {
                float waveProximityScale = ResolveWaveProximityScale();
                _waveLoopSource.volume = ambientVolume * (_cueLibrary != null ? _cueLibrary.WaveLoop.BaseVolume : 1f) * duck * waveProximityScale;
            }

            if (_underwaterLoopSource != null)
                _underwaterLoopSource.volume = ambientVolume * (_cueLibrary != null ? _cueLibrary.UnderwaterLoop.BaseVolume : 1f) * (_isUnderwater ? 1f : 0f);

            if (_reelLoopSource != null)
                _reelLoopSource.volume = sfxVolume;
        }

        private static float ResolveSfxVolume()
        {
            SettingsData data = SettingsPersistence.Current;
            return data == null ? 1f : Mathf.Clamp01(data.SFXVolume / 100f);
        }

        private static float ResolveAmbientVolume()
        {
            SettingsData data = SettingsPersistence.Current;
            return data == null ? 1f : Mathf.Clamp01(data.AmbientVolume / 100f);
        }

        private AudioSource CreateChildSource(string childName, bool loop, float spatialBlend)
        {
            GameObject child = new(childName);
            child.transform.SetParent(transform, false);

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            return source;
        }

        private float ResolveWaveProximityScale()
        {
            Vector3 listenerPosition = ResolveListenerPosition();
            float distance = WaterVolume.DistanceToNearestWaterXZ(listenerPosition);
            if (distance == float.MaxValue)
                return _waveFarVolumeScale;

            float nearDistance = Mathf.Max(0f, _waveNearDistance);
            float farDistance = Mathf.Max(nearDistance + 0.01f, _waveFarDistance);
            float t = Mathf.InverseLerp(farDistance, nearDistance, distance);
            t = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.1f, _waveProximityExponent));
            return Mathf.Lerp(_waveFarVolumeScale, 1f, t);
        }

        private Vector3 ResolveListenerPosition()
        {
            if (_cachedListenerTransform == null || Time.time >= _nextListenerRefreshTime)
            {
                _nextListenerRefreshTime = Time.time + 1f;

                AudioListener listener = FindFirstObjectByType<AudioListener>(FindObjectsInactive.Exclude);
                _cachedListenerTransform = listener != null ? listener.transform : null;

                if (_cachedListenerTransform == null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    _cachedListenerTransform = player != null ? player.transform : null;
                }
            }

            return _cachedListenerTransform != null ? _cachedListenerTransform.position : transform.position;
        }
    }
}
