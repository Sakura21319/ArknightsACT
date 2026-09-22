# 黑（Schwarz）角色接入交接文档

更新时间：2026-09-22  
项目：`D:\WorkSpace\ArknightsACT`  
当前场景：`Assets/_Game/Scenes/PrototypeRun.unity`  
当前调试角色：Schwarz / 黑  
当前皮肤：`snow#1`  
本地素材源：`D:\Ark\_Unpacked\shwaz`

---

## 1. 当前总体状态

黑已经完整接入现有 2.5D ACT 原型，保持“通用 Player Gameplay + Schwarz 专属 Skill / Presentation / FX / Editor Factory”的结构。

当前两个技能槽不是 S1 + S3，而是：

- Slot 1 / `L`：原版 S2「暮眼锐瞳」
- Slot 2 / `I` 或 RMB：原版 S3「战术的终结」

为了当前视觉调试，两技能均暂时开启 `debugInfiniteDuration = true`：

- 技能开启后不会自动结束；
- 再按同一技能键可手动退出；
- 调试模式下技力会直接补满；
- S3 调试模式跳过正式 startup 等待，便于反复测试。

后续正式平衡前要关闭该调试开关，恢复 40s / 25s 等正式持续逻辑。

---

## 2. 角色与皮肤

Schwarz 三套皮肤均已接入：

- Default：无后缀
- Snow：`_snow#1`
- Striker：`_striker#1`

当前 `PrototypeRun.unity` 使用：

`skinId: snow#1`

战斗 Spine：

- `char_340_shwaz`
- `char_340_shwaz_snow#1`
- `char_340_shwaz_striker#1`

BaseMotion：

- `build_char_340_shwaz`
- `build_char_340_shwaz_snow#1`
- `build_char_340_shwaz_striker#1`

头像与技能图标已通过 `PlayableOperatorIdentity` 接入 HUD。

---

## 3. 角色切换

Unity 顶部菜单目前只保留主要入口：

`ArknightsACT > 角色切换`

文件：

`Assets/_Game/Editor/OperatorSwitcherWindow.cs`

当前可切换：

- 陈
- 黑 · Default
- 黑 · Snow
- 黑 · Striker

角色切换目前采用 Editor 重建 Player 的方式，不做局内热切换，以避免 StageRuntime / HUD / 背包 / 商店 / 奖励等系统残留旧 Player 引用。

“黑特效调试 / Schwarz FX Tuning”入口也位于角色切换窗口内。

---

## 4. 普攻

黑的基础攻击已经改成长距离射手逻辑，不再使用陈的近战判定。

相关：

- `SchwarzRangedBasicAttack.cs`
- `PlayerAttackController.cs`
- `IPlayerBasicAttackModifier.cs`

普攻 FX：

- Start：`shwaz_attack_01_start_<skin>`
- Trail：当前使用程序 tracer 表现
- Hit：`shwaz_attack_01_hit_<skin>`

当前基础/S2 出箭局部偏移：

`basicProjectileLocalOffset = (0.22, 0.04, 0)`

S3 使用独立更低出箭位置，见后文。

---

## 5. S2 / 暮眼锐瞳

代码类名仍是：

`SchwarzSkill1.cs`

注意：它代表游戏 Slot 1，但实际映射的是原版 S2。

当前行为：

- `L` 开启；
- 再按 `L` 手动退出；
- 调试状态无限持续；
- 开启后强化基础攻击；
- 与 S3 互斥；
- 当前不播放角色 Spine 技能起手动作，因为当前素材/表现设计里 S2 没有独立角色动作；
- S2 不再错误使用 S1 的 start/trail 特效。

S2 Buff 使用公共特效组合：

- `common_064_ignite_attack_red`
- `common_combustion_buff_02`

源目录：

`D:\Ark\_Unpacked\shwaz\effects\candidates\black_s2\common_buff\frames`

Import 已通过 `ExtractedFrameFxImporter.ImportSelected()` 接入。

S2 攻击箭矢使用独立 tracer 风格：

- 比普通攻击更明显；
- 红橙色；
- 独立长度 / 粗细 / 左右调参槽 `Skill2Trail`。

---

## 6. S3 / 战术的终结

代码类名：

`SchwarzSkill2.cs`

注意：它代表游戏 Slot 2，但实际映射原版 S3。

### 6.1 进入与退出

- `I` / RMB 进入 S3；
- 再按 `I` / RMB 退出；
- 调试模式无限持续；
- 进入技能本身不会自动开枪；
- 只有鼠标实际点击成功锁定的敌人后才触发射击。

### 6.2 鼠标朝向

S3 激活期间：

- 鼠标在角色左侧 → 黑朝左；
- 鼠标在角色右侧 → 黑朝右。

由：

`PlayerMotor25D.ForceFacingSign()`

和：

`SchwarzPresentationDriver25D`

共同保证逻辑方向与 Spine 显示方向同步。

### 6.3 狙击镜

文件：

`SchwarzSniperModeController.cs`

当前采用标准 Unity `RawImage` 运行时生成狙击镜，不再使用自定义 UI Mesh。

当前视觉方向：

- 整个圆形镜心跟随鼠标移动；
- 圆外半透明暗色遮罩；
- 极细圆边；
- 圆边使用软过渡抗锯齿；
- 横竖准线在中心断开；
- 中间小红点；
- 少量短刻度；
- 平时不显示 SEARCH 文案；
- 锁定时仅显示小型 `LOCK`。

目标判定采用三层兜底：

1. 鼠标 3D Raycast 点中敌人 Collider；
2. 鼠标位于敌人 Collider 的屏幕投影包围盒；
3. 鼠标附近最近敌人辅助锁定。

左键点击时会重新执行更宽松的 Click Target 解析，不要求 Hover UI 预先进入 LOCK 才允许射击。

### 6.4 S3 攻击 FX

S3 进入状态时不会播放攻击 FX。

每次鼠标实际成功射击后：

1. Shot / muzzle：
   `skill_03_start_<skin>`
2. 专属箭矢：
   `skill_03_trail_<skin>`
3. 命中：
   `skill_01_hit_<skin>`

当前 Hit 映射已于 2026-09-22 接入：

- Default → `skill_01_hit`
- Snow → `skill_01_hit_snow#1`
- Striker → `skill_01_hit_striker#1`

当前 Snow 场景已直接序列化绑定：

`skill_01_hit_snow#1.prefab`

S3 命中时不会再使用普通攻击的 `shwaz_attack_01_hit`。

### 6.5 S3 Trail

S3 存在专属 `skill_03_trail_<skin>` 时：

- 不再叠加普通/通用程序 tracer；
- 只显示该皮肤自己的 authored Trail；
- Snow 当前使用 `skill_03_trail_snow#1`；
- Trail 左右默认 Scale 当前为约 `1.7`，可继续通过 FX Tuning 面板调整。

S3 当前独立出箭位置：

`projectileLocalOffset = (0.22, -0.04, 0)`

普通/S2 则仍是：

`basicProjectileLocalOffset = (0.22, 0.04, 0)`

即 S3 比普通/S2 再低一档。

### 6.6 S3 持续 Buff

对应皮肤：

- `skill_03_buff_02_<skin>`
- `skill_03_buff_03_<skin>`

两层都属于 S3 持续态，不是一次性 Start。

Buff02：

- 持续循环。

Buff03：

- 原素材整段循环会有明显显隐；
- Runtime 使用稳定可见区间循环，而不是冻结成静态图片，也不是从 0 帧反复重播；
- 当前循环区间由：
  - `skill3Buff03LoopStartNormalized`
  - `skill3Buff03LoopEndNormalized`
  控制。

---

## 7. S3 动作表现

文件：

`SchwarzPresentationDriver25D.cs`

当前规则：

- S2 不复用 S3 的 Skill 动作；
- 进入 S3 不自动播放攻击动作；
- 实际点击射击时才寻找 S3 专用 Shot 动作；
- 优先查找 `Skill_Attack / Skill_Shoot / Skill_Shot` 等命名；
- 允许 `Skill_Loop` 作为单次 Shot fallback，但不会在进入技能后自动循环射击；
- 最后才回退 `Skill_Begin`，且只允许由真实射击事件触发。

后续若要做到 1:1，仍建议继续确认三个皮肤 Spine 的真实 S3 射击 clip 名称。

---

## 8. Schwarz FX 调参系统

Runtime 配置：

`Assets/_Game/Resources/Config/SchwarzFxTuningProfile.asset`

代码：

- `SchwarzFxTuningProfile.cs`
- `SchwarzFxTuningWindow.cs`
- `SchwarzExtractedFxController.cs`

面板入口：

`ArknightsACT > 角色切换 > 黑特效调试 / Schwarz FX Tuning`

每个 FX 都支持：

- Right X/Y Offset
- Right Scale
- Right Angle
- Left X/Y Offset
- Left Scale
- Left Angle

左右参数完全独立，不再只通过负 Scale 数学镜像。

当前调参项：

### 普攻

- BasicStart
- BasicTrail
- BasicHit

### S2

- Skill2IgniteRed
- Skill2Combustion
- Skill2Trail

### S3

- Skill3Start
- Skill3Trail
- Skill3Hit
- Skill3Buff02
- Skill3Buff03

其中新增的 `Skill3Hit` 对应按皮肤加载的 `skill_01_hit_<skin>`。

点击“保存并应用”后会写入 Profile，并让当前运行中的 Schwarz 重新读取持续 FX 参数。

---

## 9. 皮肤 FX 绑定规则

Editor 绑定：

`SchwarzLocalAssetBootstrap.cs`

`SchwarzExtractedFxSetup.Load(baseName, skin)` 统一按后缀选择：

- Default：`baseName`
- Snow：`baseName_snow#1`
- Striker：`baseName_striker#1`

当前严格遵守皮肤分组，不应把 Snow/Striker FX 与原皮混用。

S3 当前完整映射：

- Shot：`skill_03_start_<skin>`
- Trail：`skill_03_trail_<skin>`
- Hit：`skill_01_hit_<skin>`
- Persistent A：`skill_03_buff_02_<skin>`
- Persistent B：`skill_03_buff_03_<skin>`

`HasVariantImported()` 当前也会检查 `skill_01_hit_<skin>` 是否存在。

---

## 10. 测试木桩

原先测试木桩只由陈的 Factory 挂载，因此切 Schwarz 后曾消失。

目前 Schwarz 运行时会确保存在：

`ChenTrainingDummySpawner`

Schwarz Factory 也会挂该组件。

用途：

- S3 锁敌测试；
- 箭矢方向/位置测试；
- Hit FX 测试；
- 技能伤害数字测试。

当前仍复用了 Chen 命名的通用训练木桩组件，后续可重命名为角色无关的 `TrainingDummySpawner`，但目前功能正常，暂不必阻塞视觉调试。

---

## 11. 主要 Runtime 文件

- `Assets/_Game/Scripts/Gameplay/Characters/PlayableOperatorIdentity.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/IPlayerRunResettable.cs`
- `Assets/_Game/Scripts/Gameplay/Combat/IPlayerBasicAttackModifier.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzSkinVariant.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzSkill1.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzSkill2.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzRangedBasicAttack.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzSniperModeController.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzPresentationDriver25D.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzExtractedFxController.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzFxTuningProfile.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/SchwarzSkillUpgradeApplier.cs`

---

## 12. 主要 Editor 文件

- `Assets/_Game/Editor/SchwarzLocalAssetBootstrap.cs`
- `Assets/_Game/Editor/SchwarzPrototypePlayerFactory.cs`
- `Assets/_Game/Editor/SchwarzPrototypeSceneBuilder.cs`
- `Assets/_Game/Editor/OperatorSwitcherWindow.cs`
- `Assets/_Game/Editor/SchwarzFxTuningWindow.cs`
- `Assets/_Game/Editor/Effects/ExtractedFrameFxImporter.cs`

---

## 13. 当前验证状态

2026-09-22 最后一次 FolderBridge source validation：

- exit code：0
- issues：0
- 当前 Bee 日志搜索没有新的 `error CS`

已确认：

- 当前场景为 Snow；
- S3 Hit 已直接绑定 `skill_01_hit_snow#1`；
- Bootstrap 会按皮肤加载 `skill_01_hit`；
- Runtime 的 S3 命中分支使用 `SchwarzFxSlot.Skill3Hit`；
- FX 调参窗口已有 S3 Hit 项。

注意：

FolderBridge 的 build 是 source validation / smoke，不等价于 Unity Editor 完整编译和 PlayMode 验证。下一次 Unity Reload 后仍应优先确认 Console 是否存在新错误。

---

## 14. 当前优先待办

### 高优先级

1. 实机确认 S3 Hit 的大小/位置：
   - 当前默认 Scale 1；
   - 如果 `skill_01_hit_snow#1` 偏大/偏小，直接用 FX Tuning 的 S3 Hit 左右参数调。
2. 继续微调 Snow S3 Trail：
   - 当前 S3 Trail 已放大；
   - 出箭 Y 为 -0.04；
   - 如仍偏高/低，优先只改 `projectileLocalOffset`，不要影响 Basic/S2。
3. 验证狙击镜最终简约样式：
   - 极细抗锯齿圆边；
   - 中心十字断开；
   - 小红点；
   - 少量刻度。
4. 验证 S3 左/右朝向下：
   - Start
   - Trail
   - Hit
   - Buff02
   - Buff03
   的独立偏移。

### 视觉调试完成后

1. 将 S2/S3 的 `debugInfiniteDuration` 恢复为 false；
2. 恢复正式持续时间、初始技力和技能循环；
3. 根据实际 Spine 动画列表锁定 S3 精确 Shot clip；
4. 再考虑接入黑的天赋/破甲逻辑和项目正式 DEF/Armor 层。

---

## 15. 不要回退的设计决定

后续接手时不要重新引入以下旧问题：

- 不要把 `_snow#1 / _striker#1` 与原皮 FX 混用；
- 不要给 S2 播 S3 的角色 Skill 动作；
- 不要在进入 S3 时自动播放攻击 FX；
- 不要在 S3 有 authored `skill_03_trail` 时再叠通用箭矢 tracer；
- 不要用整段 `skill_03_buff_03` 从头循环造成周期性消失；
- 不要通过负 Transform Scale 粗暴镜像全部 FX；
- 不要把左右 FX 偏移合并成一套；
- 不要让狙击镜依赖自定义 UI Mesh；当前 RawImage 方案更稳定；
- 不要让 S3 Hit 回退到普通攻击 `shwaz_attack_01_hit`；当前明确使用 `skill_01_hit_<skin>`。

---

## 16. 本轮最后改动

本轮最后一项完成内容：

**S3 Hit 使用 `skill_01_hit_<skin>`。**

当前 Snow：

`skill_01_hit_snow#1`

另外：

- 新增 `skill3Hit` Runtime 引用；
- `SchwarzExtractedFxSetup` 增加皮肤化 Hit 绑定；
- `HasVariantImported()` 增加 Hit 完整性检查；
- `SchwarzFxSlot` 增加 `Skill3Hit`；
- `SchwarzFxTuningProfile` 增加左右独立 S3 Hit 参数；
- `SchwarzFxTuningWindow` 增加 S3 Hit 调参项；
- 当前 `PrototypeRun.unity` 已直接写入 Snow Hit Prefab 引用。

