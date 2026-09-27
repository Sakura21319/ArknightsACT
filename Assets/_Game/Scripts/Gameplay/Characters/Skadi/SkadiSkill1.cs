using System;
using System.Collections;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Skadi
{
    /// <summary>
    /// Gameplay slot 1 maps to Skadi's original S2, 跃浪击.
    /// It is a deployment passive: the official mastery snapshot arms one timed ATK buff
    /// automatically once per run/deployment instead of behaving like a press-to-cast skill.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatEntity))]
    public sealed class SkadiSkill1 :
        MonoBehaviour,
        IPlayerSkill,
        IPlayerSkillLifecycleState,
        IOperatorSkillMasteryTarget,
        ILayeredCombatStatModifier,
        IPlayerRunResettable
    {
        [SerializeField] private string displayName = "跃浪击";
        [SerializeField, Min(0f)] private float officialAttackBonus;
        [SerializeField, Min(0f)] private float deploymentDuration;

        private Coroutine _routine;
        private float _activeUntil;
        private bool _deploymentBuffConsumed;
        private bool _officialSkillDataApplied;

        public int Slot => 1;
        public int MasterySlot => 1;
        public string DisplayName => displayName;
        public float SkillPointCost => 0f;
        public float SkillPoints => 0f;
        public float SkillPointRatio => 0f;
        public float NaturalSkillPointPerSecond => 0f;
        public float CooldownRemaining => 0f;
        public bool IsCasting => false;
        public bool IsActive { get; private set; }
        public bool HasOfficialSkillData => _officialSkillDataApplied;
        public float OfficialAttackBonus => Mathf.Max(0f, officialAttackBonus);
        public PlayerSkillLifecycleType LifecycleType => PlayerSkillLifecycleType.Duration;
        public float ActiveDurationSeconds => Mathf.Max(0f, deploymentDuration);
        public float ActiveSecondsRemaining =>
            IsActive ? Mathf.Max(0f, _activeUntil - Time.time) : 0f;
        public int AmmoRemaining => 0;
        public int AmmoCapacity => 0;
        public CombatStatModifierLayer ModifierLayer => CombatStatModifierLayer.Temporary;

        public event Action BuffStarted;
        public event Action BuffEnded;

        public void AccumulateStatModifiers(
            CombatStatType stat,
            ref float flat,
            ref float additivePercent)
        {
            if (!IsActive)
                return;

            if (stat == CombatStatType.Attack)
                additivePercent += Mathf.Max(0f, officialAttackBonus);
        }

        public void ApplyMasterySnapshot(OperatorSkillMasterySnapshot snapshot)
        {
            _officialSkillDataApplied = false;
            if (snapshot == null)
                return;

            if (!string.IsNullOrWhiteSpace(snapshot.DisplayName))
                displayName = snapshot.DisplayName;

            var hasAttack = snapshot.TryGetBlackboard("atk", out var attackBonus);
            var hasDuration = snapshot.TryGetBlackboard("duration", out var duration);
            officialAttackBonus = hasAttack ? Mathf.Max(0f, attackBonus) : 0f;
            deploymentDuration = hasDuration ? Mathf.Max(0f, duration) : 0f;

            _officialSkillDataApplied =
                hasAttack &&
                hasDuration &&
                officialAttackBonus > 0f &&
                deploymentDuration > 0f;

            if (!_officialSkillDataApplied)
            {
                if (IsActive)
                    EndBuff();
                return;
            }

            TryBeginDeploymentBuff();
        }

        public bool TryCast() => false;

        public void TickSkillPoints(
            float deltaTime,
            float recoveryMultiplier,
            float flatRecoveryPerSecond)
        {
        }

        public void GainSkillPoints(float amount)
        {
        }

        public void SetSkillPoints(float amount)
        {
        }

        public void ReduceCooldown(float seconds)
        {
        }

        public void ResetForNewRun()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;

            var wasActive = IsActive;
            IsActive = false;
            _activeUntil = 0f;
            _deploymentBuffConsumed = false;

            if (wasActive)
                BuffEnded?.Invoke();

            TryBeginDeploymentBuff();
        }

        private void OnEnable()
        {
            if (IsActive)
            {
                if (Time.time >= _activeUntil)
                {
                    EndBuff();
                    return;
                }

                if (_routine == null)
                    _routine = StartCoroutine(WaitForBuffEnd());
                return;
            }

            TryBeginDeploymentBuff();
        }

        private void OnDisable()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            // Test-only reserve switching may disable the object. Keep deployment-consumed
            // state and absolute end time so the passive cannot be retriggered by re-enable.
        }

        private void TryBeginDeploymentBuff()
        {
            if (!Application.isPlaying ||
                !_officialSkillDataApplied ||
                _deploymentBuffConsumed ||
                IsActive ||
                !isActiveAndEnabled)
                return;

            _deploymentBuffConsumed = true;
            IsActive = true;
            _activeUntil = Time.time + ActiveDurationSeconds;
            BuffStarted?.Invoke();

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(WaitForBuffEnd());
        }

        private IEnumerator WaitForBuffEnd()
        {
            while (IsActive && Time.time < _activeUntil)
                yield return null;

            if (IsActive)
                EndBuff();
        }

        private void EndBuff()
        {
            var wasActive = IsActive;
            IsActive = false;
            _activeUntil = 0f;
            _routine = null;
            if (wasActive)
                BuffEnded?.Invoke();
        }
    }
}
