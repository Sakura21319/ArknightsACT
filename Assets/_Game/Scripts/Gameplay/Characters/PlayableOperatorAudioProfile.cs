using UnityEngine;

namespace ArknightsACT.Gameplay.Characters
{
    public interface IPlayerSpecialAttackAudioState
    {
        bool UseSpecialAttackAudio { get; }
    }

    /// <summary>
    /// Character-owned audio data consumed by the generic roguelite audio runtime.
    /// Adding an operator should populate this profile instead of branching in stage/audio systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayableOperatorAudioProfile : MonoBehaviour
    {
        [SerializeField] private AudioClip skill1Sfx;
        [SerializeField] private AudioClip skill2Sfx;
        [SerializeField] private AudioClip skill1LayerSfx;
        [SerializeField] private AudioClip skill2LayerSfx;
        [SerializeField] private AudioClip[] skill1Voices;
        [SerializeField] private AudioClip[] skill2Voices;
        [SerializeField] private AudioClip[] basicAttackSwings;
        [SerializeField] private AudioClip basicAttackImpact;
        [SerializeField] private string manualAttackSourceId;
        [SerializeField] private AudioClip manualAttackSfx;
        [SerializeField] private AudioClip manualAttackImpact;
        [SerializeField] private bool skillSfxOptional;

        public AudioClip Skill1Sfx => skill1Sfx;
        public AudioClip Skill2Sfx => skill2Sfx;
        public AudioClip Skill1LayerSfx => skill1LayerSfx;
        public AudioClip Skill2LayerSfx => skill2LayerSfx;
        public AudioClip[] Skill1Voices => skill1Voices;
        public AudioClip[] Skill2Voices => skill2Voices;
        public AudioClip[] BasicAttackSwings => basicAttackSwings;
        public AudioClip BasicAttackImpact => basicAttackImpact;
        public string ManualAttackSourceId => manualAttackSourceId;
        public AudioClip ManualAttackSfx => manualAttackSfx;
        public AudioClip ManualAttackImpact => manualAttackImpact;
        public bool SkillSfxOptional => skillSfxOptional;

        public void Configure(
            AudioClip slot1Sfx,
            AudioClip slot2Sfx,
            AudioClip[] slot1Voices,
            AudioClip[] slot2Voices,
            AudioClip[] attackSwings,
            AudioClip attackImpact,
            bool optionalSkillSfx = false,
            string specialManualAttackSourceId = null,
            AudioClip specialManualAttackSfx = null,
            AudioClip specialManualAttackImpact = null,
            AudioClip slot1LayerSfx = null,
            AudioClip slot2LayerSfx = null)
        {
            skill1Sfx = slot1Sfx;
            skill2Sfx = slot2Sfx;
            skill1LayerSfx = slot1LayerSfx;
            skill2LayerSfx = slot2LayerSfx;
            skill1Voices = slot1Voices;
            skill2Voices = slot2Voices;
            basicAttackSwings = attackSwings;
            basicAttackImpact = attackImpact;
            manualAttackSourceId = specialManualAttackSourceId ?? string.Empty;
            manualAttackSfx = specialManualAttackSfx;
            manualAttackImpact = specialManualAttackImpact;
            skillSfxOptional = optionalSkillSfx;
        }
    }
}
