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

## 新增底层

- `Combat.Status.StatusController`
- `CombatStatusType`：Shock / Burn / Ink，为后续能天使和夕复用
- `IPlayerSkill` + `PlayerSkillController`：角色仍只有一个主动技能按钮
- `PlayerAttackController` 暴露 AttackStarted / AttackHit 事件，而不写入德克萨斯业务
- 德克萨斯专属逻辑全部放在 `Characters/Texas/`
- 斩击与剑雨表现全部位于 Presentation 层

## 当前 Build Lab

这一阶段的 1/2/3 键只是调试入口。后续 Roguelite 三选一不会改写这些效果，只把“布尔开关/参数修改”的来源改成 Upgrade Runtime。

## 素材

正式 PRTS 干员战斗模型尚未与核心代码绑定。PRTS 页面显示德克萨斯原作 S2「剑雨」为手动触发的范围法术伤害技能并带控制效果；本原型保留「多段范围剑雨」作为辨识核心，再转译成 ACT 的 Shock / Build 联动。

角色 Sprite/Spine 后续只替换 `Presentation`，不会进入 Combat / Texas Build 逻辑。
