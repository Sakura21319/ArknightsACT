# Architecture

## Dependency direction

```text
Game.Core
   ↓
Game.Combat
   ↓
Game.Gameplay
```

高层允许依赖低层，低层不能反向依赖。

## Core

不关心玩家、敌人、输入、动画或 Roguelite，只放可复用基础数据结构。

## Combat

负责 CombatEntity、Team、Health、DamageContext、DamageSystem。未来继续放 Status、Effect、GameplayTag、Combat events。Combat 不知道德克萨斯是谁。

## Gameplay

组合 Combat：PlayerInputReader、PlayerMotor2D、PlayerDashController、PlayerAttackController、AttackDefinition、CameraShake2D、HitStopService、DummyEnemy。

未来 CharacterDefinition 决定角色配置，不通过 `if (characterName)` 分支。

## Presentation

角色 Sprite、动画、VFX、SFX 必须是可替换引用。Gameplay 调用表现层接口/组件，但不把资源路径写进战斗规则。

## Summon（后续）

```text
CombatEntity
├─ Player Combat Entity
├─ Enemy Combat Entity
└─ Summon Combat Entity
```

Summon 共享 Health、Damage、Status、Trigger，但只拥有 Owner reference 和 Follow/Target/Attack AI。绝不复制 Player input、Jump、Dash、Skill bar。
