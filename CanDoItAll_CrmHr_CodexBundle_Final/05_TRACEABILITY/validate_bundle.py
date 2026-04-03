from pathlib import Path
import csv
import json
import sys

root = Path(__file__).resolve().parents[1]
required_root = [
    root / "README.md",
    root / "00_INPUTS" / "ORIGINAL_USER_REQUEST.md",
    root / "01_ANALYSIS" / "CURRENT_STATE_SUMMARY.md",
    root / "02_REQUIREMENTS" / "ENTERPRISE_USER_STORY_CATALOG.md",
    root / "03_ARCHITECTURE" / "TARGET_ARCHITECTURE.md",
    root / "04_PLAN" / "IMPLEMENTATION_SEQUENCE.md",
    root / "05_TRACEABILITY" / "user_story_catalog.csv",
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
    "ASCII_LAYOUTS.md",
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

catalog_path = root / "05_TRACEABILITY" / "user_story_catalog.csv"
traceability_path = root / "05_TRACEABILITY" / "traceability_matrix.csv"

story_ids = set()
mapped_story_ids = set()

if catalog_path.exists():
    with catalog_path.open("r", encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            story_ids.add(row["user_story_id"])

if traceability_path.exists():
    with traceability_path.open("r", encoding="utf-8", newline="") as f:
        for row in csv.DictReader(f):
            mapped_story_ids.add(row["user_story_id"])

if story_ids != mapped_story_ids:
    missing = sorted(story_ids - mapped_story_ids)
    extra = sorted(mapped_story_ids - story_ids)
    if missing:
        errors.append(f"Traceability missing story IDs: {', '.join(missing)}")
    if extra:
        errors.append(f"Traceability has unexpected story IDs: {', '.join(extra)}")

summary = {
    "item_count": len(items),
    "user_story_count": len(story_ids),
    "mapped_user_story_count": len(mapped_story_ids),
    "errors": errors,
    "passed": not errors,
}

print(json.dumps(summary, indent=2))
sys.exit(0 if not errors else 1)
