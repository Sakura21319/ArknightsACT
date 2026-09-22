# 黑（Schwarz）角色接入交接

更新时间：2026-09-21  
项目：`D:\WorkSpace\ArknightsACT`  
本地素材：`D:\Ark\_Unpacked\shwaz`

## 1. 当前状态

黑已经按现有“通用 Player Gameplay + 角色专属 Skill/Presentation + Editor Factory”的边界接入。

当前不是把黑写进陈的代码，而是新增独立角色组合层，同时把原来写死陈的两个公共点泛化：

- 战斗 HUD 头像/技能图标改为读取 `PlayableOperatorIdentity`；
- 新 Run 的角色表现重置改为 `IPlayerRunResettable`；
- 临时强化普攻改为 `IPlayerBasicAttackModifier`，S3 不需要在 `PlayerAttackController` 内写 Schwarz 分支。

## 2. 本地素材映射

源目录：

- `D:\Ark\_Unpacked\shwaz\spine`
- `D:\Ark\_Unpacked\shwaz\icons`
- `D:\Ark\_Unpacked\shwaz\ui_assets\avatars`
- `D:\Ark\_Unpacked\shwaz\effects\frames`

战斗 Spine：

- 原版：`char_340_shwaz`
- Snow：`char_340_shwaz_snow#1`
- Striker：`char_340_shwaz_striker#1`

基建动作源：

- 原版：`build_char_340_shwaz`
- Snow：`build_char_340_shwaz_snow#1`
- Striker：`build_char_340_shwaz_striker#1`

Unity 本地导入时会按现有 Spine 规则重命名：

- `.atlas` -> `.atlas.txt`
- `.skel` -> `.skel.bytes`
- Atlas 内实际引用的 PNG 页逐页复制。

头像：

- 原版 -> `Resources/UI/HUD/Operators/schwarz_default`
- Snow -> `Resources/UI/HUD/Operators/schwarz_snow`
- Striker -> `Resources/UI/HUD/Operators/schwarz_striker`

技能图标：

- S1 -> `Resources/UI/Skills/Schwarz/s1`
- S2 -> `Resources/UI/Skills/Schwarz/s2`
- S3 -> `Resources/UI/Skills/Schwarz/s3`

当前游戏的两个技能槽改为 S2 + S3，所以 HUD 显示 `暮眼锐瞳` 与 `战术的终结` 图标。S1 图标仍保留在本地资源中但当前不装备。

## 3. 皮肤特效

提取目录实际包含三套：

- 原版：无后缀
- Snow：`_snow#1`
- Striker：`_striker#1`

已映射：

### 普攻

- `shwaz_attack_01_start`
- `shwaz_attack_01_trail`
- `shwaz_attack_01_hit`

### S1（已导入但当前不装备）

- `skill_01_start`
- `skill_01_trail`
- `skill_01_hit`

本地提取包没有 `skill_02_*` 帧序列；S2 不再错误复用 S1 特效，而是优先播放战斗 Spine 中的 `Skill_2 / Skill_02 / Skill2` 动作。

### S3

- `skill_03_start`
- `skill_03_trail`
- `skill_03_buff_02`
- `skill_03_buff_03`

旧 `ExtractedFrameFxImporter` 只认 `attack_*` 开头的普攻目录，现已扩展为同时接受 `*_attack_*`，否则黑的 `shwaz_attack_*` 会被漏掉。

Runtime 绑定：

`SchwarzExtractedFxController.cs`

Editor 皮肤选择与 Prefab 映射：

`SchwarzLocalAssetBootstrap.cs / SchwarzExtractedFxSetup`

## 4. 技能实现

游戏技能槽：

### Slot 1 / L

`暮眼锐瞳`（原版 S2）

当前 ACT 化实现：

- 自动回复技力，手动开启；
- 默认 Initial 20 / Cost 30 / Duration 40s；
- BUFF 期间基础普攻伤害倍率约 2.3x，对应原版专三攻击力 +130% 的核心强化；
- BUFF 持续期间不恢复该技能技力；
- 与 S3 互斥，防止两个原版技能同时叠开；
- 当前项目尚无正式 DEF / 破甲层，因此原版“天赋发动概率提高”暂不伪造，等待正式防御系统接入；
- 本地无 `skill_02_*` 提取帧，表现层使用 Spine S2 动作，不冒用 S1 特效。

### Slot 2 / I 或鼠标右键

`战术的终结`（原版 S3）

当前 ACT 化实现：

- 消耗 SP 后进入强化窗口；
- 默认 Initial 12 / Cost 25 / Duration 25s；
- 强化期间提高通用普攻伤害（约 2.8x）；
- 强化期间把狙击基础射程进一步拉长；
- 强化期间攻击循环变慢，近似原版攻击间隔 +0.4s；
- BUFF 持续期间不恢复该技能技力，并与 S2 互斥；
- 普攻改播对应皮肤 `skill_03_trail`；
- 开启时播放 `skill_03_start / buff_02 / buff_03`。

S3 通过 `IPlayerBasicAttackModifier` 接入通用攻击层，没有把角色判断写进 `PlayerAttackController`。

## 5. 肉鸽角色成长

已新增：

`SchwarzSkillUpgradeApplier.cs`

并修改：

`PrototypeRogueliteFactory.cs`

现在会根据当前 Player 身上的角色 UpgradeApplier 自动选择角色技能池。

黑当前有：

- S2 普攻强化
- S2 持续时间
- S2 技力需求
- S3 普攻强化
- S3 额外射程
- S3 持续时间

陈仍使用原来的 Chen 技能池。

## 6. 换角色接口

新增 Unity Editor 窗口：

`ArknightsACT > Operator Switcher`

对应文件：

`Assets/_Game/Editor/OperatorSwitcherWindow.cs`

当前提供：

- 陈；
- 黑 · 原版；
- 黑 · Snow；
- 黑 · Striker。

窗口会显示当前场景中的 `PlayableOperatorIdentity`，点击“切换”后直接调用对应角色 Factory 重建 `PrototypeRun.unity`。

当前采用“重建 Player 组合”而不是运行时热切换，原因是现阶段 StageRuntime、奖励、背包、商店、HUD 等仍持有具体 Player 引用。此方式最简单且不会留下旧角色引用。

只能在非 Play Mode 下切换。

## 7. 构建入口

保留原来的：

`ArknightsACT > Build Prototype Scene`

它仍构建陈，不破坏现有默认流程。

新增：

- `ArknightsACT > Build Schwarz Scene > Original`
- `ArknightsACT > Build Schwarz Scene > Snow Skin`
- `ArknightsACT > Build Schwarz Scene > Striker Skin`

构建黑场景时会自动：

1. 从 `D:\Ark\_Unpacked\shwaz` 同步头像、图标和对应 Spine；
2. 构建对应战斗 Spine 与 BaseMotion Prefab；
3. 如果黑 FX 尚未导入，则一次性导入三套提取帧 FX；
4. 用 `Player_Schwarz` 代替陈生成当前 `PrototypeRun.unity`；
5. 保留现有搜打撤、肉鸽、背包、撤离、HUD、关卡 Runtime。

也可手动执行：

`ArknightsACT > Assets > Schwarz > Import Local Assets + FX`

## 7. 主要新增文件

Runtime：

- `PlayableOperatorIdentity.cs`
- `IPlayerRunResettable.cs`
- `IPlayerBasicAttackModifier.cs`
- `SchwarzSkinVariant.cs`
- `SchwarzSkill1.cs`
- `SchwarzSkill2.cs`
- `SchwarzSkillUpgradeApplier.cs`
- `SchwarzPresentationDriver25D.cs`
- `SchwarzExtractedFxController.cs`

Editor：

- `SchwarzLocalAssetBootstrap.cs`
- `SchwarzPrototypePlayerFactory.cs`
- `SchwarzPrototypeSceneBuilder.cs`

修改：

- `GameplayHUDController.cs`
- `PlayerAttackController.cs`
- `RogueliteGameFlowController.cs`
- `ChenPresentationDriver25D.cs`
- `ChenPrototypePlayerFactory.cs`
- `PrtsPrototypeAssetCatalog.cs`
- `ExtractedFrameFxImporter.cs`
- `PrototypeRogueliteFactory.cs`

## 8. 当前验证状态

bridge 已执行：

- source safe-build：通过，0 issues；
- workspace smoke test：通过，0 issues。

注意：FolderBridge 当前没有可用的 Unity Editor 真编译入口，因此以上不是 `Assembly-CSharp/Game.Editor` 的真实 Unity 编译结果。

下一次打开 Unity 后优先检查：

1. Console 是否有 C# 编译错误；
2. 三个 Schwarz Build 菜单是否出现；
3. 原版 / Snow / Striker 是否分别加载正确小人与头像；
4. 普攻、S3 是否绑定对应皮肤 FX；S2 应只使用 Spine `Skill_2` 动作，不应出现 S1 帧特效；
5. FX 位置/缩放是否需要按黑的枪口、身体中心继续做视觉调参；
6. Spine 技能动作优先尝试 `Skill_2 / Skill_3`（并兼容 `Skill_02 / Skill_03` 等命名），找不到时才回退通用 Skill 绑定；需要根据 Unity 实际动画列表继续校准播放速度/结束段；
7. Schwarz 基础普攻判定已扩大到约 11.5~13 个世界单位的长窄射击区域，明显大于陈约 2 个世界单位的近战范围；
8. Spine 本地导入顺序已修为 PNG -> atlas -> skel，并会自动删除 `mainTexture == null` 的旧坏材质后重建。

## 9. 暂未做

- 主页“编队”按钮仍保持交接文档原设计的占位状态，没有为了黑强行改成运行时换人；
- 没有做局内动态切换原版/Snow/Striker，当前皮肤在 Editor Build Scene 时选择；
- 黑的数值目前是 ACT 原型平衡值，不宣称与明日方舟原版数值 1:1；
- 黑的技能动作已加 S2/S3 命名优先映射与安全回退，但仍需 Unity 实机日志确认实际 clip 名称、播放速度和结束段，之后才能做到与陈同等级的逐段精确还原；
- 黑的原版天赋“破甲箭头”及 S2 对天赋触发概率的提升，等待项目正式 DEF / Armor 层完成后接入。

