# 地图互动元素扩充与轮椅系统交接

在 `main` 工作区直接改动（应要求未开新分支）。前置设计见 `CITY_INTERACTIONS_LAYER2_HANDOFF_2026_09_22.md` 与 `CITY_EXPLORATION_POLISH_HANDOFF_2026_09_23.md`。

## 新增可互动设施

沿用既有「按住 G 读条」设施体系（`CityFacilityKind` 追加枚举值，未改动已有序号）。每个公共设施院在主设施之外新增一台**次级设施**，小地图按未使用设施统一以紫色菱形显示，无需额外图标逻辑。

| 设施 | 操作 | 效果 |
| --- | --- | --- |
| 源石充能桩 ChargeStation | 按住 G 3 秒 | 两个技能各回复 15 点技力，一次性 |
| 应急净水点 WaterStation | 按住 G 1.5 秒 | 恢复 12% 最大生命，一次性；满血不消耗 |
| 废料回收堆 ScrapCache | 按住 G 2 秒 | 回收换得 4 源石锭，一次性 |

生成规则：起点街区次级设施固定为净水点；其余设施院按 `(区块号 / 3 + 阶段序号) % 3` 轮换三种。设施仍只属于当前地图，随关卡销毁，不写入长期存档。

## 轮椅

- 起点街区（医疗补给院）必生成一台应急轮椅并带世界文字标识；其余每第 6 个区块的设施院也会生成一台。
- 靠近按住 G 0.6 秒乘坐，再按住 G 0.6 秒离开；起身后站在轮椅侧方，轮椅留在原地可再次乘坐。受击、移动超过 0.5 米会中断上下车读条。
- 轮椅交互与设施共用 `CityFacilityController` 的 G 键通道：范围内有可用设施时设施优先，否则选中轮椅，不与搜索 F / 出口 E 争用。
- 乘坐时禁用 `PlayerMotor25D`，由 `WheelchairLocomotion25D` 驱动同一 `CharacterController`，按现实手动轮椅调校：
  - 最高速度 2.0 m/s（步行 4.8 的约四成）；
  - 加速度 2.4 m/s²，起步明显迟缓；输入有推轮节奏平滑，不按即时响应；
  - 减速度 0.8 m/s²，松手后惯性滑行约 2.5 秒才停；
  - 转向角速度随速度从 220°/s 降到 105°/s，急转/反向会先把速度泄掉再弧线掉头，不能瞬间反向；
  - 不能跳跃；通过 `IPlayerControlLockSource` 屏蔽冲刺；攻击与技能不受影响（技能施放锁移动仍生效）。
- 角色死亡、局内换人（`PlayerTransform` 变更）或控制器卸载会自动下车并恢复 `PlayerMotor25D`。
- 轮椅本体无碰撞体（交互按距离判定，障碍仍由玩家 CharacterController 解决）；乘坐时每帧跟随玩家位置并按移动朝向旋转。

## 代码

- `World/CityFacility25D.cs`：新增三种设施类型、文案与效果。
- `World/CityFacilityController.cs`：轮椅注册、候选选择（设施优先）、上下车读条、乘坐状态与自动下车；暴露 `CurrentActor` 供充能桩补技力。
- `World/Wheelchair25D.cs`：轮椅世界对象与盒体拼装外观（大后轮、万向前轮、扶手、脚踏、推手）。
- `World/WheelchairLocomotion25D.cs`：乘坐期玩家移动（拟真轮椅物理）。
- `World/RogueliteStageCityStreetsController.Sectors.cs`：次级设施与轮椅的生成及外观。

以上路径相对 `Assets/_Game/Scripts/Gameplay/Roguelite/`。

## 验证

`Tools/compile_check.py` 通过：Game.Gameplay / Game.Editor 各 0 错误（新文件已被编辑器吃进响应文件）。

`Assets/_Game/Editor/MobileCityGenerationValidation.cs` 已同步更新：通用设施循环覆盖三种新设施（激活、打断、一次性、净水点满血保留与 12% 治疗量）；Stage 2 设施种类断言从 6 更新为 9；新增 `ValidateWheelchairs`（轮椅已注册、无碰撞体、可被 G 选中读条、无 `PlayerMotor25D` 的裸 Actor 无法乘坐且不抛异常、移动中断读条）。编辑器打开状态下未实际执行该入口，需在 Unity 中跑 `MobileCityGenerationValidation.Run`（或交互菜单）确认。

尚未跑 Unity Play Mode：上下车手感、惯性滑行距离、转向泄速节奏、与设施交互优先级、换关/换人边界需在正式 `PrototypeRun` 场景试玩确认；数值（速度/加减速/转向率/技力与源石锭奖励）预期按试玩反馈再调。

## 2026-09-24 晚：手感与外观调整（按试玩反馈）

**移动手感**（`WheelchairLocomotion25D.cs`）

- 最高速度 2.0 → **3.0 m/s**；加速 2.4 → **2.0 m/s²**（约 1.5 秒缓慢推到满速）；松手减速 0.8 → **1.0 m/s²**（满速约 3 秒缓慢刹停）。
- 新增 **Shift 漂移**：移动速度 > 1.1 m/s 且有方向输入时按住 Shift 进入漂移——轮子转向率 ×2.1 急转，但动量方向仅以 150°/s 追随新朝向（正常为 900°/s 近似锁定），轮椅沿原路线侧滑；漂移额外以 1.3 m/s² 磨掉速度，松开 Shift 或速度过低自动恢复抓地。漂移时机身向滑动方向侧倾（最大 12°）。

**外观**（`Wheelchair25D.cs`）

- 轮子改为**真·圆轮**：内置圆柱网格做轮胎，外加发光轮环、轮毂、3 根辐条；后轮随滚动距离真实自转，万向前轮也是圆盘。
- 花里胡哨套件：青色霓虹材质（克隆自暖窗材质加强自发光）用在轮环、侧裙灯条、底盘氛围灯、靠背灯条、推手把套，另加一根斜插的安全旗杆 + 霓虹旗面。
- 新增**轮胎轨迹**：两个后轮接地点各挂一条 TrailRenderer（暖橙色、2.6 秒淡出），滚动速度 > 0.4 m/s 时留下轮迹，漂移侧滑时自然甩出弧线；下车即停止。

交互提示文案同步更新（乘坐提示中加入 Shift 漂移说明）。

**验证**：`Tools/compile_check.py` 通过（Game.Gameplay / Game.Editor 各 0 错误）。`SyncPose` 签名变为 `(position, heading, rollDistance, leanDegrees)`，仅轮椅移动组件一处调用，已同步。Play Mode 待试玩：漂移的滑动手感、侧倾幅度、轨迹颜色/粗细、漂移磨速数值按体验再调。

## 2026-09-24 晚：乘坐姿态与透视关系

- `WheelchairLocomotion25D` 乘坐时通过 `SpineBoneMotionRetarget2D` 持续播放 build/BaseMotion 的 `Sit` 动作；组件执行顺序设为 900，在普通 Gameplay/Presentation 更新之后、motion source(1000)之前重新确认 Sit，避免轮椅移动时回到站立 Idle。对于启用 full-source BaseMotion 的角色（当前 Wisadel default/game#9），Sit 直接显示完整 build Spine，而不是只复制骨骼，因此皮肤专属坐姿/attachment 不会被 combat Spine 覆盖。
- 下车销毁 `WheelchairLocomotion25D` 时自动 `StopMotionAction()`，恢复普通角色表现。
- 该逻辑依赖角色导入流程保留 `Sit` BaseMotion；没有 Sit 的角色不会伪造替代动作。
- 轮椅整体视觉缩放调整为 **1.22x**；跟随骑乘者时沿行进朝向向身后偏移 **0.10m**，让角色 Sit 姿态更自然地落入座椅/靠背层次，而不是与轮椅根节点完全共面。
- 为避免放大后的后轮/车架在 2.5D 透视里挡住人物主体，轮椅视觉额外沿 gameplay camera forward（远离相机方向）后压 **0.16m**；角色仍保持碰撞/移动根节点不变，这只是视觉景深调整。
- 后轮自转半径同步乘以视觉缩放，避免放大轮椅后轮胎转速与移动距离不匹配。
