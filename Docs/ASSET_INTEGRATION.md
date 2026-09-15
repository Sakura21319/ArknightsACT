# Asset Integration

## 原则

PRTS 资源只进入本地 Art / Generated Presentation 流程，Gameplay / Combat 不直接引用具体素材路径。

```text
PRTS local source
    ↓ Editor importer / prefab builder
Generated Presentation Prefab
    ↓
Character-specific presentation adapter
    ↓ events/state
Gameplay / Combat
```

核心代码不得依赖：

- PRTS URL
- 中文资源名
- atlas / skel 文件名
- `char_xxx` model id
- PRTS 文件夹结构

## 当前陈接入

战斗模型：

```text
Assets/_Game/Art/Characters/Chen/PRTS/Spine
char_010_chen
```

移动动作源：

```text
Assets/_Game/Art/Characters/Chen/PRTS/BaseMotion
build_char_010_chen
```

可见角色始终使用战斗模型。BaseMotion 模型隐藏，仅提供 `Move` 骨骼动作，通过 `SpineBoneMotionRetarget2D` 重定向。

## 表现与战斗职责

`ChenPresentationDriver2D` 负责：

- 三段普攻对应哪个 PRTS clip/片段
- 技能 1 / 技能 2 对应哪个 PRTS clip
- 播放速度
- Idle / Move / Die 的表现切换

`PlayerAttackController / ChenSkill1 / ChenSkill2` 负责真实命中、伤害、冷却和战斗状态。

动画不能直接修改 HP；Gameplay 也不通过动画帧事件作为唯一伤害来源。

## 替换美术

未来若替换成授权/原创角色素材，应优先替换 Presentation Adapter / Prefab，而不是改 `DamageSystem`、移动、房间或敌人逻辑。
