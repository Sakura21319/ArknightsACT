import json, os, sys, collections

src = sys.argv[1]
out = sys.argv[2]

with open(src, "r", encoding="utf-8-sig") as f:
    data = json.load(f)

interesting_tokens = (
    "relic", "collect", "item", "treasure", "artifact", "rogue",
    "rarity", "icon", "price", "unlock", "description", "desc", "usage", "effect"
)

def summarize_value(v):
    if isinstance(v, dict):
        return {"type":"dict","count":len(v),"keys":list(v.keys())[:40]}
    if isinstance(v, list):
        sample = None
        for x in v:
            if x is not None:
                sample = x
                break
        s = {"type":"list","count":len(v)}
        if isinstance(sample, dict):
            s["sample_keys"] = list(sample.keys())[:40]
        elif sample is not None:
            s["sample_type"] = type(sample).__name__
            s["sample"] = str(sample)[:200]
        return s
    return {"type":type(v).__name__,"sample":str(v)[:300]}

sections = []
candidates = []
seen_section = set()

def walk(v, path="$", depth=0):
    if depth > 10:
        return
    if isinstance(v, dict):
        keys = list(v.keys())
        key_lower = [str(k).lower() for k in keys]
        joined = " ".join(key_lower)
        if any(t in joined or t in path.lower() for t in interesting_tokens):
            sig = path
            if sig not in seen_section:
                seen_section.add(sig)
                sections.append({
                    "path": path,
                    "summary": summarize_value(v),
                })
        score = 0
        matched = []
        for k in keys:
            lk = str(k).lower()
            for t in ("name","description","desc","usage","effect","rarity","price","icon","unlock","id"):
                if t in lk:
                    score += 1
                    matched.append(k)
                    break
        if score >= 3:
            sample = {}
            for k in keys[:60]:
                val = v[k]
                if isinstance(val, (str,int,float,bool)) or val is None:
                    sample[k] = val
                elif isinstance(val, list):
                    sample[k] = f"<list:{len(val)}>"
                elif isinstance(val, dict):
                    sample[k] = f"<dict:{len(val)}>"
            candidates.append({
                "path": path,
                "score": score,
                "matched_keys": matched,
                "sample": sample,
            })
        for k, child in v.items():
            if isinstance(child, (dict,list)):
                walk(child, f"{path}.{k}", depth+1)
    elif isinstance(v, list):
        for i, child in enumerate(v[:300]):
            if isinstance(child, (dict,list)):
                walk(child, f"{path}[{i}]", depth+1)

walk(data)
candidates.sort(key=lambda x:(-x["score"], x["path"]))
sections = sections[:600]
candidates = candidates[:600]

result = {
    "source": src,
    "root": summarize_value(data),
    "sections": sections,
    "candidates": candidates,
}

os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)

print(json.dumps({
    "ok": True,
    "root": result["root"],
    "sections": len(sections),
    "candidates": len(candidates),
    "out": out,
}, ensure_ascii=False))
