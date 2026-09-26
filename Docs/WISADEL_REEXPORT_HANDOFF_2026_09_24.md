# Wisadel 重导出与角色导入流程 Handoff

更新时间：2026-09-24

项目：`D:\WorkSpace\ArknightsACT`  
本轮素材源：`D:\Ark\_Unpacked\wisdel`

相关文档：
- `Docs/CHARACTER_IMPORT_WORKFLOW.md`
- `Docs/CHARACTER_IMPORT_PIPELINE_REVIEW_2026_09_24.md`
- `Docs/WISADEL_IMPORT_REVIEW_SUMMARY_2026_09_24.md`
- `Docs/WISADEL_FX_BINDING_2026_09_24.md`

---

## 1. 本轮目标

本轮重点不是重新实现 Wisadel Gameplay，而是完成新版素材重导入后的 FX 关系校正，并把角色资源接入流程继续通用化。

核心要求：

1. 以新版导出 JSON / timing metadata 为准，不再只凭文件名猜绑定。
2. 根 tag 为 `*_trail` 的 FX 一律视为 **Projectile Flight**。
3. Trail 必须从发射点飞向目标/落点，禁止挂在角色身上作为 Start 或 Hit。
4. 复合 FX 必须保留全部层，例如 `hit + hit_02 + hit_03`、`buff_b + buff_f`。
5. 最终 offset / scale 仍由用户在游戏内自行微调。
6. 出生区域保留公共无限血假人，便于一进场测试普攻、技能和弹道。

---

## 2. 新版 Wisadel 导出包

入口：

`D:\Ark\_Unpacked\wisdel\unity_import_manifest.json`

本次 manifest 生成时间：

`2026-09-24T05:58:34.043Z`

包中包含 default、`game#9`、`sale#14`、combat/build Spine、技能图标、HUD、voice/sound、79 组 FX，以及每组 FX 自己的 `timing.json`。

当前工程正式接入：
- `default`
- `game#9`

`sale#14` 当前仍排除。

### 重要限制

本次实际导出的：

`skillTiming.effectSchedule = []`

因此当前工程不能伪造不存在的 `releaseDelaySec`。

当前可以可靠使用：
- FX 根 tag
- 原始 prefab 路径
- `timing.json.fps`
- renderDuration
- particle / animation metadata
- audio event 语义

精确释放帧仍需结合 Gameplay 事件与实际画面复核。

---

## 3. Trail 硬规则

根 tag 中的 `*_trail` 统一解释为 **Projectile Flight**。

例如：
- `wisdel_attack_01_trail`
- `skill_03_trail`
- `skill_03_trail_game#9`

正确绑定：

`发射点 -> 目标/落点`

禁止：
- `SpawnOnActor`
- 固定为角色 Transform child
- 当 Start 播放
- 当 Hit 播放

注意：`timing.json -> particles[].path` 内部节点也可能出现 `trail` 字样，那只是 prefab 内部节点名。只有 FX 根 tag 用于判定整个 FX 的 Gameplay 类型。

---

## 4. 当前普攻 FX

普通 Start：
- `wisdel_attack_a_start`
- `wisdel_attack_b_start`
- `wisdel_attack_c_start`

向下方向 Start：
- `wisdel_attack_down_a_start`
- `wisdel_attack_down_b_start`
- `wisdel_attack_down_c_start`

三段连击按 A/B/C 选择。Down variant 根据角色方向判断，不可用时回退普通 A/B/C。

Basic Trail：

`wisdel_attack_01_trail`

流程：

`AttackStarted -> Resolve Target -> StartTrailFlight -> Target`

有目标时飞向目标；没有目标时沿当前朝向飞向 miss destination。

Basic Hit 同时播放：
- `wisdel_attack_01_hit`
- `wisdel_attack_01_hit_02`

不能再只播第一层。

---

## 5. 当前 S2 FX

当前 Gameplay Slot 1 = 原作 S2。

Start：
`skill_02_start`

Buff：
- `skill_02_buff`
- `skill_02_buff_02`

Hit：
- `skill_02_hit`
- `skill_02_hit_02`

Overload：
`skill_02_overload_start`

Overload 当前仅导入并绑定到 Controller 的显式入口。简化 Gameplay 尚未还原原作 Overload 条件，因此禁止每次 S2 都强行播放。

新版导出中当前 S2 没有根级 `*_trail`，因此不要人为给 S2 制造 projectile FX。

---

## 6. 当前 S3 FX

当前 Gameplay Slot 2 = 原作 S3。

Start：
- default：`skill_03_start`
- game#9：`skill_03_start_game#9`

Up / Down：
- `skill_03_up_start`
- `skill_03_down_start`

当前含义：
- 技能进入：Start + UpStart
- 技能结束：DownStart

S3 Trail：
- default：`skill_03_trail`
- game#9：`skill_03_trail_game#9`

流程：

`S3 Start -> ProjectileLaunched -> Trail Flight -> Impact -> Hit Layers`

每个 pulse 发射一条独立 Trail，并飞向本次 impact center。

S3 Hit：

default：
- `skill_03_hit`
- `skill_03_hit_02`
- `skill_03_hit_03`

game#9：
- `skill_03_hit_game#9`
- `skill_03_hit_02_game#9`
- `skill_03_hit_03_game#9`

三层在 Impact 同时播放。

S3 Persistent Buff：

公共：
- `skill_03_buff_b`
- `skill_03_buff_f`
- `skill_03_buff_02_b`

Front02：
- default：`skill_03_buff_02_f`
- game#9：`skill_03_buff_02_f_game#9`

这些是角色持续状态表现，S3 开始创建，S3 结束统一清理。

---

## 7. 当前暂不绑定的 FX

原作 S1 当前不在 playable loadout：
- `skill_01_start`
- `skill_01_start_02`
- `skill_01_trail`
- `skill_01_hit`
- `skill_01_hit_02`
- `skill_01_hit_03`

未来接 S1 时，`skill_01_trail` 同样必须走 projectile flight。

Token / Talent / Bomb / Camouflage：
- `wisdel_birth_01_start`
- `wisdel_bomb_01_hit`
- `wisdel_bomb_buff_01`
- `wisdel_camouflage_buff_01`

这些当前不要误塞进普通普攻/S2/S3，等对应 Gameplay 对象和生命周期实现后再绑定。

---

## 8. game#9 替换原则

不要因为当前 skin 是 game#9 就给所有 FX 自动拼 `_game#9`。

只有导出包实际存在专属版本时才替换。

当前确认的 game#9 专属 Gameplay FX：
- `skill_03_start_game#9`
- `skill_03_trail_game#9`
- `skill_03_hit_game#9`
- `skill_03_hit_02_game#9`
- `skill_03_hit_03_game#9`
- `skill_03_buff_02_f_game#9`

其它层继续复用 common 版本。

---

## 9. FX FPS

每个 `effects/frames/<tag>/timing.json` 包含真实导出 FPS。

Wisadel 当前多组 FX 为 `fps = 15`。

旧 importer 固定 30 FPS 会导致效果快一倍。

当前导入规则：
1. 优先读取 `timing.json.fps`
2. metadata 缺失或无效时才 fallback 30 FPS
3. Spine 动作速度与 FX FPS 分开处理

不要因为角色动作使用 2x，就把 FX 也乘 2。

---

## 10. 当前工程中的 Wisadel FX

目录：

`Assets/_Game/Art/FX/Extracted/Wisadel/Prefabs`

当前已有 **32 个白名单 FX Prefab**，覆盖 Basic、S2、S3 及 game#9 当前 Gameplay 所需层。

新版重导出已经实际进入工程资产。

---

## 11. 重导出迁移

文件：

`Assets/_Game/Editor/WisadelReexportImportMigration20260924.cs`

当前 Migration ID：

`2026-09-24-wisadel-basemotion-fullsource-v5`

本轮迁移调用：

`WisadelLocalAssetBootstrap.ImportOriginalAndGame9(force: true, showDialog: false)`

原因是本次为全新重导出，同名旧 Frames / AnimationClip / AnimatorController / Prefab 不能继续因为“已经存在”而跳过。

本次脚本 reload 后强制完整重建一次；之后仍回到正常增量刷新。

---

## 12. Wisadel Runtime Controller

主文件：

`Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelExtractedFxController.cs`

当前已支持：
- 普攻 A/B/C Start
- 普攻 Down A/B/C Start
- Basic Trail world-space projectile
- Basic Hit + Hit02
- S2 Start
- S2 Buff + Buff02
- S2 Hit + Hit02
- S2 Overload 显式入口
- S3 Start / UpStart / DownStart
- S3 projectile Trail
- S3 Hit + Hit02 + Hit03
- S3 persistent buff 四层
- default / game#9 资源替换
- LEFT / RIGHT offset
- FX enable/disable

### FX 调参面板

当前面板已经取消 Runtime Preview/Spawn 职责。

现在只保存：
1. 该已绑定 FX 是否启用；
2. RIGHT X/Y offset；
3. LEFT X/Y offset。

勾选 FX 不会实例化或循环播放任何 Start / Buff / Hit / Trail。
Trail 仍只由真实攻击事件生成并飞向真实目标/落点；Hit 仍只在真实目标处生成。

---

## 13. Wisadel Tuning

主要文件：
- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelFxTuningProfile.cs`
- `Assets/_Game/Editor/WisadelFxTuningWindow.cs`

Profile：

`Assets/_Game/Resources/Config/WisadelFxTuningProfile.asset`

原则：
- 工程负责正确 FX 生命周期和语义
- 最终 offset / scale 用户自己微调
- LEFT / RIGHT 可分别调
- checkbox 是正式 Runtime FX enable/disable，不是 Preview

已有 `WisadelFxSlot` 顺序禁止重排。新增 slot 必须继续 append，避免旧序列化索引错位。

---

## 14. 出生区域公共测试假人

系统：

`Assets/_Game/Scripts/Gameplay/Facilities/TrainingDummyFacility.cs`

Editor 创建：

`Prototype25DProductionFactory.CreateTrainingDummy(player.transform)`

场景入口：

`PrototypeRunSceneBuilder`

对象名：

`TrainingDummy_Infinite`

假人特性：
- Enemy Team
- 走正常 DamageSystem
- 有 Hit Reaction
- 有 Damage Number
- 有血条
- 近似无限血
- 与具体角色解耦

当前位置：

**激活角色出生位置沿初始朝向前方 3.2m**

不与玩家精确重叠，避免 Collider 重叠，同时保留可观察的 projectile 飞行距离。

以后出生点变化时，假人应继续相对 active player spawn 计算，不要重新硬编码世界坐标。

---

## 15. 通用角色导入框架

公共 Editor 工具：
- `Assets/_Game/Editor/LocalOperatorAssetImportUtility.cs`
- `Assets/_Game/Editor/LocalOperatorPresentationImporter.cs`
- `Assets/_Game/Editor/LocalOperatorPackageScanner.cs`
- `Assets/_Game/Editor/LocalOperatorQuickImportWindow.cs`

推荐流程：

`Source Package -> Scan -> Import Plan -> Presentation Import -> Role Builder -> Runtime Binding -> Manual Tune`

默认解包根目录：

`D:\Ark\_Unpacked`

Quick Import 菜单：

`ArknightsACT > 角色资源 > 快速导入角色素材...`

Quick Import 只负责快速导入 Spine/build/avatar/icons/FX candidates/voice，**不应该猜 Gameplay**。

角色最终使用哪些 FX，应在正式 `LocalOperatorImportPlan` / Builder 中维护 whitelist。

---

## 16. Wisadel 正式 ImportPlan

主要文件：

`Assets/_Game/Editor/WisadelLocalAssetBootstrap.cs`

当前做法是按 Gameplay 白名单导入，而不是把 79 个 FX 全部塞到 Runtime。

不要退回“扫描整个 effects/frames 后全导入并全绑定”的方式。

---

## 17. Definition 与资源刷新

`PrototypeOperatorDefinitionAssetUtility.Ensure()` 已由“只创建不存在的 asset”改为“存在时也校验并同步 metadata / skins”。

`CurrentOperatorAssetRefreshService` 当前原则：
- 只刷新当前 operator
- 只刷新当前 skin
- 不自动 SaveScene
- 只 MarkSceneDirty

不要恢复“每次编译重导所有角色/所有皮肤”的方案。

---

## 18. Skadi 在本轮中的作用

路径：

`D:\Ark\_Unpacked\skadi`

对应：

`Assets/_Game/Editor/SkadiLocalAssetBootstrap.cs`

Skadi 主要作为第二个真实资源包验证多 skin、combat/move Spine、avatar、icons、FX、voice 与通用 ImportPlan。

不要把当前 Skadi presentation import 当成完整 Gameplay 还原。

---

## 19. 下一个 Agent 的 P0 验证

打开 Unity 后优先：

1. 确认本轮 C# 编译通过。
2. 等待 `WisadelReexportImportMigration20260924` 执行。
3. 检查 Console 中 CS / AssetDatabase / Spine importer 错误。
4. 进入 `PrototypeRun`。
5. 选 Wisadel default。
6. 测普攻 A/B/C 和 Down variant。
7. 确认 Basic Trail 从角色飞向假人。
8. 确认 Basic Hit + Hit02 在目标位置。
9. 测 S2：没有敌人时保持正常移动表现，不播放 S2 攻击动作，也不播放 `skill_02_start`。
10. S2 找到敌人后才进入 `Skill_2_Begin -> Skill_2_Loop`，同时触发 `skill_02_start`；丢失全部目标后退出攻击 pose。
11. 确认 S2 `skill_02_buff*` 只挂自身并持续到技能结束，`skill_02_hit*` 只在自动攻击实际目标处出现。
12. 测 S3：点击技能只进入 6 发弹药状态/Begin 姿态，不自动循环攻击、不自动发弹。
13. 每点击一次普攻才播放一次 `Skill_3_Loop` 并消耗 1 发；连续点击 6 次，每发都应独立播放 `skill_03_trail`，命中时播放三层 `skill_03_hit*`。
14. 确认 S3 `skill_03_buff*` 全程挂自身，最后一发耗尽后清理。
15. 确认 `TrainingDummy_Infinite` 能被普攻/S2/S3 正常锁定、命中并显示 Damage Number，同时永不死亡。
14. 切到 `game#9` 重复验证。

若仅仅是 FX 位置不准，不要重新分类绑定，直接用 Wisadel FX 偏移调参窗口。

若 Trail 方向不对，优先检查：
- `ResolveTrailOrigin`
- `OrientTrail`
- Camera plane
- Sprite 自身朝向

**不要把 Trail 改回 SpawnOnActor。**

---

## 20. 后续优化建议

P1：让 exporter 真正输出 `effectSchedule`，至少包含 action、FX tag、releaseDelaySec、duration、attach/projectile/impact semantic。

推荐 schema 明确区分：
- `actor_start`
- `actor_buff`
- `projectile_trail`
- `target_hit`
- `world_impact`
- `token`

P1：未来可考虑 `OperatorFxBindingProfile` 数据化 event、tag、mount type、skin override、offset slot、loop、projectile、composite hit group。但不要为了抽象立即推翻已经工作的 Wisadel Controller。

P2：Quick Import 增加 FX tag 搜索、checkbox、start/hit/trail/buff 分类、manifest relation preview、只导 selected tags。

---

## 21. 禁止回退项

后续 Agent 不要：
1. 把 `*_trail` 绑定到 Actor Start。
2. 把 `*_trail` 固定在角色 Transform 上。
3. 复合 Hit 只绑定第一层。
4. 漏掉 `buff_b / buff_f` 前后层。
5. 给所有 game#9 FX 自动拼后缀。
6. 把 S1 FX 塞到当前 S2/S3。
7. 把 token / bomb / camouflage FX 当普通技能 FX。
8. 在 Runtime 硬编码 `D:\Ark`。
9. 每次编译导入所有角色所有皮肤。
10. 自动 SaveScene。
11. 把 FX 播放重新固定成 30 FPS。
12. effectSchedule 为空时自行编造精确 releaseDelay。
13. 调换已有 `WisadelFxSlot` 枚举顺序。
14. 把 TrainingDummy 塞回 Wisadel Builder。
15. 因为 offset 不准就推翻当前语义绑定。

---

## 22. 关键文件

Runtime：
- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelExtractedFxController.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelSkill.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelRangedBasicAttack.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelFxTuningProfile.cs`
- `Assets/_Game/Scripts/Gameplay/Facilities/TrainingDummyFacility.cs`

Editor / Import：
- `Assets/_Game/Editor/WisadelLocalAssetBootstrap.cs`
- `Assets/_Game/Editor/WisadelReexportImportMigration20260924.cs`
- `Assets/_Game/Editor/WisadelFxTuningWindow.cs`
- `Assets/_Game/Editor/LocalOperatorAssetImportUtility.cs`
- `Assets/_Game/Editor/LocalOperatorPresentationImporter.cs`
- `Assets/_Game/Editor/LocalOperatorPackageScanner.cs`
- `Assets/_Game/Editor/LocalOperatorQuickImportWindow.cs`
- `Assets/_Game/Editor/CurrentOperatorAssetRefreshService.cs`
- `Assets/_Game/Editor/PrototypeOperatorRegistry.cs`
- `Assets/_Game/Editor/Prototype25DProductionFactory.cs`
- `Assets/_Game/Editor/PrototypeRunSceneBuilder.cs`

---

## 23. 当前验证状态

Bridge 当前只能做 source / file-level validation。

当前项目的 bridge build capability 是 validation-only，**不能证明 Unity C# compilation 已通过**。

当前已确认：
- Wisadel 新版 32 个 FX Prefab 已出现在项目目录
- Trail Runtime 绑定已改为 projectile flight
- Trail Tuning Preview 已改为 projectile flight
- 公共 TrainingDummy 已放到出生区域
- Wisadel re-export migration 已改成一次性 force rebuild
- 通用角色 import pipeline 已建立基本框架

仍需 Unity Editor 完成：
- C# 编译
- migration 执行
- AssetDatabase refresh
- Play Mode 实测

如果 Unity 出现 CS 错误，下一步优先直接修编译错误，不要先重做资源结构。

---

## 24. 2026-09-24 Runtime 语义纠正（本轮接手新增）

本轮确认并修正了之前把 Wisadel S2/S3 当成短 pulse 技能的问题。

### 通用 FX 命名规则

当前 Wisadel 绑定优先遵守以下语义：

- 名称含 `buff`：通常是**角色自身持续层**，应挂 Actor / CustomFxMountPoint，并跟随技能生命周期。
- 名称含 `hit`：通常是**击打 / 命中层**，应挂实际受击目标或目标世界位置，不应挂角色自身。
- 名称含 `trail`：是**弹道飞行轨迹**，必须从角色发射点飞向目标，不得 `SpawnOnActor`。
- `start`：一次性起手层；S3 的 `up/down/start` 目前继续沿用既有方向 variant 逻辑，不额外猜测新的 enter/exit 语义。

### S2：持续自动索敌攻击

当前 Gameplay Slot 1 对应原 S2。

已改为：

1. 按下技能只进入 S2 Duration 状态，不立即播放攻击动画，也不立即播放 `skill_02_start`。
2. startup 完成后进入持续自动索敌。
3. 没有有效敌人时保持正常移动表现。
4. 首次找到有效敌人时才进入 S2 攻击表现。
5. **每一次实际自动攻击都会触发一次攻击动作事件，并同步播放一次 `skill_02_start`**；不是只在首次锁敌时播放一次。
6. 丢失全部有效目标后退出 S2 攻击 pose；之后再次找到目标时重新进入攻击动画。
6. `skill_02_buff` + `skill_02_buff_02` 作为自身 persistent FX，持续到技能结束。
7. 技能持续期间屏蔽玩家手动普攻，但角色可以移动/冲刺；S2 Active 不再用共享 IsCasting 长时间锁住 Motor。
8. 技能自己按攻击间隔反复重新索敌，当前实现优先最近敌人。
9. 每次自动攻击自行结算 Skill Damage。
10. 每次自动攻击命中时，在实际目标上播放：
   - `skill_02_hit`
   - `skill_02_hit_02`
11. S2 结束后清理自身 persistent buff。
12. `skill_02_overload_start` 仍保留显式触发接口；在没有可靠原版 overload 条件前，不要擅自绑定到每次施法或固定时间点。

### S3：6 发弹药状态

当前 Gameplay Slot 2 对应原 S3。

已改为：

1. 激活后进入 Ammo lifecycle。
2. 当前 prototype capacity = 6。
3. 激活后仍允许普通攻击输入，但移动/冲刺被锁定；普攻/S3 的目标搜索使用范围内最近有效敌人并自动转向，不再受最后移动方向的 forward-dot 限制。
4. 点击技能只播放一次 `Skill_3_Begin` 并进入弹药姿态；不会自动循环 `Skill_3_Loop`，也不会自动发弹。
5. 只有玩家点击普攻时才播放一次 `Skill_3_Loop`，并消耗 1 发 S3 弹药；不会切回 `Attack_A/B/C`.
6. 该发不再播放普通 `wisdel_attack_01_trail`，改用 `skill_03_trail` 飞向真实目标。
7. 该发命中时播放：
   - `skill_03_hit`
   - `skill_03_hit_02`
   - `skill_03_hit_03`
8. 四层 S3 `buff*` 只挂角色自身，并持续到最后一发耗尽。
9. 第 6 发消耗后结束 S3 lifecycle 并清理 persistent buff。
10. HUD 可直接通过共享 `PlayerSkillLifecycleType.Ammo` 显示剩余弹药。

### FX 调参面板语义

- `WisadelFxTuningWindow` 不再生成/循环预览任何 FX。
- 每个复选框是“该已绑定 FX 是否启用”的正式 Runtime 配置。
- 面板只负责 enable/disable 与左右 XY offset。
- 普攻/技能 `hit*` 以真实敌人为挂点，没有目标时不允许回退挂到角色。
- `buff*` 仍由技能生命周期挂角色，`trail` 仍走真实 projectile flight。

### 本轮修改文件

- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelSkill.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelExtractedFxController.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/Wisadel/WisadelPresentationDriver25D.cs`
- `Assets/_Game/Editor/WisadelPrototypePlayerFactory.cs`

Bridge source smoke 已通过，但 bridge 当前仍没有真正调用 Unity C# compiler；必须以 Unity Console / Play Mode 为最终验证。

### TrainingDummy 受击链路修正

- 公共假人仍与角色完全解耦。
- 假人现在使用独立 3D `CapsuleCollider` 作为锁敌/受击碰撞体，不再依赖 `CharacterController` 作为唯一碰撞体。
- 旧场景里已存在的 `TrainingDummy_Infinite` 会自动补齐/修复：Health、Status、CombatStats、CombatEntity、Team.Enemy、TrainingDummyInvincible、HitReaction、DamageNumber、HealthBar、Collider。
- 修复后调用 `Physics.SyncTransforms()`，保证当帧后的 OverlapSphere/目标搜索能看到假人。

### 2026-09-24 普攻 / S3 索敌链修正

- S2 能锁假人说明目标的 Team/Health/CombatEntity 链路有效，问题不应继续归因于 TrainingDummy。
- Wisadel 普攻/S3 现在实现 `IPlayerBasicAttackTargetProvider`；`PlayerAttackController` 在开始攻击周期前先索敌。
- 有远程 target provider 但没有目标时，不进入 AttackRoutine，因此不会误播攻击、不会发空弹、S3 也不会消耗弹药。
- Wisadel 目标发现改为 active `CombatEntity` 搜索并选范围内最近敌人，不再依赖 Physics.OverlapSphere 返回哪个 Collider。
- 成功预锁的目标缓存到 `WisadelRangedBasicAttack`，后续 Presentation / FX / impact resolver 复用该目标。

### 2026-09-24 远程索敌公共化 + S3 Hit 修正

- `CombatEntity` 新增 ActiveEntities 运行时注册表；TrainingDummy 作为动态 Enemy 会在 OnEnable 时和普通敌人一样注册。
- `RangedBasicAttackTargeting` 成为公共远程候选筛选入口。
- 当前三个远程 Resolver（Wisadel / Schwarz / FrostNova）全部实现 `IPlayerBasicAttackTargetProvider`，由 `PlayerAttackController` 在攻击周期开始前预锁目标。
- Wisadel 不再自己扫描场景对象；训练假人和普通敌人使用同一个 ActiveEntities 候选集合。
- S3 每发在 `OnAttackStarted` 就锁定 `_skill3ShotPending`，同时保留 AmmoConsumed 标记；这样 `skill_03_hit / hit_02 / hit_03` 不再依赖 AttackStarted 订阅回调先后顺序。

### 2026-09-24 实际运行态问题复查

- 发现 `PrototypeRun.unity` 中 Wisadel 的 `WisadelExtractedFxController` 仍保存旧字段集合；新复合 FX 字段没有全部序列化进旧场景。已提升 Wisadel re-export migration 版本，下一次脚本重载会重新执行 `ConfigurePlayer` 修补当前打开场景中的完整引用，但仍遵守“不在导入流程自动 SaveScene”的规则。
- 公共远程索敌现使用 ActiveEntities + Physics 候选合并；TrainingDummy 的 `CombatEntity` 会被强制启用并在 SetTeam 时刷新注册，避免出现 S2 Physics 能锁而基础远程索敌不能锁的状态差异。
- Wisadel 普攻/S3 命中特效不再使用 `_skill3ShotPending` 跨事件猜测。预锁目标时记录 `wasSkill3`，Resolver 在 DamageSystem 真正 Applied 后直接发 `ShotHitResolved(target, wasSkill3)`，FX Controller 据此播放 basic hit 或完整 `skill_03_hit / hit_02 / hit_03`。

### 2026-09-24 假人无法被普攻/S3命中的最终根因

- 诊断日志确认：Wisadel 普攻与 S3 都能正确预锁 `TrainingDummy_Infinite`，Team/Health/Collider/注册状态均正常；失败点是 `DamageSystem.Apply()` 返回 `Applied=false`。
- 根因是公共假人曾使用 `1_000_000_000f` HP。Unity/C# `float` 在该数量级精度不足，Wisadel 当前约 15 点的单次普攻伤害可能在 `CurrentHealth - amount` 时直接舍入回原值，`Health.TakeDamage()` 因此得到 `dealt=0`，DamageSystem 将其视为 Rejected。S3 Hit FX 又正确绑定在真实 Applied 命中上，所以也同步缺失。
- 已把公共假人最大生命统一改为 `TrainingDummyFacility.DummyMaxHealth = 100_000f`；无敌仍由 `TrainingDummyInvincible` 在每次 Applied 后立即回满，而不是依赖超大 HP。
- 诊断日志暂时保留，确认本修复后应清理 `[WisadelDiag/*]` 与 `[TrainingDummyDiag]` 输出。

### 2026-09-24 S3 命中特效可见性修正

- 在确认 DamageSystem 已 Applied 后，S3 三层 Hit 仍不可见；导出数据表明 `skill_03_hit_02 / hit_03` 属于 `static_offset/fixed` 世界固定冲击层。
- S3 Hit 三层已从“挂到目标 Transform”改为在真实命中世界坐标生成；加入轻微朝相机方向的 depth bias，并把 SpriteRenderer sortingOrder 保底到 120，避免目标层级旋转/深度遮挡。
- `WisadelFxTuningProfile` 中原先被关闭的 `Skill3Buff02Front` 已重新启用，S3 四层常驻 buff 都参与显示。
- 临时 `[WisadelDiag/*]` 与 `[TrainingDummyDiag]` 日志已全部移除。

### 2026-09-24 vfx_binding / audio_mapping 校正

- `D:\\Ark\\_Unpacked\\wisdel\\vfx_binding.json` 明确区分普通 projectile 与 S3 `projectile_chr_wisdel_s3`，S3 projectileTrail 为 `wisdel_skill_03_trail`（game#9 使用对应替换版本），projectileHit 为 `skill_03_hit / _02 / _03` 三层。
- 已核对导出与 Unity 目标帧 SHA：`wisdel_attack_01_trail` 与 `skill_03_trail` 是不同资源且导入无串包；两者原始画面本身非常接近，因此“看起来一样”不是当前引用指向同一个 prefab。导出包只给出了 S3 projectile graphic/logic 的配置 key，没有单独可直接导入的 projectile graphic 资源。
- `audio_mapping.json` 已开始作为音频绑定真值源：普攻起手 `p_atk_dkmrcaygn_n`，S2/S3 激活 `b_char_atkboost`，S3 射击 `p_atk_dkmrcaygnfr_s`，S2 命中 `p_imp_dkmrcaygn_h`，S3 命中 `p_imp_dkmrcaygn_s`。Battle SFX 会从角色本地 `sound/_banks` 导入 Unity，并由角色 profile / 精确命中事件播放。

### 2026-09-24 VFX runtime correction pass

- Confirmed serialized scene bindings are not cross-wired: default Wisadel basic trail uses `wisdel_attack_01_trail`, S3 uses `skill_03_trail`; game#9 uses `skill_03_trail_game#9`. The source captures are genuinely very similar.
- `timing.json` for both normal and S3 trail describes a looping particle stack captured for 2.5 s. Runtime must not compress the whole captured sequence into the ~0.22 s projectile travel time. Trail playback now stays at authored FPS and is recycled when flight ends.
- During an S3 ammo shot, do not additionally spawn ordinary `wisdel_attack_[a/b/c]_start`. `vfx_binding.json` classifies S3 shot visuals as `skill_03_trail` + `skill_03_hit/_02/_03`; ordinary basic starts are not listed in the S3 binding.
- `CustomFxMountPoint` is already created at local Y=0.82. Actor-attached FX previously added `actorLocalOffset.y=0.82` again, shifting start/buff FX to roughly Y=1.64. Actor-local base offset is now zero when the custom mount exists.
- S3 `*_buff_B` / `*_buff_F` are now rendered around the player presentation order (player Spine order 30): B at 25, F at 35. Previously all imported frame FX were order 90, so back layers were incorrectly rendered in front.
- Removed the S3-hit-only runtime material override. Imported Wisadel frame FX already use the shared additive material (`One/One`, ZTest Always); changing only S3 hits to One/OneMinusSrcAlpha + HDR 2.6 altered the exported composite.

### 2026-09-24 Wisadel authored action-state pass

- 普攻继续按 `Attack_A -> Attack_B -> Attack_C` 的当前 combo 顺序播放；`wisdel_attack_a/b/c_start` 与 `wisdel_attack_down_a/b/c_start` 仍按同一段位选择，Down 仅由屏幕纵向朝向决定，不再把这些资源误认为技能 FX。
- S2 Spine 状态机改为导出资源的真实语义：按下技能立即播放 `Skill_2_Begin`；技能存续且无攻击对象时循环 `Skill_2_Idle`；锁定攻击对象后持续循环 `Skill_2_Loop`；技能结束后播放 `Skill_2_End`。不再用“第一发 Begin、后续每发 Loop”的旧逻辑。
- S2 原始 `skill_table.json` 的总持续时间为 25 秒。运行时现在在持续时间中点进入 overload，因此普通段与过载段各占一半；过载阶段使用更短的自动攻击间隔，并通过新的 `OverloadStarted` 生命周期事件自动播放 `skill_02_overload_start`。原始数据还描述过载为 4 连发；当前原型继续保留既有单次伤害/范围模型，只先落实阶段、节奏和特效状态，避免在本次视觉接入中同时重做数值模型。
- S3 与 S2 使用相同的动作语义：`Skill_3_Begin -> Skill_3_Idle -> Skill_3_Loop(实际攻击) -> Skill_3_End`。每次真实 S3 基础攻击播放一次 `Skill_3_Loop`，若仍有弹药则回到 `Skill_3_Idle`；最后一发会先让 Loop 播完，再进入 End。
- default 与 `game#9` 共用上述动作状态逻辑，动作名在两套重新导出的 Skeleton 中都已确认存在。
- `game#9` 行走资源已确认完整导入：`Assets/_Game/Art/Characters/Wisadel/PRTS/Game9/BaseMotion/` 下存在 build Spine 资源，并且 `Assets/_Game/Generated/PRTS/Prefabs/wisadel_game_9_motion.prefab` 已生成；`WisadelPrototypePlayerFactory.AttachMotionRetarget` 会对 game#9 使用该 Move source。
- FolderBridge 的 build/test 当前均为 validation-only/source smoke，已通过且未发现文本/JSON 级问题；仍需在 Unity Editor 完成真实 C# 编译与 PlayMode 动画/FX 验证。

### 2026-09-24 Wisadel attack cadence correction

- PRTS 与本地 `skill_table.json` 一致：维什戴尔基础攻击间隔为 2.1s；S2 专三 `base_attack_time = -0.7`，因此普通阶段攻击周期应为 **1.4s**。旧原型的 0.35s 已移除。
- S2 过载不再把整个攻击周期压成 0.175s。当前仍以 1.4s 为一个攻击周期，但每个过载周期改成 **4 连发**；连发子弹间隔暂按当前 `Skill_2_Loop` 在项目统一 2x 后的有效 0.7s / 4 = **0.175s** 分布。
- S3 每发攻击新增真实输入冷却。`WisadelRangedBasicAttack` 直接读取当前皮肤 `Skill_3_Loop` 的 `TryGetAnimationDuration()` 有效时长作为下一发允许时间；当前原始 Loop 5.0s、项目统一 2x，因此运行时约 **2.5s/发**。这只限制 S3 弹药攻击，不影响普通普攻。
- PRTS 原始 S3 数据为基础间隔 2.1s + `base_attack_time +2.9` = 5.0s，与原始 `Skill_3_Loop` 5.0s 一致；项目因统一 2x 动作规则使用其运行时有效时长。

### 2026-09-24 BaseMotion full-source correction

- `action_gif_manifest.json` 已确认 default 与 `game#9` 均有 `Move / Relax / Interact / Sit / Sleep`；`game#9` 额外有 `Special`，default 无 `Special`。
- 之前 `SpineBoneMotionRetarget2D` 只复制 bone transform，不复制 Spine slot/attachment，因此 default Move 虽然身体在走，眼睛仍保持 combat Idle 的睁眼 attachment；`game#9` 的 build/combat 差异更大，骨骼覆盖率不足时还会直接判定 motion source 不兼容，表现为没有 Move、Sit/Sleep 不完整或被 combat Spine 盖住。
- Wisadel 现启用 **full-source BaseMotion**：Move / Relax / Interact / Sit / Sleep / Special 播放期间直接显示当前皮肤完整 `build_char_*` Spine，并隐藏 combat Spine；回到 Idle/Attack/Skill 时恢复 combat Spine。这样眼睛、嘴、服装/坐姿等 attachment 完整遵守原始 build 动作。
- full-source 初版曾直接保留 build prefab 的初始 0.38 scale，而 combat Spine 会经过 `SpineVisualAutoLayout2D` 运行时校准，因此切基建动作会明显变小。现已改为 BaseMotion 每帧继承 combat visual **最终校准后的 localScale / localPosition / localRotation**；`fullSourceScaleMultiplier` 默认 1.0，仅作为个别皮肤微调项，旧序列化场景中的 0 值按 1.0 兼容处理。
- full-source 模式不再依赖 build/combat bone coverage，因此 `game#9` MotionSource 即使不适合骨骼 retarget，也仍可直接播放完整 Move/BaseMotion。
- MotionSource 根节点会与 combat presentation 对齐，并同步左右朝向及 sorting order，避免 BaseMotion 切换时跳位置/反向/被 combat presentation 遮挡。
- `BaseMotionActionShortcutController` 新增自动 `Relax`：站立无移动/攻击/冲刺/技能至少 3 秒后，在 0~2 秒随机窗口内单次触发；任意 Gameplay 行为或手动 BaseMotion 会重新计时。Sit/Sleep 循环也修正为可再次按 F6/F7 退出。
- migration 已提升到 v5，脚本 reload 后会重新执行 Wisadel 导入/ConfigurePlayer，使旧场景中的已有 retarget 也切换到 full-source 模式。
