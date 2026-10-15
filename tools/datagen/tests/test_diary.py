from __future__ import annotations

import csv
import io

import pytest

from datagen.__main__ import DOCS, build_all
from datagen.diary import build_diary, localization_rows, render_strings_csv
from datagen.items import DataError

ITEMS = [{"id": "sample_brute", "category": "sample"}, {"id": "wood", "category": "material"}]


def _entry(**kw):
    base = {"id": "d1", "source": "Nota", "trigger": "match_start", "text": "Hola"}
    base.update(kw)
    return "```yaml\n" + "\n".join(f"{k}: {v}" for k, v in base.items()) + "\n```\n"


def test_real_lore_has_triggers_for_every_entry():
    outputs = build_all()
    diary = next(text for path, text in outputs.items() if path.name == "diary.json")
    assert '"trigger": "match_start"' in diary and '"trigger": "sample:sample_brute"' in diary


@pytest.mark.parametrize("trigger, message", [
    ("on_full_moon", "unknown trigger"),
    ("night_started:0", "unknown trigger"),
    ("building_kind:castle", "does not exist"),
    ("sample:wood", "not a sample"),
])
def test_invalid_triggers(trigger, message):
    with pytest.raises(DataError, match=message):
        build_diary(_entry(trigger=trigger), ITEMS)


def test_strings_csv_keeps_translations_from_docs(tmp_path):
    (tmp_path / "en.csv").write_text('key,text\n"item.wood.name","Wood"\n', encoding="utf-8")
    rows = localization_rows({"zombies": []}, [{"id": "wood", "name": "Madera"}, {"id": "nails", "name": "Clavos"}], [])
    text, missing = render_strings_csv(rows, tmp_path)
    table = list(csv.DictReader(io.StringIO(text)))
    assert table[0] == {"key": "item.wood.name", "es": "Madera", "en": "Wood"}
    assert table[1]["en"] == "" and missing == {"en": 1}


def test_real_strings_cover_items_zombies_and_diary():
    outputs = build_all()
    text = next(t for p, t in outputs.items() if p.name == "strings.csv")
    keys = {r["key"] for r in csv.DictReader(io.StringIO(text))}
    assert {"item.pistol.name", "zombie.brute.name", "diary.diary_001.text"} <= keys
    assert (DOCS / "traducciones" / "en.csv").exists()
