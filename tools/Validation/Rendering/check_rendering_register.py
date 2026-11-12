"""Require explicit review of changed rendering inputs; this is not a semantic analyzer."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


CLASSIFICATIONS = {"EXTRACTED_RENDERER", "NATIVE_HOST", "FRAMEWORK_HOST", "RETAINED_COMPATIBILITY", "NON_RENDERING"}
SIGNALS = re.compile(r"BuildRenderTree|RenderFragment|ComponentBase|DynamicComponent|OpenComponent(?:<|\()|OpenAsync<|OpenDialog|AddRazorComponents|AddAdditionalAssemblies|I(?:ConversationShellContributor|SettingsRendererSource)")
REQUIRED = ("identity", "type", "project", "caller", "native_owner", "visual_owner", "descendants", "assets", "scenario", "evidence", "rationale")


def source_inputs(root: Path) -> dict[str, dict]:
    result = subprocess.run(["git", "-C", str(root), "ls-files", "--cached", "--others", "--exclude-standard", "-z"], capture_output=True, check=True)
    names = sorted(set(result.stdout.decode("utf-8").split("\0")) - {""})
    texts = {}
    for name in names:
        path = root / name
        if not (name.startswith("src/") or name in {"Directory.Build.props", "Directory.Build.targets"}):
            continue
        if path.suffix not in {".razor", ".cs", ".csproj", ".props", ".targets", ".css", ".js"}:
            continue
        if not path.exists():
            continue  # Git can still list an intentionally deleted tracked path.
        if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
            raise ValueError(f"Rendering input needs explicit external-source review: {name}")
        texts[name] = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n")
    component_names = {Path(name).stem for name in texts if name.endswith(".razor")}
    inputs = {}
    for name, text in texts.items():
        suffix = Path(name).suffix
        kind = None
        if suffix == ".razor":
            kind = "IMPORT" if Path(name).name == "_Imports.razor" else "RAZOR"
        elif suffix == ".cs" and (SIGNALS.search(text) or name.endswith((".razor.cs", "AssemblyMarker.cs")) or any(
                match in component_names for match in re.findall(r"partial\s+class\s+(\w+)", text))):
            kind = "CS_RENDER_OR_ACTIVATION"
        elif suffix in {".csproj", ".props", ".targets"}:
            kind = "BUILD"
        elif suffix in {".css", ".js"}:
            kind = "ASSET"
        if kind:
            inputs[name] = {"sha256_lf": hashlib.sha256(text.encode("utf-8")).hexdigest(), "kind": kind,
                            "role": "SCENARIO" if name.startswith("src/Sandboxes/") else "PRODUCT"}
    return inputs


def validate(current: dict[str, dict], register: dict) -> list[str]:
    failures = []
    rows = register.get("entries", [])
    families = register.get("families", {})
    registered = {}
    for row in rows:
        path = row.get("path", "")
        if path in registered:
            failures.append(f"Duplicate entry: {path}")
        registered[path] = row
        if row.get("classification") not in CLASSIFICATIONS:
            failures.append(f"Unreviewed classification: {path}")
        for field in REQUIRED:
            if field not in row or row[field] is None or row[field] == "":
                failures.append(f"Missing {field}: {path}")
        for field in ("assets", "scenario", "evidence"):
            if row.get(field) not in families:
                failures.append(f"Unresolved {field} family: {path}")
    for path, actual in current.items():
        recorded = registered.get(path)
        if recorded is None:
            failures.append(f"New rendering input requires review: {path}")
        elif any(recorded.get(key) != value for key, value in actual.items()):
            failures.append(f"Changed rendering input requires review: {path}")
    failures.extend(f"Stale rendering input requires disposition: {path}" for path in registered.keys() - current.keys())
    return failures


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--register", type=Path, default=Path("docs/architecture/components-decoupling-register.json"))
    args = parser.parse_args()
    try:
        root = args.repo.resolve()
        register = json.loads((root / args.register).read_text(encoding="utf-8"))
        current = source_inputs(root)
        failures = validate(current, register)
        if failures:
            print("\n".join(failures), file=sys.stderr)
            return 1
        print(f"Rendering review register PASS: {len(current)} current inputs. Semantic, evaluated and runtime proof remain separate gates.")
        return 0
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        print(f"Rendering register failed: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
