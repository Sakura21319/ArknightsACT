using System.Collections;
using ArknightsACT.Gameplay.Abilities;
using UnityEngine;

namespace ArknightsACT.Gameplay.Audio
{
    /// <summary>
    /// Lightweight prototype audio layer. It owns the Chernobog intro/loop BGM pair and reacts only
    /// to successful player-skill casts, so failed/cooldown inputs never trigger SFX or voice lines.
    /// AudioClip references are populated by the editor scene builder from local-only PRTS downloads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoguelitePrototypeAudioController : MonoBehaviour
    {
        [Header("Owner")]
        [SerializeField] private Transform player;

        [Header("BGM")]
        [SerializeField] private AudioClip bgmIntro;
        [SerializeField] private AudioClip bgmLoop;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.30f;
        [SerializeField, Range(0.1f, 1f)] private float voiceDuckMultiplier = 0.56f;

        [Header("Skill SFX")]
        [SerializeField] private AudioClip skill1Sfx;
        [SerializeField] private AudioClip skill2Sfx;
        [SerializeField, Range(0f, 1f)] private float skillSfxVolume = 0.82f;

        [Header("Chen Voice")]
        [SerializeField] private AudioClip[] skill1Voices;
        [SerializeField] private AudioClip[] skill2Voices;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.96f;

        private PlayerSkillController _skillController;
        private AudioSource _bgmIntroSource;
        private AudioSource _bgmLoopSource;
        private AudioSource _skillSource;
        private AudioSource _voiceSource;
        private Coroutine _duckRoutine;
        private bool _subscribed;
        private bool _bgmStarted;
        private bool _warnedMissingAudio;
        private int _lastSkill1Voice = -1;
        private int _lastSkill2Voice = -1;

        public void Configure(
            Transform playerTransform,
            AudioClip intro,
            AudioClip loop,
            AudioClip slot1Sfx,
            AudioClip slot2Sfx,
            AudioClip[] slot1Voices,
            AudioClip[] slot2Voices)
        {
            player = playerTransform;
            bgmIntro = intro;
            bgmLoop = loop;
            skill1Sfx = slot1Sfx;
            skill2Sfx = slot2Sfx;
            skill1Voices = slot1Voices;
            skill2Voices = slot2Voices;
        }

        private void Awake()
        {
            EnsureSources();
        }

        private void OnEnable()
        {
            EnsureSources();
            ResolveAndSubscribe();
            StartBackgroundMusic();
            WarnIfIncomplete();
        }

        private void Start()
        {
            // Scene-builder references are serialized before Play, but resolving again here makes the
            // controller robust when it is added manually or when another component initializes late.
            ResolveAndSubscribe();
            StartBackgroundMusic();
            WarnIfIncomplete();
        }

        private void ResolveAndSubscribe()
        {
            if (_skillController == null)
            {
                if (player != null)
                    _skillController = player.GetComponent<PlayerSkillController>();
                if (_skillController == null)
                    _skillController = FindFirstObjectByType<PlayerSkillController>();
            }

            if (_skillController == null || _subscribed)
                return;

            _skillController.SkillCastSucceeded += HandleSkillCast;
            _subscribed = true;
        }

        private void HandleSkillCast(int slot)
        {
            EnsureSources();

            var sfx = slot == 1 ? skill1Sfx : slot == 2 ? skill2Sfx : null;
            if (sfx != null)
                _skillSource.PlayOneShot(sfx, skillSfxVolume);

            if (slot == 1)
                PlayVoice(skill1Voices, ref _lastSkill1Voice);
            else if (slot == 2)
                PlayVoice(skill2Voices, ref _lastSkill2Voice);
        }

        private void PlayVoice(AudioClip[] pool, ref int lastIndex)
        {
            if (pool == null || pool.Length == 0)
                return;

            var validCount = 0;
            for (var i = 0; i < pool.Length; i++)
                if (pool[i] != null)
                    validCount++;
            if (validCount == 0)
                return;

            var selected = PickNonRepeatingClip(pool, lastIndex);
            if (selected < 0 || pool[selected] == null)
                return;

            lastIndex = selected;
            _voiceSource.Stop();
            _voiceSource.clip = pool[selected];
            _voiceSource.volume = voiceVolume;
            _voiceSource.Play();

            if (_duckRoutine != null)
                StopCoroutine(_duckRoutine);
            _duckRoutine = StartCoroutine(DuckForVoice(pool[selected].length));
        }

        private static int PickNonRepeatingClip(AudioClip[] pool, int lastIndex)
        {
            if (pool == null || pool.Length == 0)
                return -1;

            if (pool.Length == 1)
                return pool[0] != null ? 0 : -1;

            var start = Random.Range(0, pool.Length);
            for (var offset = 0; offset < pool.Length; offset++)
            {
                var index = (start + offset) % pool.Length;
                if (pool[index] != null && index != lastIndex)
                    return index;
            }

            for (var i = 0; i < pool.Length; i++)
                if (pool[i] != null)
                    return i;
            return -1;
        }

        private IEnumerator DuckForVoice(float voiceLength)
        {
            SetBgmGain(voiceDuckMultiplier);
            yield return new WaitForSecondsRealtime(Mathf.Max(0.12f, voiceLength + 0.06f));
            SetBgmGain(1f);
            _duckRoutine = null;
        }

        private void StartBackgroundMusic()
        {
            if (_bgmStarted)
                return;
            EnsureSources();

            if (bgmIntro == null && bgmLoop == null)
                return;

            _bgmStarted = true;
            SetBgmGain(1f);

            if (bgmIntro != null && bgmLoop != null)
            {
                var startDsp = AudioSettings.dspTime + 0.12d;
                _bgmIntroSource.clip = bgmIntro;
                _bgmIntroSource.loop = false;
                _bgmLoopSource.clip = bgmLoop;
                _bgmLoopSource.loop = true;
                _bgmIntroSource.PlayScheduled(startDsp);
                _bgmLoopSource.PlayScheduled(startDsp + bgmIntro.length);
                return;
            }

            if (bgmLoop != null)
            {
                _bgmLoopSource.clip = bgmLoop;
                _bgmLoopSource.loop = true;
                _bgmLoopSource.Play();
                return;
            }

            _bgmIntroSource.clip = bgmIntro;
            _bgmIntroSource.loop = false;
            _bgmIntroSource.Play();
        }

        private void SetBgmGain(float multiplier)
        {
            var volume = Mathf.Clamp01(bgmVolume * Mathf.Clamp01(multiplier));
            if (_bgmIntroSource != null)
                _bgmIntroSource.volume = volume;
            if (_bgmLoopSource != null)
                _bgmLoopSource.volume = volume;
        }

        private void EnsureSources()
        {
            if (_bgmIntroSource == null)
                _bgmIntroSource = CreateSource("BGM_Intro", false);
            if (_bgmLoopSource == null)
                _bgmLoopSource = CreateSource("BGM_Loop", true);
            if (_skillSource == null)
                _skillSource = CreateSource("SkillSFX", false);
            if (_voiceSource == null)
                _voiceSource = CreateSource("Voice", false);
        }

        private AudioSource CreateSource(string objectName, bool loop)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = objectName.StartsWith("BGM_") ? 128 : 64;
            return source;
        }

        private void WarnIfIncomplete()
        {
            if (_warnedMissingAudio)
                return;

            var missingBgm = bgmIntro == null && bgmLoop == null;
            var missingSkill = skill1Sfx == null || skill2Sfx == null;
            var missingVoice = !HasAnyClip(skill1Voices) || !HasAnyClip(skill2Voices);
            if (!missingBgm && !missingSkill && !missingVoice)
                return;

            _warnedMissingAudio = true;
            Debug.LogWarning(
                "[ArknightsACT/Audio] PRTS prototype audio is incomplete. Run " +
                "ArknightsACT > Assets > PRTS > Download Gameplay Audio (BGM + Chen), then rebuild Prototype Scene.",
                this);
        }

        private static bool HasAnyClip(AudioClip[] clips)
        {
            if (clips == null)
                return false;
            for (var i = 0; i < clips.Length; i++)
                if (clips[i] != null)
                    return true;
            return false;
        }

        private void OnDisable()
        {
            if (_skillController != null && _subscribed)
                _skillController.SkillCastSucceeded -= HandleSkillCast;
            _subscribed = false;

            if (_duckRoutine != null)
            {
                StopCoroutine(_duckRoutine);
                _duckRoutine = null;
            }
            SetBgmGain(1f);
        }
    }
}
