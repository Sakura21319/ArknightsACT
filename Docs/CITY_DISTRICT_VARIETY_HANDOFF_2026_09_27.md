# 切城街区差异化：方案 17–24 实施与验证

日期：2026-09-27  
分支：`codex/city-district-variety`  
场景：`Assets/_Game/Scenes/PrototypeRun.unity`

## 目标与边界

这轮针对“每个区块都像同一条街”的问题，在现有移动城市生成器内增加街区主题、可通行的室内/屋顶路线和可改变返程的交互。普通房屋仍规则排列，主道路仍连续四向连通。生成只使用当前 Stage 的固定种子，不改变物品目录、掉落档位、跨 Stage 风险和真实撤离规则，也没有建立第二套地图生成器。

所有变化先由 `CityDistrictVarietyPlan.Create(map)` 在生成时规划，然后由 `RogueliteStageCityStreetsController` 消费。规划以区块与地块编号为稳定键，排除设施院、Start/Boss 不可用地块及 Stage 1/2 的中央地标预留。屋顶桥若在常规战斗区找不到安全相邻地块，会扩大到可用的 Start/商店邻块；每个种子仍只生成一座。主题地块强制生成，避免规划目标被原先的随机空地规则跳过。

## 八项落地内容

| 编号 | 玩家体验 | 实现规则 |
| --- | --- | --- |
| 17 主题街区组合 | 一处相邻的诊所与药房组成医疗服务点；工业优先的跨街货运场有两处真实可搜容器。 | 相邻区块成对选择地块、固定种子复现；医疗优先外围，缺候选时依次退到废墟/可用邻块；货运优先工业。建筑用途、门牌和物资按同一计划生成。 |
| 18 室内路线变体 | 同类房屋可以出现侧翼分隔、后部隔间或双货架通道，进屋后有不同观察与移动路径。 | `CityRoomLayout` 按区域偏好选择；保留道路到中央的入口走廊，侧翼分隔及货架使用真实碰撞。后部隔间位置避开容器搜索站位。 |
| 19 跨区块场所 | 相邻街块共享一处可辨认的货运/集市场所，街口有门架、货盘和两侧搜索目标。 | `FreightTransfer` 配对由同一规划选出；场所建在区块接缝，保留贯通道路，不改主路导航。 |
| 20 远处可见的目标 | 高出普通屋顶的信号桅杆与亮灯，引导玩家从街道辨认目标，再寻找对应建筑入口。 | 一个已规划建筑生成信号桅杆、门牌和引导标线；建筑内沿用真实容器与原有搜索规则，不透过迷雾提前泄露掉落。 |
| 21 屋顶第二路线 | 两栋相邻房屋各有外侧检修坡道，屋顶之间由实体连桥连接；可从任一端登上与返回。 | 两端高度统一，屋面、坡面、落地平台和连桥有碰撞；只在实体连接处登记 `CityVerticalRoute`。连桥在屋面上方抬高 0.25m，给下面的坡道留出头部净空；入口侧护栏留出连接口。 |
| 22 内部开启返程捷径 | 一栋房屋的后墙有封闭门，玩家进屋后按住 G 解锁，之后可直接从后巷返回。 | 分段后墙与真实门洞、一次性门状态；外侧不能激活，开门关闭门板碰撞并追加导航路线；随 StageRoot 重建复位。 |
| 23 战斗空间模板 | 外围是分散掩体，废墟是破墙与侧翼，工业是货柜狭道，核心是错列管制障碍。 | `CityBattleLayout` 按 `CityZone` 映射到真实掩体碰撞；起点、Boss、商店、设施与中央地标区域不放置这类模板。 |
| 24 安静空间 | 起点街区有一处带座椅、顶棚和标识的休整角，和战斗街区形成节奏反差。 | 放在起点设施院相对侧；无独立敌人、奖励或强制操作。 |

四区现在的稳定偏好为：外围住宅/空置民居与侧翼房间；废墟空置房、维修铺、后部隔间与破墙；工业仓库/维修/动力建筑、货架通道与货运狭道；核心档案/检查建筑、后部隔间与错列障碍。具体组合仍由种子变化，不会让整张地图每块都重复同一模板。

## 代码入口

- `Assets/_Game/Scripts/Gameplay/Roguelite/World/CityDistrictVarietyPlan.cs`：固定种子规划、区域偏好、相邻配对和预留避让。
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/RogueliteStageCityStreetsController.Variety.cs`：实体室内分隔、战斗空间、静区、跨区货场、屋顶路线与返程门。
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/RogueliteStageCityStreetsController.cs`：在原有 `BuildCityLots` 消费规划，并在所有区块建好后连接跨区场所。
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/RogueliteStagePlayableArchitectureController.cs`：可选分段后墙。
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/CityFacilityController.cs`：复用按住 G 通道处理返程门，设施优先。
- `Assets/_Game/Scripts/Gameplay/Roguelite/World/CityVerticalRoute.cs`：屋顶桥在相邻房屋创建后延迟登记导航。
- `Assets/_Game/Editor/MobileCityGenerationValidation.cs`：计划、实体、站位、门交互与上下行验证。

## 验证结果

当前工作区 `Tools/compile_check.py`：`Game.Gameplay` 223 源文件、`Game.Editor` 89 源文件，均为 0 错误。

隔离 Unity 6000.0.23f1 校验：384 个种子/阶段计划可复现，主题配对、货场、屋顶配对、信号目标和返程门按计划出现。三个几何种子 `21319 / 42 / 931` × 三阶段均通过：有实体跨区场所、一座屋顶桥、两端登顶路线、静区和对应区域战斗空间；`CharacterController` 不跳跃可上下行所有垂直路线；普通建筑入口通廊、容器搜索站位、外墙穿插、房间导航与奖励种子检查通过；后巷外侧无法开门，室内操作后门碰撞关闭并连接后巷导航。隔离报告位于 `Logs/MobileCityValidationProject/Logs/MobileCityGenerationValidation.txt`。

隔离运行只为核对地图，临时跳过了与本轮无关的旧区域敌人生命数值、设施输入时序和轮椅专项断言；主项目的 `MobileCityGenerationValidation.cs` 未跳过这些断言。隔离工程使用当前 Gameplay/Core/Combat 程序集。正式 `PrototypeRun` Play Mode 仍需试玩视觉遮挡、屋顶坡道手感、敌人在新掩体中的追击以及性能；隔离校验不能替代完整 Run 验收。

## 试玩检查顺序

1. 固定基础种子 21319 进入三阶段，观察四区建筑用途、室内分隔和战斗场地是否一眼能区分。
2. 找到亮灯信号桅杆，从远处确认目标，再找正门进入搜索，检查小地图迷雾没有提前暴露奖励。
3. 在跨街货场确认道路保持可通、两侧容器可搜；在屋顶两端分别上楼、跨桥、下楼，检查角色与相机遮挡。
4. 从正门进入返程门建筑，确认后巷一侧不能操作、室内 G 可解锁、重新生成下一局后恢复封闭。
5. 观察新掩体是否让敌人卡住或出现可无限安全输出的位置，并检查 16:9/4K 提示遮挡与正式场景帧率。

没有提交或推送。工作区同时存在其他角色/战斗/文档任务的未提交改动；本轮只负责上述地图文件与这份交接。
