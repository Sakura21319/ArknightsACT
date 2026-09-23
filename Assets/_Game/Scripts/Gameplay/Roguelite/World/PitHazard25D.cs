using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.World
{
    /// <summary>
    /// ACT adaptation of tile_hole. Enemies that enter are defeated immediately; the player
    /// loses a fraction of max HP and is reset to the block safe point. The shallow trigger can
    /// still be cleared by a deliberate jump.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PitHazard25D : MonoBehaviour
    {
        [SerializeField] private Vector3 playerResetPosition;
        [SerializeField, Range(0.05f, 0.80f)] private float playerDamageFraction = 0.20f;
        [SerializeField, Min(0.1f)] private float retriggerCooldown = 0.80f;

        private readonly Dictionary<CombatEntity, float> _cooldowns = new();

        public void Configure(Vector3 resetPosition, float damageFraction = 0.20f)
        {
            playerResetPosition = resetPosition;
            playerDamageFraction = Mathf.Clamp(damageFraction, 0.05f, 0.80f);
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void Awake()
        {
            var trigger = GetComponent<BoxCollider>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryProcess(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // CharacterController motion can begin overlapping a trigger between physics steps.
            // Staying in the hole must still count as falling instead of requiring a clean Enter.
            TryProcess(other);
        }

        private void TryProcess(Collider other)
        {
            var entity = other.GetComponentInParent<CombatEntity>();
            if (entity == null || entity.Health == null || entity.Health.IsDead)
                return;
            if (_cooldowns.TryGetValue(entity, out var availableAt) && Time.time < availableAt)
                return;
            _cooldowns[entity] = Time.time + retriggerCooldown;

            if (entity.Team == Team.Player)
            {
                DamageSystem.Apply(new DamageContext(
                    null,
                    null,
                    entity,
                    entity.Health.MaxHealth * playerDamageFraction,
                    DamageType.True,
                    Vector2.zero,
                    sourceId: "Environment_Pit", tags: DamageTags.Environment));
                if (entity.Health != null && !entity.Health.IsDead)
                    ResetActor(entity.transform);
                return;
            }

            if (entity.Team == Team.Enemy)
            {
                DamageSystem.Apply(new DamageContext(
                    null,
                    null,
                    entity,
                    entity.Health.CurrentHealth + entity.Health.MaxHealth,
                    DamageType.True,
                    Vector2.zero,
                    sourceId: "Environment_Pit", tags: DamageTags.Environment));
            }
        }

        private void ResetActor(Transform actor)
        {
            if (actor == null)
                return;
            var controller = actor.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            actor.position = playerResetPosition;
            if (controller != null)
                controller.enabled = true;
            actor.GetComponent<PlayerMotor25D>()?.ResetMotion();
        }
    }
}
