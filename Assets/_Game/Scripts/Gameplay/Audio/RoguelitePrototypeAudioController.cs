using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Enemies;
using UnityEngine;

namespace ArknightsACT.Gameplay.Audio
{
    /// <summary>
    /// Prototype battle-audio layer backed by local-only PRTS downloads. It owns the Chernobog BGM,
    /// successful skill SFX/voice, normal sword swings, damage/death feedback and enemy attack sounds.
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

        [Header("Skill Voice")]
        [SerializeField] private AudioClip[] skill1Voices;
        [SerializeField] private AudioClip[] skill2Voices;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.96f;

        [Header("Combat SFX")]
        [SerializeField] private AudioClip[] playerAttackSwings;
        [SerializeField] private AudioClip swordImpact;
        [SerializeField] private AudioClip playerHurt;
        [SerializeField] private AudioClip playerDeath;
        [SerializeField] private AudioClip enemyMeleeAttack;
        [SerializeField] private AudioClip enemyRangedAttack;
        [SerializeField] private AudioClip enemyDeath;
        [SerializeField, Range(0f, 1f)] private float combatSfxVolume = 0.78f;
        [SerializeField, Range(0f, 1f)] private float hurtSfxVolume = 0.84f;

        private PlayerSkillController _skillController;
        private PlayerAttackController _playerAttack;
        private Health _playerHealth;
        private float _lastPlayerHealth;

        private AudioSource _bgmIntroSource;
        private AudioSource _bgmLoopSource;
        private AudioSource _skillSource;
        private AudioSource _voiceSource;
        private AudioSource _combatSource;

        private readonly List<EnemyBinding> _enemyBindings = new(24);
        private Coroutine _duckRoutine;
        private float _nextEnemyRefreshAt;
        private float _lastImpactAt = -999f;
        private bool _subscribed;
        private bool _bgmStarted;
        private bool _warnedMissingAudio;
        private int _lastSkill1Voice = -1;
        private int _lastSkill2Voice = -1;

        private sealed class EnemyBinding
        {
            public PrototypeEnemyCombatBrain25D Brain;
            public Health Health;
            public float LastHealth;
            public Action<int> AttackStarted;
            public Action<float, float> HealthChanged;
            public Action Died;
        }

        public void Configure(
            Transform playerTransform,
            AudioClip intro,
            AudioClip loop,
            AudioClip slot1Sfx,
            AudioClip slot2Sfx,
            AudioClip[] slot1Voices,
            AudioClip[] slot2Voices,
            AudioClip[] normalAttackSwings,
            AudioClip normalSwordImpact,
            AudioClip ownerHurt,
            AudioClip ownerDeath,
            AudioClip meleeEnemyAttack,
            AudioClip rangedEnemyAttack,
            AudioClip genericEnemyDeath)
        {
            player = playerTransform;
            bgmIntro = intro;
            bgmLoop = loop;
            skill1Sfx = slot1Sfx;
            skill2Sfx = slot2Sfx;
            skill1Voices = slot1Voices;
            skill2Voices = slot2Voices;
            playerAttackSwings = normalAttackSwings;
            swordImpact = normalSwordImpact;
            playerHurt = ownerHurt;
            playerDeath = ownerDeath;
            enemyMeleeAttack = meleeEnemyAttack;
            enemyRangedAttack = rangedEnemyAttack;
            enemyDeath = genericEnemyDeath;
        }

        private void Awake()
        {
            EnsureSources();
        }

        private void OnEnable()
        {
            EnsureSources();
            ResolveAndSubscribePlayer();
            RefreshEnemyBindings(force: true);
            StartBackgroundMusic();
            WarnIfIncomplete();
        }

        private void Start()
        {
            ResolveAndSubscribePlayer();
            RefreshEnemyBindings(force: true);
            StartBackgroundMusic();
            WarnIfIncomplete();
        }

        private void Update()
        {
            ResolveAndSubscribePlayer();
            RefreshEnemyBindings(force: false);
        }

        private void ResolveAndSubscribePlayer()
        {
            if (player == null)
            {
                var skill = FindFirstObjectByType<PlayerSkillController>();
                if (skill != null)
                    player = skill.transform;
            }

            if (player == null)
                return;

            _skillController ??= player.GetComponent<PlayerSkillController>();
            _playerAttack ??= player.GetComponent<PlayerAttackController>();
            var entity = player.GetComponent<CombatEntity>();
            _playerHealth ??= entity != null ? entity.Health : null;

            if (_subscribed)
                return;

            if (_skillController != null)
                _skillController.SkillCastSucceeded += HandleSkillCast;
            if (_playerAttack != null)
                _playerAttack.AttackStarted += HandlePlayerAttackStarted;
            if (_playerHealth != null)
            {
                _lastPlayerHealth = _playerHealth.CurrentHealth;
                _playerHealth.Changed += HandlePlayerHealthChanged;
                _playerHealth.Died += HandlePlayerDied;
            }

            _subscribed = _skillController != null || _playerAttack != null || _playerHealth != null;
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

        private void HandlePlayerAttackStarted(int comboIndex)
        {
            if (playerAttackSwings == null || playerAttackSwings.Length == 0)
                return;

            var start = Mathf.Abs(comboIndex) % playerAttackSwings.Length;
            for (var offset = 0; offset < playerAttackSwings.Length; offset++)
            {
                var clip = playerAttackSwings[(start + offset) % playerAttackSwings.Length];
                if (clip == null)
                    continue;
                PlayCombat(clip, combatSfxVolume * 0.88f);
                return;
            }
        }

        private void HandlePlayerHealthChanged(float current, float maximum)
        {
            if (current < _lastPlayerHealth - 0.001f && current > 0f)
                PlayCombat(playerHurt, hurtSfxVolume);
            _lastPlayerHealth = current;
        }

        private void HandlePlayerDied()
        {
            PlayCombat(playerDeath, hurtSfxVolume);
        }

        private void RefreshEnemyBindings(bool force)
        {
            if (!force && Time.unscaledTime < _nextEnemyRefreshAt)
                return;
            _nextEnemyRefreshAt = Time.unscaledTime + 0.35f;

            for (var i = _enemyBindings.Count - 1; i >= 0; i--)
            {
                var binding = _enemyBindings[i];
                if (binding?.Brain != null)
                    continue;
                UnsubscribeEnemy(binding);
                _enemyBindings.RemoveAt(i);
            }

            var brains = FindObjectsByType<PrototypeEnemyCombatBrain25D>(FindObjectsSortMode.None);
            for (var i = 0; i < brains.Length; i++)
            {
                var brain = brains[i];
                if (brain == null || ContainsEnemy(brain))
                    continue;

                var entity = brain.GetComponent<CombatEntity>();
                var health = entity != null ? entity.Health : null;
                if (health == null)
                    continue;

                var binding = new EnemyBinding
                {
                    Brain = brain,
                    Health = health,
                    LastHealth = health.CurrentHealth
                };

                binding.AttackStarted = _ => HandleEnemyAttack(binding);
                binding.HealthChanged = (current, maximum) => HandleEnemyHealthChanged(binding, current);
                binding.Died = () => HandleEnemyDied(binding);

                brain.AttackStarted += binding.AttackStarted;
                health.Changed += binding.HealthChanged;
                health.Died += binding.Died;
                _enemyBindings.Add(binding);
            }
        }

        private bool ContainsEnemy(PrototypeEnemyCombatBrain25D brain)
        {
            for (var i = 0; i < _enemyBindings.Count; i++)
                if (_enemyBindings[i]?.Brain == brain)
                    return true;
            return false;
        }

        private void HandleEnemyAttack(EnemyBinding binding)
        {
            if (binding?.Brain == null)
                return;
            var clip = binding.Brain.Archetype == PrototypeEnemyArchetype.Ranged
                ? enemyRangedAttack
                : enemyMeleeAttack;
            PlayCombat(clip, combatSfxVolume * 0.78f);
        }

        private void HandleEnemyHealthChanged(EnemyBinding binding, float current)
        {
            if (binding == null)
                return;

            if (current < binding.LastHealth - 0.001f && current > 0f)
            {
                // Multi-hit skills can damage more than one enemy on the same frame. A tiny shared
                // throttle keeps the sword impact punchy instead of stacking identical clips into clipping.
                if (Time.unscaledTime - _lastImpactAt >= 0.035f)
                {
                    PlayCombat(swordImpact, combatSfxVolume);
                    _lastImpactAt = Time.unscaledTime;
                }
            }
            binding.LastHealth = current;
        }

        private void HandleEnemyDied(EnemyBinding binding)
        {
            PlayCombat(enemyDeath, combatSfxVolume * 0.82f);
        }

        private void PlayCombat(AudioClip clip, float volume)
        {
            if (clip == null)
                return;
            EnsureSources();
            _combatSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void PlayVoice(AudioClip[] pool, ref int lastIndex)
        {
            if (pool == null || pool.Length == 0)
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

            var start = UnityEngine.Random.Range(0, pool.Length);
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
            if (_combatSource == null)
                _combatSource = CreateSource("CombatSFX", false);
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

            var identity = player != null
                ? player.GetComponent<ArknightsACT.Gameplay.Characters.PlayableOperatorIdentity>()
                : null;
            var isSchwarz = identity != null && identity.OperatorId == "Schwarz";
            var missingBgm = bgmIntro == null && bgmLoop == null;
            // Schwarz has no verified standalone S2/S3 activation event in the extracted package.
            // Null is intentional there; never substitute Ch'en's skill SFX.
            var missingSkill = !isSchwarz && (skill1Sfx == null || skill2Sfx == null);
            var missingVoice = !HasAnyClip(skill1Voices) || !HasAnyClip(skill2Voices);
            var missingCombat = !HasAnyClip(playerAttackSwings) || swordImpact == null ||
                                playerHurt == null || playerDeath == null || enemyMeleeAttack == null ||
                                enemyRangedAttack == null || enemyDeath == null;
            if (!missingBgm && !missingSkill && !missingVoice && !missingCombat)
                return;

            _warnedMissingAudio = true;
            Debug.LogWarning(
                "[ArknightsACT/Audio] Prototype audio is incomplete for the current operator. " +
                "Rebuild the selected operator scene after its local audio assets are available.",
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

        private static void UnsubscribeEnemy(EnemyBinding binding)
        {
            if (binding == null)
                return;
            if (binding.Brain != null && binding.AttackStarted != null)
                binding.Brain.AttackStarted -= binding.AttackStarted;
            if (binding.Health != null)
            {
                if (binding.HealthChanged != null)
                    binding.Health.Changed -= binding.HealthChanged;
                if (binding.Died != null)
                    binding.Health.Died -= binding.Died;
            }
        }

        private void UnsubscribePlayer()
        {
            if (_skillController != null)
                _skillController.SkillCastSucceeded -= HandleSkillCast;
            if (_playerAttack != null)
                _playerAttack.AttackStarted -= HandlePlayerAttackStarted;
            if (_playerHealth != null)
            {
                _playerHealth.Changed -= HandlePlayerHealthChanged;
                _playerHealth.Died -= HandlePlayerDied;
            }
            _subscribed = false;
        }

        private void OnDisable()
        {
            UnsubscribePlayer();
            for (var i = 0; i < _enemyBindings.Count; i++)
                UnsubscribeEnemy(_enemyBindings[i]);
            _enemyBindings.Clear();

            if (_duckRoutine != null)
            {
                StopCoroutine(_duckRoutine);
                _duckRoutine = null;
            }
            SetBgmGain(1f);
        }
    }
}
