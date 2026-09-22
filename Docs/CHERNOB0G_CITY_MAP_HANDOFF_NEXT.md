# CHERNOB0G CITY MAP HANDOFF NEXT

> 最新交接：[2026-09-20 城市地图与搜打撤](CITY_EXPLORATION_HANDOFF_2026_09_20.md)。区块现为 36×30，近景房屋开放，新增 22 件藏品目录、9 个搜索容器与阶段撤离结算。下面保留历史记录。

## 本轮继续开发记录（2026-09-18）

已完成：

- `RogueliteStageRuntimeContext` 现在统一持有 `StageMap`、`RunState`、`EnvironmentKit` 和 `StageRoot`，并在 StageRoot 切换时提供 `StageRootChanged` 通知。
- `PrototypeStageRuntimeFactory` 修复重复添加 Context 的问题；`RogueliteStageRuntimeController` 在 StageRuntime 建立/销毁时同步 Context 生命周期。
- World 目录内的视觉、道路、建筑、地形、碰撞和遮挡控制器全部移除 `FindFirstObjectByType` 与 `GameObject.Find` 的运行时查找，改用同一 GameObject 上的 Context。
- 修复 Context 批量接入时的 `Transform`/`GameObject` 类型边界，并清理了一个无用局部常量。
- 修复地图大面积空洞：`RogueliteStageLayoutController` 不再用旧的 14×11 地面尺寸覆盖 30×24 区块；FloorSockets 现在直接覆盖当前区块尺寸，Expansion 层也不会二次放大地面。
- 为每次 StageRoot 增加连续的 `[GameplaySafetyDeck]` 碰撞底板，避免地面模块重建或接缝异常时角色掉出地图；道路细节根节点也改为挂载到当前 StageRoot。
- `RogueliteStageLayoutController`、`RoguelitePitFloorSyncController` 和 StageRuntime 的地图引用改用 Runtime Context，不再跨场景查找地图/StageRoot。
- 记录到的 Unity Assertion 调用栈来自编辑器 FontAsset 字体图集写入（`AssetDatabase.AddObjectToAsset`），与地图运行时代码无关。
- Unity Roslyn 编译检查通过：`Game.Gameplay`、`Game.Editor` 均为 exit code 0。

## 本轮继续开发记录（2026-09-19）

- 定位并修复“重新打开场景后城市、底座同时消失”的生命周期问题：`RogueliteStageRuntimeContext` 的 `StageMap`、`RunState`、`EnvironmentKit` 改为可序列化 backing fields；各视觉控制器在 `Awake` 中使用 `??=`，不会再用空 Context 覆盖场景里已经保存的引用。
- 按参考图把城市层拆成明确职责：`CityStreets` 负责道路与四类街区（Residential / Commercial / Industrial / Checkpoint），`UrbanComposition` / `PlayableArchitecture` 负责建筑，`MobileCityChassis` / `MobileCityDeepBase` 负责南侧与西侧可见底座，`DistantDistrict` / `HorizonCity` / `ChernobogCityBackdrop` 负责北侧、东侧远景工业城区、高架和能源管线。
- 重构 `ChernobogCityBackdropController`：不再在 `StageRuntime` 根下用默认 Cube 临时生成，而是跟随 `StageRoot`、共享 `ChernobogEnvironmentKit` 和倒角网格，只生成无碰撞的远景工业天际线；工厂创建时同步保存 `stageMap` / `kit` 引用。
- 重新确认连续 `[GameplaySafetyDeck]` 是物理兜底，视觉道路和底座不再承担防掉落职责；地图接缝异常时角色不会掉出移动城市。
- 本轮 Unity Roslyn 静态编译已重新执行：`Game.Gameplay`、`Game.Editor` 均为 exit code 0。Unity 编辑器中的场景仍需重新进入一次 Play（或重新执行 Prototype 场景构建）才能把新增 Context / Backdrop 序列化引用写回场景。

下一步：

- 将目前仍以短间隔 `Update` 等待依赖 Root 的视觉控制器逐步改为 `StageRootChanged` 驱动；保留对模块依赖的轻量重试，避免改变现有执行顺序。
- 在编辑器中完成一次 Stage 1 → Stage 2 → Stage 3 的实际运行验证，重点检查道路视觉层、可进入建筑和移动城市底盘在换图时是否完整重建。

## 当前目标

继续完善 ArknightsACT 中的切尔诺伯格城市地图生成，使其更接近明日方舟本体的城市氛围，同时保持代码可维护性。

核心方向：

- 城市道路真实化
- 建筑工业化、层次化
- 移动城市结构表现
- 减少 World 层代码重复
- 统一运行时生命周期

---

# 已完成工作

## RuntimeContext 架构

已建立：

```
RogueliteStageRuntimeContext
```

目标：替代大量：

```
FindFirstObjectByType<RogueliteStageMapController>()
GameObject.Find("[Stage_xx_Runtime]")
```

统一提供：

```
RuntimeContext
├── StageMap
├── EnvironmentKit
├── StageRoot
└── Visual Services
```

---

# 已接入 Controller

目前已经处理：

- RogueliteStageCityStreetsController
- RogueliteStageUrbanCompositionController
- ChernobogFacadeModuleController
- ChernobogBuildingDetailController
- RogueliteStageHorizonCityController
- RogueliteStageDistantDistrictController
- RogueliteMobileCityChassisController
- RogueliteMobileCityDeepBaseController
- RogueliteStageArtDirectionController
- RogueliteStageAuthenticityController
- RogueliteStageLightingController
- RogueliteStageUrbanDensityController

处理内容：

- Context 接入
- StageRoot 获取统一
- 减少运行时 Find
- Root 生命周期统一

---

# Root 管理

已增加：

```
RogueliteStageVisualRootUtility
```

负责：

- 创建视觉 Root
- 查找已有 Root
- 防止重复生成
- 统一命名

避免：

```
new GameObject("[xxx]")
```

散落在各 Controller。

---

# 下一阶段必须完成

## 1. RuntimeContext 全面接入

剩余 Controller：

- RogueliteStagePlayableArchitectureController
- RogueliteStageBackdropFacadeController
- RogueliteStageEnvironmentController
- RogueliteStageSetDressingController
- RogueliteStageTerrainPresentationController
- RogueliteStageQualityPassController

处理：

- 删除 FindFirstObjectByType
- 删除 GameObject.Find Runtime Root
- 使用 Context

---

# 2. 代码清理

继续扫描：

## using

删除：

- 重复 using
- 未使用 namespace


## 字段

检查：

- 无用 SerializeField
- 重复缓存字段
- 可由 Context 提供的字段


## 生命周期

当前旧模式：

```
Update()
{
    Find对象
    等待Stage
    判断生成
}
```

目标：

```
RuntimeFactory
        |
        v
Context Ready
        |
        v
Controller Initialize
        |
        v
Build Visual
```

减少 Update 轮询。

---

# 3. 地图视觉继续优化

## 道路

目标：从测试平面升级为城市道路。

增加：

- 车道线
- 黄黑警戒线
- 人行区域
- 路缘石
- 排水系统
- 路灯
- 护栏
- 路面磨损

注意：

视觉道路 != 战斗碰撞道路

不要影响：

- 寻路
- 战斗格
- 阻挡

---

## 建筑

当前结构：

```
UrbanComposition
        |
        +-- FacadeModule
        |
        +-- BuildingDetail
```

不要新增 BuildingGenerator。

继续增加：

- 外墙管线
- 空调设备
- 窗户阵列
- 屋顶机械
- 工业平台
- 建筑连接桥
- 城市维护设施

---

# 4. 切尔诺伯格特色增强

利用已有：

- ChernobogInfrastructureController
- ChernobogCityBackdropController
- RogueliteStageHorizonCityController

增加：

- 巨型城市承重结构
- 能源管网
- 工业塔
- 远景城区
- 移动城市底盘
- 外露机械结构

目标：

不是普通城市地图，而是移动都市切尔诺伯格截面。

---

# 5. 维护原则

禁止新增：

```
NewRoadGenerator
NewBuildingGenerator
NewCityGenerator
```

原因：已有：

```
CityStreets
UrbanComposition
FacadeModule
BuildingDetail
Infrastructure
```

继续扩展已有系统。

---

# 当前优先级

1. 将仍以短间隔 `Update` 等待依赖 Root 的控制器逐步改为 `StageRootChanged` 驱动
2. 在编辑器中完成 Stage 1 → Stage 2 → Stage 3 实际运行验证
3. 全项目 World 目录扫描重复代码
4. 道路、建筑和工业远景的材质/灯光质感继续迭代
