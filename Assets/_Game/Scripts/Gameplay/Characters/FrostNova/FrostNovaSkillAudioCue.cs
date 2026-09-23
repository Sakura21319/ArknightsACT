using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.FrostNova
{
    /// <summary>
    /// FrostNova-only timed audio cues that cannot be represented by the generic
    /// slot-level PlayableOperatorAudioProfile. The generic audio controller owns
    /// cast-start SFX; this component owns the authored IceBurst impact/on phase.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FrostNovaSkill2))]
    public sealed class FrostNovaSkillAudioCue : MonoBehaviour
    {
        [SerializeField] private AudioClip iceBurstImpact;
        [SerializeField, Range(0f, 1f)] private float volume = 0.82f;
        [SerializeField] private bool enabledForCurrentSkin;

        private FrostNovaSkill2 _skill2;
        private AudioSource _source;

        public void Configure(AudioClip impactClip, bool enabledForSkin)
        {
            iceBurstImpact = impactClip;
            enabledForCurrentSkin = enabledForSkin;
        }

        private void Awake()
        {
            _skill2 = GetComponent<FrostNovaSkill2>();
            EnsureSource();
        }

        private void OnEnable()
        {
            _skill2 ??= GetComponent<FrostNovaSkill2>();
            if (_skill2 != null)
                _skill2.Pulse += OnSkill2Pulse;
        }

        private void OnDisable()
        {
            if (_skill2 != null)
                _skill2.Pulse -= OnSkill2Pulse;
        }

        private void OnSkill2Pulse(Vector3 _, int pulseIndex)
        {
            if (!enabledForCurrentSkin || pulseIndex != 0 || iceBurstImpact == null)
                return;

            EnsureSource();
            _source.PlayOneShot(iceBurstImpact, volume);
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
    }
}
