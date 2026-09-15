# ArknightsACT

Unity 6 横版 2D ACT Roguelite 原型。

当前主线方向已经收敛为：**陈（Ch'en）作为主控角色的横版 2D ACT**。

> PRTS / 明日方舟素材仅用于本地原型与玩法验证。正式公开发行或商业化前应替换为获得授权或原创素材。

## 当前可验证内容

- 横版移动、跳跃
- Dash / 短暂无敌
- 陈三段地面普攻
- 普攻输入缓冲
- Attack → Dash Cancel
- 两个主动技能
- PRTS Spine 原版动作映射
- 敌人近战 / 快速近战 / 远程原型
- 房间循环、敌人血量成长、清房回血
- HitStop、命中闪色、轻微 Camera Shake

当前刻意**不做**：

- 空中攻击
- 下劈
- Dash Attack
- 自动攻击
- TopDown / Survivor 模式
- Texas Build / 剑雨 / 雷系实验
- 普攻额外斩击特效
- Roguelite 三选一（等陈基础战斗验证后再重新设计）

## 陈的动作映射

本地 PRTS `char_010_chen` 当前发现 13 段动画，其中战斗使用：

```text
普攻 1 -> Attack 前半段
普攻 2 -> Attack 后半段
普攻 3 -> Skill

技能 1 -> Skill_2 + Skill_End_2
技能 2 -> Skill_3 + Skill_End_3

Idle -> Idle
Death -> Die
Move -> build_char_010_chen 的 Move 骨骼重定向到战斗模型
```

`Attack_Pre / Attack_End / Skill_End` 暂不作为独立招式。

## 操作

```text
A / D             移动
Space             跳跃
J / 鼠标左键       三段普攻
K / Left Shift    Dash
L                 技能 1（赤霄·拔刀）
I / 鼠标右键       技能 2（赤霄·绝影）
```

普通攻击和两个技能当前都只允许在地面触发。

## 首次本地运行

推荐 Unity 6.x。

1. 等待 Package Manager 完成依赖安装。
2. 如果缺少 Spine Runtime：`ArknightsACT > Assets > PRTS > 1. Install Spine 3.8-Compatible Runtime`。
3. 下载资源：
   - `Download Ch'en`
   - `Download Ch'en Base Motion Source`
   - `Download Prototype Enemies`
   - 或直接 `Download Full Prototype Pack`
4. 执行 `2.5 Apply High Quality Texture Settings`。
5. 执行 `3. Build Presentation Prefabs`。
6. 执行 `4. Validate Presentation Setup`。
7. 执行 `ArknightsACT > Build Prototype Scene`。
8. 打开/运行 `Assets/_Game/Scenes/PrototypeRun.unity`。

如果需要重新确认陈的 PRTS 动作目录，进入 Play Mode 后运行：

```text
ArknightsACT > Diagnostics > Dump Ch'en Animation Catalog
```

## 代码职责

```text
Game.Core
  └─ 通用统计/纯数据基础

Game.Combat
  └─ CombatEntity / Health / Damage / Team / Status

Game.Gameplay
  ├─ Input
  ├─ PlayerMotor2D
  ├─ PlayerDashController
  ├─ PlayerAttackController
  ├─ PlayerSkillController
  ├─ Characters/Chen
  ├─ Enemy AI
  └─ Feedback / Presentation adapters

Game.Editor
  └─ PRTS 本地素材接入、Prefab 构建、Prototype Scene 生成
```

保持以下规则：

- 不做巨型 `PlayerController`。
- 通用战斗代码不写 `if (Chen)` / `if (Texas)`。
- 角色特有技能与动作映射放进 `Gameplay/Characters/<Character>`。
- Spine 动画不是伤害权威；伤害由 Gameplay / Combat 结算。
- PRTS 路径只存在于 Editor 资产接入层。

## 当前 Handoff

继续开发前先读：

`Docs/HANDOFF_CHEN_2D_ACT.md`
