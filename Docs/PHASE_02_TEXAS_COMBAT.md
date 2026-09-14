# Phase 02 — Texas Combat Lab

目标：在完整 Roguelite 系统之前验证德克萨斯本体战斗是否足够爽。

## 操作

- A/D：移动
- Space：跳跃
- J / LMB：四段普攻
- K / Shift：Dash
- L / RMB：剑雨
- 1：开关 Swift Blade（第 8 次攻击动作产生剑气）
- 2：开关 Residual Thunder（剑雨结束留下 3 秒雷场）
- 3：开关 Conductive（攻击 Shock 敌人减少剑雨 CD）

## 代码边界

- `Combat.Status.StatusController`：通用状态容器，当前先支持 Shock / Burn / Ink 类型。
- `AreaDamageResolver`：通用范围伤害去重，技能和 Build 不再各写一套 Collider 遍历。
- `IPlayerSkill + PlayerSkillController`：所有角色保持一个主动技能槽。
- `PlayerAttackController`：只负责通用攻击，并暴露 `AttackStarted / AttackHit` 信号。
- `Characters/Texas/`：德克萨斯专属技能和 Build 效果。
- `TexasSwiftBladeEffect / TexasResidualThunderEffect / TexasConductiveEffect`：三个 Build 独立组件，互不揉在一个 Runtime 类里。
- `TexasBuildLab`：Phase 2 的数字键调试器，之后 Roguelite Upgrade Runtime 直接调用 Set 方法。
- `Presentation/`：斩击、剑雨、Shock 标记和角色占位 Rig。

## PRTS 素材

Unity 菜单新增：

`ArknightsACT > Assets > Download Texas PRTS Spine Source`

它会按 PRTS `/德克萨斯/spine` 提供的 `char_102_texas` 路径，把 Spine 源文件下载到独立 Art 目录。当前不强绑 Spine Runtime，所以没有素材时仍可用 `TexasPlaceholderRig2D` 试玩。

## 当前测试重点

1. 四段斩击的节奏是否有明显层次。
2. 剑雨两次伤害是否有足够反馈。
3. Shock 标记是否容易辨认。
4. Residual Thunder 是否让“技能流”开始形成循环。
5. Conductive 是否能让攻击 Shock 敌人的行为明显缩短剑雨等待时间。
6. Swift Blade 第 8 次攻击的剑气是否值得继续保留。
