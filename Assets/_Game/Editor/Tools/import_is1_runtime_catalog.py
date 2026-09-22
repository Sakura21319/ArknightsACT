import json
import os
import posixpath
import re
import shutil
import sys
import time
import zipfile
import xml.etree.ElementTree as ET

PROJECT_ROOT = os.path.abspath(sys.argv[1])
XLSX_PATH = os.path.abspath(sys.argv[2])
OUTPUT_JSON = os.path.abspath(sys.argv[3])
ICON_OUTPUT_DIR = os.path.abspath(sys.argv[4])
REPORT_PATH = os.path.abspath(sys.argv[5])

NS_MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
NS_REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
NS_PKGREL = "http://schemas.openxmlformats.org/package/2006/relationships"
NS_DRAW = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"
NS_A = "http://schemas.openxmlformats.org/drawingml/2006/main"

HEADER_ALIASES = {
    "name": ["名称", "物品名称", "name"],
    "kind": ["类型", "物品类型", "分类", "item_kind", "kind"],
    "rarity": ["游戏稀有度", "稀有度", "game_rarity", "rarity"],
    "value": ["基础龙门币价值", "游戏价值", "龙门币价值", "价值", "game_value", "collectionValue"],
    "grid": ["物品大小", "物品尺寸", "尺寸", "grid_size", "grid"],
    "description": ["官方描述", "描述", "description"],
    "effect": ["游戏效果", "效果", "官方效果", "game_effect", "effect"],
    "enabled": ["启用", "enabled"],
    "id": ["内部ID", "ID", "relic_id", "id"],
    "order": ["IS1原编号", "原编号", "is1_order_id", "order_id"],
}

PROFESSION_MAP = {
    "先锋": "Vanguard",
    "近卫": "Guard",
    "重装": "Defender",
    "狙击": "Sniper",
    "术师": "Caster",
    "辅助": "Supporter",
    "医疗": "Medic",
    "特种": "Specialist",
}
MELEE_PROFESSIONS = ["Vanguard", "Guard", "Defender", "Specialist"]
RANGED_PROFESSIONS = ["Sniper", "Caster", "Supporter", "Medic"]


def log(message):
    print(message, flush=True)


def q(ns, name):
    return f"{{{ns}}}{name}"


def resolve_zip_target(base_part, target):
    target = (target or "").replace("\\", "/")
    if target.startswith("/"):
        return target.lstrip("/")
    return posixpath.normpath(posixpath.join(posixpath.dirname(base_part), target))


def rels_path_for(part_path):
    return posixpath.join(posixpath.dirname(part_path), "_rels", posixpath.basename(part_path) + ".rels")


def get_shared_strings(z):
    path = "xl/sharedStrings.xml"
    if path not in z.namelist():
        return []
    root = ET.fromstring(z.read(path))
    values = []
    for si in root.findall(q(NS_MAIN, "si")):
        values.append("".join(t.text or "" for t in si.iter(q(NS_MAIN, "t"))))
    return values


def workbook_first_sheet_path(z):
    workbook = ET.fromstring(z.read("xl/workbook.xml"))
    sheet = workbook.find(f".//{q(NS_MAIN, 'sheet')}")
    if sheet is None:
        raise RuntimeError("Workbook has no worksheet")
    rid = sheet.attrib.get(q(NS_REL, "id"))
    rels = ET.fromstring(z.read("xl/_rels/workbook.xml.rels"))
    for rel in rels.findall(q(NS_PKGREL, "Relationship")):
        if rel.attrib.get("Id") == rid:
            return resolve_zip_target("xl/workbook.xml", rel.attrib.get("Target"))
    raise RuntimeError("Worksheet relationship not found")


def col_letters(ref):
    m = re.match(r"([A-Z]+)", ref or "")
    return m.group(1) if m else ""


def cell_value(cell, shared):
    ctype = cell.attrib.get("t", "")
    if ctype == "inlineStr":
        return "".join(t.text or "" for t in cell.iter(q(NS_MAIN, "t")))
    value = cell.find(q(NS_MAIN, "v"))
    raw = value.text if value is not None and value.text is not None else ""
    if ctype == "s" and raw:
        try:
            return shared[int(raw)]
        except Exception:
            return raw
    return raw


def read_sheet_rows(z, sheet_path):
    shared = get_shared_strings(z)
    root = ET.fromstring(z.read(sheet_path))
    rows = {}
    for row in root.findall(f".//{q(NS_MAIN, 'row')}"):
        row_num = int(row.attrib.get("r", "0") or 0)
        values = {}
        for cell in row.findall(q(NS_MAIN, "c")):
            values[col_letters(cell.attrib.get("r", ""))] = cell_value(cell, shared)
        rows[row_num] = values
    return root, rows


def read_embedded_images(z, sheet_path, sheet_root):
    result = {}
    drawing = sheet_root.find(q(NS_MAIN, "drawing"))
    if drawing is None:
        return result
    drawing_rid = drawing.attrib.get(q(NS_REL, "id"))
    sheet_rels_path = rels_path_for(sheet_path)
    if not drawing_rid or sheet_rels_path not in z.namelist():
        return result

    drawing_path = None
    sheet_rels = ET.fromstring(z.read(sheet_rels_path))
    for rel in sheet_rels.findall(q(NS_PKGREL, "Relationship")):
        if rel.attrib.get("Id") == drawing_rid:
            drawing_path = resolve_zip_target(sheet_path, rel.attrib.get("Target"))
            break
    if not drawing_path or drawing_path not in z.namelist():
        return result

    drawing_rels_path = rels_path_for(drawing_path)
    if drawing_rels_path not in z.namelist():
        return result
    drawing_rels = ET.fromstring(z.read(drawing_rels_path))
    image_targets = {}
    for rel in drawing_rels.findall(q(NS_PKGREL, "Relationship")):
        image_targets[rel.attrib.get("Id", "")] = resolve_zip_target(drawing_path, rel.attrib.get("Target"))

    drawing_root = ET.fromstring(z.read(drawing_path))
    for anchor in list(drawing_root):
        frm = anchor.find(q(NS_DRAW, "from"))
        if frm is None:
            continue
        row_el = frm.find(q(NS_DRAW, "row"))
        col_el = frm.find(q(NS_DRAW, "col"))
        if row_el is None or col_el is None or int(col_el.text or 0) != 0:
            continue
        blip = anchor.find(f".//{q(NS_A, 'blip')}")
        if blip is None:
            continue
        rid = blip.attrib.get(q(NS_REL, "embed"))
        member = image_targets.get(rid or "")
        if not member or member not in z.namelist():
            continue
        excel_row = int(row_el.text or 0) + 1
        result[excel_row] = (member, z.read(member))
    return result


def find_column(headers, aliases):
    normalized = {str(v).strip(): col for col, v in headers.items() if str(v).strip()}
    lowered = {k.casefold(): v for k, v in normalized.items()}
    for alias in aliases:
        if alias in normalized:
            return normalized[alias]
        if alias.casefold() in lowered:
            return lowered[alias.casefold()]
    return None


def get_value(row, col):
    return str(row.get(col, "") or "").strip() if col else ""


def safe_int(value, default=0):
    text = str(value or "").strip().replace(",", "")
    m = re.search(r"-?\d+(?:\.\d+)?", text)
    if not m:
        return default
    try:
        return int(round(float(m.group(0))))
    except Exception:
        return default


def sanitize_id(value):
    text = str(value or "").strip()
    text = re.sub(r"[^A-Za-z0-9_.-]+", "_", text)
    return text.strip("_") or "relic"


def enabled_value(value):
    text = str(value or "").strip().casefold()
    if not text:
        return True
    return text not in {"否", "false", "0", "no", "off", "disabled"}


def parse_grid(value):
    text = str(value or "").strip().lower().replace("×", "x").replace("*", "x")
    m = re.search(r"(\d+)\s*x\s*(\d+)", text)
    if not m:
        return 1, 1
    return max(1, int(m.group(1))), max(1, int(m.group(2)))


def map_rarity(value):
    text = str(value or "").strip()
    if text == "绝世":
        return "Epic", "Mythic"
    if text == "珍贵":
        return "Rare", "Precious"
    if text == "稀有":
        return "Rare", "Rare"
    return "Common", "Common"


def percent_number(raw):
    try:
        return float(raw) / 100.0
    except Exception:
        return 0.0


def signed_number(raw):
    try:
        return float(raw)
    except Exception:
        return 0.0


def nearest_professions(text, start):
    window = text[max(0, start - 42):start]
    if "近战" in window:
        return MELEE_PROFESSIONS
    if "远程" in window:
        return RANGED_PROFESSIONS

    found = []
    for cn, en in PROFESSION_MAP.items():
        pos = window.rfind(cn)
        if pos >= 0:
            found.append((pos, en))
    if not found:
        return [""]
    max_pos = max(p for p, _ in found)
    threshold = max(0, max_pos - 16)
    ordered = []
    for pos, profession in sorted(found):
        if pos >= threshold and profession not in ordered:
            ordered.append(profession)
    return ordered or [""]


def add_effect(effects, effect_type, value, professions=None, duration=0.0):
    if abs(value) < 0.000001:
        return
    professions = professions or [""]
    for profession in professions:
        item = {
            "type": effect_type,
            "value": round(float(value), 6),
            "profession": profession,
            "duration": round(float(duration), 3),
        }
        key = (item["type"], item["value"], item["profession"], item["duration"])
        if key not in {(e["type"], e["value"], e["profession"], e["duration"]) for e in effects}:
            effects.append(item)


def parse_effects(text):
    raw = str(text or "").strip()
    if not raw:
        return []

    normalized = raw.replace("％", "%").replace("＋", "+").replace("－", "-")
    effects = []
    duration = 0.0
    m_duration = re.search(r"战斗开始\s*(\d+(?:\.\d+)?)\s*秒内", normalized)
    if m_duration:
        duration = float(m_duration.group(1))

    # Shared-value stat groups such as 攻击力、防御力和生命值+8%.
    compound_pattern = re.compile(
        r"((?:攻击力|防御力|生命值)(?:[、，,和及]+(?:攻击力|防御力|生命值))+)\s*([+-]\d+(?:\.\d+)?)\s*%"
    )
    for m in compound_pattern.finditer(normalized):
        value = percent_number(m.group(2))
        prefix = normalized[max(0, m.start() - 70):m.start()]
        enemy_pos = max(prefix.rfind("敌方"), prefix.rfind("敌人"))
        friendly_pos = max(
            prefix.rfind("我方"), prefix.rfind("干员"),
            prefix.rfind("近战"), prefix.rfind("远程")
        )
        is_enemy = enemy_pos >= 0 and enemy_pos > friendly_pos
        professions = [""] if is_enemy else nearest_professions(normalized, m.start())
        stats = m.group(1)
        if "攻击力" in stats:
            add_effect(effects, "IncomingDamagePercent" if is_enemy else "AllDamagePercent", value, professions, duration)
        if "防御力" in stats:
            add_effect(effects, "PhysicalDamagePercent" if is_enemy else "IncomingDamagePercent", -value, professions, duration)
        if "生命值" in stats:
            add_effect(effects, "EnemyMaxHealthPercent" if is_enemy else "MaxHealthPercent", value, professions, duration)

    # Explicit typed outgoing-damage modifiers.
    for effect_type, token in [
        ("PhysicalDamagePercent", "物理伤害"),
        ("ArtsDamagePercent", "法术伤害"),
        ("TrueDamagePercent", "真实伤害"),
    ]:
        for m in re.finditer(re.escape(token) + r"\s*([+-]\d+(?:\.\d+)?)\s*%", normalized):
            add_effect(effects, effect_type, percent_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    # Generic attack. Enemy attack reductions become incoming-damage modifiers.
    for m in re.finditer(r"攻击力\s*([+-]\d+(?:\.\d+)?)\s*%", normalized):
        value = percent_number(m.group(1))
        prefix = normalized[max(0, m.start() - 28):m.start()]
        if "敌方" in prefix or "敌人" in prefix:
            add_effect(effects, "IncomingDamagePercent", value, [""], duration)
        else:
            add_effect(effects, "AllDamagePercent", value, nearest_professions(normalized, m.start()), duration)

    # Defense is represented as incoming-damage scaling in the ACT combat model.
    for m in re.finditer(r"防御力\s*([+-]\d+(?:\.\d+)?)\s*%", normalized):
        defense = percent_number(m.group(1))
        prefix = normalized[max(0, m.start() - 28):m.start()]
        if "敌方" in prefix or "敌人" in prefix:
            # ACT currently has no target Defense stat in DamageSystem. Represent defense reduction
            # as the equivalent physical-damage modifier so the approved relic remains functional.
            add_effect(effects, "PhysicalDamagePercent", -defense, [""], duration)
            continue
        add_effect(effects, "IncomingDamagePercent", -defense, nearest_professions(normalized, m.start()), duration)

    # Player max health / enemy max health.
    for m in re.finditer(r"生命值?\s*([+-]\d+(?:\.\d+)?)\s*%", normalized):
        value = percent_number(m.group(1))
        prefix = normalized[max(0, m.start() - 32):m.start()]
        if "敌方" in prefix or "敌人" in prefix:
            add_effect(effects, "EnemyMaxHealthPercent", value, [""], duration)
        else:
            add_effect(effects, "MaxHealthPercent", value, nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"攻击速度\s*([+-]\d+(?:\.\d+)?)\s*%?", normalized):
        value = signed_number(m.group(1)) / 100.0
        add_effect(effects, "AttackSpeedPercent", value, nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"初始技力\s*([+-]\d+(?:\.\d+)?)", normalized):
        add_effect(effects, "InitialSkillPoints", signed_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:自然回复技能的)?(?:自然)?技力恢复(?:速度)?\s*([+-]\d+(?:\.\d+)?)\s*/\s*(?:秒|s)", normalized, re.IGNORECASE):
        add_effect(effects, "SkillPointRecoveryPerSecond", signed_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:所有)?技能每\s*(\d+(?:\.\d+)?)\s*秒回复\s*(\d+(?:\.\d+)?)\s*点?技力", normalized):
        seconds = max(0.01, signed_number(m.group(1)))
        amount = signed_number(m.group(2))
        add_effect(effects, "SkillPointRecoveryPerSecond", amount / seconds, nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"技力恢复(?:速度)?\s*([+-]\d+(?:\.\d+)?)\s*%", normalized):
        add_effect(effects, "SkillPointRecoveryPercent", percent_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:普攻|普通攻击)[^，。；;]{0,18}?技力\s*\+\s*(\d+(?:\.\d+)?)", normalized):
        add_effect(effects, "SkillPointOnBasicHit", signed_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:施放|释放)技能[^，。；;]{0,18}?技力\s*\+\s*(\d+(?:\.\d+)?)", normalized):
        add_effect(effects, "SkillPointOnSkillCast", signed_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:普攻|普通攻击)(?:命中)?[^，。；;]{0,20}?缩短(?:技能)?冷却\s*(\d+(?:\.\d+)?)\s*秒", normalized):
        add_effect(effects, "CooldownOnBasicHitSeconds", signed_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:施放|释放)技能[^，。；;]{0,20}?缩短(?:技能)?冷却\s*(\d+(?:\.\d+)?)\s*秒", normalized):
        add_effect(effects, "CooldownOnSkillCastSeconds", signed_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:施放|释放)技能[^，。；;]{0,20}?恢复\s*(\d+(?:\.\d+)?)\s*%\s*生命", normalized):
        add_effect(effects, "HealOnSkillCastFraction", percent_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"(?:造成的)?(?:总)?伤害\s*([+-]\d+(?:\.\d+)?)\s*%", normalized):
        add_effect(effects, "AllDamagePercent", percent_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    for m in re.finditer(r"立即获得源石锭\s*\+\s*(\d+(?:\.\d+)?)", normalized):
        add_effect(effects, "IngotOnAcquire", signed_number(m.group(1)), [""], duration)

    # Existing extension points.
    for m in re.finditer(r"召唤物[^，。；;]{0,12}?攻击力\s*([+-]\d+(?:\.\d+)?)\s*%", normalized):
        add_effect(effects, "SummonDamagePercent", percent_number(m.group(1)), nearest_professions(normalized, m.start()), duration)

    return effects


def resource_path_from_file(path):
    normalized = os.path.abspath(path).replace("\\", "/")
    marker = "/Resources/"
    idx = normalized.find(marker)
    if idx < 0:
        return ""
    rel = normalized[idx + len(marker):]
    rel = os.path.splitext(rel)[0]
    return rel


def main():
    if not os.path.isfile(XLSX_PATH):
        raise RuntimeError("Curated workbook not found: " + XLSX_PATH)

    os.makedirs(os.path.dirname(OUTPUT_JSON), exist_ok=True)
    os.makedirs(ICON_OUTPUT_DIR, exist_ok=True)
    os.makedirs(os.path.dirname(REPORT_PATH), exist_ok=True)

    log("[RogueRelicRuntime] Reading curated workbook...")
    with zipfile.ZipFile(XLSX_PATH, "r") as z:
        sheet_path = workbook_first_sheet_path(z)
        sheet_root, rows = read_sheet_rows(z, sheet_path)
        embedded_images = read_embedded_images(z, sheet_path, sheet_root)

        headers = rows.get(1, {})
        columns = {key: find_column(headers, aliases) for key, aliases in HEADER_ALIASES.items()}
        if not columns["name"]:
            raise RuntimeError("Column 名称/name not found in curated workbook")

        log("[RogueRelicRuntime] Header mapping: " + json.dumps(columns, ensure_ascii=False))

        items = []
        unsupported = []
        skipped = []
        used_ids = set()

        for row_num in sorted(rows):
            if row_num <= 1:
                continue
            row = rows[row_num]
            name = get_value(row, columns["name"])
            if not name:
                continue
            if columns["enabled"] and not enabled_value(get_value(row, columns["enabled"])):
                skipped.append({"row": row_num, "name": name, "reason": "disabled"})
                continue

            kind_text = get_value(row, columns["kind"])
            effect_text = get_value(row, columns["effect"])
            description = get_value(row, columns["description"])
            rarity_text = get_value(row, columns["rarity"])
            value_text = get_value(row, columns["value"])
            grid_text = get_value(row, columns["grid"])
            source_id = get_value(row, columns["id"])
            order_id = safe_int(get_value(row, columns["order"]), 0)

            commodity = kind_text == "收集品" or kind_text.casefold() in {"commodity", "salvage"}
            if not kind_text:
                commodity = not bool(effect_text)

            item_id = sanitize_id(source_id or (f"is1_{order_id:03d}" if order_id > 0 else name))
            original_id = item_id
            suffix = 2
            while item_id in used_ids:
                item_id = f"{original_id}_{suffix}"
                suffix += 1
            used_ids.add(item_id)

            combat_rarity, salvage_rarity = map_rarity(rarity_text)
            width, height = parse_grid(grid_text)
            collection_value = max(0, safe_int(value_text, 0))
            effects = [] if commodity else parse_effects(effect_text)

            icon_resource = ""
            embedded = embedded_images.get(row_num)
            if embedded is not None:
                member, data = embedded
                ext = os.path.splitext(member)[1].lower()
                if ext not in {".png", ".jpg", ".jpeg"}:
                    ext = ".png"
                icon_path = os.path.join(ICON_OUTPUT_DIR, item_id + ext)
                with open(icon_path, "wb") as f:
                    f.write(data)
                icon_resource = resource_path_from_file(icon_path)
            elif order_id > 0:
                fallback = os.path.join(
                    PROJECT_ROOT,
                    "Assets", "_Game", "Resources", "RogueRelics", "Icons",
                    "legacy_roguelike", f"is1_order_{order_id:03d}.png"
                )
                if os.path.isfile(fallback):
                    icon_resource = resource_path_from_file(fallback)

            if not commodity and effect_text and not effects:
                unsupported.append({"row": row_num, "name": name, "effect": effect_text})

            item = {
                "id": item_id,
                "name": name,
                "description": description,
                "rawEffect": effect_text,
                "profession": "Unspecified",
                "rarity": combat_rarity,
                "kind": "Commodity" if commodity else "Collectible",
                "salvageRarity": salvage_rarity,
                "icon": icon_resource,
                "value": 0.0,
                "stacks": 99 if commodity else 1,
                "collectionValue": collection_value,
                "w": width,
                "h": height,
                "effects": effects,
            }
            items.append(item)

    document = {"items": items}
    with open(OUTPUT_JSON, "w", encoding="utf-8") as f:
        json.dump(document, f, ensure_ascii=False, indent=2)

    report = {
        "source_xlsx": XLSX_PATH,
        "generated_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        "output_json": OUTPUT_JSON,
        "icon_output_dir": ICON_OUTPUT_DIR,
        "rows_imported": len(items),
        "collectibles": sum(1 for x in items if x["kind"] == "Collectible"),
        "commodities": sum(1 for x in items if x["kind"] == "Commodity"),
        "embedded_icons_found": len(embedded_images),
        "runtime_icons_assigned": sum(1 for x in items if x["icon"]),
        "unsupported_effect_count": len(unsupported),
        "unsupported_effects": unsupported,
        "skipped": skipped,
    }
    with open(REPORT_PATH, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)

    log("[RogueRelicRuntime] Imported: " + str(len(items)))
    log("[RogueRelicRuntime] Collectibles: " + str(report["collectibles"]) +
        ", commodities: " + str(report["commodities"]))
    log("[RogueRelicRuntime] Icons assigned: " + str(report["runtime_icons_assigned"]))
    log("[RogueRelicRuntime] Unsupported combat effects: " + str(len(unsupported)))
    if unsupported:
        for item in unsupported[:20]:
            log("  UNPARSED: " + item["name"] + " -> " + item["effect"])
    log("[RogueRelicRuntime] Output: " + OUTPUT_JSON)
    log("[RogueRelicRuntime] Report: " + REPORT_PATH)


if __name__ == "__main__":
    main()
