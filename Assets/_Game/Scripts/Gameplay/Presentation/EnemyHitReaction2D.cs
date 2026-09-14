using System.Collections;
using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Generic fallback hit reaction for enemies that do not ship a dedicated Hit animation.
    /// Works by punching the presentation child only; gameplay/physics roots stay untouched.
    /// Red tint is handled separately by DamageTintFlash2D so both player and enemies share
    /// the same Arknights-style damage readability.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class EnemyHitReaction2D : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float duration = 0.16f;
        [SerializeField, Min(0f)] private float kickDistance = 0.18f;
        [SerializeField, Min(0f)] private float squashAmount = 0.09f;
        [SerializeField, Min(0f)] private float tiltDegrees = 8f;

        private CombatEntity _entity;
        private Transform _presentationRoot;
        private Vector3 _baseLocalPosition;
        private Vector3 _baseLocalScale;
        private Quaternion _baseLocalRotation;
        private Coroutine _routine;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            ResolvePresentationRoot();
        }

        private void OnEnable()
        {
            if (_entity != null)
                _entity.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_entity != null)
                _entity.Damaged -= OnDamaged;
            ResetPose();
        }

        private void OnDamaged(DamageContext context, DamageResult _)
        {
            ResolvePresentationRoot();
            if (_presentationRoot == null)
                return;

            if (_routine != null)
                StopCoroutine(_routine);

            var sourceX = context.Source != null ? context.Source.transform.position.x : transform.position.x - 1f;
            var awaySign = transform.position.x >= sourceX ? 1f : -1f;
            _routine = StartCoroutine(ReactionRoutine(awaySign));
        }

        private IEnumerator ReactionRoutine(float awaySign)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var punch = Mathf.Sin(t * Mathf.PI);

                _presentationRoot.localPosition = _baseLocalPosition + new Vector3(awaySign * kickDistance * punch, 0.035f * punch, 0f);
                _presentationRoot.localRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, -awaySign * tiltDegrees * punch);
                _presentationRoot.localScale = new Vector3(
                    _baseLocalScale.x * (1f + squashAmount * punch),
                    _baseLocalScale.y * (1f - squashAmount * punch),
                    _baseLocalScale.z);
                yield return null;
            }

            ResetPose();
            _routine = null;
        }

        private void ResolvePresentationRoot()
        {
            if (_presentationRoot != null)
                return;

            var spine = GetComponentInChildren<SpineCharacterPresentation2D>(true);
            if (spine != null)
                _presentationRoot = spine.transform;
            else if (transform.childCount > 0)
                _presentationRoot = transform.GetChild(0);

            if (_presentationRoot == null)
                return;

            _baseLocalPosition = _presentationRoot.localPosition;
            _baseLocalScale = _presentationRoot.localScale;
            _baseLocalRotation = _presentationRoot.localRotation;
        }

        private void ResetPose()
        {
            if (_presentationRoot == null)
                return;
            _presentationRoot.localPosition = _baseLocalPosition;
            _presentationRoot.localScale = _baseLocalScale;
            _presentationRoot.localRotation = _baseLocalRotation;
        }

        private void OnDestroy()
        {
            ResetPose();
        }
    }
}
