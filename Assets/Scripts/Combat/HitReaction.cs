using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sol.Combat
{
    [AddComponentMenu("Sol/Combat/Hit Reaction")]
    public class HitReaction : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _hitClip;
        [SerializeField] private Transform _fallbackRoot;
        [SerializeField] private float _fallbackDuration = 0.18f;
        [SerializeField] private float _fallbackLeanDegrees = 8f;
        [SerializeField] private float _fallbackSettleSpeed = 18f;

        private PlayableGraph _graph;
        private Coroutine _stopClipRoutine;
        private Coroutine _fallbackRoutine;
        private float _reactionUntil;

        public bool IsReacting => Time.time < _reactionUntil
            || _graph.IsValid()
            || _stopClipRoutine != null
            || _fallbackRoutine != null;

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();

            if (_fallbackRoot == null)
                _fallbackRoot = transform;
        }

        private void OnDisable()
        {
            StopActiveReaction();
        }

        private void OnDestroy()
        {
            StopActiveReaction();
        }

        public void PlayHitReaction(Vector3 hitDirection)
        {
            if (_hitClip != null && _animator != null && _animator.enabled && _animator.isActiveAndEnabled)
            {
                PlayClip();
                return;
            }

            PlayFallback(hitDirection);
        }

        private void PlayClip()
        {
            StopActiveReaction();
            _reactionUntil = Time.time + Mathf.Max(0.01f, _hitClip.length);

            _graph = PlayableGraph.Create($"{name}_HitReaction");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "HitReaction", _animator);
            AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(_graph, _hitClip);
            output.SetSourcePlayable(clipPlayable);

            _graph.Play();
            _stopClipRoutine = StartCoroutine(StopClipAfter(_hitClip.length));
        }

        private IEnumerator StopClipAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));

            if (_graph.IsValid())
                _graph.Destroy();

            _stopClipRoutine = null;
        }

        private void PlayFallback(Vector3 hitDirection)
        {
            if (_fallbackRoot == null)
                return;

            if (_fallbackRoutine != null)
                StopCoroutine(_fallbackRoutine);

            _reactionUntil = Time.time + Mathf.Max(0.01f, _fallbackDuration);
            _fallbackRoutine = StartCoroutine(FallbackFlinch(hitDirection));
        }

        private IEnumerator FallbackFlinch(Vector3 hitDirection)
        {
            Quaternion startRotation = _fallbackRoot.localRotation;
            Vector3 localHit = _fallbackRoot.InverseTransformDirection(hitDirection);
            float leanSign = localHit.x >= 0f ? -1f : 1f;
            Quaternion targetRotation = startRotation * Quaternion.Euler(0f, 0f, leanSign * _fallbackLeanDegrees);

            float elapsed = 0f;
            while (elapsed < _fallbackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _fallbackDuration));
                _fallbackRoot.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            while (Quaternion.Angle(_fallbackRoot.localRotation, startRotation) > 0.1f)
            {
                _fallbackRoot.localRotation = Quaternion.Slerp(
                    _fallbackRoot.localRotation,
                    startRotation,
                    _fallbackSettleSpeed * Time.deltaTime);
                yield return null;
            }

            _fallbackRoot.localRotation = startRotation;
            _fallbackRoutine = null;
        }

        private void StopActiveReaction()
        {
            if (_stopClipRoutine != null)
            {
                StopCoroutine(_stopClipRoutine);
                _stopClipRoutine = null;
            }

            if (_fallbackRoutine != null)
            {
                StopCoroutine(_fallbackRoutine);
                _fallbackRoutine = null;
            }

            if (_graph.IsValid())
                _graph.Destroy();

            _reactionUntil = 0f;
        }
    }
}
