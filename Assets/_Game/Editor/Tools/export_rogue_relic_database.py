import base64
import csv
import gzip
import hashlib
import html
import json
import os
import re
import sys
import time
import unicodedata
import urllib.parse
import urllib.request
import zipfile
from collections import defaultdict
from concurrent.futures import ThreadPoolExecutor, as_completed

TOPIC_PATH = sys.argv[1]
OLD_PATH = sys.argv[2] if len(sys.argv) > 2 and sys.argv[2] != "-" else None
OUT_DIR = sys.argv[3]
ICON_DIR = sys.argv[4]

os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(ICON_DIR, exist_ok=True)

MASTER_CSV = os.path.join(OUT_DIR, "RogueRelicDatabase.csv")
ALL_CSV = os.path.join(OUT_DIR, "RogueRelicDatabase_all_versions.csv")
DUP_CSV = os.path.join(OUT_DIR, "RogueRelicDatabase_duplicates.csv")
JSON_PATH = os.path.join(OUT_DIR, "RogueRelicDatabase.json")
STATS_PATH = os.path.join(OUT_DIR, "RogueRelicDatabase.stats.json")
HTML_PATH = os.path.join(OUT_DIR, "RogueRelicDatabase.html")
XLSX_PATH = os.path.join(OUT_DIR, "RogueRelicDatabase.xlsx")
README_PATH = os.path.join(OUT_DIR, "README.md")
COMPACT_B64_PATH = os.path.join(OUT_DIR, "RogueRelicDatabase.compact.json.gz.b64")
EXCEL_CSV_PATH = os.path.join(OUT_DIR, "RogueRelicDatabase_Excel.csv")

PRTS_ASSET_ROOT = "https://torappu.prts.wiki/assets/roguelike_topic_itempic/"
PRTS_API = "https://prts.wiki/api.php"

THEME_FILE_NAMES = {
    "IS2": "傀影与猩红孤钻",
    "IS3": "水月与深蓝之树",
    "IS4": "探索者的银凇止境",
    "IS5": "萨卡兹的无终奇语",
    "IS6": "岁的界园志异",
    "IS7": "沉沦者的黑流树海",
}

EDIT_FIELDS = ["game_rarity", "game_value", "game_effect", "enabled", "notes"]

def load_json(path):
    if not path or not os.path.isfile(path):
        return None
    with open(path, "r", encoding="utf-8-sig") as f:
        return json.load(f)

def scalar(obj, *keys):
    if not isinstance(obj, dict):
        return ""
    for k in keys:
        v = obj.get(k)
        if isinstance(v, (str, int, float, bool)) and v is not None:
            return v
    return ""

def compact(v):
    if v is None:
        return ""
    if isinstance(v, (str, int, float, bool)):
        return str(v)
    return json.dumps(v, ensure_ascii=False, separators=(",", ":"))

def clean_markup(text):
    if text is None:
        return ""
    s = str(text)
    s = re.sub(r"<@[^>]+>", "", s)
    s = s.replace("</>", "")
    s = s.replace("\\n", "\n")
    return s.strip()

def is_number(label):
    m = re.search(r"(\d+)", str(label or ""))
    return int(m.group(1)) if m else 999

def order_number(value):
    try:
        return int(str(value).strip())
    except Exception:
        return 999999

def normalize_name(name):
    s = unicodedata.normalize("NFKC", str(name or "")).strip()
    s = re.sub(r"\s+", "", s)
    s = s.replace("“", '"').replace("”", '"').replace("‘", "'").replace("’", "'")
    return s.casefold()

def find_archive_map(detail):
    cur = detail.get("archiveComp", {})
    if not isinstance(cur, dict):
        return {}

    candidates = []
    stack = [cur]
    while stack:
        node = stack.pop()
        if isinstance(node, dict):
            vals = list(node.values())
            if vals and all(isinstance(v, dict) for v in vals):
                score = sum(1 for v in vals if "relicId" in v and "orderId" in v)
                if score:
                    candidates.append((score, len(vals), node))
            for child in vals:
                if isinstance(child, (dict, list)):
                    stack.append(child)
        elif isinstance(node, list):
            for child in node:
                if isinstance(child, (dict, list)):
                    stack.append(child)

    if not candidates:
        return {}
    candidates.sort(key=lambda x: (x[0], x[1]), reverse=True)
    return candidates[0][2]

def build_topic_rows(data):
    topics = data.get("topics", {}) if isinstance(data, dict) else {}
    details = data.get("details", {}) if isinstance(data, dict) else {}
    rows = []

    for topic_id, detail in details.items():
        if not isinstance(detail, dict):
            continue

        archive = find_archive_map(detail)
        if not archive:
            continue

        items = detail.get("items", {})
        if not isinstance(items, dict):
            items = {}
        mechanics = detail.get("relics", {})
        if not isinstance(mechanics, dict):
            mechanics = {}
        params = detail.get("relicParams", {})
        if not isinstance(params, dict):
            params = {}

        topic_meta = topics.get(topic_id, {}) if isinstance(topics, dict) else {}
        theme = scalar(topic_meta, "name") or topic_id

        m = re.search(r"(\d+)$", topic_id)
        rogue_no = int(m.group(1)) if m else 0
        # legacy IS1 is a separate old table. rogue_1 is Phantom (IS2), etc.
        is_label = f"IS{rogue_no + 1}" if rogue_no else topic_id

        for rid, arch in archive.items():
            if not isinstance(arch, dict):
                continue

            display = items.get(rid, {})
            if not isinstance(display, dict):
                display = {}
            mech = mechanics.get(rid, {})
            if not isinstance(mech, dict):
                mech = {}

            # archive is authoritative for what appears in the in-game collection.
            name = scalar(display, "name")
            icon_id = scalar(display, "iconId", "icon", "iconName")
            effect = clean_markup(scalar(
                display,
                "usage", "effect", "effectDesc", "effectDescription",
                "functionDesc", "funcDesc", "shortUsage"
            ))
            description = clean_markup(scalar(display, "description", "desc", "flavorDesc"))
            unlock = clean_markup(scalar(
                display,
                "unlockCondDesc", "unlockDesc", "unlockConditionDesc"
            ))
            rarity = scalar(display, "rarity", "level", "grade", "quality")
            value = scalar(display, "value", "price", "cost", "sellPrice")
            order_id = scalar(arch, "orderId")
            group_id = scalar(arch, "relicGroupId", "groupId")

            rows.append({
                "is": is_label,
                "theme": theme,
                "topic_id": topic_id,
                "order_id": str(order_id),
                "group_id": str(group_id),
                "category": "",
                "relic_id": rid,
                "name": str(name),
                "official_rarity": str(rarity),
                "official_value": str(value),
                "effect": effect,
                "description": description,
                "unlock": unlock,
                "icon_id": str(icon_id),
                "icon_url": (PRTS_ASSET_ROOT + str(icon_id) + ".png") if icon_id else "",
                "local_icon": "",
                "is_special": "是" if bool(arch.get("isSpRelic", False)) else "否",
                "enroll_id": compact(arch.get("enrollId")),
                "params_json": compact(params.get(rid)),
                "mechanics_json": compact(mech),
                "raw_json": compact(display),
                "game_rarity": "",
                "game_value": "",
                "game_effect": "",
                "enabled": "是",
                "notes": "",
            })

    return rows

def recursively_find_old_relic_dicts(data):
    found = []
    seen_ids = set()

    def walk(v, depth=0):
        if depth > 10:
            return
        if isinstance(v, dict):
            for k, item in v.items():
                if not isinstance(item, dict):
                    continue
                rid = scalar(item, "id", "relicId") or (k if isinstance(k, str) else "")
                name = scalar(item, "name")
                icon = scalar(item, "iconId", "icon")
                item_type = str(scalar(item, "type", "itemType")).upper()
                looks_id = isinstance(rid, str) and ("relic" in rid.lower() or "collect" in rid.lower())
                looks_relic = item_type == "RELIC" or looks_id
                if looks_relic and name and icon:
                    if rid not in seen_ids:
                        seen_ids.add(rid)
                        found.append((rid, item))
            for child in v.values():
                if isinstance(child, (dict, list)):
                    walk(child, depth + 1)
        elif isinstance(v, list):
            for child in v:
                if isinstance(child, (dict, list)):
                    walk(child, depth + 1)

    walk(data)
    return found

def build_old_rows(data):
    if not data:
        return []

    rows = []
    for idx, (rid, item) in enumerate(recursively_find_old_relic_dicts(data), start=1):
        order_id = scalar(item, "sortId", "orderId") or idx
        icon_id = scalar(item, "iconId", "icon", "iconName")
        rows.append({
            "is": "IS1",
            "theme": "刻俄柏的灰蕈迷境",
            "topic_id": "legacy_roguelike",
            "order_id": str(order_id),
            "group_id": str(scalar(item, "groupId", "relicGroupId")),
            "category": "",
            "relic_id": rid,
            "name": str(scalar(item, "name")),
            "official_rarity": str(scalar(item, "rarity", "level", "grade", "quality")),
            "official_value": str(scalar(item, "value", "price", "cost", "sellPrice")),
            "effect": clean_markup(scalar(
                item, "usage", "effect", "effectDesc", "effectDescription",
                "functionDesc", "funcDesc", "shortUsage"
            )),
            "description": clean_markup(scalar(item, "description", "desc", "flavorDesc")),
            "unlock": clean_markup(scalar(
                item, "unlockCondDesc", "unlockDesc", "unlockConditionDesc"
            )),
            "icon_id": str(icon_id),
            # IS1 PRTS images use old MediaWiki filenames, resolved later.
            "icon_url": "",
            "local_icon": "",
            "is_special": "是" if bool(item.get("isSpRelic", False)) else "否",
            "enroll_id": "",
            "params_json": "",
            "mechanics_json": "",
            "raw_json": compact(item),
            "game_rarity": "",
            "game_value": "",
            "game_effect": "",
            "enabled": "是",
            "notes": "",
        })
    return rows

def load_existing_edits(path):
    edits = {}
    if not os.path.isfile(path):
        return edits
    try:
        with open(path, "r", encoding="utf-8-sig", newline="") as f:
            for row in csv.DictReader(f):
                key = row.get("canonical_key") or normalize_name(row.get("name"))
                if not key:
                    continue
                edits[key] = {field: row.get(field, "") for field in EDIT_FIELDS}
    except Exception:
        pass
    return edits

def enrich_duplicate_metadata(rows):
    groups = defaultdict(list)
    for row in rows:
        key = normalize_name(row.get("name"))
        if not key:
            key = "id:" + str(row.get("relic_id") or "")
        row["canonical_key"] = key
        groups[key].append(row)

    duplicate_groups = {}
    for key, members in groups.items():
        versions = sorted({m["is"] for m in members}, key=is_number)
        effects = sorted({m["effect"] for m in members if m["effect"]})
        descriptions = sorted({m["description"] for m in members if m["description"]})
        rarities = sorted({m["official_rarity"] for m in members if m["official_rarity"]})
        variant_changed = len(effects) > 1 or len(descriptions) > 1 or len(rarities) > 1

        meta = {
            "duplicate_count": len(members),
            "appears_in": " / ".join(versions),
            "variant_changed": "是" if variant_changed else "否",
            "effect_variants": " || ".join(
                f'{m["is"]}: {m["effect"]}' for m in members if m["effect"]
            ),
            "source_ids": " | ".join(f'{m["is"]}:{m["relic_id"]}' for m in members),
        }
        for m in members:
            m.update(meta)
        if len(members) > 1:
            duplicate_groups[key] = members

    return groups, duplicate_groups

def choose_canonical(members):
    # User-facing master data is strictly latest-version wins.
    # Do not back-fill description/effect/icon from older IS versions: one relic should have
    # one current description and one current icon in the editable database.
    def score(row):
        completeness = sum(bool(row.get(k)) for k in (
            "name", "effect", "description", "official_rarity", "icon_id", "unlock"
        ))
        return (is_number(row.get("is")), completeness, order_number(row.get("order_id")))

    return dict(max(members, key=score))

def build_unique_rows(groups, prior_edits):
    unique = []
    for key, members in groups.items():
        row = choose_canonical(members)
        edits = prior_edits.get(key)
        if edits:
            for field in EDIT_FIELDS:
                if edits.get(field, "") != "":
                    row[field] = edits[field]
        unique.append(row)

    unique.sort(key=lambda r: (
        is_number(r.get("is")),
        order_number(r.get("order_id")),
        r.get("name", ""),
    ))
    return unique

def resolve_mediawiki_file_url(filename, timeout=20):
    params = urllib.parse.urlencode({
        "action": "query",
        "format": "json",
        "prop": "imageinfo",
        "iiprop": "url",
        "titles": "File:" + filename,
    })
    req = urllib.request.Request(
        PRTS_API + "?" + params,
        headers={
            "User-Agent": "Mozilla/5.0 ArknightsACT relic DB",
            "Referer": "https://prts.wiki/",
        },
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        payload = json.loads(resp.read().decode("utf-8"))
    pages = payload.get("query", {}).get("pages", {})
    for page in pages.values():
        info = page.get("imageinfo")
        if info and isinstance(info, list):
            url = info[0].get("url")
            if url:
                return url
    return ""

def prts_filename_for_row(row):
    order = row.get("order_id", "")
    try:
        n = int(str(order))
    except Exception:
        return ""

    if row.get("is") == "IS1":
        return f"收藏品_{n}.png"

    theme = THEME_FILE_NAMES.get(row.get("is"))
    if not theme:
        return ""
    return f"收藏品_{theme}_{n:03d}.png"

def prts_redirect_file_url(filename):
    return "https://prts.wiki/index.php?title=Special:Redirect/file/" + urllib.parse.quote(filename, safe="")

def download_bytes(url, timeout=30, retries=4):
    last_error = None
    for attempt in range(retries):
        req = urllib.request.Request(
            url,
            headers={
                "User-Agent": "Mozilla/5.0 ArknightsACT relic DB local-cache/1.0",
                "Referer": "https://prts.wiki/",
                "Accept": "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8",
                "Cache-Control": "no-cache",
            },
        )
        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                data = resp.read()
            if data.startswith(b"\x89PNG\r\n\x1a\n"):
                return data
            last_error = RuntimeError("response is not PNG")
        except Exception as e:
            last_error = e
        if attempt + 1 < retries:
            time.sleep(0.45 + attempt * 0.65)
    raise last_error or RuntimeError("download failed")

def download_icon(row):
    icon_id = row.get("icon_id")
    topic_folder = re.sub(r"[^A-Za-z0-9_\-]", "_", row.get("topic_id") or row.get("is") or "unknown")
    folder = os.path.join(ICON_DIR, topic_folder)
    os.makedirs(folder, exist_ok=True)

    safe_name = icon_id or (row.get("relic_id") or "unknown")
    safe_name = re.sub(r"[^A-Za-z0-9_\-.]", "_", safe_name)
    target = os.path.join(folder, safe_name + ".png")
    row["local_icon"] = os.path.relpath(target, OUT_DIR).replace("\\", "/")

    if os.path.isfile(target) and os.path.getsize(target) >= 128:
        try:
            with open(target, "rb") as f:
                if f.read(8) == b"\x89PNG\r\n\x1a\n":
                    return True, "cached"
        except Exception:
            pass

    candidates = []
    if row.get("icon_url"):
        candidates.append(row["icon_url"])

    filename = prts_filename_for_row(row)
    if filename:
        redirect = prts_redirect_file_url(filename)
        if redirect not in candidates:
            candidates.append(redirect)
        # API resolution is fallback only. Excel no longer depends on any remote URL.
        try:
            resolved = resolve_mediawiki_file_url(filename)
            if resolved and resolved not in candidates:
                candidates.append(resolved)
        except Exception:
            pass

    last_error = "no PRTS image candidate"
    for url in candidates:
        try:
            data = download_bytes(url)
            with open(target, "wb") as f:
                f.write(data)
            row["icon_url"] = url
            return True, "downloaded"
        except Exception as e:
            last_error = str(e)

    row["local_icon"] = ""
    return False, last_error

def apply_icon_paths_to_unique(unique_rows, all_rows):
    # Unique rows are copies. Propagate icon result from the matching canonical source row.
    by_source = {(r["is"], r["relic_id"]): r for r in all_rows}
    for row in unique_rows:
        source = by_source.get((row.get("is"), row.get("relic_id")))
        if source:
            row["icon_url"] = source.get("icon_url", "")
            row["local_icon"] = source.get("local_icon", "")

def write_csv(path, rows, fields):
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)

def excel_image_url(row):
    filename = prts_filename_for_row(row)
    if filename:
        encoded = urllib.parse.quote(filename, safe="")
        return "https://prts.wiki/index.php?title=Special:Redirect/file/" + encoded
    return row.get("icon_url", "")

def master_columns():
    return [
        ("图标", "image"),
        ("名称", "name"),
        ("来源版本", "is"),
        ("主题", "theme"),
        ("图鉴编号", "order_id"),
        ("原始稀有度", "official_rarity"),
        ("原始价值", "official_value"),
        ("官方效果", "effect"),
        ("官方描述", "description"),
        ("解锁条件", "unlock"),
        ("游戏稀有度（可编辑）", "game_rarity"),
        ("游戏价值（可编辑）", "game_value"),
        ("游戏效果（可编辑）", "game_effect"),
        ("启用（可编辑）", "enabled"),
        ("备注（可编辑）", "notes"),
        ("内部ID", "relic_id"),
        ("图标URL", "image_url"),
    ]

def master_value(row, key):
    if key == "image_url":
        return excel_image_url(row)
    return row.get(key, "")

def write_excel_csv(path, rows):
    columns = master_columns()
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([title for title, _ in columns])
        for row in rows:
            values = []
            for _, key in columns:
                if key == "image":
                    values.append(row.get("local_icon", ""))
                else:
                    values.append(master_value(row, key))
            writer.writerow(values)

def _xml_text(value):
    text = "" if value is None else str(value)
    text = "".join(ch for ch in text if ch in "\t\n\r" or ord(ch) >= 32)
    return html.escape(text, quote=False)

def _xlsx_col_name(index):
    name = ""
    while index > 0:
        index, rem = divmod(index - 1, 26)
        name = chr(65 + rem) + name
    return name

def _xlsx_inline_cell(ref, value, style):
    return (
        f'<c r="{ref}" s="{style}" t="inlineStr"><is><t xml:space="preserve">'
        + _xml_text(value)
        + '</t></is></c>'
    )

def _xlsx_formula_cell(ref, formula, style):
    return f'<c r="{ref}" s="{style}"><f>{_xml_text(formula)}</f><v></v></c>'

def _local_icon_file(row):
    rel = str(row.get("local_icon", "") or "").strip()
    if not rel:
        return ""
    path = rel if os.path.isabs(rel) else os.path.normpath(os.path.join(OUT_DIR, rel))
    if not os.path.isfile(path) or os.path.getsize(path) < 128:
        return ""
    try:
        with open(path, "rb") as f:
            return path if f.read(8) == b"\x89PNG\r\n\x1a\n" else ""
    except Exception:
        return ""

def write_xlsx(unique_rows, all_rows, dup_rows, fields):
    # Images are embedded into the XLSX package from the project-local cache.
    # Opening the workbook never contacts PRTS and therefore cannot be rate-limited.
    columns = master_columns()
    editable_keys = {"game_rarity", "game_value", "game_effect", "enabled", "notes"}

    widths = [
        11, 24, 11, 24, 11, 15, 12, 42, 48, 38,
        20, 18, 44, 16, 34, 30, 58,
    ]

    image_entries = []
    sheet_rows = []
    header_cells = []
    for col_idx, (title, _) in enumerate(columns, start=1):
        ref = f"{_xlsx_col_name(col_idx)}1"
        header_cells.append(_xlsx_inline_cell(ref, title, 1))
    sheet_rows.append('<row r="1" ht="26" customHeight="1">' + "".join(header_cells) + "</row>")

    for row_idx, row in enumerate(unique_rows, start=2):
        cells = []
        for col_idx, (_, key) in enumerate(columns, start=1):
            ref = f"{_xlsx_col_name(col_idx)}{row_idx}"
            style = 3 if key in editable_keys else 2
            if key == "image":
                cells.append(_xlsx_inline_cell(ref, "", 2))
                local = _local_icon_file(row)
                if local:
                    image_entries.append((row_idx - 1, local, row.get("name", "")))
            else:
                cells.append(_xlsx_inline_cell(ref, master_value(row, key), style))
        sheet_rows.append(
            f'<row r="{row_idx}" ht="52" customHeight="1">' + "".join(cells) + "</row>"
        )

    last_row = len(unique_rows) + 1
    last_col = _xlsx_col_name(len(columns))
    cols_xml = "".join(
        f'<col min="{i}" max="{i}" width="{widths[i-1]}" customWidth="1"/>'
        for i in range(1, len(columns) + 1)
    )

    drawing_tag = '<drawing r:id="rId1"/>' if image_entries else ""
    worksheet_xml = f'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheetViews>
    <sheetView workbookViewId="0">
      <pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/>
      <selection pane="bottomLeft" activeCell="B2" sqref="B2"/>
    </sheetView>
  </sheetViews>
  <sheetFormatPr defaultRowHeight="18"/>
  <cols>{cols_xml}</cols>
  <sheetData>{"".join(sheet_rows)}</sheetData>
  <autoFilter ref="A1:{last_col}{last_row}"/>
  {drawing_tag}
  <pageMargins left="0.3" right="0.3" top="0.5" bottom="0.5" header="0.2" footer="0.2"/>
</worksheet>'''

    styles_xml = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <fonts count="2">
    <font><sz val="11"/><name val="Calibri"/><family val="2"/></font>
    <font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/><family val="2"/></font>
  </fonts>
  <fills count="4">
    <fill><patternFill patternType="none"/></fill>
    <fill><patternFill patternType="gray125"/></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FF1F4E5F"/><bgColor indexed="64"/></patternFill></fill>
    <fill><patternFill patternType="solid"><fgColor rgb="FFFFF2CC"/><bgColor indexed="64"/></patternFill></fill>
  </fills>
  <borders count="2">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style="thin"><color rgb="FFD9E2E5"/></left>
      <right style="thin"><color rgb="FFD9E2E5"/></right>
      <top style="thin"><color rgb="FFD9E2E5"/></top>
      <bottom style="thin"><color rgb="FFD9E2E5"/></bottom>
      <diagonal/>
    </border>
  </borders>
  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
  <cellXfs count="4">
    <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
    <xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyAlignment="1">
      <alignment horizontal="center" vertical="center" wrapText="1"/>
    </xf>
    <xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyAlignment="1">
      <alignment vertical="top" wrapText="1"/>
    </xf>
    <xf numFmtId="0" fontId="0" fillId="3" borderId="1" xfId="0" applyAlignment="1">
      <alignment vertical="top" wrapText="1"/>
    </xf>
  </cellXfs>
  <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>'''

    workbook_xml = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets><sheet name="藏品主表" sheetId="1" r:id="rId1"/></sheets>
</workbook>'''

    workbook_rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>'''

    root_rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>'''

    drawing_xml = ""
    drawing_rels = ""
    sheet_rels = ""
    if image_entries:
        anchors = []
        rel_items = []
        for idx, (row_zero, local, name) in enumerate(image_entries, start=1):
            rel_items.append(
                f'<Relationship Id="rId{idx}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/image{idx}.png"/>'
            )
            anchors.append(f'''<xdr:oneCellAnchor>
  <xdr:from><xdr:col>0</xdr:col><xdr:colOff>28575</xdr:colOff><xdr:row>{row_zero}</xdr:row><xdr:rowOff>28575</xdr:rowOff></xdr:from>
  <xdr:ext cx="571500" cy="571500"/>
  <xdr:pic>
    <xdr:nvPicPr><xdr:cNvPr id="{idx}" name="{_xml_text(name or ('icon_' + str(idx)))}"/><xdr:cNvPicPr/></xdr:nvPicPr>
    <xdr:blipFill><a:blip r:embed="rId{idx}"/><a:stretch><a:fillRect/></a:stretch></xdr:blipFill>
    <xdr:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="571500" cy="571500"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></xdr:spPr>
  </xdr:pic>
  <xdr:clientData/>
</xdr:oneCellAnchor>''')
        drawing_xml = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<xdr:wsDr xmlns:xdr="http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing"
 xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">''' + "".join(anchors) + "</xdr:wsDr>"
        drawing_rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">''' + "".join(rel_items) + "</Relationships>"
        sheet_rels = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing" Target="../drawings/drawing1.xml"/>
</Relationships>'''

    content_types = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="png" ContentType="image/png"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>''' + (
        '<Override PartName="/xl/drawings/drawing1.xml" ContentType="application/vnd.openxmlformats-officedocument.drawing+xml"/>'
        if image_entries else ""
    ) + "</Types>"

    def write_archive(target):
        with zipfile.ZipFile(target, "w", compression=zipfile.ZIP_DEFLATED) as z:
            z.writestr("[Content_Types].xml", content_types)
            z.writestr("_rels/.rels", root_rels)
            z.writestr("xl/workbook.xml", workbook_xml)
            z.writestr("xl/_rels/workbook.xml.rels", workbook_rels)
            z.writestr("xl/worksheets/sheet1.xml", worksheet_xml)
            z.writestr("xl/styles.xml", styles_xml)
            if image_entries:
                z.writestr("xl/worksheets/_rels/sheet1.xml.rels", sheet_rels)
                z.writestr("xl/drawings/drawing1.xml", drawing_xml)
                z.writestr("xl/drawings/_rels/drawing1.xml.rels", drawing_rels)
                for idx, (_, local, _) in enumerate(image_entries, start=1):
                    z.write(local, f"xl/media/image{idx}.png")

    actual_target = XLSX_PATH
    try:
        write_archive(actual_target)
    except PermissionError:
        stem, ext = os.path.splitext(XLSX_PATH)
        actual_target = stem + "_generated" + ext
        try:
            write_archive(actual_target)
        except PermissionError:
            actual_target = stem + "_generated_" + str(int(time.time())) + ext
            write_archive(actual_target)

    return True, "embedded " + str(len(image_entries)) + " local icons into " + actual_target

MASTER_FIELDS = [
    "canonical_key",
    "is", "theme", "topic_id", "order_id", "group_id", "category",
    "relic_id", "name", "official_rarity", "official_value",
    "effect", "description", "unlock",
    "icon_id", "icon_url", "local_icon",
    "is_special", "enroll_id",
    "game_rarity", "game_value", "game_effect", "enabled", "notes",
    "params_json", "mechanics_json", "raw_json",
]

DIAGNOSTIC_FIELDS = MASTER_FIELDS + [
    "duplicate_count", "appears_in", "variant_changed", "effect_variants", "source_ids"
]

topic = load_json(TOPIC_PATH) or {}
old = load_json(OLD_PATH) if OLD_PATH else None
prior_edits = load_existing_edits(MASTER_CSV)

all_rows = build_topic_rows(topic)
all_rows.extend(build_old_rows(old))
all_rows.sort(key=lambda r: (
    is_number(r.get("is")),
    order_number(r.get("order_id")),
    r.get("relic_id", ""),
))

groups, duplicate_groups = enrich_duplicate_metadata(all_rows)
unique_rows = build_unique_rows(groups, prior_edits)

# Download one image per occurrence; results are cached and later copied to unique rows by source.
icon_results = []
with ThreadPoolExecutor(max_workers=3) as pool:
    future_to_row = {
        pool.submit(download_icon, row): row
        for row in all_rows
        if row.get("name")
    }
    for future in as_completed(future_to_row):
        row = future_to_row[future]
        try:
            ok, message = future.result()
        except Exception as e:
            ok, message = False, str(e)
        icon_results.append((row.get("relic_id"), ok, message))

apply_icon_paths_to_unique(unique_rows, all_rows)

dup_rows = []
for key in sorted(duplicate_groups):
    members = duplicate_groups[key]
    for row in sorted(members, key=lambda r: (
        is_number(r.get("is")), order_number(r.get("order_id"))
    )):
        dup_rows.append(row)

write_csv(MASTER_CSV, unique_rows, MASTER_FIELDS)
write_csv(ALL_CSV, all_rows, DIAGNOSTIC_FIELDS)
write_csv(DUP_CSV, dup_rows, DIAGNOSTIC_FIELDS)
write_excel_csv(EXCEL_CSV_PATH, unique_rows)

with open(JSON_PATH, "w", encoding="utf-8") as f:
    json.dump(
        {
            "policy": "same-name relics use latest IS version only",
            "rows": unique_rows,
        },
        f,
        ensure_ascii=False,
        indent=2,
    )

stats = {
    "records_total": len(all_rows),
    "unique_names": len(unique_rows),
    "duplicate_name_groups": len(duplicate_groups),
    "duplicate_extra_records": sum(len(v) - 1 for v in duplicate_groups.values()),
    "variant_changed_groups": sum(
        1 for v in duplicate_groups.values() if v and v[0].get("variant_changed") == "是"
    ),
    "by_is": {},
    "by_topic": {},
    "missing_names": sum(1 for r in all_rows if not r.get("name")),
    "missing_effects": sum(1 for r in all_rows if not r.get("effect")),
    "missing_icons": sum(1 for r in all_rows if not r.get("local_icon")),
    "icons_ok": sum(1 for _, ok, _ in icon_results if ok),
    "icons_failed": sum(1 for _, ok, _ in icon_results if not ok),
    "legacy_is1_loaded": bool(old),
    "master_policy": "same-name relics use latest IS version only",
    "master_missing_icons": sum(1 for r in unique_rows if not r.get("local_icon")),
    "excel_icons_embedded_locally": True,
}
for r in all_rows:
    stats["by_is"][r["is"]] = stats["by_is"].get(r["is"], 0) + 1
    stats["by_topic"][r["topic_id"]] = stats["by_topic"].get(r["topic_id"], 0) + 1

xlsx_ok, xlsx_message = write_xlsx(unique_rows, all_rows, dup_rows, MASTER_FIELDS)
stats["xlsx_generated"] = xlsx_ok
stats["xlsx_message"] = xlsx_message

with open(STATS_PATH, "w", encoding="utf-8") as f:
    json.dump(stats, f, ensure_ascii=False, indent=2)

# Compact transport copy used to build the editable Excel outside the Unity editor.
# It keeps only the human-facing master fields, then gzip+base64 so FolderBridge can
# transfer the full table as bounded UTF-8 text without truncating long descriptions.
compact_fields = [
    "canonical_key", "is", "theme", "order_id", "category", "relic_id", "name",
    "official_rarity", "official_value", "effect", "description", "unlock",
    "icon_id", "icon_url", "local_icon",
    "game_rarity", "game_value", "game_effect", "enabled", "notes"
]
compact_rows = [
    {field: row.get(field, "") for field in compact_fields}
    for row in unique_rows
]
compact_bytes = json.dumps(
    {"fields": compact_fields, "rows": compact_rows, "stats": stats},
    ensure_ascii=False,
    separators=(",", ":")
).encode("utf-8")
with open(COMPACT_B64_PATH, "w", encoding="ascii") as f:
    f.write(base64.b64encode(gzip.compress(compact_bytes, compresslevel=9)).decode("ascii"))

with open(README_PATH, "w", encoding="utf-8") as f:
    f.write(
        "# 全集成战略藏品数据库\n\n"
        "主编辑文件：`RogueRelicDatabase.xlsx` 或 `RogueRelicDatabase.csv`。\n\n"
        "- `RogueRelicDatabase.xlsx`：主编辑表；同名藏品严格采用最新一期的描述、效果、稀有度和 PRTS 图标。\n"
        "- `RogueRelicDatabase.csv`：与主表相同的最新版本去重数据，后续游戏适配读取这张表。\n"
        "- `RogueRelicDatabase_all_versions.csv`：仅作为内部校验，保留每一期原始记录。\n"
        "- `RogueRelicDatabase_duplicates.csv`：仅作为内部校验，不参与游戏适配。\n"
        "- `RogueRelicDatabase.html`：带 PRTS 图片的浏览/编辑界面。\n"
        "- `RogueRelicDatabase_Excel.csv`：Excel 兼容备用表，第一列使用 IMAGE() 直接显示 PRTS 图标。\n"
        "- 图片缓存：`Assets/_Game/Resources/RogueRelics/Icons`。\n\n"
        "主表按标准化名称去重，并严格使用该藏品最新一期出现时的官方字段和图标。"
        "旧版本不再向主表回填描述、效果或图标。\n"
    )

def td(value, editable=False, cls=""):
    attr = ' contenteditable="true"' if editable else ""
    clsattr = f' class="{cls}"' if cls else ""
    return f"<td{attr}{clsattr}>{html.escape(str(value or ''))}</td>"

with open(HTML_PATH, "w", encoding="utf-8") as f:
    f.write("""<!doctype html><meta charset="utf-8">
<title>ArknightsACT 全肉鸽藏品编辑库</title>
<style>
body{font-family:Arial,'Microsoft YaHei',sans-serif;background:#11191d;color:#e8eeee;margin:20px}
.toolbar{position:sticky;top:0;background:#11191d;padding:10px 0;z-index:4}
button,input{font-size:14px;padding:7px 10px;margin-right:8px}
table{border-collapse:collapse;font-size:12px;width:max-content;min-width:100%}
th{position:sticky;top:58px;background:#244852;color:white;z-index:3}
td,th{border:1px solid #395158;padding:5px;vertical-align:top;max-width:360px}
tr:nth-child(even){background:#152126}.edit{background:#3d3518}.desc{white-space:pre-wrap}
img{width:64px;height:64px;object-fit:contain;background:#263237}
.small{color:#93a6ab;font-size:11px}
</style>
<div class="toolbar">
<b>ArknightsACT · 全肉鸽藏品主表（同名仅保留最新版本）</b>
<button onclick="exportCSV()">导出编辑后的 CSV</button>
<input id="q" placeholder="搜索名称/效果/描述" oninput="filterRows()">
<span class="small">黄色列可编辑；描述、效果、稀有度和图标均采用最新一期。</span>
</div>
<table id="db"><thead><tr>
<th>图</th><th>名称</th><th>来源版本</th><th>主题</th><th>编号</th><th>稀有度</th>
<th>官方效果</th><th>官方描述</th><th>解锁</th>
<th>游戏稀有度</th><th>游戏价值</th><th>游戏效果</th><th>启用</th><th>备注</th>
</tr></thead><tbody>
""")
    for r in unique_rows:
        f.write("<tr>")
        image_url = excel_image_url(r)
        f.write(
            f'<td><img src="{html.escape(image_url)}" loading="lazy"></td>'
            if image_url else "<td></td>"
        )
        f.write(td(r.get("name")))
        f.write(td(r.get("is")))
        f.write(td(r.get("theme")))
        f.write(td(r.get("order_id")))
        f.write(td(r.get("official_rarity")))
        f.write(td(r.get("effect"), cls="desc"))
        f.write(td(r.get("description"), cls="desc"))
        f.write(td(r.get("unlock"), cls="desc"))
        f.write(td(r.get("game_rarity"), True, "edit"))
        f.write(td(r.get("game_value"), True, "edit"))
        f.write(td(r.get("game_effect"), True, "edit desc"))
        f.write(td(r.get("enabled"), True, "edit"))
        f.write(td(r.get("notes"), True, "edit desc"))
        f.write("</tr>\n")

    f.write("""</tbody></table>
<script>
function filterRows(){
 const q=document.getElementById('q').value.toLowerCase();
 document.querySelectorAll('#db tbody tr').forEach(tr=>{
   tr.style.display=tr.innerText.toLowerCase().includes(q)?'':'none';
 });
}
function csvCell(v){return '"'+v.replaceAll('"','""')+'"'}
function exportCSV(){
 const headers=['名称','来源版本','主题','编号','稀有度','官方效果','官方描述','解锁','游戏稀有度','游戏价值','游戏效果','启用','备注'];
 const lines=[headers.map(csvCell).join(',')];
 document.querySelectorAll('#db tbody tr').forEach(tr=>{
   const c=tr.querySelectorAll('td');
   const vals=[c[1],c[2],c[3],c[4],c[5],c[6],c[7],c[8],c[9],c[10],c[11],c[12],c[13]].map(x=>x.innerText);
   lines.push(vals.map(csvCell).join(','));
 });
 const blob=new Blob(['\ufeff'+lines.join('\r\n')],{type:'text/csv;charset=utf-8'});
 const a=document.createElement('a');
 a.href=URL.createObjectURL(blob);
 a.download='RogueRelicDatabase_edited.csv';
 a.click();
 setTimeout(()=>URL.revokeObjectURL(a.href),1000);
}
</script>""")

print(json.dumps({
    "ok": True,
    "master_csv": MASTER_CSV,
    "all_csv": ALL_CSV,
    "duplicates_csv": DUP_CSV,
    "excel_csv": EXCEL_CSV_PATH,
    "xlsx": XLSX_PATH if xlsx_ok else None,
    "html": HTML_PATH,
    "stats": stats,
}, ensure_ascii=False))
