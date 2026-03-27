
from pathlib import Path
import csv
import json
import sys

root = Path(__file__).resolve().parents[1]
required_root = [
    root / "README.md",
    root / "00_INPUTS" / "extracted-notes.md",
    root / "01_ANALYSIS" / "CURRENT_STATE_SUMMARY.md",
    root / "02_REQUIREMENTS" / "IMPROVED_REQUIREMENTS.md",
    root / "03_ARCHITECTURE" / "TARGET_ARCHITECTURE.md",
    root / "04_PLAN" / "IMPLEMENTATION_SEQUENCE.md",
    root / "05_TRACEABILITY" / "notes_catalog.csv",
    root / "05_TRACEABILITY" / "traceability_matrix.csv",
    root / "06_SHARED_PROMPTS" / "MASTER_CODEX_PROMPT.md",
    root / "08_QA" / "QA_INSPECTOR_REPORT.md",
]
required_item_files = [
    "README.md",
    "SPECIFICATION.md",
    "FILE_REFERENCES.md",
    "IMPLEMENTATION_PROMPT.md",
    "VALIDATION_PROMPT.md",
    "ACCEPTANCE_CRITERIA.md",
    "CHECKLIST.md",
    "SCREENSHOT_REQUIREMENTS.md",
]

errors = []

for path in required_root:
    if not path.exists():
        errors.append(f"Missing root file: {path.relative_to(root)}")

manifest_path = root / "05_TRACEABILITY" / "bundle_manifest.json"
if not manifest_path.exists():
    errors.append("Missing bundle manifest.")
    manifest = {}
else:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))

items = manifest.get("items", [])
for item in items:
    folder = root / item["folder"]
    if not folder.exists():
        errors.append(f"Missing item folder: {item['folder']}")
        continue
    for file_name in required_item_files:
        if not (folder / file_name).exists():
            errors.append(f"Missing item file: {item['folder']}/{file_name}")

notes_catalog = root / "05_TRACEABILITY" / "notes_catalog.csv"
traceability = root / "05_TRACEABILITY" / "traceability_matrix.csv"

note_ids = set()
mapped_ids = set()

if notes_catalog.exists():
    with notes_catalog.open("r", encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            note_ids.add(row["note_id"])

if traceability.exists():
    with traceability.open("r", encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            mapped_ids.add(row["note_id"])

if note_ids != mapped_ids:
    missing = sorted(note_ids - mapped_ids)
    extra = sorted(mapped_ids - note_ids)
    if missing:
        errors.append(f"Traceability missing note IDs: {', '.join(missing)}")
    if extra:
        errors.append(f"Traceability has unexpected note IDs: {', '.join(extra)}")

summary = {
    "item_count": len(items),
    "note_count": len(note_ids),
    "mapped_note_count": len(mapped_ids),
    "errors": errors,
    "passed": not errors,
}

print(json.dumps(summary, indent=2))
sys.exit(0 if not errors else 1)
