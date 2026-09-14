# ArknightsACT

Unity 6 横版 2D ACT Roguelite 原型。

当前代码阶段：**Phase 0 + Phase 1 — 工程骨架与德克萨斯 Graybox ACT**。

## 当前可以验证什么

- 横版移动
- 跳跃
- 闪避与无敌时间窗口
- 德克萨斯占位四段连击
- Combo 输入缓冲
- Attack → Dash Cancel
- 统一 `CombatEntity / Health / DamageSystem`
- 击退
- HitStop
- Camera Shake
- Graybox Dummy Enemy
- Unity Input System 键鼠输入
- 核心 EditMode tests

目前尚未接入 PRTS 正式 Sprite/动画。这样做是有意的：第一阶段先让**战斗逻辑完全不依赖具体美术资源**，之后接入角色素材时只替换 Presentation 层。

## 打开方式

推荐 Unity 6.x。

首次打开后：

1. 等待 Package Manager 安装依赖；如果 Unity 提示启用 New Input System，选择启用（或 Both）并重启 Editor。
2. 菜单选择 `ArknightsACT > Build Prototype Scene`。
3. Unity 会生成 `Assets/_Game/Scenes/PrototypeRun.unity` 及德克萨斯四段攻击配置。
4. 打开 `PrototypeRun`。
5. Play。

### 操作

- `A/D`：移动
- `Space`：跳跃
- `J` 或鼠标左键：攻击
- `K` / Left Shift：闪避

## 架构

```text
Game.Core
  └─ 通用统计、纯数据基础

Game.Combat
  └─ CombatEntity / Health / Damage / Team

Game.Gameplay
  ├─ Input
  ├─ Character Motor
  ├─ Dash
  ├─ Attack
  ├─ Feedback
  └─ Graybox Enemy

Game.Editor
  └─ 原型场景/数据生成，不进入运行时包

Game.Tests.EditMode
  └─ 核心计算测试
```

### 明确禁止的结构

- 巨型 `PlayerController`
- `if (Texas) ... else if (Exusiai) ...`
- Summon 继承整个 Player Controller
- 动画直接修改血量
- 角色脚本硬编码具体 Sprite 路径
- Upgrade 每个都做独立 MonoBehaviour

## 后续顺序

1. 当前：德克萨斯 Graybox ACT
2. Status / Projectile / Burn / Shock / Ink
3. 剑雨 + DP + Facility + Tactical
4. Roguelite Upgrade Framework
5. 能天使
6. 夕 + SummonCombatEntity
7. 三人 Skill Evolution
8. Boss / 完整 8~12 分钟 Run
9. Mobile Touch

## 素材说明

PRTS / 明日方舟素材仅建议作为内部原型与玩法验证资源。正式公开发行或商业化前应替换成获得授权或原创素材，并重新核对实际发行地区的版权/二创规则。
