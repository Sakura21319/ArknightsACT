# PRTS Prototype Asset Pack

本项目只把 PRTS / 明日方舟资源作为本地玩法原型素材，不提交角色/敌人的原始二进制资源。

## 一键下载

Unity Editor：

```text
ArknightsACT
→ Assets
→ PRTS
→ Download Full Prototype Pack
```

也可单独下载：

- `Download Ch'en`
- `Download Ch'en Base Motion Source`
- `Download Prototype Enemies`

## 当前玩家角色

| 用途 | PRTS 名称 | Model ID | 本地目录 |
|---|---|---|---|
| 战斗模型 | 陈 | `char_010_chen` | `Assets/_Game/Art/Characters/Chen/PRTS/Spine` |
| 移动动作源 | 陈·基建 | `build_char_010_chen` | `Assets/_Game/Art/Characters/Chen/PRTS/BaseMotion` |

战斗模型保持武器与战斗附件；基建模型只作为隐藏的 `Move` 骨骼动作源，通过 `SpineBoneMotionRetarget2D` 重定向到战斗模型。

## 当前 Demo 敌人素材

| 原型用途 | PRTS 名称 | Model ID | 本地目录 |
|---|---|---|---|
| 杂兵 | 源石虫 | `enemy_1007_slime` | `OriginiumSlug` |
| 普通近战 | 士兵 | `enemy_1002_nsabr` | `Soldier` |
| 普通远程 | 弩手 | `enemy_1003_ncbow` | `Crossbowman` |
| 快速近战 | 猎狗 | `enemy_1000_gopro` | `Hound` |
| 飞行候选 | 妖怪 | `enemy_1005_yokai` | `YokaiDrone` |
| 精英候选 | 重装防御者 | `enemy_1006_shield` | `HeavyDefender` |

当前 `PrototypeRun` 房间循环实际使用士兵、猎狗、弩手三种模板；其余保留为后续原型素材。

## 接入流程

```text
Download assets
→ 2.5 Apply High Quality Texture Settings
→ 3. Build Presentation Prefabs
→ 4. Validate Presentation Setup
→ Build Prototype Scene
```

生成的 Presentation Prefab 位于：

```text
Assets/_Game/Generated/PRTS/Prefabs
```

下载资源和生成资源均应保持 local-only / ignored。

## 代码边界

Gameplay / Combat 不允许直接依赖：

- PRTS URL
- PRTS 本地目录
- `char_010_chen` 字符串
- atlas / skel 文件名

这些信息只存在于 Editor 资产接入层。运行时只看到已经挂接好的 Presentation 组件。

## 版权边界

这些资源只用于本地原型验证。正式公开或商业发行前，替换为获得授权的素材或原创素材，并重新核对相关版权/二创规则。
