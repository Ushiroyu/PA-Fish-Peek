"""从插件内置数据生成工作台快照；不执行 C# 内容。"""
import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src" / "PAFishPeekPlugin"
OUTPUT = ROOT / "assistant" / "fishpeek" / "data" / "catalog.json"
STRING = r'"(?:[^"\\]|\\.)*"'


def text(name):
    return (SOURCE / name).read_text(encoding="utf-8-sig")


def build():
    levels = {json.loads(name): int(level) for name, level in re.findall(
        rf'\[({STRING})\]\s*=\s*(\d+)', text("FishLevelData.cs"))}
    contexts = {}
    for match in re.finditer(rf'new\(({STRING}),\s*([^()]+)\)', text("FishContextData.cs")):
        name = json.loads(match[1])
        values = [float(v.strip().removesuffix("f")) for v in match[2].split(",")]
        if len(values) != 16:
            raise ValueError(f"Unexpected fish fields: {name}")
        contexts[name] = dict(location=int(values[0]), season=int(values[1]), time=int(values[2]),
                              weather=int(values[3]), factors=values[4:8], weights=values[8:])
    rods = []
    pattern = r'new ExactRodProfile\(\s*(.*?)\s*new ExactRodFishEntry\[\]\s*\{(.*?)\}\s*\)'
    for match in re.finditer(pattern, text("BuiltInExactRodProfilesData.cs"), re.S):
        fields = re.findall(STRING + r'|-?\d+(?:\.\d+)?(?:UL|f)?', match[1])
        if len(fields) != 11:
            raise ValueError(f"Unexpected rod header: {fields}")
        entries = {json.loads(name): float(weight) for name, weight in re.findall(
            rf'new ExactRodFishEntry\(({STRING}),\s*([\d.]+)f\)', match[2])}
        rods.append(dict(uid=fields[0].removesuffix("UL"), id=json.loads(fields[1]), guid=json.loads(fields[2]),
                         fishing_type=int(fields[3]), accessory=int(fields[4]), rod_level=float(fields[5].removesuffix("f")),
                         tier=float(fields[6].removesuffix("f")), luck=float(fields[7].removesuffix("f")),
                         season=int(fields[8]), location=int(fields[9]), label=json.loads(fields[10]), fish=entries))
    categories = {}
    for mode in re.finditer(r'\[FishingModeContext\.(\w+)\](.*?)(?=\[FishingModeContext\.|\Z)', text("RodFishCategoryData.cs"), re.S):
        categories[mode[1].lower()] = {accessory: [json.loads(s) for s in re.findall(STRING, body)]
            for accessory, body in re.findall(r'\[([12])\]\s*=\s*new HashSet<string>.*?\{(.*?)\}', mode[2], re.S)}
    names = set(levels) | set(contexts)
    if not levels or len(rods) < 10 or set(categories) != {"route", "ice", "pond"}:
        raise ValueError("Incomplete source catalog")
    for rod in rods:
        missing = set(rod["fish"]) - names
        if missing:
            raise ValueError(f"Unmapped rod fish: {missing}")
    fish = []
    for name in sorted(names):
        if name not in levels or name not in contexts:
            raise ValueError(f"Incomplete fish: {name}")
        fish.append(dict(name=name, level=levels[name], **contexts[name]))
    return dict(schema_version=1, version="plugin-" + re.search(r'PluginVersion = "([^"]+)"', text("FishPeekPlugin.cs"))[1],
                source="src/PAFishPeekPlugin", fish=fish, rods=rods, categories=categories)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    catalog = build()
    if args.check:
        if not OUTPUT.exists() or json.loads(OUTPUT.read_text(encoding="utf-8")) != catalog:
            raise SystemExit("Catalog differs from plugin data; run scripts/export-catalog.py")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Catalog: {len(catalog['fish'])} fish, {len(catalog['rods'])} rods, {catalog['version']}")
