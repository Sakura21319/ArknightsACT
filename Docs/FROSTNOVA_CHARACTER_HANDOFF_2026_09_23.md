# 霜星（FrostNova）角色接入交接文档

更新时间：2026-09-23  
项目：`D:\WorkSpace\ArknightsACT`  
主要场景：`Assets/_Game/Scenes/PrototypeRun.unity`  
本地素材源：`D:\Effect\_output\frostn`

---

## 1. 当前总体状态

霜星已经接入现有 2.5D ACT 角色系统。

当前实现重点以 **冬痕** 为调试和数值基准，原皮以后也计划复用冬痕这一套数值，不再维护两份独立平衡/调参数据。

当前技能为调试模式：

- 无 CD；
- 技力始终视为满；
- 不消耗技力；
- 可以连续重复释放；
- 技能仍会等待当前完整角色动画播放结束，避免被下一次动作直接截断。

注意：

- “无限释放”只针对 FrostNova 自己的 `FrostNovaSkill1 / FrostNovaSkill2`；
- 没有修改其他角色的技能 CD/技力规则。

---

## 2. 角色皮肤 / Spine 结构

霜星目前定义了 4 套 Presentation：

| SkinId | 显示名 | Spine 来源 | 技能数值族 |
| --- | --- | --- | --- |
| `default` | 霜星 | `D:\Effect\_output\frostn\spine\enemy_1505_frstar` | 当前计划复用冬痕数值 |
| `winter#1` | 冬痕 | `D:\Effect\_output\frostn\spine\enemy_1510_frstar2` | 冬痕 |
| `new#1` | 霜星·新 | `D:\Effect\_output\frostn\spine_new\enemy_1505_frstar` | 当前计划复用冬痕数值 |
| `winter_new#1` | 冬痕·新 | `D:\Effect\_output\frostn\spine_new\enemy_1510_frstar2` | 冬痕 |

### 2.1 重要约束

`spine_new` 是 **额外两套 Presentation**，不要覆盖或替换 `spine`。

项目内目标目录分别为：

```text
Assets/_Game/Art/Characters/FrostNova/Local/Default/Spine
Assets/_Game/Art/Characters/FrostNova/Local/Winter/Spine
Assets/_Game/Art/Characters/FrostNova/Local/DefaultNew/Spine
Assets/_Game/Art/Characters/FrostNova/Local/WinterNew/Spine
```

为避免同名 Skeleton 覆盖，PRTS descriptor 已增加独立 `PrefabKey`：

```text
frostnova_default
frostnova_winter
frostnova_default_new
frostnova_winter_new
```

不要退回只按 `BaseName` 生成 prefab 的旧方式，否则 `spine` / `spine_new` 会互相覆盖。

---

## 3. Skin 状态恢复：非常重要

此前 FrostNova 的 `_skin` 只通过 Editor 创建时的 `Configure(skin)` 写入，且字段没有可靠从场景身份恢复。

这导致：

- Unity Reload / 场景重新载入后；
- 冬痕角色可能被运行时当成 `Default`；
- 第二技能因此错误播放 `Skill_2`。

现在以下组件会从：

`PlayableOperatorIdentity.SkinId`

在运行时重新恢复 FrostNovaSkinVariant：

- `FrostNovaPresentationDriver25D`
- `FrostNovaExtractedFxController`
- `FrostNovaSkill1`
- `FrostNovaSkill2`

冬痕 `winter#1` 必须解析为：

`FrostNovaSkinVariant.Winter`

后续不要删除这套 `SyncSkinFromIdentity()` / Awake 恢复逻辑。

---

## 4. 冬痕技能动作映射

当前已确认：

### Slot 1 / 冰环

冬痕第一个技能槽当前使用 Spine：

`Skill_1`

注意：早期曾错误使用 `Skill_2`，已经改掉。

当前 Presentation 映射：

```text
Winter:
Slot 1 -> Skill_1
Slot 2 -> Skill_3
```

### Slot 2 / 冰暴

冬痕冰暴使用原始 Spine：

`enemy_1510_frstar2 -> Skill_3`

该动作本身就是“跪下释放”，这是原版正确表现，不是倒地动画。

此前为了排查错误动作曾实现过：

`spine/frames/enemy_1510_frstar2/Skill_3`

111 帧逐帧覆盖方案。

后来确认真正问题是 skin 状态丢失，而逐帧覆盖会在：

- Spine -> 帧序列；
- 帧序列 -> Spine；

切换时造成起手/收尾坐标跳变。

因此 **当前运行时已经撤掉逐帧角色覆盖方案**，重新直接使用 Winter Spine 的原生 `Skill_3`。

遗留的：

- `FrostNovaFrameSequenceAsset.cs`
- `FrostNovaWinterSkill3Frames.asset`
- Winter/Skill3Frames 导入逻辑
- `ForceReimportWinterSkill3Frames()`

属于排查阶段遗留资产/工具，当前角色运行时不应再依赖它们。后续确认完全无需求后可以清理，但不要误以为它们仍是当前 Skill_3 播放链路。

---

## 5. 动画速度与完整播放

霜星动画速度已经改成：

**速度只改变完整动画的总播放时间，不允许因为 gameplay 状态提前结束而截断动画。**

相关文件：

- `FrostNovaPresentationDriver25D.cs`
- `SpineCharacterPresentation2D.cs`

通用 Spine Presentation 新增：

- `SetCurrentAnimationSpeed(float)`
- `TryGetAnimationDuration(string, out float)`

FrostNova Presentation Driver 使用真实 Spine animation duration：

```text
视觉锁时间 = 动画原始 Duration / AnimationSpeed
```

因此：

- 0.5x：完整动作慢放；
- 2x：完整动作加速；
- 3x：完整动作更快；
- 不会因为技能逻辑结束而只播放前半截。

`FrostNovaSkill1 / FrostNovaSkill2` 在伤害/FX逻辑完成后还会等待：

`FrostNovaPresentationDriver25D.RemainingActionVisualSeconds`

确保当前动画完整结束后才退出施法状态。

---

## 6. 普攻

### 6.1 基础逻辑

文件：

- `FrostNovaRangedBasicAttack.cs`
- `FrostNovaExtractedFxController.cs`
- `PlayerAttackController.cs`

普攻为远程 Arts 攻击。

### 6.2 普攻移动卡顿修复

早期问题：

- 普攻动画已经播放完；
- 但 `PlayerAttackController` 仍按旧 recovery/cycle 锁移动；
- 所以攻击后立刻走会有一小段不自然停顿。

现在新增：

`IPlayerBasicAttackMovementLockProvider`

由 FrostNova Presentation Driver 根据 **普攻真实动画播放时间** 提供移动锁。

结果：

- 普攻动画完整播放；
- 动作结束立即允许移动；
- 不需要等待弹道命中；
- 弹道可以继续独立飞行和结算伤害。

不要重新让 FrostNova 的移动锁依赖旧 `AttackDefinition.recovery`。

---

## 7. 普攻出伤时机

新增通用接口：

`IPlayerBasicAttackImpactTimingProvider`

FrostNova 的实际出伤时间由当前视觉弹道参数自动决定：

```text
实际出伤时间 =
BasicTrail.Delay
+
BasicProjectileFlightSeconds
```

这样修改弹道出现时间/飞行时间后，不需要再手动同步一份伤害 startup。

当前 Profile：

```text
BasicTrail Delay = 0.60s
Projectile Flight = 0.22s
=> 当前出伤约 0.82s
```

`BasicHit Delay` 当前为 0。

---

## 8. 普攻弹道偏移

当前支持左右独立偏移。

调参项：

```text
普攻 Trail / 弹道

朝右
- 右 Offset
- 右 Angle

朝左
- 左 Offset
- 左 Angle
```

当前值：

```text
Right Offset = (-1.2, -0.5)
Left  Offset = ( 1.2, -0.5)
```

### 8.1 已修复的重要问题

旧逻辑：

```text
Lerp(origin, target) + visualOffset
```

这会导致：

- 起点正确；
- 但 Offset 一直叠加到终点；
- 最终弹道超过目标“多飞一截”。

当前逻辑：

```text
launchPosition = origin + directionalOffset

Lerp(
    launchPosition,
    realTargetPosition,
    t
)
```

即：

**Offset 只影响枪口/弹道起点，不影响最终目标点。**

后续不要把 visualOffset 再加回每一帧的位置结果。

---

## 9. 冬痕 Slot 1：冰环

Gameplay 类：

`FrostNovaSkill1.cs`

尽管类名叫 Skill1，它现在代表游戏 Slot 1 / 冰环。

当前行为：

- 动作：Spine `Skill_1`
- 可以空放；
- 有敌人时自动搜索附近目标；
- 目标存在时：
  - 对目标区域结算伤害；
  - `frstar2_skill_02_range` 挂在目标 Transform 上；
  - 敌人移动时 FX 跟随敌人。
- 没有目标时：
  - 仍正常播放 `Skill_1` 动作；
  - **不产生打击/冰环 FX**；
  - **不产生这次范围伤害**。

这部分的“允许空放”与“空放不产生 Hit FX”不要混为一谈。

---

## 10. 冬痕 Slot 2：冰暴

Gameplay 类：

`FrostNovaSkill2.cs`

Presentation：

Spine `Skill_3`

已确认 FX 语义：

### 第一段手部

`frstar2_skill_03_start`

用途：

**第一段手部特效**

### 第一段聚气

`frstar2_skill_03_range`

用途：

**第一段聚气**

### 第二段爆开

`frstar2_skill_03_range_02`

用途：

**第二段爆开**

当前视觉链：

```text
Skill_3 跪下动作
    ↓
Start：手部特效
    ↓
Range：第一段聚气
    ↓
Range02：第二段爆开
```

`Range02` 使用自己的 Delay 控制相对爆开时间。

当前 Profile：

```text
Skill_3 Start
Offset = (-0.3, 0.4)
Scale = 2
Delay = 0.9
PlaybackSpeed = 2

Skill_3 Range
Offset = (0, 0.7)
Scale = 3
Delay = 0
PlaybackSpeed = 4

Skill_3 Range02
Offset = (0, 0.7)
Scale = 2.8
Delay = 0.5
PlaybackSpeed = 2
```

这些是当前调试值，不是官方 canonical 数据。

---

## 11. 冬痕 Buff FX

目前确认：

| FX | 当前结论 |
| --- | --- |
| `frstar2_buff_01_start` | 不使用 |
| `frstar2_buff_02_start` | 不使用 |
| `frstar2_buff_03_start` | 眼部特效 |
| `frstar2_buff_04_start` | 用途暂未确认 |
| `frstar2_buff_05_start` | 常驻背部特效 |
| `frstar2_buff_06_start` | 原始 sharedbattle 中发现依赖，但当前 frostn 导出 frames 中没有资源 |

`buff_01 / buff_02` 已从当前 Winter import/runtime/tuning UI 移出。

---

## 12. Buff05：常驻背部特效

`buff_05` 已确认是：

**冬痕常驻背部特效**

当前行为：

- Winter 角色启用时生成；
- 始终跟随角色；
- Runtime 自动循环播放；
- 不再随 Skill_2 / Skill_3 每次重复 Instantiate；
- Skill_3 开始时隐藏；
- Skill_3 完整结束后恢复并重新播放。

Runtime 实例名：

`FrostNova_Winter_BackFx_Persistent`

### 12.1 Buff05 左右独立调参

当前支持：

```text
朝右
- Right Offset
- Right Angle

朝左
- Left Offset
- Left Angle

Shared
- Scale
- Playback Speed
```

当前 Profile：

```text
Right Offset = (-0.7, 0.5)
Left Offset  = ( 0.5, 0.3)
Scale = 1
PlaybackSpeed = 1
```

左侧不要只靠简单 `flipX` + 右侧相同 Offset，因为角色身上挂点视觉位置并不完全对称。

---

## 13. Buff03 / Buff04

### Buff03

已确认：

**眼部特效**

当前调参项仍允许：

- Enable
- Trigger Skill
- Delay
- Playback Speed
- Offset
- Scale
- Angle

当前 Profile 中 Buff03 目前为：

`enabled = false`

虽然资源语义已确认是眼部 FX，但目前没有强制默认开启，后续按实机对齐决定具体出现阶段。

### Buff04

用途未确认。

当前默认关闭。

不要在没有确认原版用途前强制绑定。

---

## 14. 冰暴白色正方形问题

此前 `frstar2_skill_03_range` 会闪一个明显白色正方形。

根因已确认：

目录里存在辅助帧：

`f0030_flat.png`

旧的 `ExtractedFrameFxImporter` 会把目录里所有 `*.png` 当动画帧，因此该辅助图被插入动画。

当前通用导入器已经改成：

只接受：

```text
f + 纯数字 + .png
```

例如：

- `f0001.png`
- `f0030.png`

不会再接受：

- `f0030_flat.png`
- 其他非纯数字辅助图。

导入时还会先清理目标目录旧 PNG，避免旧污染帧继续残留。

这个修复属于通用 `ExtractedFrameFxImporter`，不要回退，否则其他 extracted FX 也可能再次吃入辅助图。

---

## 15. 冬痕调参面板

入口：

`ArknightsACT > 角色 > 霜星 > 冬痕调节`

文件：

`Assets/_Game/Editor/FrostNovaTuningWindow.cs`

Profile：

`Assets/_Game/Resources/Config/FrostNovaTuningProfile.asset`

当前只维护一套 **冬痕基准参数**。

### 动画

- Move
- 普攻 Attack
- 2技能动作 / Skill_1
- Skill_3

### 普攻

- Start
- Trail / 弹道
  - Delay
  - Playback Speed
  - Scale
  - Right Offset/Angle
  - Left Offset/Angle
- Hit
- Projectile Flight Time
- 当前实际出伤时间只读显示

### Skill 2 / 冰环

- Range
- Delay
- Playback Speed
- Offset
- Scale
- Angle

### Skill 3 / 冰暴

- 第一段手部 Start
- 第一段聚气 Range
- 第二段爆开 Range02

每层支持：

- Delay
- Playback Speed
- Offset
- Scale
- Angle

### Character Attached FX

- Buff03 · 眼部
- Buff04 · 未确认
- Buff05 · 常驻背部

Buff05 额外支持左右独立 Offset / Angle。

---

## 16. 当前调参快照

来自当前：

`Assets/_Game/Resources/Config/FrostNovaTuningProfile.asset`

### Animation

```text
Move = 2.0x
Basic Attack = 2.0x
Slot 1 / Skill_1 = 2.0x
Skill_3 = 2.0x
```

注意：

代码内部属性名仍叫：

`Skill2AnimationSpeed`

但现在 UI 已标为：

`2技能动作 / Skill_1`

这是历史命名遗留。后续可以重命名字段，但若重命名 ScriptableObject serialized field，需要做序列化兼容，不要直接暴力改名导致现有调参丢失。

### Basic

```text
Projectile Flight = 0.22

Trail:
Right Offset = (-1.2, -0.5)
Left Offset  = ( 1.2, -0.5)
Delay = 0.6
Scale = 1
Playback = 1
```

### Skill 2

```text
Range:
Scale = 1
Delay = 0
Playback = 1.67
```

### Skill 3

见第 10 节。

### Buff05

见第 12 节。

---

## 17. 主要 Runtime 文件

FrostNova：

- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaSkinVariant.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaRangedBasicAttack.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaCombatUtility.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaSkill1.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaSkill2.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaPresentationDriver25D.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaExtractedFxController.cs`
- `Assets/_Game/Scripts/Gameplay/Characters/FrostNova/FrostNovaTuningProfile.cs`

通用层本轮有改动：

- `Assets/_Game/Scripts/Gameplay/Presentation/SpineCharacterPresentation2D.cs`
- `Assets/_Game/Scripts/Gameplay/Combat/PlayerAttackController.cs`
- `Assets/_Game/Scripts/Gameplay/Combat/IPlayerBasicAttackImpactTimingProvider.cs`
- `Assets/_Game/Scripts/Gameplay/Combat/IPlayerBasicAttackMovementLockProvider.cs`

遗留、当前运行时不使用：

- `FrostNovaFrameSequenceAsset.cs`

---

## 18. 主要 Editor 文件

- `Assets/_Game/Editor/FrostNovaLocalAssetBootstrap.cs`
- `Assets/_Game/Editor/FrostNovaPrototypePlayerFactory.cs`
- `Assets/_Game/Editor/FrostNovaPrototypeSceneBuilder.cs`
- `Assets/_Game/Editor/Operators/FrostNovaPrototypeOperatorBuilder.cs`
- `Assets/_Game/Editor/FrostNovaTuningWindow.cs`
- `Assets/_Game/Editor/PRTS/PrtsPrototypeAssetCatalog.cs`
- `Assets/_Game/Editor/PRTS/PrtsAssetDescriptor.cs`
- `Assets/_Game/Editor/PRTS/PrtsSpinePrefabBuilder.cs`
- `Assets/_Game/Editor/Effects/ExtractedFrameFxImporter.cs`

Operator Definition：

`Assets/_Game/Data/Operators/Operator_FrostNova.asset`

---

## 19. FrostNova FX 导入

主要输出：

`Assets/_Game/Art/FX/Extracted/FrostNova`

Winter 已使用/导入的主要 FX：

```text
frstar2_attack_01_start
frstar2_attack_01_trail
frstar2_attack_01_hit

frstar2_skill_02_range

frstar2_skill_03_start
frstar2_skill_03_range
frstar2_skill_03_range_02

frstar2_buff_03_start
frstar2_buff_04_start
frstar2_buff_05_start
```

当前不要重新把 buff01/02 接回 runtime。

---

## 20. 当前头像 / 图标

素材目录只发现一套霜星剧情立绘，没有确认到 4 套独立 HUD avatar。

目前所有 FrostNova skin 暂时共用：

`UI/HUD/Operators/frostnova_default`

当前 FrostNova 素材包里也没有可靠的 operator skill icon 套件，因此未伪造技能图标。

---

## 21. 音频

当前 frostn 的 manifest 中可以看到 FrostNova 相关音效语义，但本轮 FolderBridge 可访问的：

`D:\Effect\_output\frostn\sound`

并没有形成可直接绑定的一套实际音频文件。

因此 FrostNova 当前不要绑定猜测音频，也不要错误复用其他角色音效。

待用户补充/确认实际音频文件后再接。

---

## 22. 当前验证状态

最后一次 FolderBridge：

- source test：exit 0
- issues：0
- safe build：exit 0
- build mode：validation-only

注意：

**FolderBridge build 不是 Unity Editor 真正编译。**

每次 Unity Reload 后仍需要看 Console 是否出现真实：

- `error CSxxxx`
- Asset import error
- Spine material / SkeletonDataAsset error
- Missing prefab / missing FX

---

## 23. 下一步实机重点验证

优先验证以下几项：

1. **冰环**
   - Winter Slot 1 是否确实播放 `Skill_1`；
   - 有敌人时是否正确锁定；
   - Range FX 是否附着在目标而不是人物/地面；
   - 敌人移动时是否跟随；
   - 空放是否只有动作、没有 Range/Hit FX、没有伤害。

2. **普攻**
   - 朝右 Trail 起点；
   - 朝左 Trail 起点；
   - 两个方向最终都必须落到目标真实位置；
   - 不能再出现 Offset 导致“终点多一截”；
   - 动画结束后立即移动是否自然。

3. **Skill 3**
   - 使用 Winter 原生 Spine `Skill_3`；
   - 起手/收尾不能再有逐帧切换导致的位置跳变；
   - buff05 在整个 Skill3 期间隐藏；
   - Skill3 完整结束后 buff05 恢复。

4. **Buff05**
   - 朝右位置当前 `(-0.7, 0.5)`；
   - 朝左位置当前 `(0.5, 0.3)`；
   - 继续实机微调左右 Offset/Angle；
   - 确认持续循环中没有一帧明显闪烁/断档。

5. **Buff03**
   - 已确认眼部语义；
   - 目前仍默认关闭；
   - 后续需要确认它究竟在哪个阶段/技能中显示，再固定触发。

6. **Buff04**
   - 继续确认原版用途；
   - 在确认前保持关闭。

---

## 24. 不要回退的设计决定

后续接手请特别注意：

- 不要把 `spine_new` 覆盖到原 `spine`；
- 不要让 4 套 FrostNova prefab 重新按相同 BaseName 覆盖；
- 不要依赖仅在 Editor Configure 时写入的 `_skin`；运行时必须从 `PlayableOperatorIdentity.SkinId` 恢复；
- Winter Slot 1 当前明确用 `Skill_1`；
- Winter Slot 2 当前明确用 `Skill_3`；
- 不要重新启用 Skill3 逐帧覆盖角色方案，除非有新的明确理由；
- 不要把 `buff_05` 当技能一次性 FX，它是常驻背部层；
- Skill3 期间要隐藏 buff05；
- 不要把 buff01/02 接回来；
- 不要把 `f0030_flat.png` 重新导入 FX 动画；
- 不要用 `Lerp(origin,target)+offset` 做弹道，否则终点会被 Offset 推偏；
- 普攻左右 Trail Offset 必须可以独立调；
- 动画速度改变后仍必须完整播放；
- 普攻移动锁应跟角色攻击动画完成时间，而不是旧 recovery；
- FrostNova 当前调试技能是无限释放，不要误改全局技能系统；
- 当前只维护冬痕一套调参值，原皮未来复用该基准。

---

## 25. 本轮最后状态

本轮最后完成的三项：

### A. 冬痕冰环动作

从：

`Skill_2`

改为：

`Skill_1`

### B. 冰环空放

现在：

```text
无目标
→ 允许释放
→ 播放 Skill_1 完整动画
→ 不生成冰环打击 FX
→ 不结算该次范围伤害
```

### C. 普攻弹道

新增左右独立起点 Offset，并把插值改为：

```text
launchPosition = origin + directionalOffset
Lerp(launchPosition, realTargetPosition, t)
```

解决：

**起点偏移正确，但终点仍被 Offset 推出去一截** 的问题。

