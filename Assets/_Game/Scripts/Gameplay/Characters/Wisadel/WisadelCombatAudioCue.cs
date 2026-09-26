using ArknightsACT.Combat;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Wisadel
{
    /// <summary>
    /// Character-specific battle audio events that the generic operator profile cannot express:
    /// S2 auto-projectile impacts and S3 ranged-shot impacts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WisadelCombatAudioCue : MonoBehaviour
    {
        [SerializeField] private AudioClip skill2Impact;
        [SerializeField] private AudioClip skill3Impact;
        [SerializeField, Range(0f, 1f)] private float volume = 0.9f;

        private AudioSource _source;
        private WisadelSkill _skill2;
        private WisadelRangedBasicAttack _rangedAttack;

        public void Configure(AudioClip skill2ImpactClip, AudioClip skill3ImpactClip)
        {
            skill2Impact = skill2ImpactClip;
            skill3Impact = skill3ImpactClip;
        }

        private void Awake()
        {
            Resolve();
            EnsureSource();
        }

        private void OnEnable()
        {
            Resolve();
            EnsureSource();

            if (_skill2 != null)
                _skill2.AutoTargetImpact += OnAutoTargetImpact;
            if (_rangedAttack != null)
                _rangedAttack.ShotHitResolved += OnShotHitResolved;
        }

        private void OnDisable()
        {
            if (_skill2 != null)
                _skill2.AutoTargetImpact -= OnAutoTargetImpact;
            if (_rangedAttack != null)
                _rangedAttack.ShotHitResolved -= OnShotHitResolved;
        }

        private void Resolve()
        {
            var skills = GetComponents<WisadelSkill>();
            _skill2 = null;
            for (var i = 0; i < skills.Length; i++)
            {
                if (skills[i] != null && skills[i].Slot == 1)
                {
                    _skill2 = skills[i];
                    break;
                }
            }

            _rangedAttack = GetComponent<WisadelRangedBasicAttack>();
        }

        private void EnsureSource()
        {
            if (_source != null)
                return;

            _source = GetComponent<AudioSource>();
            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.priority = 64;
        }

        private void OnAutoTargetImpact(int slot, CombatEntity target)
        {
            if (slot == 1)
                Play(skill2Impact);
        }

        private void OnShotHitResolved(CombatEntity target, bool wasSkill3)
        {
            if (wasSkill3)
                Play(skill3Impact);
        }

        private void Play(AudioClip clip)
        {
            if (clip == null)
                return;
            EnsureSource();
            _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
