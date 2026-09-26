using System;
using System.Collections;
using System.Collections.Generic;
using ArknightsACT.Combat;
using ArknightsACT.Gameplay.Abilities;
using ArknightsACT.Gameplay.Combat;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Enemies;
using ArknightsACT.Gameplay.Roguelite.Routing;
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

        [Header("BGM Playlist")]
        [SerializeField] private AudioClip[] uiBgmIntros;
        [SerializeField] private AudioClip[] uiBgmLoops;
        [SerializeField] private AudioClip[] mapBgmIntros;
        [SerializeField] private AudioClip[] mapBgmLoops;

        [Header("Skill SFX")]
        [SerializeField] private AudioClip skill1Sfx;
        [SerializeField] private AudioClip skill2Sfx;
        [SerializeField] private AudioClip skill1LayerSfx;
        [SerializeField] private AudioClip skill2LayerSfx;
        [SerializeField, Range(0f, 1f)] private float skillSfxVolume = 0.82f;

        [Header("Skill Voice")]
        [SerializeField] private AudioClip[] skill1Voices;
        [SerializeField] private AudioClip[] skill2Voices;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.96f;

        [Header("Combat SFX")]
        [SerializeField] private AudioClip[] playerAttackSwings;
        [SerializeField] private AudioClip swordImpact;
        [SerializeField] private string manualAttackSourceId;
        [SerializeField] private AudioClip manualAttackSfx;
        [SerializeField] private AudioClip manualAttackImpact;
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
        private bool _skillSfxOptional;
        private Transform _audioProfileOwner;
        private int _lastSkill1Voice = -1;
        private int _lastSkill2Voice = -1;
        private int _lastUiBgm = -1;
        private int _lastMapBgm = -1;
        private RogueliteGameFlowController _gameFlow;
        private BgmGroup _activeBgmGroup;

        private enum BgmGroup
        {
            None,
            Ui,
            Map
        }

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
            ApplyOperatorAudioProfile(player);
        }

        public void ConfigureBgmPlaylist(
            AudioClip[] interfaceIntros,
            AudioClip[] interfaceLoops,
            AudioClip[] mapIntros,
            AudioClip[] mapLoops)
        {
            // A legacy serialized fallback track may already be playing when the scene is
            // reconfigured. Stop it explicitly before replacing the playlist; otherwise the old
            // BGM can continue underneath the newly scheduled sources and make the switch seem
            // ineffective.
            StopBackgroundMusic();

            uiBgmIntros = interfaceIntros;
            uiBgmLoops = interfaceLoops;
            mapBgmIntros = mapIntros;
            mapBgmLoops = mapLoops;
            _activeBgmGroup = BgmGroup.None;

            if (isActiveAndEnabled)
                StartBackgroundMusic();
        }

        private void Awake()
        {
            EnsureSources();
        }

        private void OnEnable()
        {
            EnsureSources();
            BindGameFlow();
            if (PlayerRuntimeContext.Instance != null)
                PlayerRuntimeContext.Instance.ActivePlayerChanged += OnActivePlayerChanged;
            ResolveAndSubscribePlayer();
            RefreshEnemyBindings(force: true);
            StartBackgroundMusic();
            WarnIfIncomplete();
        }

        private void Start()
        {
            BindGameFlow();
            ResolveAndSubscribePlayer();
            RefreshEnemyBindings(force: true);
            StartBackgroundMusic();
            WarnIfIncomplete();
        }

        private void Update()
        {
            BindGameFlow();
            ResolveAndSubscribePlayer();
            RefreshEnemyBindings(force: false);
        }

        private void OnActivePlayerChanged(Transform previousPlayer, Transform nextPlayer)
        {
            ResolveAndSubscribePlayer();
        }

        private void ResolveAndSubscribePlayer()
        {
            var resolvedPlayer = PlayerRuntimeContext.Resolve(player);
            if (resolvedPlayer == null)
            {
                var skill = FindFirstObjectByType<PlayerSkillController>();
                if (skill != null)
                    resolvedPlayer = skill.transform;
            }

            if (resolvedPlayer == null)
                return;

            if (resolvedPlayer != player)
            {
                UnsubscribePlayer();
                player = resolvedPlayer;
                _skillController = null;
                _playerAttack = null;
                _playerHealth = null;
                _audioProfileOwner = null;
                _lastSkill1Voice = -1;
                _lastSkill2Voice = -1;
                _warnedMissingAudio = false;
            }

            // The stage audio controller also serializes fallback clips. Do not let those stale
            // scene values win just because its serialized player already equals the active player.
            // Every newly-bound operator must hydrate runtime audio from its character-owned profile.
            if (_audioProfileOwner != player)
            {
                ApplyOperatorAudioProfile(player, clearWhenMissing: true);
                _audioProfileOwner = player;
            }

            _skillController ??= player.GetComponent<PlayerSkillController>();
            _playerAttack ??= player.GetComponent<PlayerAttackController>();
            var entity = player.GetComponent<CombatEntity>();
            _playerHealth ??= entity != null ? entity.Health : null;

            if (_subscribed)
                return;

            if (_skillController != null)
                _skillController.SkillCastSucceeded += HandleSkillCast;
            if (_playerAttack != null)
            {
                _playerAttack.AttackStarted += HandlePlayerAttackStarted;
                _playerAttack.AttackHit += HandlePlayerAttackHit;
            }
            if (_playerHealth != null)
            {
                _lastPlayerHealth = _playerHealth.CurrentHealth;
                _playerHealth.Changed += HandlePlayerHealthChanged;
                _playerHealth.Died += HandlePlayerDied;
            }

            _subscribed = _skillController != null || _playerAttack != null || _playerHealth != null;
            WarnIfIncomplete();
        }

        private void ApplyOperatorAudioProfile(Transform owner, bool clearWhenMissing = false)
        {
            var profile = owner != null ? owner.GetComponent<PlayableOperatorAudioProfile>() : null;
            if (profile == null)
            {
                if (clearWhenMissing)
                {
                    skill1Sfx = null;
                    skill2Sfx = null;
                    skill1LayerSfx = null;
                    skill2LayerSfx = null;
                    skill1Voices = null;
                    skill2Voices = null;
                    playerAttackSwings = null;
                    swordImpact = null;
                    manualAttackSourceId = string.Empty;
                    manualAttackSfx = null;
                    manualAttackImpact = null;
                    _skillSfxOptional = false;
                }
                return;
            }

            skill1Sfx = profile.Skill1Sfx;
            skill2Sfx = profile.Skill2Sfx;
            skill1LayerSfx = profile.Skill1LayerSfx;
            skill2LayerSfx = profile.Skill2LayerSfx;
            skill1Voices = profile.Skill1Voices;
            skill2Voices = profile.Skill2Voices;
            playerAttackSwings = profile.BasicAttackSwings;
            swordImpact = profile.BasicAttackImpact;
            manualAttackSourceId = profile.ManualAttackSourceId;
            manualAttackSfx = profile.ManualAttackSfx;
            manualAttackImpact = profile.ManualAttackImpact;
            _skillSfxOptional = profile.SkillSfxOptional;
        }

        private void HandleSkillCast(int slot)
        {
            EnsureSources();

            var sfx = slot == 1 ? skill1Sfx : slot == 2 ? skill2Sfx : null;
            var layeredSfx = slot == 1 ? skill1LayerSfx : slot == 2 ? skill2LayerSfx : null;
            if (sfx != null)
                _skillSource.PlayOneShot(sfx, skillSfxVolume);
            if (layeredSfx != null)
                _skillSource.PlayOneShot(layeredSfx, skillSfxVolume);

            if (slot == 1)
                PlayVoice(skill1Voices, ref _lastSkill1Voice);
            else if (slot == 2)
                PlayVoice(skill2Voices, ref _lastSkill2Voice);
        }

        private void HandlePlayerAttackStarted(int comboIndex)
        {
            if (IsSpecialAttackAudioActive())
            {
                PlayCombat(manualAttackSfx, combatSfxVolume);
                return;
            }

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

        private bool IsSpecialAttackAudioActive()
        {
            if (_playerAttack != null &&
                !string.IsNullOrWhiteSpace(manualAttackSourceId) &&
                string.Equals(
                    _playerAttack.CurrentAttackSourceId,
                    manualAttackSourceId,
                    StringComparison.OrdinalIgnoreCase))
                return true;

            if (player == null)
                return false;

            var behaviours = player.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerSpecialAttackAudioState state && state.UseSpecialAttackAudio)
                    return true;
            return false;
        }

        private void HandlePlayerAttackHit(CombatEntity target)
        {
            if (Time.unscaledTime - _lastImpactAt < 0.035f)
                return;
            _lastImpactAt = Time.unscaledTime;

            if (_playerAttack != null &&
                !string.IsNullOrWhiteSpace(manualAttackSourceId) &&
                string.Equals(
                    _playerAttack.LastHitSourceId,
                    manualAttackSourceId,
                    StringComparison.OrdinalIgnoreCase))
            {
                PlayCombat(manualAttackImpact, combatSfxVolume);
                return;
            }

            PlayCombat(swordImpact, combatSfxVolume);
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

            // Player impact audio is driven by PlayerAttackController.AttackHit so the exact
            // attack source (normal shot vs. Schwarz S3 aimed shot) is still available here.
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

            var group = ResolveBgmGroup();
            _activeBgmGroup = group;
            if (TrySelectBgm(group, out var playlistIntro, out var playlistLoop))
            {
                PlayBackgroundMusic(playlistIntro, playlistLoop);
                return;
            }

            // Once a playlist is configured, never silently fall back to the old prototype
            // track. A missing playlist entry should be obvious instead of masking configuration
            // problems with the legacy BGM.
            if (HasPairedTrack(uiBgmIntros, uiBgmLoops) || HasPairedTrack(mapBgmIntros, mapBgmLoops))
                return;

            PlayBackgroundMusic(bgmIntro, bgmLoop);
        }

        private void SwitchBgmGroup(BgmGroup group)
        {
            if (group == BgmGroup.None)
                group = BgmGroup.Ui;
            if (_activeBgmGroup == group && _bgmStarted)
                return;

            StopBackgroundMusic();
            _activeBgmGroup = group;
            if (TrySelectBgm(group, out var playlistIntro, out var playlistLoop))
            {
                PlayBackgroundMusic(playlistIntro, playlistLoop);
                return;
            }

            if (HasPairedTrack(uiBgmIntros, uiBgmLoops) || HasPairedTrack(mapBgmIntros, mapBgmLoops))
                return;

            PlayBackgroundMusic(bgmIntro, bgmLoop);
        }

        private void PlayBackgroundMusic(AudioClip intro, AudioClip loop)
        {
            if (intro == null && loop == null)
                return;

            _bgmStarted = true;
            SetBgmGain(1f);

            if (intro != null && loop != null)
            {
                var startDsp = AudioSettings.dspTime + 0.12d;
                _bgmIntroSource.clip = intro;
                _bgmIntroSource.loop = false;
                _bgmLoopSource.clip = loop;
                _bgmLoopSource.loop = true;
                _bgmIntroSource.PlayScheduled(startDsp);
                _bgmLoopSource.PlayScheduled(startDsp + intro.length);
                return;
            }

            if (loop != null)
            {
                _bgmLoopSource.clip = loop;
                _bgmLoopSource.loop = true;
                _bgmLoopSource.Play();
                return;
            }

            _bgmIntroSource.clip = intro;
            _bgmIntroSource.loop = false;
            _bgmIntroSource.Play();
        }

        private void StopBackgroundMusic()
        {
            EnsureSources();
            _bgmIntroSource.Stop();
            _bgmLoopSource.Stop();
            _bgmIntroSource.clip = null;
            _bgmLoopSource.clip = null;
            _bgmStarted = false;
        }

        private bool TrySelectBgm(BgmGroup group, out AudioClip intro, out AudioClip loop)
        {
            intro = null;
            loop = null;
            var intros = group == BgmGroup.Map ? mapBgmIntros : uiBgmIntros;
            var loops = group == BgmGroup.Map ? mapBgmLoops : uiBgmLoops;
            var count = Mathf.Min(intros != null ? intros.Length : 0, loops != null ? loops.Length : 0);
            if (count <= 0)
                return false;

            var last = group == BgmGroup.Map ? _lastMapBgm : _lastUiBgm;
            var start = UnityEngine.Random.Range(0, count);
            var index = -1;
            for (var offset = 0; offset < count; offset++)
            {
                var candidate = (start + offset) % count;
                if (intros[candidate] != null && loops[candidate] != null && candidate != last)
                {
                    index = candidate;
                    break;
                }
            }

            if (index < 0)
            {
                for (var i = 0; i < count; i++)
                {
                    if (intros[i] == null || loops[i] == null)
                        continue;
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return false;

            if (group == BgmGroup.Map)
                _lastMapBgm = index;
            else
                _lastUiBgm = index;
            intro = intros[index];
            loop = loops[index];
            Debug.Log(
                $"[ArknightsACT/Audio/BGM] group={group}, index={index}, " +
                $"intro='{(intro != null ? intro.name : "<none>")}', loop='{(loop != null ? loop.name : "<none>")}'.",
                this);
            return true;
        }

        private BgmGroup ResolveBgmGroup()
        {
            return _gameFlow != null && _gameFlow.State == RogueliteShellState.Running
                ? BgmGroup.Map
                : BgmGroup.Ui;
        }

        private void BindGameFlow()
        {
            var flow = RogueliteGameFlowController.Instance ?? FindFirstObjectByType<RogueliteGameFlowController>();
            if (flow == _gameFlow)
                return;

            if (_gameFlow != null)
                _gameFlow.StateChanged -= OnGameFlowStateChanged;
            _gameFlow = flow;
            if (_gameFlow != null)
            {
                _gameFlow.StateChanged += OnGameFlowStateChanged;
                SwitchBgmGroup(ResolveBgmGroup());
            }
        }

        private void OnGameFlowStateChanged(RogueliteShellState state)
        {
            SwitchBgmGroup(state == RogueliteShellState.Running ? BgmGroup.Map : BgmGroup.Ui);
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

            var missingBgm = bgmIntro == null && bgmLoop == null &&
                             !HasPairedTrack(uiBgmIntros, uiBgmLoops) &&
                             !HasPairedTrack(mapBgmIntros, mapBgmLoops);
            var missingSkill = !_skillSfxOptional && (skill1Sfx == null || skill2Sfx == null);
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

        private static bool HasPairedTrack(AudioClip[] intros, AudioClip[] loops)
        {
            if (intros == null || loops == null)
                return false;
            var count = Mathf.Min(intros.Length, loops.Length);
            for (var i = 0; i < count; i++)
                if (intros[i] != null && loops[i] != null)
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
            {
                _playerAttack.AttackStarted -= HandlePlayerAttackStarted;
                _playerAttack.AttackHit -= HandlePlayerAttackHit;
            }
            if (_playerHealth != null)
            {
                _playerHealth.Changed -= HandlePlayerHealthChanged;
                _playerHealth.Died -= HandlePlayerDied;
            }
            _subscribed = false;
            _audioProfileOwner = null;
        }

        private void OnDisable()
        {
            if (_gameFlow != null)
                _gameFlow.StateChanged -= OnGameFlowStateChanged;
            _gameFlow = null;
            if (PlayerRuntimeContext.Instance != null)
                PlayerRuntimeContext.Instance.ActivePlayerChanged -= OnActivePlayerChanged;
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
