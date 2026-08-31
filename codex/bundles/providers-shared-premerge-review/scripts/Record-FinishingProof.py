import hashlib
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
BUNDLE = ROOT / "codex/bundles/providers-shared-premerge-review"
PROOF = BUNDLE / "proof/SB09/finishing"
SHARED = ROOT.parent / "CanDoItAll.SharedInfo"

def git(root, *args):
    return subprocess.run(["git", "-C", str(root), *args], check=True, capture_output=True).stdout

def sha(data):
    return hashlib.sha256(data).hexdigest().upper()

def changed_sources(root, label):
    paths = set(git(root, "diff", "--name-only", "HEAD", "-z").decode().split("\0"))
    paths.update(git(root, "ls-files", "--others", "--exclude-standard", "-z").decode().split("\0"))
    rows = []
    for relative in sorted(filter(None, paths)):
        path = root / relative
        if not path.is_file() or (root == ROOT and "/proof/" in relative):
            continue
        previous = subprocess.run(["git", "-C", str(root), "show", f"HEAD:{relative}"], capture_output=True)
        rows.append({"path": f"{label}://{relative}",
                     "beforeSha256": sha(previous.stdout) if previous.returncode == 0 else None,
                     "afterSha256": sha(path.read_bytes())})
    return {"repository": label, "baselineCommit": git(root, "rev-parse", "HEAD").decode().strip(), "files": rows}

historical = json.loads((BUNDLE / "proof/SB09/changed-files.json").read_text(encoding="utf-8"))
original_behavior = []
for source in historical["sources"]:
    if source["repository"] != "repo":
        continue
    for item in source["files"]:
        relative = item["path"].removeprefix("repo://")
        if not relative.startswith(("src/", "tests/")):
            continue
        actual = sha((ROOT / relative).read_bytes())
        original_behavior.append({"path": item["path"], "sha256": actual, "matchesOriginalProof": actual == item["afterSha256"]})
if any(not row["matchesOriginalProof"] for row in original_behavior):
    raise RuntimeError("Original behavior proof has drifted; reconcile its affected tests explicitly.")

active = json.loads((PROOF / "installed-skill-hashes.json").read_text(encoding="utf-8-sig"))
for item in active:
    directories = [p for p in (SHARED / "codex/skills").rglob(item["package"]) if p.is_dir()]
    if len(directories) != 1:
        raise RuntimeError("Source skill package is ambiguous: " + item["package"])
    installed = Path.home() / ".codex/skills" / item["package"] / item["path"]
    source = directories[0] / item["path"]
    if sha(source.read_bytes()) != item["sha256"] or sha(installed.read_bytes()) != item["sha256"]:
        raise RuntimeError("Source/active hash drift: " + item["package"] + "/" + item["path"])

artifacts = [{"path": "bundle://" + p.relative_to(BUNDLE).as_posix(),
              "sha256": sha(p.read_bytes()), "bytes": p.stat().st_size}
             for p in sorted(PROOF.rglob("*")) if p.is_file() and p.name != "manifest.json"]
result = {"generatedUtc": datetime.now(timezone.utc).isoformat(),
          "status": "Live finishing acceptance passed; overall merge closure blocked on original three-app proof and independent review.",
          "note": "Historical SB09 hashes are preserved. This manifest identifies current finishing changes and artifacts; no clean-commit claim.",
          "sources": [changed_sources(ROOT, "repo"), changed_sources(SHARED, "sharedinfo")],
          "unchangedOriginalBehavior": original_behavior,
          "activeSkillFilesVerified": len(active),
          "dependencies": [{"repository": name, "commit": git(ROOT.parent / name, "rev-parse", "HEAD").decode().strip(),
                            "status": git(ROOT.parent / name, "status", "--porcelain=v1").decode().splitlines()}
                           for name in ("CanDoItAll.Components", "CanDoItAll.FileTools")],
          "artifacts": artifacts}
(PROOF / "manifest.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"sourceFiles": sum(len(s["files"]) for s in result["sources"]),
                  "artifacts": len(artifacts), "unchangedOriginalBehaviorFiles": len(original_behavior),
                  "activeSkillFilesVerified": len(active)}))
