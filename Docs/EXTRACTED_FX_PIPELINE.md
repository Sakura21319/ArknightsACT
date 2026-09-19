# 提取特效的两段式工作流

当前项目将特效处理固定为两部分：

```text
第一部分：外部 EffectExtractor.exe
    游戏 AB -> 转换主包 -> 扫描依赖 -> Unity 渲染 PNG 帧

第二部分：本 Unity 项目导入器
    PNG 帧 -> Sprite/Animation/Prefab -> 挂到角色挂点 -> 由技能事件播放
```

## 第一部分：提取

使用 `D:\Effect\EffectExtractor.exe`，输出目录可以保持默认。工具输出的有效输入是：

```text
<run>\effects\frames\skill_02_start\f0001.png
<run>\effects\frames\skill_03_hit_01\f0001.png
<run>\effects\frames\attack_01_start\f0001.png
<run>\effects\frames\attack_01_hit\f0001.png
```

也支持选择交付包中的：

```text
<delivery_v2>\frames\skill_02_start\f0001.png
```

GIF 和 contact sheet 仅用于预览；Unity 导入器使用 PNG 帧。

## 第二部分：Unity 导入并应用

在 Unity 菜单执行：

```text
ArknightsACT > Assets > Import Extracted Frame FX
```

选择提取结果根目录、`delivery_v2`、`effects` 或 `frames` 任意一层即可。导入器会：

1. 将 PNG 复制到 `Assets/_Game/Art/FX/Extracted/<角色>/Frames/`。
2. 设置 Sprite、透明、Clamp、无压缩和 512 PPU。
3. 为每个 `skill_*` 和 `attack_*` 文件夹创建 AnimationClip、AnimatorController 和 Prefab。
4. Prefab 使用项目 Shader，并附带帧动画生命周期组件和 2.5D billboard。

Chen 使用“导入并应用到当前陈原型”时，会按以下映射自动接入：

```text
游戏槽位 1 / ChenSkill1 -> skill_02_start、skill_02_buff、skill_02_hit
游戏槽位 2 / ChenSkill2 -> skill_03_start、start_02、start_03、hit_01..hit_10
普攻第 1/2 段 AttackStarted/AttackHit -> attack_01_start / attack_01_hit
普攻第 3 段 AttackStarted/AttackHit -> skill_01_start / skill_01_hit
```

运行时控制器只监听普攻/技能事件，不修改伤害、目标、冷却或技能状态。出刀效果挂在
`Player_Chen/CustomFxMountPoint`，命中效果挂在目标 Transform。横向刀光和普攻出刀会按角色朝向
水平镜像，不再固定朝右。

### 游戏内实时调参

进入 Play Mode 后，左上角会显示 `Chen FX Tuning` 面板。面板可以实时调整：

- S2 Start/Buff/Hit：分别调整出现位置 X/Y，以及整体等比缩放
- S3 Start/Start02/Dragon/Hit：分别调整出现位置 X/Y，以及整体等比缩放
- 普攻 Attack/Hit：分别调整出现位置 X/Y，以及整体等比缩放
- `Dragon back depth`：绝影起手龙相对摄像机的深度；正值表示远离摄像机、位于人物后方

这里的 X/Y 是出现位置偏移，不是把特效横向或纵向拉伸；缩放始终保持等比。

`Save` 会保存到当前机器的 Unity PlayerPrefs，下次运行仍会加载；`Reset` 恢复项目默认值。按 `F8` 可隐藏/显示面板。

当前原型中两个主动技能的基础冷却均为 1 秒。

2 技能横向刀光第 1 帧出现时立即结算伤害；出生点还会自动生成一个使用大盾模型、可受击但不会死亡/攻击的
`Chen_TrainingDummy_Infinite` 测试假人。假人会触发红闪、受击弹动和伤害数字，受伤后立即回满血。

如果当前场景没有 `Player_Chen`，导入器会保留 Prefab，并在下次执行 `ArknightsACT > Build Prototype Scene` 时由 `ChenPrototypePlayerFactory` 自动接入。

## 资产边界

- 该流程导入的是渲染帧，不恢复原客户端 `OriginalClient` MonoBehaviour。
- 不重新启用 `ChenOriginalSkillFxController`。
- 游戏逻辑仍由 `ChenSkill1`、`ChenSkill2` 和通用战斗系统负责。
- 如果需要可编辑的原始 ParticleSystem/材质层级，应继续使用 OHMS Structured 导入流程；它与本帧序列导入器是两条不同用途的路径。
