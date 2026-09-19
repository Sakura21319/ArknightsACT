# 切尔诺伯格城市地图交接记录

更新时间：2026-09-18  
适用范围：当前工作树中的 2×2 城市街区重建与视觉样式开发

## 1. 本阶段目标

本阶段不是扩大区块数量，而是保留 2×2 的关卡结构，扩大每个区块的实际街区尺度，并让四个区块拥有清晰、符合人类认知的城市功能。

最终视觉方向由三张概念预览合并而来：

1. 预览 1：冷灰工业主街、宽阔十字路口、连续人行道和斑马线。
2. 预览 2：住宅与商业混合街区、二层楼体、阳台、店面和檐廊。
3. 预览 3：工业厂房、储罐、管架、检查车道和门禁检查站。

预览 4 不作为主样式。整体应保持《明日方舟》切尔诺伯格式的冷灰工业城镇感：低密度、可读、功能明确，避免建筑堆叠和廉价页游式装饰。

## 2. 当前固定地图结构

地图固定为 2×2，坐标原点在四个区块的中央路口附近：

```text
(0,1) Industrial / 工业区       (1,1) Checkpoint / 检查站与 Boss 区
(0,0) Residential / 住宅起始区  (1,0) Commercial / 商业与商店区
```

当前物理尺寸：

- 每个区块：`30 × 24` 世界单位。
- 主路宽度：`6.10` 世界单位。
- 人行道宽度：`1.55` 世界单位。
- 四个区块各自生成中央十字道路的一半，拼接后形成一个连续的城市十字路口。
- 起点为左下住宅区，右上为 Boss/检查站区。

逻辑区块类型和城市外观类型仍然分离：`RogueliteBlockType` 负责战斗/商店/Boss 流程，`ChernobogDistrictType` 负责城市空间语义。

## 3. 已完成的视觉实现

主要文件：

- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteStageWorldMetrics.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/RogueliteStageCityStreetsController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/RogueliteStagePlayableArchitectureController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/RogueliteStageDistrictTemplateController.cs`
- `Assets/_Game/Scripts/Gameplay/Roguelite/Routing/RogueliteStageRuntimeController.cs`

### 道路与路口

- 主路从原来的窄条调整为宽阔城市道路。
- 每个区块生成对应的人行道、路缘、排水沟和车道标线。
- 四个区块拼接处增加连续斑马线，强化“真实十字路口”识别。
- 不要再在单个区块中心额外盖一条随机黑色十字路，否则会与 `CityStreets` 的道路重叠。

### 住宅区

- 保留 `PlayableArchitecture` 中可进入的 `WalkInStreetTenement`，它负责实际门口、坡道、二楼和导航节点。
- `CityStreets` 增加两段低密住宅立面，用于形成预览 2 的住宅街廓。
- 新增住宅立面是纯视觉楼体，不带碰撞体；可进入建筑的碰撞只由 `PlayableArchitecture` 管理。

### 商业区

- 保留可进入的 `WalkInCommercialUnit`。
- 市场排增加二层窗、店门、遮棚和檐廊柱，避免商业区只显示为一排无功能方块。
- 商业建筑仍应保持低密度，并保留面向道路的装卸空间。

### 工业区

- 保留工业厂房和可用的两层设施空间。
- 新增工业庭院、两个储罐、管架、水平管线和可选工作灯，复刻预览 3 的工业功能感。
- 储罐和管线均为纯视觉元素，不添加碰撞体，避免敌人和玩家被装饰物卡住。

### 检查站

- 保留检查站门房和可穿行的检查棚。
- 新增从南侧主路伸入区块内部的直线检查车道、人行通道、车道标线、门架、信号灯和隔离栏。
- 检查站的内部车道应保持清晰，不能用过多箱体堵塞通路。

## 4. 运行时生成顺序

`PrototypeStageRuntimeFactory` 当前按以下职责组织运行时内容：

1. `RogueliteStageRuntimeController`：创建区块地面、基础障碍和导航图。
2. `RogueliteStageDistrictTemplateController`：给每个区块写入城市区域标记。
3. `RogueliteStageUrbanCompositionController`：提供旧版工业城市的背景构成。
4. `RogueliteStagePlayableArchitectureController`：提供可进入房间、坡道、二楼和可用掩体。
5. `RogueliteStageCityStreetsController`：提供道路、街区立面和本阶段的主视觉焦点。
6. `RogueliteStageUrbanDensityController`：削减与新街区重复的旧工业壳体。
7. `RogueliteStageCompositionCleanupController`：处理建筑与装饰重叠。
8. `RogueliteStageDressingCollisionController`：只给指定的废墟/货物/脚手架装饰补碰撞。

如果新增控制器，必须明确它属于“逻辑地面、可玩建筑、街道视觉、装饰”中的哪一层，不能让多个控制器同时拥有同一座建筑的最终碰撞。

## 5. 敌人卡建筑约束

- `PrototypeNavigationGraph25D` 是敌人的主要路径依据；修改建筑位置时，要同步检查 `RogueliteStageRuntimeController.BuildNavigationGraph()` 中对应的入口、坡道和二楼节点。
- 纯视觉楼体、储罐、管线、门架标识默认不加碰撞体。
- 真正可进入的建筑必须使用“分段墙体 + 明确门洞”，不要使用覆盖整个建筑体积的单一实心碰撞体。
- 不要把装饰碰撞放进中央十字道路、区块连接口或导航节点附近。
- 生成完成后保留 `Physics.SyncTransforms()`，否则 CharacterController 可能在同一帧使用旧的碰撞位置。
- 新增建筑后需要实际测试：敌人从道路进入区块、绕过建筑追击玩家、从可进入房间退出，以及跨区块连接口移动。

## 6. 验证状态

已完成：

- `RogueliteStageCityStreetsController.cs` 源码级编译检查通过，`CSC_EXIT=0`。
- 针对本阶段两个街道尺寸/生成脚本执行了 `git diff --check`，没有发现新增格式错误。
- 新增的视觉楼体与工业设备默认不添加碰撞体。

尚待完成：

- 当前已有一个 Unity 实例打开项目，批处理 Unity 无法同时占用同一项目，因此本轮没有重新启动 Unity 做全量脚本编译和 Play Mode 截图验证。
- 需要在现有 Unity 实例刷新脚本后重新执行 `ArknightsACT > Build Prototype Scene`，检查场景实际画面。
- 需要用实际敌人测试确认工业储罐、检查站车道、住宅立面没有造成卡位。

## 7. 下一位开发者接手步骤

1. 关闭或刷新当前 Unity 实例，等待脚本重新编译，确认 Console 没有新的 `CS` 错误。
2. 执行 `ArknightsACT > Build Prototype Scene`，进入 Play Mode。
3. 先从固定摄像机观察四个区块：确认主路连续、中心斑马线位置正确、建筑没有互相穿插。
4. 分别测试住宅入口/二楼、商业店面、工业设施和检查站车道。
5. 召唤敌人做绕建筑追击测试；如发生卡位，优先检查新增碰撞体和导航节点，不要直接扩大敌人脱困速度。
6. 若需要进一步贴近预览图，优先调整 `CityStreets` 中的建筑比例、道路标线和灯光位置；不要通过增加建筑数量来解决空旷问题。

## 8. 与陈特效工作的边界

陈的自绘特效仍遵循 `Docs/CHEN_CUSTOM_FX_HANDOFF.md`：

- 特效挂点固定为 `Player_Chen/CustomFxMountPoint`。
- 不要恢复 `ChenOriginalSkillFxController`、`ChenDashSpineFxGateController` 或其他旧原客户端 FX 运行时挂载。
- 城市地图视觉改动不应修改陈的攻击、技能伤害和特效事件逻辑。

## 9. 工作树注意事项

当前工作树已有较多阶段性未提交修改，包含环境、特效、场景和敌人逻辑。接手时不要使用 `git reset --hard` 或批量回滚来清理工作区；先查看 `git status --short`，只处理本任务涉及的文件。
