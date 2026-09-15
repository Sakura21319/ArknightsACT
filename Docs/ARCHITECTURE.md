# Architecture

## Dependency direction

```text
Game.Core
   ↓
Game.Combat
   ↓
Game.Gameplay
   ↓
Character-specific composition / Presentation adapters
```

高层允许依赖低层，低层不能反向依赖。

## Core

不关心玩家、敌人、输入、动画或 Roguelite，只放可复用基础数据结构。

## Combat

负责：

- `CombatEntity`
- `Team`
- `Health`
- `DamageContext / DamageResult`
- `DamageSystem`
- Status 基础

Combat 不知道陈、德克萨斯或任何具体角色是谁。

## Gameplay 通用层

当前主要组件：

```text
PlayerInputReader
PlayerMotor2D
PlayerDashController
PlayerAttackController
PlayerSkillController
AttackDefinition
PrototypeEnemyCombatBrain2D
PrototypeRoomLoopController
HitStopService
CameraShake2D
```

这些组件不能通过角色名分支决定行为。

## Character-specific layer

角色特有逻辑组合在：

```text
Assets/_Game/Scripts/Gameplay/Characters/<Character>/
```

当前陈：

```text
ChenSkill1
ChenSkill2
ChenPresentationDriver2D
```

角色特有技能可以依赖通用 Combat / Gameplay；通用 Gameplay 不能依赖 `ChenSkill1` 等具体实现。

## Presentation

角色动画、VFX、SFX 必须保持可替换。

PRTS 接入路径只存在于 Editor 资产接入层。运行时 Gameplay 不硬编码：

- PRTS URL
- model id
- atlas/skel 路径

当前 Spine 动画映射由 `ChenPresentationDriver2D` 负责，真实伤害仍由 Gameplay / Combat 负责。

## 禁止结构

- 巨型 `PlayerController`
- `if (characterName == "Chen") ...`
- 动画直接修改 HP
- 角色技能散落进通用 `PlayerAttackController`
- Gameplay 硬编码 PRTS 文件路径

未来如果重新加入 Roguelite Build，也应以通用升级框架 + 角色配置/效果组件组合，不回到每个实验功能互相引用的结构。
