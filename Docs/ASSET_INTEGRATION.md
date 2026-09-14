# Asset Integration

## 原则

PRTS 资源只进入：

```text
Assets/_Game/Art/
Assets/_Game/Audio/
```

核心代码不得依赖 PRTS URL、中文资源名、Sprite 文件名、动画文件夹结构。

## 角色资源接入

未来创建：

```text
CharacterDefinition
CharacterPresentation
```

`CharacterDefinition`：战斗配置。  
`CharacterPresentation`：Sprite/Animator/VFX/SFX。

替换美术资源时不改 Combat / Gameplay 代码。

## 当前阶段

Phase 1 暂用 `PlaceholderVisual2D` 运行时生成纯色 Sprite。

等 Graybox 手感通过后再接德克萨斯素材，以避免“动画资源先决定代码结构”。
