from __future__ import annotations

import json
import sys
from pathlib import Path


def main() -> int:
    root = Path(__file__).resolve().parents[1]
    errors: list[str] = []

    manifest = json.loads((root / "manifest.json").read_text(encoding="utf-8"))
    status = json.loads((root / "bundle-status.json").read_text(encoding="utf-8"))
    requirements = json.loads((root / "requirements/requirements.json").read_text(encoding="utf-8"))

    if manifest.get("terminalExecutionState") != "awaiting-user-agent-chat-regression":
        errors.append("Manifest terminal state is not the manual regression gate.")
    if status.get("simpleChatUiActivationAllowed") is not False:
        errors.append("Simple Chat UI activation is not explicitly false.")

    req_ids = {item["id"] for item in requirements["requirements"]}
    for rid in ["UIR-003", "UIR-005", "UIR-062", "UIR-063", "UIR-080", "UIR-081", "UIR-082"]:
        if rid not in req_ids:
            errors.append(f"Missing phase-exclusion requirement {rid}.")

    root_readme = (root / "README.md").read_text(encoding="utf-8")
    required_phrases = [
        "This bundle does not implement Simple Chat UI.",
        "no mixed Agent/Simple Chat catalog",
        "future **Add context** button",
        "Simple Chat API or SSE clients",
        "Do not mark the branch `ready-for-simple-chat-ui`",
    ]
    for phrase in required_phrases:
        if phrase not in root_readme:
            errors.append(f"Root phase contract lacks {phrase!r}.")

    final_readme = next((root / "subbundles").glob("SB09-*/README.md"))
    final_text = final_readme.read_text(encoding="utf-8")
    for phrase in [
        "awaiting-user-agent-chat-regression",
        "Do not begin a new bundle automatically.",
        "Simple Chat UI remains blocked pending user approval.",
    ]:
        if phrase not in final_text:
            errors.append(f"SB09 lacks terminal phase gate {phrase!r}.")

    if errors:
        print("Phase-exclusion validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print("Phase-exclusion validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
