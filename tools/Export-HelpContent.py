"""Export mock/Help.dc.html data and white mask icons without executing its script.

Usage: python -m pip install resvg-py==0.5.0; python tools/Export-HelpContent.py
The committed PNGs are direct rasterizations of the supplied SVG paths.
"""
import json
import re
from pathlib import Path

import resvg_py

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / "mock/Help.dc.html").read_text(encoding="utf-8")


def js_data(name):
    text = re.search(r"const " + name + r" = ([\s\S]*?);", source).group(1)
    text = re.sub(r"I\.(\w+)", lambda m: "'help_" + m[1] + "'", text)
    text = re.sub(r"color: ([GORB])", lambda m: "color: '" + {
        "G": "green", "O": "orange", "R": "red", "B": "blue"
    }[m[1]] + "'", text)
    text = re.sub(r"'([^']*)'", lambda m: json.dumps(m[1], ensure_ascii=False), text)
    text = re.sub(r"([{,]\s*)(\w+):", r'\1"\2":', text)
    return json.loads(text)


sections = js_data("SECTIONS")
chips = js_data("QUALITY_CHIPS")
quality_ids = ("normal", "good", "rare", "legend", "myth", "transcend", "ultimate")
content = {
    "title": "遊び方", "backText": "戻る", "previousText": "‹ 前へ",
    "nextText": "次へ ›", "doneText": "わかった!",
    "sections": sections,
    "qualityChips": [{"quality": quality, "locked": chip.get("locked", False)}
                     for quality, chip in zip(quality_ids, chips)]
}
destination = ROOT / "Assets/Imoni/Config/HelpContent.json"
destination.write_text(json.dumps(content, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

icons = js_data("I")
icon_dir = ROOT / "Assets/Imoni/Art/Sprites/Icons"
for key, path in icons.items():
    svg = ('<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" '
           'viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.1" '
           'stroke-linecap="round" stroke-linejoin="round"><path d="' + path + '"/></svg>')
    (icon_dir / ("help_" + key + ".png")).write_bytes(resvg_py.svg_to_bytes(svg_string=svg))
question = ('<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" '
            'viewBox="0 0 24 24" fill="none" stroke="white" stroke-width="2.4" '
            'stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"/>'
            '<path d="M9.5 9.5a2.5 2.5 0 0 1 5 0c0 1.7-2.5 2-2.5 4M12 17v.5"/></svg>')
(icon_dir / "help_question.png").write_bytes(resvg_py.svg_to_bytes(svg_string=question))
print(f"Exported {len(sections)} sections, {sum(len(s['steps']) for s in sections)} steps and {len(icons)+1} icons")
