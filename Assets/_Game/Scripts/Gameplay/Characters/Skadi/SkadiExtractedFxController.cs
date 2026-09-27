using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Skadi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAttackController))]
    public sealed class SkadiExtractedFxController : MonoBehaviour
    {
        [Header("Basic attack")]
        [SerializeField] private GameObject basicStart;
        [SerializeField] private GameObject basicStartSecondary;
        [SerializeField] private GameObject basicHit;

        [Header("Skills")]
        [SerializeField] private GameObject skill2Start;
        [SerializeField] private GameObject skill2StartSecondary;
        [SerializeField] private GameObject skill3Buff;

        [Header("Placement")]
        [SerializeField] private Vector3 actorLocalOffset = new(0f, 0.78f, 0f);
        [SerializeField] private Vector3 targetLocalOffset = new(0f, 0.75f, 0f);
        [SerializeField, Min(0.1f)] private float scale = 3f;
        [SerializeField, Min(0.1f)] private float fallbackLifetime = 2.5f;

        private readonly List<GameObject> _active = new();
        private PlayerAttackController _attacks;
        private PlayerMotor25D _motor;
        private SkadiSkill1 _skill1;
        private SkadiSkill2 _skill2;
        private GameObject _persistentSkill3Buff;
        private Coroutine _persistentSkill3Loop;

        public void Configure(
            GameObject start,
            GameObject startSecondary,
            GameObject hit,
            GameObject passiveSkillStart,
            GameObject passiveSkillStartSecondary,
            GameObject activeSkillBuff)
        {
            basicStart = start;
            basicStartSecondary = startSecondary;
            basicHit = hit;
            skill2Start = passiveSkillStart;
            skill2StartSecondary = passiveSkillStartSecondary;
            skill3Buff = activeSkillBuff;
        }

        private void Awake()
        {
            _attacks = GetComponent<PlayerAttackController>();
            _motor = GetComponent<PlayerMotor25D>();
            _skill1 = GetComponent<SkadiSkill1>();
            _skill2 = GetComponent<SkadiSkill2>();
        }

        private void OnEnable()
        {
            if (_attacks == null)
                _attacks = GetComponent<PlayerAttackController>();
            if (_attacks == null)
                return;

            _attacks.AttackStarted += OnAttackStarted;
            _attacks.AttackHit += OnAttackHit;
            if (_skill1 != null)
                _skill1.BuffStarted += OnSkill1BuffStarted;
            if (_skill2 != null)
            {
                _skill2.BuffStarted += OnSkill2BuffStarted;
                _skill2.BuffEnded += OnSkill2BuffEnded;
            }
        }

        private void OnDisable()
        {
            if (_attacks != null)
            {
                _attacks.AttackStarted -= OnAttackStarted;
                _attacks.AttackHit -= OnAttackHit;
            }
            if (_skill1 != null)
                _skill1.BuffStarted -= OnSkill1BuffStarted;
            if (_skill2 != null)
            {
                _skill2.BuffStarted -= OnSkill2BuffStarted;
                _skill2.BuffEnded -= OnSkill2BuffEnded;
            }

            DestroyPersistentSkill3Buff();

            for (var i = 0; i < _active.Count; i++)
                if (_active[i] != null)
                    Destroy(_active[i]);
            _active.Clear();
        }

        private void OnAttackStarted(int _)
        {
            Spawn(basicStart, transform, actorLocalOffset, mirrorToFacing: true);
            Spawn(basicStartSecondary, transform, actorLocalOffset, mirrorToFacing: true);
        }

        private void OnAttackHit(CombatEntity target)
        {
            if (target == null)
                return;
            Spawn(basicHit, target.transform, targetLocalOffset, mirrorToFacing: true);
        }

        private void OnSkill1BuffStarted()
        {
            Spawn(skill2Start, transform, actorLocalOffset, mirrorToFacing: true);
            Spawn(skill2StartSecondary, transform, actorLocalOffset, mirrorToFacing: true);
        }

        private void OnSkill2BuffStarted()
        {
            DestroyPersistentSkill3Buff();
            if (skill3Buff == null)
                return;

            _persistentSkill3Buff = Instantiate(skill3Buff, transform, false);
            ApplyAttachedTransform(_persistentSkill3Buff, actorLocalOffset, mirrorToFacing: true);

            var playback = _persistentSkill3Buff.GetComponent<ExtractedFrameFxPlayback>();
            if (playback == null)
                return;

            playback.PlayFromStart();
            if (!playback.Loop)
                _persistentSkill3Loop = StartCoroutine(LoopPersistentSkill3Buff(playback));
        }

        private void OnSkill2BuffEnded()
        {
            DestroyPersistentSkill3Buff();
        }

        private void Spawn(
            GameObject prefab,
            Transform anchor,
            Vector3 localOffset,
            bool mirrorToFacing)
        {
            if (prefab == null || anchor == null)
                return;

            var instance = Instantiate(prefab, anchor, false);
            ApplyAttachedTransform(instance, localOffset, mirrorToFacing);
            _active.Add(instance);

            var playback = instance.GetComponent<ExtractedFrameFxPlayback>();
            if (playback != null)
            {
                playback.SetPlaybackSpeed(ExtractedFrameFxPlayback.BasicAttackPlaybackSpeed);
                playback.PlayFromStart();
                if (!playback.Loop)
                    StartCoroutine(RecycleAfter(instance, playback.EffectiveDuration));
                return;
            }

            StartCoroutine(RecycleAfter(instance, fallbackLifetime));
        }

        private void ApplyAttachedTransform(
            GameObject instance,
            Vector3 localOffset,
            bool mirrorToFacing)
        {
            if (instance == null)
                return;

            var facing = mirrorToFacing ? GetFacingSign() : 1f;
            localOffset.x *= facing;
            instance.transform.localPosition = localOffset;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = new Vector3(scale * facing, scale, 1f);
        }

        private IEnumerator LoopPersistentSkill3Buff(ExtractedFrameFxPlayback playback)
        {
            while (_persistentSkill3Buff != null &&
                   playback != null &&
                   _skill2 != null &&
                   _skill2.IsActive)
            {
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(0.05f, playback.EffectiveDuration));

                if (_persistentSkill3Buff == null ||
                    playback == null ||
                    _skill2 == null ||
                    !_skill2.IsActive)
                    break;

                playback.PlayFromStart();
            }

            _persistentSkill3Loop = null;
        }

        private void DestroyPersistentSkill3Buff()
        {
            if (_persistentSkill3Loop != null)
                StopCoroutine(_persistentSkill3Loop);
            _persistentSkill3Loop = null;

            if (_persistentSkill3Buff == null)
                return;

            Destroy(_persistentSkill3Buff);
            _persistentSkill3Buff = null;
        }

        private IEnumerator RecycleAfter(GameObject instance, float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, seconds));
            _active.Remove(instance);
            if (instance != null)
                Destroy(instance);
        }

        private int GetFacingSign()
        {
            if (_motor == null)
                _motor = GetComponent<PlayerMotor25D>();
            return _motor != null && _motor.FacingSign < 0 ? -1 : 1;
        }
    }
}
