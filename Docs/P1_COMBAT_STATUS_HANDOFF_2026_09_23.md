# P1 Combat / Defense / Status Handoff — 2026-09-23

> 当前状态：代码主体已落地，FolderBridge source smoke 通过；仍需 Unity Editor 真编译、EditMode Test Runner 与 Play Mode 实机验收。

本文件是 P1 战斗底层的当前 source of truth。后续角色、敌人、藏品与异常状态优先遵循这里，不再回到“角色自己算防御 / Status enum + timer / 防御折算成伤害倍率”的旧方案。

---

## 1. 本轮已经落地

- Physical / Arts / True 三类伤害正式结算
- PhysicalDefense / ArtsResistance
- 每次攻击独立的 Penetration
- 敌方 DEF 团队 Aura
- 数据驱动 Status
- Action Block / Interrupt
- Status Stack / Reaction / Resistance 扩展点
- Burn DOT 统一回到 DamageSystem
- DamageTags
- DamageResult 分阶段数据
- Schwarz 破甲箭头
- 旧防御藏品正式迁移
- EditMode 回归测试扩展

---

## 2. 正式伤害规则

### Physical

流程：

Target local DEF
→ attacker / owner target-stat aura
→ per-hit penetration
→ Effective DEF

公式：

Physical Damage = Max(PreMitigationDamage × 5%, PreMitigationDamage - EffectiveDEF)

最低物伤比例固定为 5%。

### Arts

流程：

Target local RES
→ attacker / owner target-stat aura
→ per-hit RES ignore
→ Effective RES

公式：

Arts Damage = PreMitigationDamage × (1 - EffectiveRES / 100)

当前 RES Clamp：-100 ~ 95，因此允许负 RES 放大法术伤害。

### True

真实伤害不吃 DEF / RES，但仍可受到明确的 Outgoing / Incoming Damage Modifier 影响。

---

## 3. DamageSystem 当前流水线

DamageContext
→ Target / ProcGeneration / Team 校验
→ IDamageGate
→ Raw Damage
→ Source outgoing IDamageModifier
→ Owner outgoing IDamageModifier
→ Target CombatStats
→ Source / Owner ICombatTargetStatModifier
→ Source / Owner IDamagePenetrationModifier
→ Physical DEF / Arts RES mitigation
→ Target incoming IDamageModifier
→ Health.TakeDamage
→ DamageResult
→ CombatEntity.Damaged
→ DamageSystem.DamageApplied

角色代码不应再自行复制通用 DEF / RES 公式。

---

## 4. CombatStats

新增核心文件：

- Assets/_Game/Scripts/Combat/CombatStats.cs
- Assets/_Game/Scripts/Combat/CombatStatType.cs
- Assets/_Game/Scripts/Combat/CombatStatModifier.cs
- Assets/_Game/Scripts/Combat/ICombatStatModifier.cs

第一批正式属性：

- PhysicalDefense
- ArtsResistance
- MoveSpeedMultiplier
- AttackSpeedMultiplier

CombatEntity 现在统一拥有 Health / StatusController / CombatStats，并保留旧 Scene runtime fallback。

---

## 5. 三种减防语义必须区分

### 5.1 Target Status：DefenseDown

例如 Schwarz 破甲。它修改目标自己的当前 DEF，后续所有攻击都能看到。

入口：

- CombatStatusIds.DefenseDown

### 5.2 Team / Relic Aura

例如“所有敌方单位 DEF -12%”。

它不是 PhysicalDamagePercent，也不是 Penetration。

入口：

- ICombatTargetStatModifier
- CollectibleEffectType.EnemyPhysicalDefensePercent

当前 118 件 Runtime Catalog 已迁移：

- relic_59 锉刀：敌方 DEF -12%
- relic_60 废铁陷阱：敌方 DEF -15%
- relic_61 酸液源石虫：敌方 DEF -21%
- rogue_6_relic_legacy_86 迷迭香之拥：敌方 DEF -30%

### 5.3 Per-hit Penetration

只属于某一发攻击，不永久修改目标属性。

当前支持：

- PhysicalDefenseIgnoreFlat
- PhysicalDefenseIgnorePercent
- ArtsResistanceIgnoreFlat
- ArtsResistanceIgnorePercent

核心：

- DamagePenetration
- IDamagePenetrationModifier

---

## 6. DamageContext / DamageResult

DamageContext 新增：

- DamagePenetration Penetration
- DamageTags Tags

当前 Tags：

- BasicAttack
- Skill
- DamageOverTime
- Environment
- SecondaryProc

DamageResult 保留 Applied / Damage / Killed，并新增：

- RawDamage
- PreMitigationDamage
- MitigatedDamage
- FinalDamage
- EffectiveDefense
- EffectiveResistance

Damage 是 FinalDamage 的兼容 alias。

---

## 7. Status 系统

旧版本只有 CombatStatusType + Dictionary timer。

现在结构为：

- CombatStatusDefinition
- StatusApplicationContext
- ActiveStatusInstance
- StatusController
- CombatStatusCatalog
- StatusResistanceProfile

旧 Shock / Burn / Ink API 仍保留兼容，新代码优先使用字符串 Stable ID / Catalog Definition。

---

## 8. Status 当前通用能力

### Stat Modifier

可表达：

- Cold
- Slow
- AttackSlow
- DefenseDown
- ResistanceDown

### Action Block

CombatActionMask：

- Movement
- Dash
- BasicAttack
- Skill
- Interaction
- Targeting

### Interrupt

Block 与 Interrupt 分开。

Freeze 当前：

Block：Movement / Dash / BasicAttack / Skill / Interaction

Interrupt：Movement / Dash / BasicAttack / Skill

### Periodic Damage

Burn 已迁入 Status，不再由 LevelUpgradeInventory 自己跑 Burn Coroutine。

### Damage Modifier

Status 可直接表达：

- Fragile：Incoming Damage +X%
- Weaken：Outgoing Damage -X%

### Reaction

当前验证：

Cold + Cold → consume Cold → Freeze

---

## 9. 当前 Status IDs

- shock
- burn
- ink
- cold
- freeze
- disarm
- silence
- stun
- root
- slow
- attack_slow
- defense_down
- resistance_down
- fragile
- weaken

以后新增大多数异常状态应优先添加 Definition，而不是继续扩大 switch。

---

## 10. 当前验证状态规则

Cold：
- 4s
- MoveSpeed -20%
- AttackSpeed -30%
- 第二次 Cold 触发 Freeze

Freeze：
- 2.5s
- 禁止移动 / 冲刺 / 普攻 / 技能 / 交互
- 中断移动 / 冲刺 / 普攻 / 技能

Disarm：
- 4s
- 只禁止并中断 BasicAttack

Burn：
- 2.05s
- Tick 0.65s
- Arts
- DamageTags = DamageOverTime | SecondaryProc
- Application magnitude = 每跳伤害

DefenseDown：
- magnitude 使用比例
- 0.20 = DEF -20%

ResistanceDown：
- magnitude 使用 RES 点数
- 20 = RES -20

Fragile：
- magnitude 0.25 = Incoming Damage +25%

Weaken：
- magnitude 0.25 = Outgoing Damage -25%

---

## 11. Stack / Resistance

StackPolicy 已预留：

- RefreshDuration
- ExtendDuration
- StackAndRefresh
- ReplaceIfStronger
- IndependentBySource

StatusResistanceProfile 支持：

- 按 Status ID 免疫 / 时长倍率
- 按 Status Tag 免疫 / 时长倍率
- ConfigureIdRule(...)
- ConfigureTagRule(...)
- ClearRules()

因此敌人 Factory / 数据层可以直接配置，不必依赖 Inspector 私有数组。

Boss 后续可以配置 HardCrowdControl immune、Freeze duration multiplier、Disarm immune 等，不要写 if(isBoss)。

目前 Boss 最终抗性表尚未配置。

---

## 12. Action Block 接入范围

### Player

已接：

- PlayerMotor25D
- PlayerMotor2D
- PlayerDashController
- PlayerAttackController
- PlayerSkillController
- ScavengingInventory25D
- CityFacilityController
- RogueliteStageRuntimeController
- PlayableOperatorSwitchController

因此 Freeze / Stun 时不能通过搜刮、设施、撤离、下一层、切角色绕过控制。

### Enemy

已接：

- PrototypeEnemyCombatBrain25D
- TreasureMonsterBrain25D
- PrototypeEnemyCombatBrain2D
- Prototype25DEnemyBrain

Slow / Cold 会影响移动速度；AttackSlow / Cold 会影响攻击节奏；Freeze / Root / Disarm 按 ActionMask 阻断对应行为。

2026-09-23 后续收口：PlayerMotor25D / PlayerMotor2D / PlayerDashController 也实现了 ICombatActionInterruptHandler。Movement interrupt 会即时清水平/平面速度；Dash interrupt 会停止当前 Dash coroutine、恢复 2D gravity、清 IsDashing 并发出 DashEnded，避免硬控后残留滑行或卡冲刺状态。

---

## 13. Skill Interrupt

新增 IPlayerSkillInterruptible。

覆盖：

- ChenSkill1
- ChenSkill2
- SchwarzSkill1
- SchwarzSkill2
- FrostNovaSkill1
- FrostNovaSkill2

规则：

Block = 不允许新动作

Interrupt = 中断当前动作

持续 Buff 与“正在起手施法”分开处理，不会因为一次硬控就无条件删除所有持续 Buff。

---

## 14. 角色切换与 Status

PlayerRuntimeContext 切角色时现在会复制当前 Active Status。

因此：

切角色 != 免费清 Debuff

Cold / Burn / Freeze 等剩余时长会跟随当前玩家。

2026-09-23 收口：CopyActiveTo 不再通过 target.Apply() 重放状态，而是直接复制 ActiveStatusInstance 语义。这样不会因为目标角色自身的 StatusResistanceProfile 再次判定而“切人解控”，同时保留原剩余时长、Stack、Magnitude、Source/Owner 关系和 DOT NextTickAt。

---

## 15. Schwarz 破甲箭头

新增：

Assets/_Game/Scripts/Gameplay/Characters/SchwarzArmorBreakTalent.cs

当前按精二天赋 / Rank III 技能参数接入：

普通：
- 20% proc

暮眼锐瞳（Slot1 / 原 S2）：
- 50% proc

战术的终结（Slot2 / 原 S3）：
- 100% proc

触发：
- 本次 BasicAttack damage ×1.6
- 目标 DefenseDown 20%
- 持续 5s

DefenseDown 走通用 Status，不在 DamageSystem 里写 Schwarz 特例。

新建 Schwarz 由 Factory 挂组件；旧场景由 SchwarzSkill1 Awake 做缺失 fallback。

SchwarzArmorBreakTalent 已改为惰性获取 SchwarzSkill1 / SchwarzSkill2，避免 Factory 动态 AddComponent 顺序导致技能触发率永远停在基础 20%。Factory 也改为 get-or-add，避免重复挂 Talent。

---

## 16. 藏品 Defense 迁移

旧 importer 曾因为项目没有 DEF，把“我方防御力 +X%”折算成 IncomingDamagePercent -X%。

现在正式改成：

PhysicalDefensePercent +X%

现有 Runtime Catalog 的 8 条我方 DEF 藏品已迁移。

旧“敌方 DEF -X%”曾折成 PhysicalDamagePercent +X%，现在正式改成：

EnemyPhysicalDefensePercent -X%

当前 4 条已迁移。

“敌方攻击力 -X%”仍保持 IncomingDamagePercent 兼容，因为它本质不是 DEF。

---

## 17. Prototype 基础 DEF / RES

为了让当前场景立即能验证公式，暂时提供轻量 baseline：

Player：
- DEF 1
- RES 0

Enemy 25D：
- FastMelee DEF 0.5
- Ranged DEF 0.75 / RES 5
- Melee DEF 1
- 高血量 Heavy fallback DEF 4 / RES 10

这些不是最终平衡值。

下一轮应由正式角色 / 敌人数据资产配置。

---

## 18. 回归测试

已扩展：

- Assets/_Game/Tests/EditMode/DamageSystemTests.cs
- Assets/_Game/Tests/EditMode/StatusControllerTests.cs

新增：

- SchwarzArmorBreakTalentTests.cs
- ScavengingCombatStatMigrationTests.cs

覆盖：

- Physical DEF
- 5% minimum physical damage
- Arts RES
- negative RES
- True Damage
- DamageResult phases
- Penetration
- Target DEF Aura
- Cold stat modifier
- Cold → Freeze
- Disarm
- DefenseDown magnitude
- ResistanceDown magnitude
- Fragile
- Status transfer
- Status transfer bypasses destination resistance / keeps existing hard CC
- StatusResistance ID immunity
- StatusResistance Tag duration multiplier
- Freeze interrupt dispatch
- Freeze immunity keeps Cold instead of错误消费 Reaction
- Burn Definition
- Schwarz armor-break
- 4 enemy-DEF relic migrations

---

## 19. P1 Editor 调试器

新增：

- Assets/_Game/Editor/P1CombatStatusDebuggerWindow.cs
- 菜单：ArknightsACT / 战斗调试 / P1 状态调试器

Play Mode 可选择：

- Active Player
- Nearest Enemy
- 当前 Selection 下的 CombatEntity

可直接验证：

- Physical / Arts / True
- Raw / PreMitigation / Mitigated / Final
- Effective DEF / RES
- Cold / Cold Again → Freeze
- Freeze
- Disarm
- Stun
- Root
- Burn
- DefenseDown
- ResistanceDown
- Fragile
- Weaken
- Clear All Statuses

面板同时显示：

- HP
- 当前 DEF / RES
- MoveSpeedMultiplier
- AttackSpeedMultiplier
- BlockedActions
- Active Status / Remaining / Stack / Magnitude

调试 Active Player 自身伤害或 Burn 时使用 world/null source，仅用于绕开正式 friendly-fire 拒绝以便测试，不修改 DamageSystem 正式规则。

---

## 20. 当前验证状态

FolderBridge：

- source safe-build：pass
- workspace smoke：pass
- issues：0

但 FolderBridge 是 validation-only。

它不是 Unity C# 真编译，也没有真实运行 EditMode tests。

---

## 21. Unity 打开后优先验证

1. Unity recompile 完成，Console 0 compile errors。
2. Test Runner 跑全部 EditMode。
3. 重点看新增 DamageSystem / StatusController / Schwarz / Scavenging tests。
4. Play Mode 验证：
   - Physical / Arts / True 差异
   - Cold 降移速 / 攻速
   - Cold 二次叠加 Freeze
   - Freeze 中断攻击 / 技能
   - Disarm 只封普攻
   - Burn 三跳
   - Freeze 不能搜刮 / 撤离 / 切角色
   - 宝箱怪 / 普通敌人同样受控制
   - Schwarz 普攻破甲
   - Schwarz S2 50%
   - Schwarz S3 100%

---

## 22. P1 剩余工作

### 必做验证

- Unity 真编译
- EditMode tests
- Play Mode
- 检查旧 Scene / Prefab 序列化兼容

### 数值正式化

- Chen / Schwarz / FrostNova 正式 DEF / RES
- 当前敌人正式 DEF / RES
- Boss 抗性表
- Status 时长平衡
- prototype baseline 迁到正式数据资产

### 表现层

- Cold / Freeze / Burn / Disarm 状态图标
- Freeze 视觉
- Burn 视觉
- Debuff HUD
- Damage debug panel：Raw / DEF / RES / Final

### 后续迁移

- 敌方 RES Aura
- Schwarz 模组版本
- Schwarz 第二天赋“交叉火力”
- 其它角色天赋迁到 DamageTags / Status / CombatStats

---

## 23. 后续开发约束

1. 不在角色脚本复制 DEF / RES 公式。
2. 不用 SourceId.Contains 判断普攻 / 技能 / DOT，优先 DamageTags。
3. 不把 DEF Down 当 PhysicalDamagePercent。
4. 不把 Penetration 当目标 Debuff。
5. 新异常状态优先 Definition，不新增巨型 switch。
6. 控制必须同时考虑 Block 和 Interrupt。
7. 玩家 / 敌人统一走 CombatEntity + CombatStats + StatusController。
8. Boss 免控走 StatusResistanceProfile。
9. DOT 必须回 DamageSystem。
10. 切角色不能清掉本应持续的状态。

---

## 一句话状态

P1 已经从“有三种 DamageType、但没有正式防御与异常层”推进到“统一 DEF / RES / True、穿透、团队减防 Aura、数据驱动 Status、控制/中断、DOT、Schwarz 破甲、藏品迁移和回归测试均已落地”。

下一步重点不是再推翻架构，而是 Unity 真编译 + 测试 + 实机验收 + 正式数值 / 视觉。
