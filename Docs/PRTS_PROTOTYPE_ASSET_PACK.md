# PRTS Prototype Asset Pack

本项目只把 PRTS / 明日方舟资源作为本地玩法原型素材。

## 一键下载

Unity Editor：

```text
ArknightsACT
→ Assets
→ PRTS
→ Download Full Prototype Pack
```

也可单独下载：

- `Download Texas`
- `Download Prototype Enemies`

下载内容不会提交到 Git 仓库。

## 当前角色

| 用途 | PRTS 名称 | Model ID |
|---|---|---|
| 玩家 | 德克萨斯 | `char_102_texas` |

## 当前 Demo 敌人素材

| 原型用途 | PRTS 名称 | Model ID | 本地目录 |
|---|---|---|---|
| 杂兵/低血量单位 | 源石虫 | `enemy_1007_slime` | `OriginiumSlug` |
| 普通近战 | 士兵 | `enemy_1002_nsabr` | `Soldier` |
| 普通远程 | 弩手 | `enemy_1003_ncbow` | `Crossbowman` |
| 快速冲锋/追击 | 猎狗 | `enemy_1000_gopro` | `Hound` |
| 飞行敌人 | 妖怪 | `enemy_1005_yokai` | `YokaiDrone` |
| 精英/高硬直抗性 | 重装防御者 | `enemy_1006_shield` | `HeavyDefender` |

## 目录边界

下载后：

```text
Assets/_Game/Art/
├─ Characters/Texas/PRTS/Spine/
└─ Enemies/PRTS/
   ├─ OriginiumSlug/Spine/
   ├─ Soldier/Spine/
   ├─ Crossbowman/Spine/
   ├─ Hound/Spine/
   ├─ YokaiDrone/Spine/
   └─ HeavyDefender/Spine/
```

Gameplay/Combat 不允许直接引用这些路径。

之后通过 Presentation Adapter 把 Spine 动画映射到：

```text
Idle
Move
Attack
Hit
Die
Skill / Special
```

敌人 AI 和数值仍然是我们自己的 ACT 逻辑，不复制明日方舟塔防敌人的原始数值/行为。

## 版权边界

这些资源只用于本地原型验证。正式公开或商业发行前，替换为获得授权的素材或原创素材，并重新核对相关版权/二创规则。
