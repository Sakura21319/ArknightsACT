# PRTS Asset Pipeline

本项目已从单独的德克萨斯下载器升级为通用 PRTS 原型素材管线。

请优先查看：

- `Docs/PRTS_PROTOTYPE_ASSET_PACK.md`

Unity 菜单：

```text
ArknightsACT
→ Assets
→ PRTS
→ Download Full Prototype Pack
```

当前一键包包含：

- 德克萨斯
- 源石虫
- 士兵
- 弩手
- 猎狗
- 妖怪
- 重装防御者

下载后的 PRTS 图像/Spine 源文件只保存在本地，并被 `.gitignore` 排除；Combat / Gameplay 不允许直接引用 PRTS 路径或 Spine 类型。

运行时表现后续通过 Presentation Adapter 接入兼容 Spine Runtime，或使用离线烘帧方案。
