using ArknightsACT.Gameplay.Characters;
using UnityEngine;

namespace ArknightsACT.Gameplay.Characters.Skadi
{
    /// <summary>
    /// Skadi S2 is a deployment passive, so the generic skill-cast audio hook never fires for it.
    /// This component only owns that passive activation cue. S3 remains on the generic slot-2 audio path.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkadiSkill1))]
    public sealed class SkadiSkillAudioCue : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.95f;

        private SkadiSkill1 _skill1;
        private AudioSource _source;

        private void Awake()
        {
            _skill1 = GetComponent<SkadiSkill1>();
            EnsureSource();
        }

        private void OnEnable()
        {
            _skill1 ??= GetComponent<SkadiSkill1>();
            if (_skill1 != null)
                _skill1.BuffStarted += OnPassiveBuffStarted;
        }

        private void OnDisable()
        {
            if (_skill1 != null)
                _skill1.BuffStarted -= OnPassiveBuffStarted;
        }

        private void OnPassiveBuffStarted()
        {
            var profile = GetComponent<PlayableOperatorAudioProfile>();
            if (profile == null)
                return;

            EnsureSource();
            if (profile.Skill1Sfx != null)
                _source.PlayOneShot(profile.Skill1Sfx, sfxVolume);

            var voices = profile.Skill1Voices;
            if (voices == null || voices.Length == 0)
                return;

            var available = 0;
            for (var i = 0; i < voices.Length; i++)
                if (voices[i] != null)
                    available++;
            if (available == 0)
                return;

            var pick = Random.Range(0, available);
            for (var i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null)
                    continue;
                if (pick-- != 0)
                    continue;

                _source.PlayOneShot(voices[i], voiceVolume);
                break;
            }
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
