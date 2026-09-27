using ArknightsACT.Combat;
using ArknightsACT.Editor;
using ArknightsACT.Gameplay.Characters;
using ArknightsACT.Gameplay.Characters.Chen;
using ArknightsACT.Gameplay.Characters.FrostNova;
using ArknightsACT.Gameplay.Characters.Skadi;
using ArknightsACT.Gameplay.Characters.Wisadel;
using NUnit.Framework;
using UnityEngine;

namespace ArknightsACT.Tests
{
    public sealed class OperatorProgressionTests
    {
        [Test]
        public void E2Progression_EvaluatesIntermediateLevel()
        {
            var progression = new OperatorE2Progression(
                90,
                "1-1",
                new OperatorProgressionSnapshot(
                    1,
                    new OperatorBaseStats(2188f, 469f, 288f, 0f, 1.3f)),
                new OperatorProgressionSnapshot(
                    30,
                    new OperatorBaseStats(2413f, 515f, 309f, 0f, 1.3f)),
                new OperatorProgressionSnapshot(
                    60,
                    new OperatorBaseStats(2647f, 562f, 330f, 0f, 1.3f)),
                new OperatorProgressionSnapshot(
                    90,
                    new OperatorBaseStats(2880f, 610f, 352f, 0f, 1.3f)));

            var level45 = progression.Evaluate(45);

            Assert.That(progression.HasCheckpoint(1), Is.True);
            Assert.That(progression.HasCheckpoint(30), Is.True);
            Assert.That(progression.HasCheckpoint(60), Is.True);
            Assert.That(progression.HasCheckpoint(90), Is.True);
            Assert.That(level45.MaxHealth, Is.EqualTo(2530f).Within(0.01f));
            Assert.That(level45.Attack, Is.EqualTo(538.5f).Within(0.01f));
            Assert.That(level45.PhysicalDefense, Is.EqualTo(319.5f).Within(0.01f));
            Assert.That(level45.AttackInterval, Is.EqualTo(1.3f).Within(0.001f));
        }

        [Test]
        public void FrostNovaWinterTrace_BuildsExplicitProjectCurveAndFlatMasteryTiers()
        {
            var data = new FrostNovaWinterTraceCombatData
            {
                sourceId = "enemy_1510_frstar2",
                level0 = new FrostNovaWinterTraceBaseLevel
                {
                    maxHealth = 30000f,
                    attack = 440f,
                    physicalDefense = 380f,
                    artsResistance = 50f,
                    attackInterval = 3.7f,
                    attackRadiusTiles = 2f
                },
                level1 = new FrostNovaWinterTraceBaseLevel
                {
                    maxHealth = 45000f,
                    attack = 530f,
                    physicalDefense = 440f,
                    artsResistance = 50f,
                    attackInterval = 3.7f,
                    attackRadiusTiles = 2f
                },
                skill1 = new FrostNovaWinterTraceSkillData
                {
                    sourceSkillId = "enemy_1510_frstar2:IceNova",
                    displayName = "冰环",
                    cooldownSeconds = 10.5f,
                    damageScale = 1f,
                    radiusTiles = 2f,
                    coldDurationSeconds = 10f
                },
                skill2 = new FrostNovaWinterTraceSkillData
                {
                    sourceSkillId = "enemy_1510_frstar2:IceNovaReborn",
                    displayName = "冰爆",
                    cooldownSeconds = 10.5f,
                    damageScale = 1f,
                    radiusTiles = 3.3f,
                    coldDurationSeconds = 10f
                },
                basicAttackColdDurationSeconds = 5f
            };

            var progression = FrostNovaWinterTraceCombatProfile.BuildProjectProgression(data);
            var level1 = progression.Evaluate(1);
            var level90 = progression.Evaluate(90);

            Assert.That(level1.MaxHealth, Is.EqualTo(30000f));
            Assert.That(level1.Attack, Is.EqualTo(440f));
            Assert.That(level1.PhysicalDefense, Is.EqualTo(380f));
            Assert.That(level1.ArtsResistance, Is.EqualTo(50f));
            Assert.That(level1.AttackInterval, Is.EqualTo(3.7f).Within(0.001f));
            Assert.That(level90.MaxHealth, Is.EqualTo(45000f));
            Assert.That(level90.Attack, Is.EqualTo(530f));
            Assert.That(level90.PhysicalDefense, Is.EqualTo(440f));

            var mastery = FrostNovaWinterTraceCombatProfile.BuildSkillMasterySet(data);
            var slot1M0 = mastery.FindSlot(1).GetSnapshot(0);
            var slot1M3 = mastery.FindSlot(1).GetSnapshot(3);
            var slot2M0 = mastery.FindSlot(2).GetSnapshot(0);
            var slot2M3 = mastery.FindSlot(2).GetSnapshot(3);

            Assert.That(slot1M0.SkillPointCost, Is.EqualTo(10.5f));
            Assert.That(slot1M3.SkillPointCost, Is.EqualTo(slot1M0.SkillPointCost));
            Assert.That(slot1M0.GetFirstBlackboard(0f, "damage_scale"), Is.EqualTo(1f));
            Assert.That(slot1M3.GetFirstBlackboard(0f, "radius_tiles"), Is.EqualTo(2f));
            Assert.That(slot2M0.GetFirstBlackboard(0f, "radius_tiles"), Is.EqualTo(3.3f).Within(0.001f));
            Assert.That(slot2M3.GetFirstBlackboard(0f, "damage_scale"), Is.EqualTo(1f));
        }

        [Test]
        public void PrtsImporter_ExtractsPhase2Checkpoints()
        {
            const string json = @"{
  ""char_010_chen"": {
    ""phases"": [
      { ""maxLevel"": 50, ""rangeId"": ""1-1"", ""attributesKeyFrames"": [] },
      { ""maxLevel"": 80, ""rangeId"": ""1-1"", ""attributesKeyFrames"": [] },
      {
        ""maxLevel"": 90,
        ""rangeId"": ""1-1"",
        ""attributesKeyFrames"": [
          {
            ""level"": 1,
            ""data"": {
              ""maxHp"": 2188,
              ""atk"": 469,
              ""def"": 288,
              ""magicResistance"": 0.0,
              ""attackSpeed"": 100.0,
              ""baseAttackTime"": 1.3
            }
          },
          {
            ""level"": 90,
            ""data"": {
              ""maxHp"": 2880,
              ""atk"": 610,
              ""def"": 352,
              ""magicResistance"": 0.0,
              ""attackSpeed"": 100.0,
              ""baseAttackTime"": 1.3
            }
          }
        ]
      }
    ]
  }
}";

            Assert.That(
                PrtsOperatorProgressionImporter.TryExtractE2Progression(
                    json,
                    "char_010_chen",
                    out var progression),
                Is.True);

            Assert.That(progression.RangeId, Is.EqualTo("1-1"));
            Assert.That(progression.MaxLevel, Is.EqualTo(90));
            Assert.That(progression.Snapshots.Count, Is.EqualTo(4));

            var level1 = progression.Evaluate(1);
            var level30 = progression.Evaluate(30);
            var level60 = progression.Evaluate(60);
            var level90 = progression.Evaluate(90);

            Assert.That(level1.MaxHealth, Is.EqualTo(2188f));
            Assert.That(level30.MaxHealth, Is.EqualTo(2413f));
            Assert.That(level30.Attack, Is.EqualTo(515f));
            Assert.That(level30.PhysicalDefense, Is.EqualTo(309f));
            Assert.That(level60.MaxHealth, Is.EqualTo(2647f));
            Assert.That(level60.Attack, Is.EqualTo(562f));
            Assert.That(level60.PhysicalDefense, Is.EqualTo(330f));
            Assert.That(level90.MaxHealth, Is.EqualTo(2880f));
            Assert.That(level90.Attack, Is.EqualTo(610f));
            Assert.That(level90.PhysicalDefense, Is.EqualTo(352f));
            Assert.That(level90.AttackInterval, Is.EqualTo(1.3f).Within(0.001f));
        }

        [Test]
        public void RecommendedMetaProgression_UsesThreeMilestones()
        {
            var plan = OperatorMetaProgressionPlan.CreateRecommendedDefault();

            var step1 = plan.GetNextStep(1);
            var step30 = plan.GetNextStep(30);
            var step60 = plan.GetNextStep(60);

            Assert.That(step1, Is.Not.Null);
            Assert.That(step1.TargetLevel, Is.EqualTo(30));
            Assert.That(step1.LmdCost, Is.EqualTo(20000));
            Assert.That(step1.Materials.Count, Is.EqualTo(1));
            Assert.That(step1.Materials[0].ItemId, Is.EqualTo("rogue_3_relic_legacy_54"));
            Assert.That(step1.Materials[0].Amount, Is.EqualTo(2));

            Assert.That(step30, Is.Not.Null);
            Assert.That(step30.TargetLevel, Is.EqualTo(60));
            Assert.That(step30.LmdCost, Is.EqualTo(45000));
            Assert.That(step30.Materials[0].ItemId, Is.EqualTo("rogue_3_relic_legacy_55"));
            Assert.That(step30.Materials[0].Amount, Is.EqualTo(2));

            Assert.That(step60, Is.Not.Null);
            Assert.That(step60.TargetLevel, Is.EqualTo(90));
            Assert.That(step60.LmdCost, Is.EqualTo(90000));
            Assert.That(step60.Materials[0].ItemId, Is.EqualTo("rogue_4_relic_legacy_187"));
            Assert.That(step60.Materials[0].Amount, Is.EqualTo(1));

            Assert.That(plan.GetNextStep(90), Is.Null);
        }

        [Test]
        public void RecommendedSkillMasteryCosts_UseThreeSteps()
        {
            var plan = OperatorSkillMasteryCostPlan.CreateRecommendedDefault();

            var m1 = plan.GetNextStep(0);
            var m2 = plan.GetNextStep(1);
            var m3 = plan.GetNextStep(2);

            Assert.That(m1, Is.Not.Null);
            Assert.That(m1.TargetMastery, Is.EqualTo(1));
            Assert.That(m1.LmdCost, Is.EqualTo(30000));
            Assert.That(m1.Materials[0].ItemId, Is.EqualTo("rogue_5_relic_legacy_11"));
            Assert.That(m1.Materials[0].Amount, Is.EqualTo(2));

            Assert.That(m2, Is.Not.Null);
            Assert.That(m2.TargetMastery, Is.EqualTo(2));
            Assert.That(m2.LmdCost, Is.EqualTo(60000));
            Assert.That(m2.Materials[0].ItemId, Is.EqualTo("rogue_3_relic_legacy_57"));
            Assert.That(m2.Materials[0].Amount, Is.EqualTo(2));

            Assert.That(m3, Is.Not.Null);
            Assert.That(m3.TargetMastery, Is.EqualTo(3));
            Assert.That(m3.LmdCost, Is.EqualTo(120000));
            Assert.That(m3.Materials[0].ItemId, Is.EqualTo("relic_24"));
            Assert.That(m3.Materials[0].Amount, Is.EqualTo(1));
            Assert.That(plan.GetNextStep(3), Is.Null);
        }

        [Test]
        public void SkillMasteryImporter_MapsGameplaySlotsToOriginalS2AndS3()
        {
            const string characterJson =
                "{\"char_test\":{\"skills\":[" +
                "{\"skillId\":\"skill_s1\"}," +
                "{\"skillId\":\"skill_s2\"}," +
                "{\"skillId\":\"skill_s3\"}]}}";

            var skillJson =
                "{\"skill_s2\":{\"levels\":" +
                BuildSkillLevels("Skill Two", 20, "INCREASE_WHEN_ATTACK") +
                "},\"skill_s3\":{\"levels\":" +
                BuildSkillLevels("Skill Three", 30, "INCREASE_WITH_TIME") +
                "}}";

            Assert.That(
                PrtsOperatorSkillMasteryImporter.TryExtractSkillMasterySet(
                    characterJson,
                    skillJson,
                    "char_test",
                    2,
                    3,
                    out var set),
                Is.True);

            var slot1 = set.FindSlot(1);
            var slot2 = set.FindSlot(2);
            Assert.That(slot1, Is.Not.Null);
            Assert.That(slot2, Is.Not.Null);
            Assert.That(slot1.SourceSkillId, Is.EqualTo("skill_s2"));
            Assert.That(slot2.SourceSkillId, Is.EqualTo("skill_s3"));

            var level7 = slot1.GetSnapshot(0);
            var mastery3 = slot2.GetSnapshot(3);
            Assert.That(level7.SourceLevel, Is.EqualTo(7));
            Assert.That(level7.SkillPointCost, Is.EqualTo(27f));
            Assert.That(level7.InitialSkillPoints, Is.EqualTo(7f));
            Assert.That(level7.Duration, Is.EqualTo(7f));
            Assert.That(level7.SkillPointRecoveryType, Is.EqualTo(OperatorSkillPointRecoveryType.Attack));
            Assert.That(level7.SourceSkillPointRecoveryType, Is.EqualTo("INCREASE_WHEN_ATTACK"));
            Assert.That(level7.GetFirstBlackboard(-1f, "atk_scale"), Is.EqualTo(7f));

            Assert.That(mastery3.SourceLevel, Is.EqualTo(10));
            Assert.That(mastery3.SkillPointCost, Is.EqualTo(40f));
            Assert.That(mastery3.InitialSkillPoints, Is.EqualTo(10f));
            Assert.That(mastery3.Duration, Is.EqualTo(10f));
            Assert.That(mastery3.SkillPointRecoveryType, Is.EqualTo(OperatorSkillPointRecoveryType.Natural));
            Assert.That(mastery3.SourceSkillPointRecoveryType, Is.EqualTo("INCREASE_WITH_TIME"));
            Assert.That(mastery3.GetFirstBlackboard(-1f, "atk_scale"), Is.EqualTo(10f));
        }


        [Test]
        public void TalentImporter_ExtractsE2RankZeroBlackboard()
        {
            const string json = @"{
  ""char_340_shwaz"": {
    ""talents"": [
      {
        ""candidates"": [
          {
            ""unlockCondition"": { ""phase"": ""PHASE_1"", ""level"": 1 },
            ""requiredPotentialRank"": 0,
            ""blackboard"": [
              { ""key"": ""atk_scale"", ""value"": 1.3 },
              { ""key"": ""def"", ""value"": -0.1 },
              { ""key"": ""prob"", ""value"": 0.2 },
              { ""key"": ""defdown_duration"", ""value"": 5.0 }
            ]
          },
          {
            ""unlockCondition"": { ""phase"": ""PHASE_2"", ""level"": 1 },
            ""requiredPotentialRank"": 0,
            ""blackboard"": [
              { ""key"": ""atk_scale"", ""value"": 1.6 },
              { ""key"": ""def"", ""value"": -0.2 },
              { ""key"": ""prob"", ""value"": 0.2 },
              { ""key"": ""defdown_duration"", ""value"": 5.0 }
            ]
          },
          {
            ""unlockCondition"": { ""phase"": ""PHASE_2"", ""level"": 1 },
            ""requiredPotentialRank"": 4,
            ""blackboard"": [
              { ""key"": ""atk_scale"", ""value"": 1.8 }
            ]
          }
        ]
      }
    ]
  }
}";

            Assert.That(
                PrtsOperatorProgressionImporter.TryExtractE2TalentBlackboard(
                    json,
                    "char_340_shwaz",
                    0,
                    out var blackboard),
                Is.True);
            Assert.That(blackboard["atk_scale"], Is.EqualTo(1.6f).Within(0.001f));
            Assert.That(blackboard["def"], Is.EqualTo(-0.2f).Within(0.001f));
            Assert.That(blackboard["prob"], Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(blackboard["defdown_duration"], Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void SkillMasteryController_CanApplyLevel7BaselineDuringConfigure()
        {
            var go = new GameObject("SkillMasteryLevel7BaselineTest");
            try
            {
                go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                go.AddComponent<CombatEntity>();
                go.AddComponent<OperatorRuntimeStats>();
                var chenSkill = go.AddComponent<ChenSkill1>();
                var mastery = go.AddComponent<OperatorSkillMasteryController>();

                var profile = new OperatorSkillMasteryProfile(
                    1,
                    2,
                    "skchr_chen_2",
                    BuildSnapshot(0, 7, 25f, 14f),
                    BuildSnapshot(1, 8, 23f, 15f));

                mastery.Configure(
                    new OperatorSkillMasterySet(profile),
                    OperatorSkillMasteryCostPlan.CreateRecommendedDefault(),
                    applyLevel7: true);

                Assert.That(chenSkill.SkillPointCost, Is.EqualTo(25f));
                Assert.That(chenSkill.SkillPoints, Is.EqualTo(14f));
                Assert.That(mastery.GetRecoveryType(1), Is.EqualTo(OperatorSkillPointRecoveryType.Attack));
                Assert.That(mastery.GetRangeId(1), Is.EqualTo("3-12"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SkillMasteryController_SwitchesFourTiers_WithoutResettingSameTier()
        {
            var go = new GameObject("SkillMasterySwitchTest");
            try
            {
                go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                go.AddComponent<CombatEntity>();
                go.AddComponent<OperatorRuntimeStats>();
                var chenSkill = go.AddComponent<ChenSkill1>();
                var mastery = go.AddComponent<OperatorSkillMasteryController>();

                var profile = new OperatorSkillMasteryProfile(
                    1,
                    2,
                    "skchr_chen_2",
                    BuildSnapshot(0, 7, 25f, 14f),
                    BuildSnapshot(1, 8, 23f, 15f),
                    BuildSnapshot(2, 9, 21f, 16f),
                    BuildSnapshot(3, 10, 20f, 20f));
                mastery.Configure(
                    new OperatorSkillMasterySet(profile),
                    OperatorSkillMasteryCostPlan.CreateRecommendedDefault(),
                    applyLevel7: false);

                Assert.That(mastery.ApplySlot(1, 0), Is.True);
                Assert.That(chenSkill.SkillPointCost, Is.EqualTo(25f));
                Assert.That(chenSkill.SkillPoints, Is.EqualTo(14f));
                Assert.That(mastery.GetRecoveryType(1), Is.EqualTo(OperatorSkillPointRecoveryType.Attack));
                Assert.That(mastery.GetRangeId(1), Is.EqualTo("3-12"));

                Assert.That(mastery.ApplySlot(1, 3), Is.True);
                Assert.That(chenSkill.SkillPointCost, Is.EqualTo(20f));
                Assert.That(chenSkill.SkillPoints, Is.EqualTo(20f));

                chenSkill.SetSkillPoints(5f);
                Assert.That(mastery.ApplySlot(1, 3), Is.True);
                Assert.That(
                    chenSkill.SkillPoints,
                    Is.EqualTo(5f),
                    "Reapplying the same mastery tier must not reset runtime SP.");

                Assert.That(mastery.ApplySlot(1, 2), Is.True);
                Assert.That(chenSkill.SkillPointCost, Is.EqualTo(21f));
                Assert.That(chenSkill.SkillPoints, Is.EqualTo(16f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OfficialSkillAdapters_MapChenS3AndWisadelS3Blackboard()
        {
            var chenGo = new GameObject("ChenS3OfficialDataTest");
            var wisadelGo = new GameObject("WisadelS3OfficialDataTest");
            try
            {
                chenGo.AddComponent<Health>();
                chenGo.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                chenGo.AddComponent<CombatStats>();
                chenGo.AddComponent<CombatEntity>();
                chenGo.AddComponent<OperatorRuntimeStats>();
                var chen = chenGo.AddComponent<ChenSkill2>();
                chen.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        3,
                        10,
                        "skchr_chen_3",
                        "赤霄·绝影",
                        "x-1",
                        string.Empty,
                        30f,
                        20f,
                        0f,
                        OperatorSkillPointRecoveryType.Attack,
                        "INCREASE_WHEN_ATTACK",
                        new OperatorSkillBlackboardValue("atk_scale", 3.2f),
                        new OperatorSkillBlackboardValue("times", 10f),
                        new OperatorSkillBlackboardValue("stun", 4f)));

                Assert.That(chen.SkillPointCost, Is.EqualTo(30f));
                Assert.That(chen.SkillPoints, Is.EqualTo(20f));
                Assert.That(chen.StrikeCount, Is.EqualTo(10));
                Assert.That(chen.FinalStrikeStunSeconds, Is.EqualTo(4f));

                wisadelGo.AddComponent<Health>();
                wisadelGo.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                wisadelGo.AddComponent<CombatStats>();
                wisadelGo.AddComponent<CombatEntity>();
                var runtime = wisadelGo.AddComponent<OperatorRuntimeStats>();
                runtime.ConfigureBase(
                    new OperatorBaseStats(
                        1888f,
                        687f,
                        256f,
                        15f,
                        2.1f),
                    refillHealth: true);
                var wisadel = wisadelGo.AddComponent<WisadelSkill>();
                wisadel.ConfigureRuntimeSlot(
                    2,
                    "爆裂黎明",
                    0.55f);
                wisadel.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        3,
                        10,
                        "skchr_wisdel_3",
                        "爆裂黎明",
                        string.Empty,
                        string.Empty,
                        50f,
                        40f,
                        0f,
                        OperatorSkillPointRecoveryType.Natural,
                        "INCREASE_WITH_TIME",
                        new OperatorSkillBlackboardValue("atk", 1.8f),
                        new OperatorSkillBlackboardValue("max_cnt", 2f),
                        new OperatorSkillBlackboardValue("base_attack_time", 2.9f),
                        new OperatorSkillBlackboardValue("attack@atk_scale_3", 2.2f),
                        new OperatorSkillBlackboardValue("attack@prob", 1f),
                        new OperatorSkillBlackboardValue("attack@trigger_time", 6f),
                        new OperatorSkillBlackboardValue("sp", 3f)));

                Assert.That(wisadel.HasOfficialSkillData, Is.True);
                Assert.That(wisadel.SkillPointCost, Is.EqualTo(50f));
                Assert.That(wisadel.SkillPoints, Is.EqualTo(40f));
                Assert.That(wisadel.AmmoCapacity, Is.EqualTo(6));
                Assert.That(wisadel.Skill3AttackIntervalSeconds, Is.EqualTo(5f).Within(0.001f));
                Assert.That(wisadel.Skill3AttackBonusMultiplier, Is.EqualTo(2.8f).Within(0.001f));
                Assert.That(wisadel.Skill3BasicAttackDamageMultiplier, Is.EqualTo(2.2f).Within(0.001f));
                Assert.That(
                    wisadel.Skill3AttackBonusMultiplier * wisadel.Skill3BasicAttackDamageMultiplier,
                    Is.EqualTo(6.16f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(chenGo);
                Object.DestroyImmediate(wisadelGo);
            }
        }


        [Test]
        public void WisadelS2_RequiresOfficialSnapshotAndReadsBurstCountFromDescription()
        {
            var go = new GameObject("WisadelS2OfficialDataTest");
            try
            {
                go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                go.AddComponent<CombatEntity>();
                var runtime = go.AddComponent<OperatorRuntimeStats>();
                runtime.ConfigureBase(
                    new OperatorBaseStats(
                        1434f,
                        583f,
                        209f,
                        15f,
                        2.1f),
                    refillHealth: true);

                var skill = go.AddComponent<WisadelSkill>();
                skill.ConfigureRuntimeSlot(1, "饱和复仇", 0.62f);

                skill.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        0,
                        7,
                        "skchr_wisdel_2",
                        "饱和复仇",
                        string.Empty,
                        "攻击力提升，过载后改为攻击力倍率的<@ba.vup>4</>连发",
                        29f,
                        15f,
                        25f,
                        OperatorSkillPointRecoveryType.Natural,
                        "INCREASE_WITH_TIME",
                        new OperatorSkillBlackboardValue("base_attack_time", -0.5f),
                        new OperatorSkillBlackboardValue("atk", 0.25f),
                        new OperatorSkillBlackboardValue("attack@atk_scale_ol", 0.8f)));

                Assert.That(skill.HasOfficialSkillData, Is.True);
                Assert.That(skill.SkillPointCost, Is.EqualTo(29f));
                Assert.That(skill.SkillPoints, Is.EqualTo(15f));
                Assert.That(skill.ActiveDurationSeconds, Is.EqualTo(25f));
                Assert.That(skill.OverloadBurstCount, Is.EqualTo(4));

                skill.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        1,
                        8,
                        "skchr_wisdel_2",
                        "饱和复仇",
                        string.Empty,
                        string.Empty,
                        28f,
                        15f,
                        25f,
                        OperatorSkillPointRecoveryType.Natural,
                        "INCREASE_WITH_TIME",
                        new OperatorSkillBlackboardValue("base_attack_time", -0.6f),
                        new OperatorSkillBlackboardValue("atk", 0.3f),
                        new OperatorSkillBlackboardValue("attack@atk_scale_ol", 0.9f)));

                Assert.That(
                    skill.HasOfficialSkillData,
                    Is.False,
                    "Missing official burst-count semantics must not reuse the previous mastery tier.");
                Assert.That(skill.OverloadBurstCount, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }


        [Test]
        public void SkadiS2_ConsumesPassiveMasteryBlackboardWithoutFakeSkillPoints()
        {
            var go = new GameObject("SkadiS2MasteryTest");
            try
            {
                go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                go.AddComponent<CombatEntity>();
                go.AddComponent<OperatorRuntimeStats>();
                var skill = go.AddComponent<SkadiSkill1>();

                skill.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        0,
                        7,
                        "skchr_skadi_2",
                        "跃浪击",
                        string.Empty,
                        "部署后{duration}秒内攻击力+{atk}",
                        0f,
                        0f,
                        0f,
                        OperatorSkillPointRecoveryType.None,
                        "PASSIVE",
                        new OperatorSkillBlackboardValue("atk", 1.2f),
                        new OperatorSkillBlackboardValue("duration", 25f)));

                Assert.That(skill.HasOfficialSkillData, Is.True);
                Assert.That(skill.SkillPointCost, Is.EqualTo(0f));
                Assert.That(skill.ActiveDurationSeconds, Is.EqualTo(25f));
                Assert.That(skill.OfficialAttackBonus, Is.EqualTo(1.2f).Within(0.001f));
                Assert.That(skill.IsActive, Is.False, "EditMode data sync must not start deployment coroutines.");

                skill.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        3,
                        10,
                        "skchr_skadi_2",
                        "跃浪击",
                        string.Empty,
                        string.Empty,
                        0f,
                        0f,
                        0f,
                        OperatorSkillPointRecoveryType.None,
                        "PASSIVE",
                        new OperatorSkillBlackboardValue("atk", 1.7f),
                        new OperatorSkillBlackboardValue("duration", 30f)));

                Assert.That(skill.HasOfficialSkillData, Is.True);
                Assert.That(skill.ActiveDurationSeconds, Is.EqualTo(30f));
                Assert.That(skill.OfficialAttackBonus, Is.EqualTo(1.7f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SkadiS3_ConsumesAtkDefMaxHpAndDoesNotInheritMissingTierFields()
        {
            var go = new GameObject("SkadiS3MasteryTest");
            try
            {
                go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                go.AddComponent<CombatEntity>();
                go.AddComponent<OperatorRuntimeStats>();
                var skill = go.AddComponent<SkadiSkill2>();

                skill.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        0,
                        7,
                        "skchr_skadi_3",
                        "涌潮悲歌",
                        string.Empty,
                        string.Empty,
                        90f,
                        60f,
                        41f,
                        OperatorSkillPointRecoveryType.Natural,
                        "INCREASE_WITH_TIME",
                        new OperatorSkillBlackboardValue("atk", 1.0f),
                        new OperatorSkillBlackboardValue("def", 1.0f),
                        new OperatorSkillBlackboardValue("max_hp", 1.0f)));

                Assert.That(skill.HasOfficialSkillData, Is.True);
                Assert.That(skill.SkillPointCost, Is.EqualTo(90f));
                Assert.That(skill.SkillPoints, Is.EqualTo(60f));
                Assert.That(skill.ActiveDurationSeconds, Is.EqualTo(41f));
                Assert.That(skill.OfficialAttackBonus, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(skill.OfficialDefenseBonus, Is.EqualTo(1.0f).Within(0.001f));
                Assert.That(skill.OfficialMaxHealthBonus, Is.EqualTo(1.0f).Within(0.001f));

                skill.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        3,
                        10,
                        "skchr_skadi_3",
                        "涌潮悲歌",
                        string.Empty,
                        string.Empty,
                        90f,
                        70f,
                        50f,
                        OperatorSkillPointRecoveryType.Natural,
                        "INCREASE_WITH_TIME",
                        new OperatorSkillBlackboardValue("atk", 1.3f),
                        new OperatorSkillBlackboardValue("def", 1.3f),
                        new OperatorSkillBlackboardValue("max_hp", 1.3f)));

                Assert.That(skill.HasOfficialSkillData, Is.True);
                Assert.That(skill.SkillPoints, Is.EqualTo(70f));
                Assert.That(skill.ActiveDurationSeconds, Is.EqualTo(50f));
                Assert.That(skill.OfficialAttackBonus, Is.EqualTo(1.3f).Within(0.001f));
                Assert.That(skill.OfficialDefenseBonus, Is.EqualTo(1.3f).Within(0.001f));
                Assert.That(skill.OfficialMaxHealthBonus, Is.EqualTo(1.3f).Within(0.001f));

                skill.ApplyMasterySnapshot(
                    new OperatorSkillMasterySnapshot(
                        2,
                        9,
                        "skchr_skadi_3",
                        "涌潮悲歌",
                        string.Empty,
                        string.Empty,
                        90f,
                        66f,
                        47f,
                        OperatorSkillPointRecoveryType.Natural,
                        "INCREASE_WITH_TIME",
                        new OperatorSkillBlackboardValue("atk", 1.2f),
                        new OperatorSkillBlackboardValue("def", 1.2f)));

                Assert.That(
                    skill.HasOfficialSkillData,
                    Is.False,
                    "Missing max_hp must invalidate the tier instead of inheriting M3.");
                Assert.That(skill.OfficialMaxHealthBonus, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static OperatorSkillMasterySnapshot BuildSnapshot(
            int mastery,
            int sourceLevel,
            float spCost,
            float initSp)
        {
            return new OperatorSkillMasterySnapshot(
                mastery,
                sourceLevel,
                "skchr_chen_2",
                "赤霄·拔刀",
                "3-12",
                string.Empty,
                spCost,
                initSp,
                0f,
                OperatorSkillPointRecoveryType.Attack,
                "INCREASE_WHEN_ATTACK",
                new OperatorSkillBlackboardValue("atk_scale", 4.1f + mastery * 0.3f),
                new OperatorSkillBlackboardValue("max_target", mastery == 3 ? 7f : 6f));
        }

        private static string BuildSkillLevels(string name, int baseCost, string spType)
        {
            var result = "[";
            for (var level = 1; level <= 10; level++)
            {
                if (level > 1)
                    result += ",";
                result +=
                    "{\"name\":\"" + name + "\"," +
                    "\"rangeId\":\"range_test\"," +
                    "\"description\":\"test\"," +
                    "\"duration\":" + level + "," +
                    "\"spData\":{\"spType\":\"" + spType + "\",\"spCost\":" + (baseCost + level) +
                    ",\"initSp\":" + level + "}," +
                    "\"blackboard\":[{\"key\":\"atk_scale\",\"value\":" +
                    level + "}]}";
            }
            return result + "]";
        }

        [Test]
        public void RangeCatalog_UsesOfficialGridShapes_AtAuthoredWorldScale()
        {
            Assert.That(
                OperatorRangeCatalog.GetForwardReachRatio("3-6", "3-2"),
                Is.EqualTo(1.4f).Within(0.001f));
            Assert.That(
                OperatorRangeCatalog.GetForwardReachRatio("3-6", "4-1"),
                Is.EqualTo(1.8f).Within(0.001f));

            Assert.That(OperatorRangeCatalog.TryGet("3-12", out var chenS2), Is.True);
            Assert.That(
                chenS2.Contains(
                    Vector3.zero,
                    Vector3.forward,
                    new Vector3(0f, 0f, 6f),
                    7f),
                Is.True,
                "The far center lane of 3-12 must be targetable.");
            Assert.That(
                chenS2.Contains(
                    Vector3.zero,
                    Vector3.forward,
                    new Vector3(2f, 0f, 6f),
                    7f),
                Is.False,
                "3-12 narrows at long range and must reject a far side cell.");
            Assert.That(
                chenS2.Contains(
                    Vector3.zero,
                    Vector3.forward,
                    new Vector3(2f, 0f, 2f),
                    7f),
                Is.True,
                "3-12 includes the near side cell.");

            Assert.That(OperatorRangeCatalog.TryGet("x-1", out var jueying), Is.True);
            Assert.That(
                jueying.Contains(
                    Vector3.zero,
                    Vector3.forward,
                    new Vector3(0f, 0f, -4f),
                    5f),
                Is.True,
                "x-1 is an all-around diamond and includes the rear axis.");
            Assert.That(
                jueying.Contains(
                    Vector3.zero,
                    Vector3.forward,
                    new Vector3(4f, 0f, 4f),
                    5f),
                Is.False,
                "x-1 is a diamond rather than a full square/radius.");
        }

        [Test]
        public void CombatStats_ResolvesModifierLayersInDeclaredOrder()
        {
            var go = new GameObject("ModifierLayerOrderTest");
            try
            {
                var stats = go.AddComponent<CombatStats>();
                var meta = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.MetaProgression);
                var run = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.RunPermanent);
                var collectible = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.CollectibleEquipment);
                var temporary = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.Temporary);

                meta.Set(CombatStatType.Attack, "meta", flat: 20f);
                run.Set(CombatStatType.Attack, "run", additivePercent: 0.5f);
                collectible.Set(CombatStatType.Attack, "relic", flat: 10f);
                temporary.Set(CombatStatType.Attack, "temp", additivePercent: -0.1f);

                Assert.That(
                    stats.Resolve(CombatStatType.Attack, 100f),
                    Is.EqualTo(171f).Within(0.001f),
                    "(100 + 20) * 1.5, then +10, then *0.9 must respect layer order.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CombatStatModifierBucket_SetIsIdempotentForStateReplay()
        {
            var go = new GameObject("ModifierBucketReplayTest");
            try
            {
                var stats = go.AddComponent<CombatStats>();
                var run = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.RunPermanent);

                run.Set(
                    CombatStatType.Skill1RangeMultiplier,
                    "chen_skill1_range",
                    additivePercent: 0.2f);
                run.Set(
                    CombatStatType.Skill1RangeMultiplier,
                    "chen_skill1_range",
                    additivePercent: 0.2f);

                Assert.That(
                    stats.Resolve(CombatStatType.Skill1RangeMultiplier, 1f),
                    Is.EqualTo(1.2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuntimeStats_ComposesGlobalAndSlotRangeLayers()
        {
            var go = new GameObject("ModifierRangeCompositionTest");
            try
            {
                go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                go.AddComponent<CombatEntity>();
                var runtime = go.AddComponent<OperatorRuntimeStats>();
                runtime.ConfigureBase(
                    new OperatorBaseStats(
                        1000f,
                        100f,
                        50f,
                        10f,
                        1f,
                        10f,
                        8f),
                    refillHealth: true);

                var run = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.RunPermanent);
                var collectible = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.CollectibleEquipment);
                run.Set(
                    CombatStatType.Skill1RangeMultiplier,
                    "slot1",
                    additivePercent: 0.25f);
                collectible.Set(
                    CombatStatType.SkillRange,
                    "global_range",
                    additivePercent: 0.20f);

                Assert.That(runtime.GetSkillRangeMultiplier(1), Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(runtime.GetSkillRangeMultiplier(2), Is.EqualTo(1.2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuntimeStats_MaxHealthModifierRefreshHealsOnlyAddedCapacity()
        {
            var go = new GameObject("ModifierMaxHealthTest");
            try
            {
                var health = go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                go.AddComponent<CombatEntity>();
                var runtime = go.AddComponent<OperatorRuntimeStats>();
                runtime.ConfigureBase(
                    new OperatorBaseStats(1000f, 100f, 50f, 10f, 1f),
                    refillHealth: true);
                health.SetCurrentHealth(400f);

                var collectible = CombatStatModifierBucket.GetOrCreate(
                    go,
                    CombatStatModifierLayer.CollectibleEquipment);
                collectible.Set(
                    CombatStatType.MaxHealth,
                    "hp_relic",
                    additivePercent: 0.20f);
                runtime.RefreshResolvedMaxHealthFromModifierChange(healAddedCapacity: true);

                Assert.That(health.MaxHealth, Is.EqualTo(1200f).Within(0.001f));
                Assert.That(health.CurrentHealth, Is.EqualTo(600f).Within(0.001f));

                collectible.ClearSource("hp_relic");
                runtime.RefreshResolvedMaxHealthFromModifierChange(healAddedCapacity: true);

                Assert.That(health.MaxHealth, Is.EqualTo(1000f).Within(0.001f));
                Assert.That(health.CurrentHealth, Is.EqualTo(600f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuntimeStats_ProgressionChangePreservesHealthRatio()
        {
            var go = new GameObject("OperatorProgressionTest");
            try
            {
                go.AddComponent<Health>();
                go.AddComponent<ArknightsACT.Combat.Status.StatusController>();
                go.AddComponent<CombatStats>();
                var entity = go.AddComponent<CombatEntity>();
                var runtime = go.AddComponent<OperatorRuntimeStats>();

                runtime.ConfigureBase(
                    new OperatorBaseStats(1000f, 100f, 100f, 0f, 1f),
                    refillHealth: true);
                entity.Health.SetCurrentHealth(400f);

                runtime.ConfigureBasePreserveHealthRatio(
                    new OperatorBaseStats(2000f, 200f, 200f, 10f, 1f));

                Assert.That(entity.Health.MaxHealth, Is.EqualTo(2000f));
                Assert.That(entity.Health.CurrentHealth, Is.EqualTo(800f).Within(0.01f));
                Assert.That(runtime.BaseAttack, Is.EqualTo(200f));
                Assert.That(runtime.BasePhysicalDefense, Is.EqualTo(200f));
                Assert.That(runtime.BaseArtsResistance, Is.EqualTo(10f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
