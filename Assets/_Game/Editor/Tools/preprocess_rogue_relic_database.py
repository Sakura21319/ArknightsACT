import csv
import hashlib
import html
import json
import math
import os
import re
import sys
import time
import urllib.parse
import zipfile
from collections import defaultdict

ROOT = sys.argv[1]

MASTER_CSV = os.path.join(ROOT, "RogueRelicDatabase.csv")
ALL_CSV = os.path.join(ROOT, "RogueRelicDatabase_all_versions.csv")
OUT_ALL = os.path.join(ROOT, "RogueRelicDatabase_Preprocessed.csv")
OUT_ALL_XLSX = os.path.join(ROOT, "RogueRelicDatabase_Preprocessed.xlsx")
OUT_IS1 = os.path.join(ROOT, "RogueRelicDatabase_IS1_CurrentPool.csv")
OUT_IS1_XLSX = os.path.join(ROOT, "RogueRelicDatabase_IS1_CurrentPool.xlsx")
OUT_HISTORY = os.path.join(ROOT, "RogueRelicDatabase_all_versions_preprocessed.csv")
OUT_STATS = os.path.join(ROOT, "RogueRelicDatabase_Preprocess.stats.json")
OUT_MD = os.path.join(ROOT, "RogueRelic_EconomyDesign.md")

FLOW_RULES = [
    ("携带/编队（当前系统无对应）", ("可携带干员", "携带干员", "编队上限", "编队")),
    ("希望（当前系统无对应）", ("希望",)),
    ("招募/进阶（当前系统无对应）", ("招募券", "招募", "晋升", "进阶")),
    ("战斗节点流程（当前系统无对应）", ("战斗节点",)),
    ("部署系统（当前系统无对应）", ("部署费用", "部署人数", "可同时部署", "部署上限", "部署干员")),
    ("局内货币（改为卖钱物）", ("源石锭",)),
    ("商店/探索流程（当前系统无对应）", ("商店", "商品", "探索", "骰子")),
]

# Only effects that can be represented with systems already present in ArknightsACT are treated
# as combat relics. Anything requiring a new recruitment/deployment/hope/node system becomes a
# sellable collection item instead.
SUPPORTED_COMBAT_RULES = [
    ("敌人生命值", ("敌人生命值", "敌方生命值", "敌人最大生命值", "敌方最大生命值")),
    ("我方最大生命值", ("我方生命值", "我方最大生命值", "干员生命值", "干员最大生命值", "最大生命值")),
    ("输出伤害", ("造成的伤害", "物理伤害", "法术伤害", "真实伤害")),
    ("技能冷却", ("冷却时间", "技能冷却", "冷却")),
    ("技能触发治疗", ("施放技能时回复", "施放技能时恢复", "技能时回复", "技能时恢复")),
]

# Used only for impact scoring after an item has already been classified as a relic.
COMBAT_SCORE_TERMS = [
    "攻击力", "伤害", "物理伤害", "法术伤害", "真实伤害",
    "生命值", "最大生命值", "冷却", "技能", "治疗", "回复", "敌人",
]

LUXURY_TERMS = (
    "钻", "宝石", "珠宝", "黄金", "金", "王冠", "皇冠", "项链", "吊坠", "戒指",
    "雕像", "古董", "珍藏", "宝藏", "圣物", "权杖", "遗物", "瑰宝", "纪念", "勋章",
)

SIZE_RULES = [
    ("2x2", ("帐篷", "引擎", "沙盒", "盆栽", "雕像", "雕塑", "大箱", "箱子", "大锅",
             "大壶", "仪器", "装置", "设备", "机器", "盾", "盔甲", "模型", "石碑", "桶")),
    ("2x1", ("书", "地图", "文件", "画", "卷轴", "卷", "盒饭", "盒", "盘", "板",
             "手册", "相册", "报纸", "图鉴", "册")),
    ("1x2", ("望远镜", "长刀", "刀", "剑", "枪", "棍", "杖", "瓶", "罐", "火把", "伞", "弓", "针筒")),
    ("1x1", ("戒指", "吊坠", "项链", "宝石", "钻", "硬币", "徽章", "钥匙", "芯片",
             "卡", "票", "药", "糖", "果", "肉", "眼镜", "手表", "零件", "照片")),
]

RANGES = {
    "普通": (900, 1800),
    "稀有": (2200, 4500),
    "珍贵": (5500, 10000),
    "绝世": (13000, 25000),
}

GRADE_SPLIT = {
    "NORMAL": ("普通", "稀有", 0.70),
    "RARE": ("稀有", "珍贵", 0.65),
    "SUPER_RARE": ("珍贵", "绝世", 0.70),
}

EDITABLE_FIELDS = {
    "item_kind", "game_rarity", "game_value", "game_effect",
    "grid_size", "apex_selected", "enabled", "notes",
}

def read_csv(path):
    with open(path, "r", encoding="utf-8-sig", newline="") as f:
        return list(csv.DictReader(f))

def write_csv(path, rows, fields):
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)

def norm_name(v):
    return re.sub(r"\s+", "", str(v or "")).strip().casefold()

def stable01(text):
    h = hashlib.sha256(str(text).encode("utf-8")).digest()
    return int.from_bytes(h[:8], "big") / float(2**64 - 1)

def numbers(text):
    return [float(x) for x in re.findall(r"(?<![A-Za-z])[-+]?\d+(?:\.\d+)?", str(text or ""))]

def classify(row):
    # Classification is based on the mechanical effect, not flavour text/name. The rule is:
    # if ArknightsACT already has a practical runtime hook for it, keep it as a combat relic;
    # otherwise turn it into a sellable collection item instead of inventing another subsystem.
    effect = str(row.get("effect", "") or "").strip()
    text = effect or str(row.get("description", "") or "")

    # Explicit user rule: enemy HP modifiers are usable with the existing enemy Health pipeline.
    if any(term in text for term in ("敌人生命值", "敌方生命值", "敌人最大生命值", "敌方最大生命值")):
        return "藏品", "现有战斗系统可映射：敌人生命值"

    # Enemy attack/defence/speed are deliberately NOT included here: unlike enemy max health,
    # there is currently no global collectible consumer for those modifiers.
    if any(target in text for target in ("敌人", "敌方")) and any(
        stat in text for stat in ("攻击力", "防御力", "防御", "攻击速度", "攻速", "移动速度", "法术抗性", "抗性")
    ):
        return "收集品", "敌人属性效果当前未接入（生命值除外）"

    # Player/operator attack percentage can reuse the existing outgoing-damage modifier.
    if "攻击力" in text and not any(target in text for target in ("敌人", "敌方")):
        return "藏品", "现有战斗系统可映射：攻击力→输出伤害"

    for reason, terms in SUPPORTED_COMBAT_RULES:
        if any(term in text for term in terms):
            return "藏品", "现有战斗系统可映射：" + reason

    # Arknights' unqualified “生命值 +N / -N” commonly means the roguelike objective-life
    # resource. Entity HP must explicitly name enemies/operators/self or say 最大生命值.
    if re.search(r"(^|[，。；;\s])生命值\s*[+＋\-－]", text) and not any(
        target in text for target in ("敌人", "敌方", "我方", "干员", "自身", "最大生命值")
    ):
        return "收集品", "目标生命资源（当前系统无对应）"
    if "目标生命" in text:
        return "收集品", "目标生命资源（当前系统无对应）"

    for reason, terms in FLOW_RULES:
        if any(term in text for term in terms):
            return "收集品", reason

    # Combat-looking effects that are not wired today stay sellable rather than silently
    # creating attack-speed/defence/crit/control/redeploy/shield systems.
    unsupported_combat = (
        "攻击速度", "攻速", "攻击间隔", "防御力", "防御", "法术抗性", "抗性",
        "暴击", "闪避", "阻挡", "再部署", "移动速度", "护盾", "晕眩", "眩晕",
        "寒冷", "冻结", "束缚", "沉默", "灼燃", "元素",
    )
    if any(term in text for term in unsupported_combat):
        return "收集品", "战斗效果当前未接入，避免新增系统"

    return "收集品", "无可直接映射的现有战斗效果"

def infer_size(row):
    text = str(row.get("name", "")) + " " + str(row.get("description", ""))
    for size, terms in SIZE_RULES:
        if any(term in text for term in terms):
            return size, "否"
    return "1x1", "是"

def area(size):
    try:
        a, b = size.lower().split("x")
        return int(a) * int(b)
    except Exception:
        return 1

def impact_score(row, item_kind):
    effect = str(row.get("effect", ""))
    desc = str(row.get("description", ""))
    name = str(row.get("name", ""))
    try:
        original_value = float(row.get("official_value") or 0)
    except Exception:
        original_value = 0.0

    nums = numbers(effect)
    pct = [abs(float(v)) for v in re.findall(r"([-+]?\d+(?:\.\d+)?)\s*[%％]", effect)]
    score = original_value * 1.8
    score += sum(min(abs(v), 100) for v in nums[:10]) * 0.18
    score += sum(min(v, 100) for v in pct[:10]) * 0.32

    if item_kind == "藏品":
        score += sum(4.5 for term in COMBAT_SCORE_TERMS if term in effect)
        if "所有" in effect or "我方" in effect or "敌人" in effect:
            score += 6
        if "每秒" in effect or "持续" in effect:
            score += 4
    else:
        score += sum(2.5 for term in LUXURY_TERMS if term in name + desc)
        score += sum(min(abs(v), 50) for v in nums[:6]) * 0.12

    if str(row.get("is_special", "")) in ("是", "True", "true", "1"):
        score += 8

    return score + stable01(row.get("canonical_key") or name) * 0.01

def assign_rarities(rows):
    grouped = defaultdict(list)
    for row in rows:
        grouped[str(row.get("official_rarity", "")).upper()].append(row)

    for official, members in grouped.items():
        low, high, low_ratio = GRADE_SPLIT.get(official, ("普通", "稀有", 0.75))
        ordered = sorted(members, key=lambda r: (r["_impact_score"], stable01(r.get("canonical_key"))))
        cutoff = max(1, min(len(ordered), int(math.ceil(len(ordered) * low_ratio))))
        for idx, row in enumerate(ordered):
            row["game_rarity"] = low if idx < cutoff else high
            row["_rarity_percentile"] = (idx + 1) / max(1, len(ordered))

def assign_values(rows):
    tier_groups = defaultdict(list)
    for row in rows:
        tier_groups[row["game_rarity"]].append(row)

    for tier, members in tier_groups.items():
        lo, hi = RANGES.get(tier, (900, 1800))
        ordered = sorted(members, key=lambda r: (r["_impact_score"], stable01(r.get("canonical_key"))))
        count = max(1, len(ordered) - 1)
        for idx, row in enumerate(ordered):
            t = idx / count
            base = lo + (hi - lo) * t
            size_factor = 1.0 + 0.14 * (area(row["grid_size"]) - 1)
            # Larger pieces sell for more in total but less efficiently per slot.
            value = base * size_factor
            row["game_value"] = int(round(value / 100.0) * 100)
            row["demand_multiplier"] = {
                "普通": "1.45x", "稀有": "1.60x", "珍贵": "1.80x", "绝世": "2.00x"
            }.get(tier, "1.50x") if row["item_kind"] == "收集品" else "1.00x"

def assign_apex_rank(rows):
    candidates = []
    for row in rows:
        if row["item_kind"] != "收集品":
            row["apex_rank"] = ""
            continue
        luxury = sum(1 for term in LUXURY_TERMS if term in (row.get("name", "") + row.get("description", "")))
        efficiency_bonus = (5 - area(row["grid_size"])) * 900
        score = int(row["game_value"]) + luxury * 4500 + efficiency_bonus
        if row["game_rarity"] == "绝世":
            score += 6000
        elif row["game_rarity"] == "珍贵":
            score += 2500
        candidates.append((score, stable01(row.get("canonical_key")), row))

    candidates.sort(key=lambda x: (x[0], x[1]), reverse=True)
    for rank, (_, _, row) in enumerate(candidates, start=1):
        row["apex_rank"] = rank if rank <= 25 else ""

def preprocess_master(master, all_versions):
    is1_source = {}
    is1_names = set()
    for row in all_versions:
        if row.get("is") == "IS1":
            key = row.get("canonical_key") or norm_name(row.get("name"))
            is1_names.add(key)
            if key not in is1_source:
                is1_source[key] = row

    for row in master:
        key = row.get("canonical_key") or norm_name(row.get("name"))
        row["canonical_key"] = key
        kind, reason = classify(row)
        row["item_kind"] = row.get("item_kind") or kind
        row["classification_reason"] = reason
        size, review = infer_size(row)
        row["grid_size"] = row.get("grid_size") or size
        row["size_review"] = review if row.get("grid_size") == size else "否"
        row["_impact_score"] = impact_score(row, row["item_kind"])
        row["current_is1_pool"] = "是" if key in is1_names else "否"
        src = is1_source.get(key, {})
        row["is1_order_id"] = src.get("order_id", "")
        row["is1_relic_id"] = src.get("relic_id", "")
        row["apex_selected"] = row.get("apex_selected") or "否"
        row["enabled"] = row.get("enabled") or "是"
        row["notes"] = row.get("notes") or ""

    assign_rarities(master)
    assign_values(master)
    assign_apex_rank(master)

    for row in master:
        if row["item_kind"] == "藏品":
            row["game_effect"] = row.get("game_effect") or row.get("effect", "")
        else:
            row["game_effect"] = ""
        row["impact_score"] = round(row["_impact_score"], 2)
        row.pop("_impact_score", None)
        row.pop("_rarity_percentile", None)

    return [r for r in master if r["current_is1_pool"] == "是"]

def propagate_to_history(master, history):
    by_key = {r["canonical_key"]: r for r in master}
    out = []
    for raw in history:
        key = raw.get("canonical_key") or norm_name(raw.get("name"))
        base = by_key.get(key)
        row = dict(raw)
        if base:
            for f in (
                "item_kind", "classification_reason", "game_rarity", "game_value",
                "game_effect", "grid_size", "size_review", "demand_multiplier",
                "apex_rank", "apex_selected", "current_is1_pool", "impact_score",
            ):
                row[f] = base.get(f, "")
        out.append(row)
    return out

def local_icon_file(row):
    rel = str(row.get("local_icon", "") or "").strip()
    if not rel:
        return ""
    path = rel if os.path.isabs(rel) else os.path.normpath(os.path.join(ROOT, rel))
    if not os.path.isfile(path) or os.path.getsize(path) < 128:
        return ""
    try:
        with open(path, "rb") as f:
            return path if f.read(8) == b"\x89PNG\r\n\x1a\n" else ""
    except Exception:
        return ""

def xlsx_col(i):
    s = ""
    while i:
        i, rem = divmod(i - 1, 26)
        s = chr(65 + rem) + s
    return s

def xml_text(v):
    s = "" if v is None else str(v)
    s = "".join(ch for ch in s if ch in "\t\n\r" or ord(ch) >= 32)
    return html.escape(s, quote=False)

def cell_inline(ref, value, style):
    return f'<c r="{ref}" s="{style}" t="inlineStr"><is><t xml:space="preserve">{xml_text(value)}</t></is></c>'

COLUMNS = [
    ("图标", "image"),
    ("名称", "name"),
    ("当前IS1投放", "current_is1_pool"),
    ("物品类型", "item_kind"),
    ("分类依据", "classification_reason"),
    ("方舟稀有度", "official_rarity"),
    ("游戏稀有度", "game_rarity"),
    ("基础龙门币价值", "game_value"),
    ("建设需求价倍率", "demand_multiplier"),
    ("物品大小", "grid_size"),
    ("尺寸待确认", "size_review"),
    ("最新来源版本", "is"),
    ("主题", "theme"),
    ("最新图鉴编号", "order_id"),
    ("IS1原编号", "is1_order_id"),
    ("官方效果", "effect"),
    ("游戏效果", "game_effect"),
    ("官方描述", "description"),
    ("解锁条件", "unlock"),
    ("顶级珍宝候选排名", "apex_rank"),
    ("最贵珍宝选用", "apex_selected"),
    ("启用", "enabled"),
    ("备注", "notes"),
    ("内部ID", "relic_id"),
    ("本地图标路径", "local_icon"),
]

WIDTHS = [11,24,12,12,22,14,14,16,16,12,12,12,24,12,12,42,42,48,38,16,16,10,34,36,50]

def write_xlsx(path, rows, title):
    editable = {"item_kind", "game_rarity", "game_value", "game_effect", "grid_size", "apex_selected", "enabled", "notes"}
    image_entries = []
    sheet_rows = []
    header = []
    for i, (caption, _) in enumerate(COLUMNS, 1):
        header.append(cell_inline(f"{xlsx_col(i)}1", caption, 1))
    sheet_rows.append('<row r="1" ht="28" customHeight="1">' + "".join(header) + "</row>")

    for ri, row in enumerate(rows, 2):
        cells = []
        for ci, (_, key) in enumerate(COLUMNS, 1):
            ref = f"{xlsx_col(ci)}{ri}"
            style = 3 if key in editable else 2
            if key == "image":
                cells.append(cell_inline(ref, "", 2))
                local = local_icon_file(row)
                if local:
                    image_entries.append((ri - 1, local, row.get("name", "")))
            else:
                cells.append(cell_inline(ref, row.get(key, ""), style))
        sheet_rows.append(f'<row r="{ri}" ht="52" customHeight="1">' + "".join(cells) + "</row>")

    last_row = len(rows) + 1
    last_col = xlsx_col(len(COLUMNS))
    cols = "".join(
        f'<col min="{i}" max="{i}" width="{WIDTHS[i-1]}" customWidth="1"/>'
        for i in range(1, len(COLUMNS) + 1)
    )
    drawing_tag = '<drawing r:id="rId1"/>' if image_entries else ""
    worksheet = f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>
<sheetFormatPr defaultRowHeight="18"/><cols>{cols}</cols><sheetData>{"".join(sheet_rows)}</sheetData>
<autoFilter ref="A1:{last_col}{last_row}"/>{drawing_tag}
<pageMargins left="0.3" right="0.3" top="0.5" bottom="0.5" header="0.2" footer="0.2"/></worksheet>'''

    styles = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/></font></fonts>
<fills count="4"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF234F59"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFFFF2CC"/></patternFill></fill></fills>
<borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color rgb="FFD9E2E5"/></left><right style="thin"><color rgb="FFD9E2E5"/></right><top style="thin"><color rgb="FFD9E2E5"/></top><bottom style="thin"><color rgb="FFD9E2E5"/></bottom><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="0" borderId="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf><xf numFmtId="0" fontId="0" fillId="3" borderId="1" applyAlignment="1"><alignment vertical="top" wrapText="1"/></xf></cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>'''

    workbook = f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheets><sheet name="{html.escape(title, quote=True)}" sheetId="1" r:id="rId1"/></sheets></workbook>'''

    rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'''
    wb_rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>'''

    drawing_xml = ""
    drawing_rels = ""
    sheet_rels = ""
    if image_entries:
        anchors = []
        rel_items = []
        for idx, (row_zero, local, name) in enumerate(image_entries, 1):
            rel_items.append(
                f'<Relationship Id="rId{idx}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/image{idx}.png"/>'
            )
            anchors.append(f'''<xdr:oneCellAnchor>
<xdr:from><xdr:col>0</xdr:col><xdr:colOff>28575</xdr:colOff><xdr:row>{row_zero}</xdr:row><xdr:rowOff>28575</xdr:rowOff></xdr:from>
<xdr:ext cx="571500" cy="571500"/>
<xdr:pic><xdr:nvPicPr><xdr:cNvPr id="{idx}" name="{xml_text(name or ('icon_' + str(idx)))}"/><xdr:cNvPicPr/></xdr:nvPicPr>
<xdr:blipFill><a:blip r:embed="rId{idx}"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>
<xdr:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="571500" cy="571500"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></xdr:spPr></xdr:pic>
<xdr:clientData/></xdr:oneCellAnchor>''')
        drawing_xml = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">''' + "".join(anchors) + "</xdr:wsDr>"
        drawing_rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">''' + "".join(rel_items) + "</Relationships>"
        sheet_rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing1.xml"/></Relationships>'''

    types = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Default Extension="png" ContentType="image/png"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>''' + (
        '<Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/>' if image_entries else ""
    ) + "</Types>"

    def write_archive(target):
        with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as z:
            z.writestr("[Content_Types].xml", types)
            z.writestr("_rels/.rels", rels)
            z.writestr("xl/workbook.xml", workbook)
            z.writestr("xl/_rels/workbook.xml.rels", wb_rels)
            z.writestr("xl/worksheets/sheet1.xml", worksheet)
            z.writestr("xl/styles.xml", styles)
            if image_entries:
                z.writestr("xl/worksheets/_rels/sheet1.xml.rels", sheet_rels)
                z.writestr("xl/drawings/drawing1.xml", drawing_xml)
                z.writestr("xl/drawings/_rels/drawing1.xml.rels", drawing_rels)
                for idx, (_, local, _) in enumerate(image_entries, 1):
                    z.write(local, f"xl/media/image{idx}.png")

    actual = path
    try:
        write_archive(actual)
    except PermissionError:
        stem, ext = os.path.splitext(path)
        actual = stem + "_generated" + ext
        try:
            write_archive(actual)
        except PermissionError:
            actual = stem + "_generated_" + str(int(time.time())) + ext
            write_archive(actual)
    return actual, len(image_entries)

def save_preprocessed_csv(path, rows):
    fields = [key for _, key in COLUMNS if key != "image"]
    write_csv(path, rows, fields)

master = read_csv(MASTER_CSV)
history = read_csv(ALL_CSV)
is1_pool = preprocess_master(master, history)
history_pre = propagate_to_history(master, history)

master.sort(key=lambda r: (0 if r["current_is1_pool"] == "是" else 1, str(r["game_rarity"]), str(r["name"])))
is1_pool.sort(key=lambda r: (int(r.get("is1_order_id") or 999999), str(r.get("name", ""))))

save_preprocessed_csv(OUT_ALL, master)
save_preprocessed_csv(OUT_IS1, is1_pool)
history_fields = list(history_pre[0].keys()) if history_pre else []
if history_fields:
    write_csv(OUT_HISTORY, history_pre, history_fields)

written_all_xlsx, embedded_all_icons = write_xlsx(OUT_ALL_XLSX, master, "全部藏品预处理")
written_is1_xlsx, embedded_is1_icons = write_xlsx(OUT_IS1_XLSX, is1_pool, "IS1当前投放池")

stats = {
    "all_unique_count": len(master),
    "is1_current_pool_count": len(is1_pool),
    "all_versions_record_count": len(history_pre),
    "type_counts_all": dict(sorted(defaultdict(int, {k: sum(1 for r in master if r["item_kind"] == k) for k in ("藏品","收集品")}).items())),
    "type_counts_is1": {k: sum(1 for r in is1_pool if r["item_kind"] == k) for k in ("藏品","收集品")},
    "rarity_counts_all": {k: sum(1 for r in master if r["game_rarity"] == k) for k in ("普通","稀有","珍贵","绝世")},
    "rarity_counts_is1": {k: sum(1 for r in is1_pool if r["game_rarity"] == k) for k in ("普通","稀有","珍贵","绝世")},
    "size_counts_is1": {k: sum(1 for r in is1_pool if r["grid_size"] == k) for k in ("1x1","1x2","2x1","2x2")},
    "size_review_is1": sum(1 for r in is1_pool if r["size_review"] == "是"),
    "all_xlsx": written_all_xlsx,
    "is1_xlsx": written_is1_xlsx,
    "embedded_icons_all": embedded_all_icons,
    "embedded_icons_is1": embedded_is1_icons,
    "missing_local_icons_all": sum(1 for r in master if not local_icon_file(r)),
    "missing_local_icons_is1": sum(1 for r in is1_pool if not local_icon_file(r)),
    "top_apex_candidates": [
        {"rank": r["apex_rank"], "name": r["name"], "rarity": r["game_rarity"], "value": r["game_value"], "size": r["grid_size"]}
        for r in sorted((x for x in master if x.get("apex_rank") != ""), key=lambda x: int(x["apex_rank"]))[:15]
    ],
}
with open(OUT_STATS, "w", encoding="utf-8") as f:
    json.dump(stats, f, ensure_ascii=False, indent=2)

with open(OUT_MD, "w", encoding="utf-8") as f:
    f.write("""# ArknightsACT 搜打撤物品与经济预处理规则

## 当前投放
- 当前实际掉落池只使用 **IS1《刻俄柏的灰蕈迷境》出现过的物品名称**。
- 如果同名物品在后续 IS 有新版描述/效果/图标，当前池使用**最新一期版本**的数据。

## 类型
- **藏品**：能直接映射到 ACT 战斗的效果，默认将官方战斗效果作为游戏效果初稿。
- **藏品**：只要当前 ACT 已经有可复用的战斗机制就保留，例如输出伤害、角色最大生命值、技能冷却/技能治疗，以及“敌人生命值±%”。
- **收集品**：希望、招募/晋升、携带干员、部署费用/人数、目标生命、源石锭、节点等当前没有对应系统的效果，改为撤离后出售与基地升级材料。
- **Excel 图标**：全部使用已下载到项目内的 PNG 并直接嵌入 XLSX；打开表格不再访问 PRTS。

## 稀有度
- NORMAL → 普通 / 稀有
- RARE → 稀有 / 珍贵
- SUPER_RARE → 珍贵 / 绝世
- 拆档不是纯随机：按原始价值、效果强度、特殊性评分排序，仅用稳定哈希打破同分。

## 龙门币基础价值
- 普通：900–1,800
- 稀有：2,200–4,500
- 珍贵：5,500–10,000
- 绝世：13,000–25,000
- 大件总价略高，但单位格价值更低。
- 当某收集品被基地升级指定需求时，建议按 1.45x / 1.60x / 1.80x / 2.00x 提高收购价。

## 物品尺寸
- 自动判定为 1x1 / 1x2 / 2x1 / 2x2。
- 无法通过名称与描述可靠判断的条目标记“尺寸待确认=是”，供人工修正。

## 顶级珍宝
- 已给高价值收集品生成“顶级珍宝候选排名”。
- 最终“非洲之心”级物品由人工在“最贵珍宝选用”列确定，不自动锁死。
""")

print(json.dumps(stats, ensure_ascii=False))
