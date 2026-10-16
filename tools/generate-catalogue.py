"""Generate operators.json from the canonical Lua source fixture.

The generated file is committed; the xUnit integrity test re-parses the Lua fixture with an
independent regex implementation and compares it to this output 1:1.

Usage: python tools/generate-catalogue.py <lua-source> <out-json>
"""
import json
import re
import sys
from pathlib import Path


def parse(lua_text: str):
    op_re = re.compile(
        r'name\s*=\s*"([^"]+)"\s*,\s*side\s*=\s*"(ATK|DEF)"\s*,\s*guns\s*=\s*\{'
    )
    gun_re = re.compile(
        r'\{\s*name\s*=\s*"([^"]+)"\s*,\s*pull\s*=\s*(\d+)\s*,\s*rapidFire\s*=\s*(true|false)\s*\}'
    )
    ops = [(m.start(), m.group(1), m.group(2)) for m in op_re.finditer(lua_text)]
    guns = [(m.start(), m.group(1), m.group(2), m.group(3) == "true") for m in gun_re.finditer(lua_text)]
    operators = []
    for i, (pos, name, side) in enumerate(ops):
        op_guns = [g for g in guns if pos < g[0] and (i + 1 >= len(ops) or g[0] < ops[i + 1][0])]
        operators.append({
            "name": name,
            "side": side,
            "weapons": [
                {"name": g[1], "pull": int(g[2]), "rapidFire": g[3]} for g in op_guns
            ],
        })
    return operators


def main() -> int:
    src, out = Path(sys.argv[1]), Path(sys.argv[2])
    operators = parse(src.read_text(encoding="utf-8"))
    for i, op in enumerate(operators, 1):
        op["id"] = f"op-{i:03d}"
        for j, w in enumerate(op["weapons"], 1):
            w["id"] = f"{op['id']}-w{j}"
            # keep stable field order for readability
            w = {"id": w["id"], "name": w["name"], "pull": w["pull"], "rapidFire": w["rapidFire"]}
        # normalize weapon list order of keys
        op["weapons"] = [
            {"id": f"{op['id']}-w{j}", "name": w["name"], "pull": w["pull"], "rapidFire": w["rapidFire"]}
            for j, w in enumerate(op["weapons"], 1)
        ]
    out.write_text(
        json.dumps({"operators": operators}, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    atk = sum(1 for o in operators if o["side"] == "ATK")
    dfn = sum(1 for o in operators if o["side"] == "DEF")
    print(f"operators={len(operators)} ATK={atk} DEF={dfn} weapons={sum(len(o['weapons']) for o in operators)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
