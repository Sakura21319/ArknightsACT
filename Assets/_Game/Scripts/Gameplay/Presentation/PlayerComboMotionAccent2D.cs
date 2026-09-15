using System.Collections;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Presentation
{
    /// <summary>
    /// Adds readable ACT-style body accents on top of Texas' single Attack_Loop Spine clip.
    /// The presentation root moves/leans only; the gameplay Rigidbody/collider never moves.
    /// </summary>
    [RequireComponent(typeof(PlayerAttackController), typeof(PlayerMotor2D), typeof(Rigidbody2D))]
    public sealed class PlayerComboMotionAccent2D : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float lightAccentSeconds = 0.16f;
        [SerializeField, Min(0.02f)] private float secondAccentSeconds = 0.19f;
        [SerializeField, Min(0.04f)] private float heavyAccentSeconds = 0.31f;
        [SerializeField, Min(0.04f)] private float dashSlashAccentSeconds = 0.24f;
        [SerializeField, Min(0f)] private float cancelMoveThreshold = 0.10f;

        private PlayerAttackController _attack;
        private PlayerMotor2D _motor;
        private Rigidbody2D _body;
        private SpineCharacterPresentation2D _presentation;
        private Transform _visual;
        private Coroutine _routine;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private bool _hasBasePose;

        private void Awake()
        {
            _attack = GetComponent<PlayerAttackController>();
            _motor = GetComponent<PlayerMotor2D>();
            _body = GetComponent<Rigidbody2D>();
            ResolvePresentation();
        }

        private void OnEnable()
        {
            if (_attack != null)
                _attack.AttackStarted += OnAttackStarted;
        }

        private void OnDisable()
        {
            if (_attack != null)
                _attack.AttackStarted -= OnAttackStarted;
            StopAccent(true);
        }

        private void Update()
        {
            if (_routine == null || _attack == null)
                return;

            if (!_attack.IsAttacking ||
                (!_attack.IsMovementLocked && _body != null && Mathf.Abs(_body.linearVelocity.x) > cancelMoveThreshold))
            {
                StopAccent(true);
            }
        }

        private void OnAttackStarted(int comboIndex)
        {
            ResolvePresentation();
            if (_visual == null || _motor == null)
                return;

            StopAccent(true);
            _baseLocalPosition = _visual.localPosition;
            _baseLocalRotation = _visual.localRotation;
            _hasBasePose = true;
            _routine = StartCoroutine(AccentRoutine(comboIndex, _motor.FacingSign));
        }

        private IEnumerator AccentRoutine(int comboIndex, int facing)
        {
            var direction = facing < 0 ? -1f : 1f;

            if (comboIndex == 3)
            {
                // Post-dash slash: very small recoil, then a long forward body cut.
                yield return AnimateSegment(
                    dashSlashAccentSeconds * 0.24f,
                    new Vector3(-0.055f * direction, 0.015f, 0f),
                    4f * direction);
                yield return AnimateSegment(
                    dashSlashAccentSeconds * 0.42f,
                    new Vector3(0.31f * direction, -0.015f, 0f),
                    -9f * direction);
                yield return AnimateSegment(
                    dashSlashAccentSeconds * 0.34f,
                    Vector3.zero,
                    0f);
            }
            else
            {
                switch (Mathf.Abs(comboIndex) % 3)
                {
                    case 0:
                        yield return AnimateSegment(
                            lightAccentSeconds * 0.42f,
                            new Vector3(0.085f * direction, 0.015f, 0f),
                            -3.5f * direction);
                        yield return AnimateSegment(
                            lightAccentSeconds * 0.58f,
                            Vector3.zero,
                            0f);
                        break;

                    case 1:
                        yield return AnimateSegment(
                            secondAccentSeconds * 0.38f,
                            new Vector3(-0.035f * direction, 0.025f, 0f),
                            4.5f * direction);
                        yield return AnimateSegment(
                            secondAccentSeconds * 0.30f,
                            new Vector3(0.13f * direction, -0.01f, 0f),
                            -6.5f * direction);
                        yield return AnimateSegment(
                            secondAccentSeconds * 0.32f,
                            Vector3.zero,
                            0f);
                        break;

                    default:
                        yield return AnimateSegment(
                            heavyAccentSeconds * 0.30f,
                            new Vector3(-0.10f * direction, 0.035f, 0f),
                            7.5f * direction);
                        yield return AnimateSegment(
                            heavyAccentSeconds * 0.32f,
                            new Vector3(0.24f * direction, -0.025f, 0f),
                            -12f * direction);
                        yield return AnimateSegment(
                            heavyAccentSeconds * 0.38f,
                            Vector3.zero,
                            0f);
                        break;
                }
            }

            RestoreBasePose();
            _routine = null;
        }

        private IEnumerator AnimateSegment(float duration, Vector3 targetOffset, float targetAngle)
        {
            if (_visual == null || !_hasBasePose)
                yield break;

            duration = Mathf.Max(0.01f, duration);
            var startPosition = _visual.localPosition;
            var startRotation = _visual.localRotation;
            var targetPosition = _baseLocalPosition + targetOffset;
            var targetRotation = _baseLocalRotation * Quaternion.Euler(0f, 0f, targetAngle);
            var elapsed = 0f;

            while (elapsed < duration && _visual != null)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                _visual.localPosition = Vector3.LerpUnclamped(startPosition, targetPosition, t);
                _visual.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, t);
                yield return null;
            }
        }

        private void ResolvePresentation()
        {
            if (_presentation != null && _presentation.enabled)
            {
                _visual = _presentation.transform;
                return;
            }

            var presentations = GetComponentsInChildren<SpineCharacterPresentation2D>(true);
            for (var i = 0; i < presentations.Length; i++)
            {
                if (presentations[i] == null || !presentations[i].enabled)
                    continue;
                _presentation = presentations[i];
                _visual = _presentation.transform;
                return;
            }

            _presentation = null;
            _visual = null;
        }

        private void StopAccent(bool restore)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (restore)
                RestoreBasePose();
        }

        private void RestoreBasePose()
        {
            if (!_hasBasePose || _visual == null)
                return;

            _visual.localPosition = _baseLocalPosition;
            _visual.localRotation = _baseLocalRotation;
            _hasBasePose = false;
        }
    }
}
