# 错落街区、互动设施与第二阶段交接

分支：`codex/mobile-city-map-generator`。

## 设计与入口

本次将“第二层”落在已有行动流程的第二阶段地图：第一阶段是居民街区，第二阶段是动力维护层，并非同一张地图上的可攀登建筑二楼。击败第一阶段首领并完成奖励选择后，到开启的下一阶段入口按 E 进入；携带的未撤离物资仍处于风险中。

保留移动城市区块与连续道路，以免破坏战斗及导航。建筑横向在 3.6 米范围内错开，纵向在 2.4 米范围内退让；部分建筑有 ±7 度偏转。门前地坪跟随朝向，房间导航仍连接真实门口。每三个区块预留一个公共设施院，起点固定提供医疗补给。废弃车辆移至独立路边位置，避免与错落建筑相交。

第二阶段优先生成维修铺、仓库、动力维护站和执勤建筑，仓库更宽，加入金属路面、货运导轨、高架供热管线、管架和维护层标识。供电设施解锁的容器使用源石封装箱；第一阶段使用密封货箱，仍采用已有珍稀度系统。

## 互动规则

| 设施 | 操作 | 效果 |
| --- | --- | --- |
| 罗德岛应急补给 | 靠近并按住 G 2 秒 | 恢复最大生命的 30%，每处一次；满血不消耗 |
| 天灾观测中继 | 靠近并按住 G 2.5 秒 | 测绘本区块及相邻四方向区块，紫色标示；不记录已探索，不生成/清除敌人 |
| 源石配电柜 | 靠近并按住 G 4 秒 | 灯色变化、解除储备箱封闭，靠近箱子按 F 搜索；每处只解锁一次 |

范围 2.8 米并检查视线。松开、离开、移动超过 0.5 米、受击、切换角色会取消进度。暂停、非行动状态、输入被界面占用时禁止操作。每阶段只由一个控制器处理 G，选择最近设施，不与搜索 F / 出口 E 争用。小地图用紫色菱形显示已知且未使用的设施。

设施状态属于当前地图，随关卡销毁；没有新增长期存档字段或物品类型。

## 代码

- `World/RogueliteStageCityStreetsController.cs`：错落建筑和安全路边车辆位置。
- `World/RogueliteStageCityStreetsController.Sectors.cs`：公共设施院、第二阶段工业装饰。
- `World/CityFacility25D.cs`：设施类型、效果和一次性状态。
- `World/CityFacilityController.cs`：交互选择、读条取消、提示和独立测绘状态。
- `World/RogueliteStageDistrictTemplateController.cs`：第二阶段街区权重。
- `Routing/RogueliteMinimapGraphic.cs` / `RogueliteMinimapController.cs`：测绘区域、设施图标和阶段名称。

以上路径相对 `Assets/_Game/Scripts/Gameplay/Roguelite/`。

## 验证

Game.Gameplay / Game.Editor 编译零错误。隔离 Unity 校验通过 384 个种子地图计划，额外验证第二阶段工业分区；三个种子 × 三个阶段生成真实几何，检查建筑与外部碰撞体无穿插、门口/搜索/设施站位可达、房间导航连通。设施检查覆盖三类效果、满血保留、松键/移动/受击取消、不可重复领取、测绘不改变探索状态。

校验入口：`Assets/_Game/Editor/MobileCityGenerationValidation.cs` 的 `Run`；诊断图入口 `RenderPreview`，输出 `Logs/CityVisuals/maintenance-overview.png`、`maintenance-detail.png` 及居民街区预览。报告：`Logs/MobileCityGenerationValidation.txt`。

预览使用隔离场景和简化环境材质。完整正式场景中的实战节奏、帧率和 UI 叠放仍需试玩确认。Unity 编译完成后，重新开始行动生成新地图。
