from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
required = [
    "README.md",
    "COPY_PASTE_PROMPT_FOR_CODEX.md",
    "inputs/raw-request.md",
    "analysis/03-db-bottleneck-inventory.md",
    "requirements/02-canonicality-invariants.md",
    "plan/01-phase-plan.md",
    "traceability/input-coverage-matrix.md",
    "reviews/00-preparation-self-review.md",
]
missing = [item for item in required if not (root / item).exists()]
subbundles = list((root / "subbundles").glob("*/README.md"))
if len(subbundles) < 8:
    missing.append("at least 8 subbundle README files")
if missing:
    print("Bundle validation failed:")
    for item in missing:
        print(f" - {item}")
    sys.exit(1)
print("Bundle validation passed.")
