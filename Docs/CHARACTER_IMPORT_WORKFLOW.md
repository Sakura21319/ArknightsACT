# 可玩角色素材绑定流程（Agent 直绑版）

更新时间：2026-09-24

本文是新增可玩角色时给本地 Agent / Codex / 其他执行 Agent 使用的 **source of truth**。

这份文档描述的是“素材已经由用户从明日方舟本体导出后，Agent 如何把素材真正绑定进 ArknightsACT”的流程。不要把“素材导出”和“Unity 工程绑定”混成一个自动导入流程。

---

## 0. 最重要的流程定义

整个工作只有两大阶段：

```text
阶段 1：用户负责
明日方舟游戏本体
    ↓
ArkModExporter / 现有导出工具
    ↓
D:\Ark\_Unpacked\<char>
    ├─ spine
    ├─ effects
    ├─ sound / voice
    ├─ icons
    ├─ ui_assets
    ├─ unity_import_manifest.json
    ├─ audio_mapping.json
    └─ 其他导出结果

阶段 2：Agent 负责
读取 D:\Ark\_Unpacked\<char>
    ↓
分析角色已有动作 / 特效 / 音效 / 皮肤
    ↓
直接复制、移动、修改工程文件
    ↓
把 Spine / 动作 / FX / Audio / UI / Gameplay 全部绑定到游戏
    ↓
Unity 只负责正常编译、资源刷新和最终 Play Mode 验收
```

### 绝对不要误解

**用户不会要求 Unity 再替我们“猜一次怎么导入”。**

本地 Agent 已经具备直接读写、移动、复制工程文件的能力，所以正常流程应该是：

> Agent 看清导出包 → Agent 决定应该绑定哪些文件 → Agent 直接落到项目正确位置 → Agent 修改代码和序列化资源 → Unity 最后正常刷新/编译。

不要把工作重新绕回：

- 让用户打开某个“快速导入窗口”；
- 让 Unity 菜单扫描整包后自动猜角色结构；
- 依赖 `DidReloadScripts` 自动决定需要导入什么；
- 依赖自动 importer 把整个目录全部扫进项目；
- 因为“自动化方便”就重新覆盖已经人工调好的 Prefab、FX offset 或 Gameplay 映射。

Unity 自己在文件进入 `Assets/` 后进行正常 AssetDatabase 刷新是正常行为；这里禁止的是**自定义自动绑定/自动猜测流程作为主流程**。

---

## 1. Agent 接手一个新角色时必须先做什么

假设用户说：

> 接入角色 X，素材在 `D:\Ark\_Unpacked\x`

Agent 第一件事不是改代码，而是完整检查导出包。

优先读取：

```text
D:\Ark\_Unpacked\<char>\unity_import_manifest.json
D:\Ark\_Unpacked\<char>\audio_mapping.json
D:\Ark\_Unpacked\<char>\spine\action_gif_manifest.json
D:\Ark\_Unpacked\<char>\effects\frames\<tag>\timing.json
```

同时列出：

```text
spine/*.atlas
spine/*.skel
spine/*.png
icons/*
ui_assets/*
effects/frames/*
sound/*
voice/*
```

必须先整理出一张内部绑定表，至少明确：

| 项 | 必须确认 |
|---|---|
| 角色 ID | 如 `char_1035_wisdel` |
| 工程 OperatorId | 如 `Wisadel` |
| 显示名 | 如 维什戴尔 |
| 皮肤 | default / game#9 / marthe#5 等 |
| combat Spine | 每套皮肤对应哪个 `char_*.skel` |
| motion Spine | 对应哪个 `build_char_*.skel` |
| 动作 | Idle / Move / Attack / Skill / Die 等真实名称 |
| FX | 每个普攻/技能实际需要哪些 tag |
| Audio | 普攻、命中、技能开始、技能命中实际用哪些 wav |
| UI | 头像、技能图标、Ready 图标等 |
| 特殊对象 | token / summon / 召唤物 / 武器独立对象 |
| 未确认项 | 明确写出来，不允许猜 |

**名字相似只表示候选，不表示绑定正确。**

---

### Ranged basic-attack target acquisition

- 远程角色如果有自定义普攻 Resolver，**索敌必须发生在 PlayerAttackController 真正开始攻击周期之前**，不能等到动画/FX/impact 阶段各自临时索敌。
- 使用 `IPlayerBasicAttackTargetProvider` 在攻击开始前返回明确目标；有 provider 但没有目标时，不启动攻击、不播放攻击 FX、也不消耗弹药。
- 命中 Resolver、Presentation、Trail/Hit FX 应复用同一份 target snapshot，避免“动画朝 A、弹道飞 B、伤害打 C”。
- 对角色级远程锁敌优先按有效 `CombatEntity` 搜索，不应依赖敌人 prefab 的 Collider 层级是否刚好能被某次 Overlap 查询命中；Collider 仍用于物理/近战/阻挡等需要几何体的系统。

## 2. 先选一个工程内的参考角色

不要从零造一套完全不同的目录和代码。

优先找结构最接近的已有角色作为模板，例如：

- 普通远程角色：Schwarz / Wisadel；
- 普通近战角色：Skadi；
- 复杂多段技能：Chen；
- 敌人改可玩角色、特殊 Spine：FrostNova；
- 有 summon/token 的角色：找已有召唤物结构。

需要比较的不是“职业名”，而是：

- Spine 是否有独立 Move 源；
- 是否有皮肤；
- 普攻是否有 start/trail/hit；
- 技能是否持续状态；
- 是否有 projectile；
- 是否有召唤物；
- 是否需要多段 FX；
- 是否有特殊音频事件。

复制模板以后，只保留真正共用的结构，不要把旧角色的 tag、音效、偏移、技能逻辑一起带过去。

---

## 3. Agent 直接放置素材，而不是让 Unity 自动猜

建议目标目录沿用现有结构：

```text
Assets/_Game/Art/Characters/<Operator>/
    <SourceType>/
        <Skin>/
            Spine/
            BaseMotion/

Assets/_Game/Art/Audio/Operator/
Assets/_Game/Resources/UI/HUD/Operators/
Assets/_Game/Resources/UI/Skills/<Operator>/
Assets/_Game/Generated/ExtractedFx/<Package>/
```

Agent 直接完成：

1. 把需要的原始 Spine 文件复制到正确目录。
2. 把头像/技能图标复制到 Resources 对应路径。
3. 把最终确认需要使用的 WAV 复制到工程。
4. 只把实际使用的 FX 帧组接入工程。
5. 修改已有/新建的 Prefab、Material、Controller、AnimationClip、代码引用。
6. 修改角色 Builder / Factory / Presentation / Gameplay。
7. 修改 Definition 和皮肤配置。

### 不要做

- 不要整包无脑复制 `effects/frames`。
- 不要把所有导出音效都塞进 Resources。
- 不要把未接入皮肤的素材一起放进工程。
- 不要把 sale# / game# / summer# 等其它皮肤“顺便”全绑定。
- 不要依赖文件名自动决定最终 Gameplay。
- 不要因为目标文件已存在就直接覆盖人工调好的值。

---

## 4. Spine 文件落地规则

一套正常角色表现通常至少需要：

```text
Combat:
    char_xxx.atlas
    char_xxx.skel
    char_xxx.png

Motion:
    build_char_xxx.atlas
    build_char_xxx.skel
    build_char_xxx.png
```

工程内通常使用：

```text
*.atlas.txt
*.skel.bytes
*.png
*_Atlas.asset
*_SkeletonData.asset
*_Material.mat
对应 Presentation Prefab
```

Agent 必须检查从原始文件到运行 Prefab 的完整引用链：

```text
PNG
 ↓
Material
 ↓
AtlasAsset
 ↓
SkeletonDataAsset
 ↓
SkeletonAnimation / Presentation Prefab
 ↓
Player Presentation
```

出现角色显示异常时，不要只盯着 Prefab Transform；要从整条链检查。

---

## 5. Spine 渲染质量：Straight Alpha 是强制注意项

ArkModExporter 当前会把游戏里的：

```text
颜色 Texture
+
独立 <texture>[alpha] 掩码
```

合并成普通 RGBA PNG。

合并后的 RGB **没有做 PMA 预乘**，因此项目里必须把这类本地导出的 Spine 图当作 **Straight Alpha** 使用。

正确设置：

- Texture：sRGB = On；
- Alpha Is Transparency = On；
- Mipmap = Off；
- Filter = Bilinear；
- Wrap = Clamp；
- Compression = Uncompressed；
- Material：`_StraightAlphaInput = 1`；
- Material keyword：`_STRAIGHT_ALPHA_INPUT`；
- MeshRenderer：不投射阴影；
- MeshRenderer：不接收阴影；
- Light Probe = Off；
- Reflection Probe = Off。

如果这里错了，最典型的现象是：

- 眼睛发白/发灰；
- 睫毛出现脏边；
- 头发边缘发亮；
- 半透明区域出现黑边；
- 小 attachment 出现黑色三角或颗粒感。

### 维什戴尔已验证案例

Wisadel 之前画质异常并不是单纯“贴图压缩”。

根因之一就是：

> Straight Alpha PNG 被 Spine PMA 材质解释。

修正 `_STRAIGHT_ALPHA_INPUT` 后恢复正常。

以后新角色如果出现类似问题，先检查 Alpha 模式，**不要先做图片锐化、离线放大或换滤镜**。

---

## 6. 优先寻找同角色真正存在的高清 Atlas

战斗 `char_*.atlas` 不一定是同一角色的最高分辨率素材。

Wisadel 实测：

```text
combat:
char_1035_wisdel.atlas
364 × 364

motion/build:
build_char_1035_wisdel.atlas
688 × 688
```

116 个 combat region 中，110 个在 build atlas 中有同名 region，覆盖约 94.8%。

例如：

```text
F_L_Eye_03
combat: 9 × 7
build: 16 × 12

F_Head_01
combat: 72 × 36
build: 144 × 72
```

因此可以继续使用 combat `.skel` 的 Attack / Skill 动画，但 Atlas 按顺序使用：

```text
1. build 高分辨率 atlas
2. combat 原始 atlas 作为 fallback
```

Spine 查 region 时优先命中 build 中的同名高清部件；build 没有的战斗专属部件再回退到 combat atlas。

### 使用条件

只有同时满足时才做：

1. build/motion atlas 对 combat region 的同名覆盖率 >= 90%；
2. build/motion atlas 最大页面积至少比 combat 大 20%。

否则保持原 combat atlas。

例如 Skadi default 的 build/combat region 覆盖不足 90%，不能照搬 Wisadel 的方案。

**严禁把低分辨率 PNG 人工放大后冒充高清资源。**

---

## 7. 角色显示尺寸不要因为贴图低清而乱改

角色显示高度是 Gameplay/Presentation 尺寸，不是“清晰度补偿”。

当前普通干员建议先以已验证角色为参考：

```text
targetWorldHeight ≈ 1.64
feetLocalY ≈ -0.72
safeInitialScale ≈ 0.38
```

之后根据游戏内实际站位调整。

不要为了“看清楚”把低分辨率 Spine 放大到明显超过同类角色。

如果图片糊：

1. 查源 atlas 分辨率；
2. 查 build/skin 是否有高清同名 region；
3. 查 Straight Alpha；
4. 查 Unity TextureImporter；
5. 最后才考虑显示尺寸。

---

## 8. 动作绑定必须由 Agent 明确完成

素材导入完成不代表角色完成。

Agent 必须把动作真正绑定到游戏行为。

需要确认的基本动作：

```text
Idle
Move / Move_Loop
Attack / Attack_Begin / Attack_End
Skill_1
Skill_2
Skill_3
Skill_*_Start
Skill_*_Loop
Skill_*_End
Hit
Die
Start
```

同时，从 2026-09-24 起，**每个可玩角色/每套实际接入皮肤都必须额外检查并接入 BaseMotion 基建动作**：

```text
Interact
Relax
Sit
Sleep
Special
```

这些动作通常位于 `build_char_*` motion Spine，而不是 combat `char_*` Spine。导入角色时不能再只从 build Spine 取 `Move`；必须同时扫描 `Interact / Relax / Sit / Sleep / Special`，存在的动作都要保留在工程运行链中。

如果某套皮肤原始 build Spine 本身缺少其中某项，**保持缺失并记录，不允许用 Relax、其它皮肤动作或相似动作伪造**。

实际角色不一定全部都有。

### 规则

- combat Spine 优先负责战斗动作。
- build Spine 负责 Move 和 BaseMotion 基建动作（`Interact / Relax / Sit / Sleep / Special`）。
- 如果 combat 没有 Move，可以沿用项目现有的 motion source / retarget 方案。
- **不能默认认为“复制骨骼”就等于完整导入动作。** Move/Relax/Sit/Sleep 等动作可能切换眼睛、嘴、服装部件、坐姿部件等 Spine slot/attachment；纯 bone retarget 不会复制这些 attachment。只要动作 GIF/运行效果存在此类变化，就应在该动作期间直接显示对应皮肤的完整 `build_char_*` Spine，并隐藏 combat Spine，战斗动作再切回 combat Spine。
- full-source BaseMotion 的显示尺寸必须继承 combat presentation **运行时校准后的 visual scale / position**，不能直接使用 build prefab 的 `safeInitialScale`。隐藏的 BaseMotion 通常不会执行 `SpineVisualAutoLayout2D`，否则切到 Move/Relax/Sit/Sleep 时会突然变小。通用实现应以 combat visual 的最终 Transform 为基准，再提供可选 `scaleMultiplier / localOffset` 做个别角色微调；不要按 Sit/Sleep 当前帧包围盒动态缩放，否则动作高度变化会导致角色忽大忽小。
- `game#` / `sale#` 等皮肤必须使用各自的 build Spine；不能拿 default 的 BaseMotion 替代皮肤动作。即使 combat/build 骨骼匹配率不足，只要完整 build Spine 可直接显示，就不应因此判定 Move/BaseMotion 不可用。
- 使用 `SpineBoneMotionRetarget2D` 的角色，不能只配置 `Move` 后就结束；需要保证 build Spine 中存在的 BaseMotion 动作也能通过同一 motion source 播放。
- 当前通用测试入口为 `BaseMotionActionShortcutController`：`F5 = Interact`、`F6 = Sit`、`F7 = Sleep`、`F9 = Special`。F8 已被现有调试功能占用，因此不要复用。
- `Relax` 为通用自动待机动作：角色保持站立无移动/攻击/冲刺/技能至少 **3 秒**后，在一个小随机窗口内自动单次播放；任何 Gameplay 行为或手动 BaseMotion 都会重新计时。不要固定每 3 秒机械播放。
- `Interact / Special / Relax` 默认按单次动作播放；`Sit / Sleep` 默认作为可切换循环动作，重复按同一快捷键退出。移动、普攻、冲刺或技能状态会中断 BaseMotion 动作并恢复正常 Gameplay Presentation。
- 不允许因为 build 有 `Attack` 就自动拿 build Attack 替换 combat Attack。
- 不允许看到 `Skill_1` 就直接认定它是游戏“一技能”。
- 动作名字与实际技能槽必须结合 GIF、manifest、游戏表现确认。

最终映射必须落实到：

- `SpineCharacterPresentation2D`；
- 对应角色 PresentationDriver；
- 技能状态/攻击状态切换代码。

### 动画播放完整性

改变 animation speed 时要保证：

> 动画逻辑结束依据实际 animation duration / complete event，而不是固定等待时间。

不能出现：

- 倍速后动画只播一半；
- 普攻结束后残留一小段延迟；
- 技能动作没播完就切 Idle；
- 技能释放完角色发生错误位移。

### 项目统一动作速度：2x

从 2026-09-24 起，**所有通过 `SpineCharacterPresentation2D` 播放的导入角色动作默认使用 2 倍速**：

```text
ImportedAnimationPlaybackSpeed = 2.0
```

这不是某个角色的临时 tuning，而是当前项目的统一 Presentation 规则。

必须同时满足：

- Idle / Move / Attack / Skill / Hit / Die 等 Spine 动作都经过统一 2x；
- `TryGetAnimationDuration()` 返回的是 **2x 后的有效时长**；
- 动作锁、技能 Begin→Loop、End→Idle 的等待时间使用有效时长；
- 角色代码如果调用 `SetCurrentAnimationSpeed(x)`，`x` 视为相对倍率，即最终速度 = `2.0 × x`。
- `SpineBoneMotionRetarget2D` 的 build/move 源同样必须是 2x。
- 任何绕过公共 Presentation、直接调用 Spine `SetAnimation/AddAnimation` 的旧角色代码也必须显式套用这个基准；陈的 2D/2.5D Driver 已按此迁移。
- 角色专属 speed 字段从现在起是**相对倍率**，默认 1.0；不要再把默认值写 2.0，否则最终会变成 4x。

禁止出现“TrackEntry 2x 了，但逻辑还按原始 duration 等待”的情况。

注意：

> **2x 只描述角色 Spine 动作。序列帧 FX 不跟着无脑乘 2。**

FX 仍按自己 `timing.json.fps` 的原始时序生成和播放。

---

## 9. FX 绑定流程

Agent 不只是“把特效文件导进来”，而是要确认：

> 哪个 Gameplay 事件 → 播哪个 FX → 在哪生成 → 朝向怎么处理 → 播多久 → 是否跟随角色/敌人。

每个角色至少整理这种表：

| 行为 | FX tag | Spawn | Follow | Timing | Left/Right |
|---|---|---|---|---|---|
| 普攻起手 | xxx_attack_start | Actor | yes/no | attack begin | 独立确认 |
| 普攻弹道 | xxx_trail | Projectile | yes/no | projectile spawn | 独立确认 |
| 普攻命中 | xxx_hit | Target | no | hit frame | 通常无需镜像 |
| S2 Start | xxx_skill_02_start | Actor | ... | ... | ... |
| S2 Hit | xxx_skill_02_hit | Target | ... | ... | ... |
| S3 | ... | ... | ... | ... | ... |

### FX 使用原则

- 只复制/生成实际需要的 tag。
- 不要“看起来有关”就全部塞进去。
- 优先查看 `timing.json`。
- 复杂 FX 要检查是否是 composite。
- 同一个逻辑 FX 可能由多个 Prefab 组成。
- 同一 Prefab 的左右方向不一定只是 X 取负。
- 起点和终点 offset 要分开。
- Projectile 起点正确不代表终点也正确。
- 角色转向时 attachment-based FX 需要单独验证。
- **FX 根 tag 中的 `_trail` 默认代表 Projectile Flight：必须绑定到发射点→目标/落点的弹道生命周期，禁止当作 Actor Start/Buff 固定在角色身上。**
- 如果只是 prefab 内部某个 particle 子节点名字含 trail，不据此改变整个根 FX 的分类。
- `hit / hit_02 / hit_03` 往往是同一次命中的复合层，不允许只挑第一层。
- `buff_b / buff_f` 通常是 Back / Front 两层，不允许二选一。
- 每个帧特效必须读取自己的 `effects/frames/<tag>/timing.json.fps`；只有缺失/非法时才回退 30 FPS。
- 角色动作 2x 与 FX FPS 分开处理，禁止把 15 FPS FX 因为“角色动作 2x”错误生成成 30 FPS。
- **名称含 `buff` 的根 FX 通常是角色自身状态层**：应由角色技能生命周期绑定到 Actor/对应挂点，不要因为调参面板勾选而临时生成。
- **名称含 `hit` 的根 FX 通常是目标命中层**：普攻 Hit、技能 Hit 都应绑定实际受击敌人/命中世界点；没有真实目标时不要 fallback 到角色自身。
- FX 的“是否使用”属于已确认绑定的 presentation 配置，不等于“预览”。调参工具只修改已绑定 FX 的 enable/disable 与 offset，Gameplay 事件才负责真正 Spawn。

### FX 偏移 / 开关面板的职责

角色 FX 调参面板必须遵守：

1. 角色 Runtime / FxController 先完成正式绑定：Attack、Skill、Projectile、Hit 等 Gameplay 事件已经明确对应 Prefab。
2. 面板中的复选框只表示“这个已经绑定的 FX 是否启用”，**勾选不能实例化或循环播放 FX**。
3. 面板不得为了方便调参把 Start / Buff / Hit / Trail 全部同时放到角色身上。
4. 面板只保存：
   - FX enable / disable；
   - RIGHT X/Y offset；
   - LEFT X/Y offset。
5. Offset 必须在该 FX 的真实挂载空间中解释：
   - Actor Start / Buff：相对角色挂点；
   - Hit：相对实际受击目标；
   - Trail：相对真实弹道发射点/轨迹逻辑。
6. Hit FX 不允许为了“看预览”改绑 Actor。要调 Hit 偏移，就在真实攻击命中训练假人/敌人时观察。
7. 修改开关后，Runtime 后续事件立即遵守该配置；持续 Buff 类 FX 在技能进行中也应能被关闭/恢复。
8. 旧配置迁移必须保持“已有 FX 默认启用”，不能因为新增 bool 字段导致所有特效默认关闭。

当前 Wisadel 的 `WisadelFxTuningWindow` 已按上述语义改造，不再承担 FX Preview/Spawn 职责。

### Wisadel BaseMotion 当前已确认

本轮 `D:\\Ark\\_Unpacked\\wisdel\\spine\\action_gif_manifest.json` 已确认：

| 皮肤 | Interact | Relax | Sit | Sleep | Special |
|---|---|---|---|---|---|
| default | 有 | 有 | 有 | 有 | **无** |
| game#9 | 有 | 有 | 有 | 有 | 有 |

工程内 default 与 `game#9` 的 `BaseMotion/build_char_*` Spine 已存在，并通过 `WisadelPrototypePlayerFactory` 接入 `SpineBoneMotionRetarget2D + BaseMotionActionShortcutController`。Wisadel 已启用 **full-source BaseMotion**：Move/Relax/Interact/Sit/Sleep/Special 直接显示对应皮肤完整 build Spine，所以 Move 中的闭眼、game#9 专属坐/睡 attachment 等不会再被 combat Idle slot 覆盖。快捷键为：

```text
F5  Interact
F6  Sit（循环开/关）
F7  Sleep（循环开/关）
F9  Special
```

default 按 F9 时不会 fallback 到其它动作；因为原始 default build Spine 没有 `Special`，Runtime 只提示该动作不存在。

`Relax` 不占快捷键：静止至少 3 秒后随机触发一次；default 与 game#9 均已确认存在。

### Wisadel 当前已确认技能表现

本轮进一步确认：

- **S2（Gameplay Slot 1）**：
  - 是持续自动索敌攻击，不是普通攻击强化；
  - 技能期间禁止手动普攻；
  - 释放和持续期间角色**可以移动/冲刺**；
  - **没有有效敌人时不要播放 S2 攻击动画，也不要播放 `skill_02_start`**；
  - 首次找到有效敌人、真正进入自动攻击状态时才播放攻击动作；
  - **每一次 S2 自动攻击都要重新触发对应攻击动作事件，同时播放一次 `skill_02_start`**；`skill_02_start` 不是“每次技能只播一次”的进入特效；
  - 丢失所有有效目标后退出 S2 攻击 pose，恢复正常移动表现；之后重新找到目标时再次进入攻击动画；
  - 技能自身按间隔重新索敌并结算攻击；
  - `skill_02_buff*` 挂角色自身；
  - `skill_02_hit*` 只挂实际被技能命中的敌人。
- **S3（Gameplay Slot 2）**：
  - 是 6 发弹药状态；
  - 技能期间不能移动/冲刺；
  - 点击技能只进入 `Skill_3_Begin` / 三技能待机姿态，**不能自动循环攻击动画、不能自动发弹**；
  - 只有玩家点击普攻后，才播放一次 `Skill_3_Loop` 攻击动作并消耗 1 发弹药；
  - S3 发弹时不能切回普通 `Attack_A/B/C`；
  - 普攻与 S3 使用维什戴尔自己的全范围最近敌人自动索敌：不能因为角色最后一次移动方向与目标不一致就拒绝目标；锁定后自动朝目标转向；
  - `skill_03_trail` 是每次点击攻击产生的真实弹道；
  - `skill_03_hit*` 是实际敌人命中层；
  - `skill_03_buff*` 挂角色自身直到弹药耗尽。

Wisadel 的完整重扫和当前绑定表见：

```text
Docs/WISADEL_FX_BINDING_2026_09_24.md
```

### Offset

初次绑定时：

```text
RIGHT = 0
LEFT = 0
```

然后在游戏里逐个调。

不要自动假设：

```text
LEFT.x = -RIGHT.x
```

很多 Spine、武器骨骼和序列帧本身并不完全对称。

---

## 10. Audio 绑定流程

`audio_mapping.json` 只能作为候选，不是最终答案。

Agent 应：

1. 读取候选。
2. 在导出 WAV 中确认文件存在。
3. 根据文件名/事件/必要时试听确认。
4. 只复制实际使用的音频。
5. 把 Clip 明确绑定到对应行为。

至少检查：

```text
Basic Attack Start
Basic Attack Hit
Skill 1 Start / Hit
Skill 2 Start / Loop / Hit / End
Skill 3 Start / Loop / Hit / End
特殊武器声
召唤物声
角色必要语音
```

禁止：

- 因为名字里有 `atk` 就直接绑普攻；
- 因为候选排名第一就认定正确；
- 技能没有专属音效时强行塞一个相似音效；
- 一个技能多段音效只绑定第一段。

如果无法确定，保持未绑定并明确告诉用户“这一项需要确认”，不要猜。

---

## 11. UI 素材绑定

至少检查：

- HUD 头像；
- 原版/皮肤头像；
- S1/S2/S3 图标；
- Ready 图标；
- Ready 动效；
- 特殊资源计数图标；
- 召唤物/弹药等特殊 UI。

资源路径应遵守项目已有约定。

不要只复制文件；还要检查：

- `PlayableOperatorDefinition` 的 key；
- HUD 是否真正加载到；
- 切皮肤后头像是否同步；
- 技能槽与图标是否对应。

---

## 12. Gameplay 接入不是自动导入器的工作，而是 Agent 的工作

Agent 完成素材整理以后，继续完成角色本身。

典型需要修改/新建：

```text
<Operator>LocalAssetBootstrap / 资源描述
<Operator>PrototypeOperatorBuilder
<Operator>PrototypePlayerFactory
<Operator>PresentationDriver
<Operator>FxController
<Operator>TuningProfile
技能定义 / AttackDefinition
必要的 Projectile / Summon Controller
Operator Definition
HUD / Skill slot mapping
```

不是每个角色都需要完全相同的类；应复用公共系统，但不要为了“统一”强行把特殊角色塞进错误模型。

### 训练假人是公共设施，不属于任何角色

训练木桩/假人的生命周期必须与角色完全解耦。

正确结构：

```text
PrototypeRun / Stage
    └─ [TrainingDummyFacility]
         └─ TrainingDummy_Infinite
```

禁止：

- 在 `ChenPrototypePlayerFactory` 创建假人；
- 在 `SchwarzPrototypePlayerFactory` 创建假人；
- 在任意 Character-specific FX Controller 的 `Awake` 中补假人；
- 切换角色时销毁/重建训练假人；
- 为了某个角色调 FX 就给该角色挂一个 DummySpawner。

训练假人要求：

- Team = Enemy，能被玩家正常选中/命中；
- 使用独立、非 Trigger 的 3D `CapsuleCollider` 作为受击/索敌碰撞体；训练假人不移动，不应依赖 `CharacterController` 充当唯一受击体；
- 已存在的旧假人进入运行时必须自动补齐/修正 Health、Team、CombatStats、Invincible、Collider，并同步 Physics；
- 正常走 `DamageSystem`，所以 Hit FX、Damage event、伤害数字都能触发；
- 不拥有 Enemy AI，不主动移动/攻击；
- 无经验奖励、无死亡清理；
- 使用公共 `TrainingDummyInvincible` 保证永不死亡；
- 每次有效命中后立即恢复满血；
- 即便出现超大单次伤害，也必须保留至少 1 HP，不能触发死亡。

当前公共实现：

```text
Gameplay/Facilities/TrainingDummyFacility.cs
Prototype25DProductionFactory.CreateTrainingDummy()
```

旧 `ChenTrainingDummySpawner` 仅保留历史场景序列化兼容，已经禁止再实际生成假人。

---

## 13. 皮肤接入规则

一个皮肤就是一套明确配置，不是一个模糊后缀。

每个 skin 单独确认：

- combat Spine；
- motion Spine；
- avatar；
- skill icons 是否共用；
- FX 哪些共用、哪些皮肤专属；
- Audio 是否共用；
- Gameplay 是否变化；
- 特殊 token 是否变化。

默认不要把导出包里所有皮肤全部接入。

用户要求：

> 只接原版和 game#9

就只接：

```text
default
game#9
```

即使目录里还有 `sale#14`，也不要顺手接入。

---

## 14. 现有自动导入代码怎么定位

工程里目前仍存在：

```text
LocalOperatorAssetImportUtility
LocalOperatorPresentationImporter
LocalOperatorImportPlan
LocalOperatorQuickImportWindow
CurrentOperatorAssetRefreshService
```

这些是此前为了减少重复代码建立的工具层，可以继续作为**实现辅助**，但不能再把它们理解成用户要求的主流程。

正确理解：

- 它们可以帮助处理重复操作。
- 它们可以保存已经确定的显式映射。
- 它们不能替 Agent 决定“什么素材应该绑定”。
- 它们不能扫描整包后自动替用户做 Gameplay 选择。
- 它们不能覆盖人工已经调好的内容。
- 本地 Agent 能直接复制/修改文件时，优先直接完成任务，不要求用户再去 Unity 菜单执行一次导入。

### Quick Import Window

`快速导入角色素材` 只保留为辅助/调试工具。

**不要把它写进新角色的标准操作步骤。**

### DidReloadScripts

自动刷新只能用于：

- 已经明确绑定好的角色；
- 同路径资源发生变化后的轻量同步。

不得用于：

- 第一次角色接入；
- 自动决定皮肤；
- 自动决定动作；
- 自动决定 FX；
- 自动决定音效；
- 自动重置人工偏移。

---

## 15. 推荐的完整 Agent 操作顺序

本地 Agent 接到“接入一个新角色”的任务后，严格按以下顺序执行：

1. 读取导出包和 manifest。
2. 列出所有 combat / build / skin / token Spine。
3. 列出全部动作名并查看动作 GIF。
4. 列出 FX tag 和 timing。
5. 列出 Audio 候选和实际 WAV。
6. 确定用户要求接入的皮肤范围。
7. 找一个工程内最接近的角色作为模板。
8. 创建目标角色目录。
9. 直接复制需要的 Spine、头像、图标、音频。
10. 检查 Straight Alpha 设置。
11. 比较 combat/build atlas，判断是否存在真实高清 atlas 可复用。
12. 建立/修正 AtlasAsset、SkeletonDataAsset、Material、Presentation Prefab。
13. 确认 Idle / Move / Attack / Skill / Die 动作实际可播放。
14. 写 Builder / Factory。
15. 写 PresentationDriver。
16. 绑定普攻。
17. 绑定技能动作。
18. 绑定每一段 FX。
19. 绑定每一段 Audio。
20. 接入 HUD / avatar / skill icons。
21. 接入皮肤切换。
22. 接入 token / summon / projectile 等特殊对象。
23. 检查左右朝向。
24. 游戏内逐项调 FX offset。
25. 游戏内检查动作是否完整结束。
26. 游戏内检查音效触发时机。
27. 检查角色死亡、切换、移动、连续攻击。
28. 最后才做清理和公共代码抽取。

如果某一步存在歧义，**停在该项并记录，不要用猜测继续污染后续绑定。**

---

## 16. 修改代码时的原则

### 可以直接做

- 直接移动/复制文件；
- 直接修改 C#；
- 直接修改 prefab / material / asset；
- 直接修改 JSON / manifest；
- 直接清理无用旧文件；
- 直接复用已验证的公共组件。

### 不应该做

- 每做一小步就等用户手动点菜单；
- 让用户自己复制素材；
- 让用户自己创建目录；
- 明明 Agent 有文件权限却只给操作步骤；
- 为了省事把所有资源都自动扫描进工程；
- 一次修改把整个角色已有人工 tuning 重置；
- 未经要求运行耗时 build/test；
- 为了“统一架构”破坏已经正常工作的特殊角色。

---

## 17. 特殊角色处理

已知特殊情况：

- Chen：OHMS / 复合 FX。
- Schwarz：S2 composite。
- FrostNova：Winter Skill3 特殊 frame sequence。
- Wisadel：combat atlas 低分辨率，可复用 build 高清 atlas。
- 有 token / summon 的角色：需要独立 Presentation / Gameplay 生命周期。
- 某些技能没有独立 Spine Skill 动作：可能复用 Attack。
- 某些角色的 build Move 与 combat 骨骼不完全一致：需要 motion retarget。

特殊情况应放在角色自己的薄扩展层里处理。

不要在 Generic Importer 里堆：

```csharp
if (operatorId == "X") ...
else if (operatorId == "Y") ...
```

除非确认这是多个角色共用的真正通用规律。

---

## 18. Unity 最终只做验收

Agent 完成文件和代码修改后，Unity 主要用于最终验证。

检查：

1. Console 无编译错误。
2. 角色 Prefab 引用没有 Missing。
3. Atlas / Material / SkeletonDataAsset 引用正确。
4. 角色画质正常，无 Straight Alpha 脏边。
5. Idle 正常。
6. Move 正常。
7. 普攻动作完整。
8. 普攻 FX 正确。
9. 普攻 Audio 正确。
10. 每个技能动作正确。
11. 每个技能 FX 正确。
12. 每个技能 Audio 正确。
13. 左右朝向正确。
14. Projectile 起点/终点正确。
15. 技能结束后能自然回 Idle/Move。
16. 连续攻击没有额外停顿。
17. 改 animation speed 后仍完整播放。
18. 皮肤切换正确。
19. HUD 头像/技能图标正确。
20. Runtime 不依赖 `D:\Ark\_Unpacked`。

除非用户明确要求，不需要为了完成资源绑定主动跑完整 Build/Test。

---

## 19. 一句话给本地 Agent

> 用户已经把游戏本体素材导出好了。你的任务不是再设计一个“自动导入器”，而是像一个真正接手 Unity 项目的开发者一样，读取导出包、判断正确映射、直接修改工程，把角色的 Spine、动作、特效、音效、UI、皮肤和 Gameplay 全部绑定完整；不确定的地方不要猜，已经人工调好的地方不要覆盖。

### Shared ranged target acquisition

- 所有远程干员的普攻 Resolver 都应实现 `IPlayerBasicAttackTargetProvider`，由 `PlayerAttackController` 在 AttackRoutine 开始前统一完成预锁定；当前 Wisadel、Schwarz、FrostNova 已接入。
- `CombatEntity.ActiveEntities` 是远程目标发现的主注册表；公共 `RangedBasicAttackTargeting` 同时合并 `Physics.OverlapSphere` 候选作为运行时设施/动态对象的生命周期兜底。普通敌人和训练假人最终走同一套 Team/Health/范围筛选。
- 远程索敌公共筛选使用 `RangedBasicAttackTargeting`；角色只保留自己的射程、前向权重、视线等差异参数，不再各自依赖 `Physics.OverlapSphere` 去“发现”敌人。
- 有远程 TargetProvider 但找不到目标时，不开始普攻周期，因此不播攻击动作、不发弹、不消耗弹药。
- TargetProvider 预锁的目标应缓存到 impact 阶段，保证动画 / Trail / Hit / Damage 使用同一个目标。

### Ranged hit presentation ownership

- 角色专属远程命中特效不要根据多个 `AttackStarted / AmmoConsumed / AttackHit` 回调的先后顺序去猜攻击类型。
- 如果技能会改变同一发基础攻击的弹道/命中特效，应在“预锁目标”时同时缓存攻击模式，并在 Resolver 真正成功结算伤害后发出带目标和模式的 resolved-hit 事件。
- Wisadel 使用 `ShotHitResolved(target, wasSkill3)`：普攻与 S3 的 Hit FX 都绑定到真实伤害目标，S3 最后一发即使在 impact 前结束生命周期也不会丢失技能类型。

### Training dummy HP precision rule

- 公共训练假人不要通过超大 `float` HP 实现“无敌”。例如 `1_000_000_000f` 在该数量级的浮点间隔过大，十几点的小伤害可能无法改变 `CurrentHealth`，导致 `Health.TakeDamage()` 返回 `dealt=0`，随后 `DamageSystem.Apply()` 返回 Rejected。
- 当前公共假人使用 `100_000f` 作为精度安全的测试血量，并由 `TrainingDummyInvincible` 在每次真实 Applied 命中后立即回满。这样普通攻击、低伤技能、伤害数字、Hit FX、受击事件都经过真实伤害管线，同时假人不会死亡。

### Fixed/world-space impact FX

- 导出层级带 `static_offset/fixed` 的爆炸/命中特效应优先按命中世界坐标生成，不要默认挂到目标 Transform 下继承目标旋转/深度。
- Wisadel S3 的 `skill_03_hit / skill_03_hit_02 / skill_03_hit_03` 现在在同一个 resolved impact world point 生成，并使用轻微 camera-depth bias + 至少 120 的 SpriteRenderer sortingOrder，避免被目标模型/场景深度遮挡。

### Authoritative exported binding manifests

- 当角色包提供 `vfx_binding.json` / `audio_mapping.json` 时，它们优先于文件名猜测：先按 manifest 区分普通攻击、技能 projectile、trail、hit 与皮肤替换关系，再做 Unity 绑定。
- projectile 配置键（例如 Wisadel 的 `projectile_chr_wisdel_s3` / `*_graphic` / `*_logic`）只说明运行时 projectile 语义；若导出包没有对应可渲染资源，不能把这些 key 当成现成 prefab。此时 `*_trail` 仍只负责导出的飞行视觉。
- 音频也必须从 `audio_mapping.json` 的事件语义绑定，不能只导 voice 后把 `PlayableOperatorAudioProfile` 留空。
