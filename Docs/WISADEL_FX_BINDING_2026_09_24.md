# Wisadel FX 完整扫描与当前绑定

更新时间：2026-09-24

素材来源：

```text
D:\Ark\_Unpacked\wisdel\effects\frames
```

本次重新扫描确认导出包共有 **79 组 FX**。此前工程只保留了 14 组，导致普攻、S2、S3 的复合层明显缺失。

### 2026-09-24 第二次重导出复核

新包的 unity_import_manifest.json.generatedAt 为 2026-09-24T05:58:34.043Z。

本次实际工程同步后，Wisadel 当前白名单已生成 **32 个 FX Prefab**（default + game#9 当前 Gameplay 所需集合）。

重导出同步使用独立 one-shot migration：
`WisadelReexportImportMigration20260924`。这一版会 `force: true` 重建一次，目的是防止同名旧 AnimationClip/Controller/Prefab 因增量判断而继续引用上一版帧序列。完成后仍回到正常增量刷新。

出生点测试假人由公共 `TrainingDummyFacility` 创建在**当前激活角色初始出生位置前方 3.2m**。它不是 Wisadel 角色组件；这个短距离偏移是为了避免与玩家 Collider 重叠，同时让 Basic/S3 Trail 一进场就有清晰的弹道测试目标。

必须以新 JSON 为准，但不能声称 JSON 提供了不存在的信息。当前新 manifest 实际是 skillTiming.effectSchedule = []，也就是没有导出“技能动画第几秒触发哪个 FX”的完整 releaseDelay 表。manifest 自己也明确写明：没有 effectSchedule 时，需要结合导出的 GIF/帧序列和原始 prefab 事件复核。

audio_mapping.json 同时给出了 ON_ABILITY_START、ON_ABILITY_ON、ON_SKILL_START、ON_SKILL_SPECIAL_POINT、ON_PROJECTILE_HIT 等事件语义。因此当前绑定遵循新包能明确证明的语义与 FX 根 tag：

- *_start：施法/攻击起手或状态进入；
- *_buff*：角色/技能状态层；
- *_hit*：命中/落点；
- **FX 根 tag 中的 *_trail*：Projectile Flight，必须从发射点飞向目标/落点。**

注意：timing.json 的 particles[].path 内部粒子节点偶尔也会包含 trail 字样，那只是 prefab 内部节点名；只有 FX 根 tag（例如 skill_03_trail）用于判定整组 FX 的 Gameplay 绑定类型。

当前可玩 Wisadel 使用：

- 原版 `default`
- `game#9`
- Gameplay 技能槽 1 = 原作 S2
- Gameplay 技能槽 2 = 原作 S3
- `sale#14` 当前不接入
- 原作 S1 当前没有装备到可玩槽，因此 S1 FX 只记录，不绑定到当前 Gameplay

---

## 1. 当前必须绑定的普攻 FX

### 普通方向 Start

```text
wisdel_attack_a_start
wisdel_attack_b_start
wisdel_attack_c_start
```

三段连击分别使用 A/B/C。

### 屏幕向下攻击 Start

```text
wisdel_attack_down_a_start
wisdel_attack_down_b_start
wisdel_attack_down_c_start
```

这些不是重复文件。

当角色攻击方向明显朝屏幕下方时，使用对应 Down A/B/C；无法使用 down 变体时才回退普通 A/B/C。

### Trail = 普攻弹道飞行

```text
wisdel_attack_01_trail
```

绑定为“枪口 / actor mount → 当前普攻目标”的真实飞行段。

它在 AttackStarted 时发射，沿 world-space 弹道移动；不能再 SpawnOnActor 粘在 Wisadel 身上。没有有效目标时可以沿当前瞄准方向播放 miss trajectory，但真正命中仍由 Gameplay 决定。

### Hit 是复合层

```text
wisdel_attack_01_hit
wisdel_attack_01_hit_02
```

命中时两层都要播放。

以前只绑定 `wisdel_attack_01_hit` 是不完整的。

---

## 2. 当前必须绑定的 S2 FX

原作 S2：饱和复仇。

### Cast / Start

```text
skill_02_start
```

### Buff 是复合层

```text
skill_02_buff
skill_02_buff_02
```

### Hit 是复合层

```text
skill_02_hit
skill_02_hit_02
```

### Overload 条件层

```text
skill_02_overload_start
```

`skill_02_overload_start` 已导入并绑定到 Wisadel FX Controller，但**当前简化版 S2 Gameplay 尚未还原原作 Overload 条件**。

因此：

- 不允许每次 S2 普通释放都强行播放它；
- Controller 提供显式触发入口；
- 等 S2 Overload Gameplay 条件还原后再从正确事件触发。

这样比“为了看起来完整而错误播放”更安全。

---

## 3. 当前必须绑定的 S3 FX

原作 S3：爆裂黎明。

### Start

default：

```text
skill_03_start
```

game#9：

```text
skill_03_start_game#9
```

### Up / Down Start

两套当前皮肤共用：

```text
skill_03_up_start
skill_03_down_start
```

新导出包复核后，不能再把 up/down 理解成“开始/结束生命周期”。两份 timing 的层级和时长高度对称，更符合**方向起手变体**：

- 普通/横向：`skill_03_start`
- 屏幕向上：`skill_03_up_start`
- 屏幕向下：`skill_03_down_start`

三者按当前射击方向三选一，不叠播。S3 结束只清理持续 Buff，不再错误触发 `skill_03_down_start`。

### Trail = S3 每发弹道飞行

default：

```text
skill_03_trail
```

game#9：

```text
skill_03_trail_game#9
```

它们不是 S3 起手光效，也不是角色常驻层。当前流程是：

S3 Start / UpStart → ProjectileLaunched → trail 从枪口飞向本次 impact center → 抵达 → hit + hit_02 + hit_03。

S3 三次 pulse 各自发射一条 trail，不再只在技能开始时播放一次。由于新 manifest 的 effectSchedule 为空，工程不会伪造“JSON releaseDelay”；只从当前 startup / pulseInterval 中切出一段视觉飞行时间，从而保持原 impact 节奏不整体后移。

导出的 trail 捕获序列可能明显长于实际 projectile 飞行时间（例如 S3 trail 捕获约 2.5s，而当前飞行段约 0.18s）。运行时只对 **trail** 将整段序列压缩到该次 flightSeconds 内播放完整；hit / buff / start 仍保持各自 timing.json 的原始时序。

### Hit 是三层复合

default：

```text
skill_03_hit
skill_03_hit_02
skill_03_hit_03
```

game#9：

```text
skill_03_hit_game#9
skill_03_hit_02_game#9
skill_03_hit_03_game#9
```

命中时三层同时播放。

### S3 持续 Buff 前后层

共用：

```text
skill_03_buff_b
skill_03_buff_f
skill_03_buff_02_b
```

Front 02：

default：

```text
skill_03_buff_02_f
```

game#9：

```text
skill_03_buff_02_f_game#9
```

这四层属于角色身上的持续表现，应在 S3 开始时激活，并在 S3 结束时统一清理。

其中：

- `b` = back layer
- `f` = front layer

不要把前后层合并成一个 Sprite，也不要漏掉其中一层。

---

## 4. 当前不绑定到可玩 S2/S3 的已导出 FX

### 原作 S1

```text
skill_01_hit
skill_01_hit_02
skill_01_hit_03
skill_01_start
skill_01_start_02
skill_01_trail
```

这些素材有效，但当前 playable loadout 没有 S1 槽，所以暂不绑定。

如果未来加入原作 S1，再按独立技能接入，不要误塞给 S2。

### Token / Talent / Bomb / Camouflage

```text
wisdel_birth_01_start
wisdel_bomb_01_hit
wisdel_bomb_buff_01
wisdel_camouflage_buff_01
```

这些不是普通普攻/S2/S3 的通用层。

应等 Wisadel 的 token / bomb / talent Gameplay 接入时再绑定到对应对象和生命周期。

### sale#14

所有以：

```text
_sale#14
```

结尾的 FX 当前全部排除。

用户当前只要求 default + game#9。

---

## 5. game#9 替换原则

不要看到 game#9 皮肤就给所有 FX 强行拼 `_game#9`。

只有导出包确实存在 game#9 专属版本时才替换。

目前确认专属：

```text
skill_03_start_game#9
skill_03_trail_game#9
skill_03_hit_game#9
skill_03_hit_02_game#9
skill_03_hit_03_game#9
skill_03_buff_02_f_game#9
```

其它没有 game#9 专属文件的层继续复用公共版本。

---

## 6. Tuning Panel 逻辑

Wisadel 调节面板中的复选框是**实时预览开关**，不是折叠 UI。

规则：

- 勾选 = 当前 Wisadel 立即播放并循环该 FX；
- 取消勾选 = 立即移除预览实例；
- 修改 RIGHT/LEFT offset = 已勾选预览实时更新；
- 关闭窗口 = 清理全部预览；
- 复选框只影响调试预览，不改变正式 Gameplay FX 是否启用。

当前新增的独立调节项：

```text
BasicDownStart
BasicHit02
Skill2Buff02
Skill2Hit02
Skill2OverloadStart
Skill3UpStart
Skill3DownStart
Skill3BuffBack
Skill3BuffFront
Skill3Buff02Back
Skill3Buff02Front
Skill3Hit02
Skill3Hit03
```

旧 9 个 slot 保持原枚举顺序，新项全部追加在后面，避免已有序列化 offset 索引错位。

---

## 7. FX FPS 规则

导出包中每个：

```text
effects/frames/<tag>/timing.json
```

包含该特效真实导出 FPS。

Wisadel 多组 FX 实测为：

```text
fps = 15
```

以前 `ExtractedFrameFxImporter` 固定按 30 FPS 生成 AnimationClip，会让 15 FPS 源特效错误快一倍。

现在规则：

> FX AnimationClip 必须读取自己的 `timing.json.fps`。

如果没有 timing.json 或 fps 无效，才回退 30 FPS。

注意：

> **角色 Spine 动作 2x 与 FX FPS 是两件不同的事。**

角色动作默认 2x，不代表 FX 也应该在源 FPS 基础上再乘 2。

---

## 8. 本次完整非 sale#14 扫描归类

### S1

```text
skill_01_hit
skill_01_hit_02
skill_01_hit_03
skill_01_start
skill_01_start_02
skill_01_trail
```

### S2

```text
skill_02_buff
skill_02_buff_02
skill_02_hit
skill_02_hit_02
skill_02_overload_start
skill_02_start
```

### S3

```text
skill_03_buff_02_b
skill_03_buff_02_f
skill_03_buff_02_f_game#9
skill_03_buff_b
skill_03_buff_f
skill_03_down_start
skill_03_hit
skill_03_hit_02
skill_03_hit_02_game#9
skill_03_hit_03
skill_03_hit_03_game#9
skill_03_hit_game#9
skill_03_start
skill_03_start_game#9
skill_03_trail
skill_03_trail_game#9
skill_03_up_start
```

### Basic

```text
wisdel_attack_01_hit
wisdel_attack_01_hit_02
wisdel_attack_01_trail
wisdel_attack_a_start
wisdel_attack_b_start
wisdel_attack_c_start
wisdel_attack_down_a_start
wisdel_attack_down_b_start
wisdel_attack_down_c_start
```

### Other character-specific

```text
wisdel_birth_01_start
wisdel_bomb_01_hit
wisdel_bomb_buff_01
wisdel_camouflage_buff_01
```

---

## 9. Agent 注意事项

以后重新接 Wisadel 或类似角色时：

1. **FX 根 tag 含 _trail 的效果是 projectile flight，不得绑到 Actor Start/Buff，也不得固定在角色身上。**
2. prefab 内部 particle 子节点名字含 trail 时，不据此重新分类整个 FX。
3. effectSchedule 为空时必须明确记录为空，禁止编造 releaseDelay。
4. 不允许只搜 `*_start / *_trail / *_hit` 各挑一个。
5. 同一事件可能有 `hit / hit_02 / hit_03` 多层。
6. `buff_b / buff_f` 是前后层，不是二选一。
7. 皮肤专属 FX 只替换实际存在的专属项。
8. 条件 FX（例如 overload）没有对应 Gameplay 条件时只导入/保留，不要错误常驻触发。
9. token/talent FX 不要误绑到玩家本体。
10. 每个 FX 的播放 FPS 读 timing.json；只有 projectile trail 可以为了匹配真实 flightSeconds 在运行时压缩序列。
11. 最终 offset 仍由用户游戏内微调。
