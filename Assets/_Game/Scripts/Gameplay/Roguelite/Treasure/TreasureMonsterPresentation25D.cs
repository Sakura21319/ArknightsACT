using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Presentation;
using UnityEngine;

namespace ArknightsACT.Gameplay.Roguelite.Treasure
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TreasureMonsterBrain25D), typeof(CombatEntity))]
    public sealed class TreasureMonsterPresentation25D : MonoBehaviour
    {
        [SerializeField] private GameObject dormantChestVisual;
        [SerializeField] private GameObject monsterVisual;

        private TreasureMonsterBrain25D _brain;
        private CombatEntity _entity;
        private SpineCharacterPresentation2D _monsterPresentation;
        private BillboardPresentation25D _billboard;
        private bool _dead;

        public void Configure(GameObject dormantVisual, GameObject activeMonsterVisual)
        {
            dormantChestVisual = dormantVisual;
            monsterVisual = activeMonsterVisual;
            CacheMonsterPresentation();
            ApplyVisualState();
        }

        private void Awake()
        {
            _brain = GetComponent<TreasureMonsterBrain25D>();
            _entity = GetComponent<CombatEntity>();
            _billboard = GetComponentInChildren<BillboardPresentation25D>(true);
            CacheMonsterPresentation();
            ApplyVisualState();
        }

        private void OnEnable()
        {
            if (_brain != null)
            {
                _brain.Activated += OnActivated;
                _brain.AttackStarted += OnAttackStarted;
            }
            if (_entity != null)
            {
                _entity.Damaged += OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            if (_brain != null)
            {
                _brain.Activated -= OnActivated;
                _brain.AttackStarted -= OnAttackStarted;
            }
            if (_entity != null)
            {
                _entity.Damaged -= OnDamaged;
                if (_entity.Health != null)
                    _entity.Health.Died -= OnDied;
            }
        }

        private void Start()
        {
            ApplyVisualState();
            if (_brain != null && _brain.IsActivated)
                _monsterPresentation?.SetLocomotion(false, _brain.FacingSign);
        }

        private void Update()
        {
            if (_dead || _brain == null || !_brain.IsActivated)
                return;

            _billboard?.SetPlanarDirection(_brain.LogicForward, _brain.FacingSign, _brain.IsAttacking ? 1f : 0.7f);
            if (!_brain.IsAttacking)
                _monsterPresentation?.SetLocomotion(_brain.IsMoving, _brain.FacingSign);
        }

        private void OnActivated()
        {
            ApplyVisualState();
            CacheMonsterPresentation();
            if (_brain != null)
            {
                _billboard?.SetPlanarDirection(_brain.LogicForward, _brain.FacingSign, 0.85f);
                _monsterPresentation?.SetLocomotion(false, _brain.FacingSign);
            }
        }

        private void OnAttackStarted(int facing)
        {
            if (!_dead && _brain != null && _brain.IsActivated)
                _monsterPresentation?.PlayAttack(0, facing);
        }

        private void OnDamaged(DamageContext _, DamageResult __)
        {
            if (!_dead && _brain != null && _brain.IsActivated)
                _monsterPresentation?.PlayHit();
        }

        private void OnDied()
        {
            _dead = true;
            if (_brain != null && _brain.IsActivated)
                _monsterPresentation?.PlayDie();
        }

        private void ApplyVisualState()
        {
            var active = _brain != null && _brain.IsActivated;
            if (dormantChestVisual != null)
                dormantChestVisual.SetActive(!active && !_dead);
            if (monsterVisual != null)
                monsterVisual.SetActive(active && !_dead);
        }

        private void CacheMonsterPresentation()
        {
            if (monsterVisual == null)
                return;
            _monsterPresentation = monsterVisual.GetComponentInChildren<SpineCharacterPresentation2D>(true);
        }
    }
}
