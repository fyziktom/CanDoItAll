import hashlib
import json
import subprocess
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
BUNDLE = ROOT / "codex/bundles/providers-shared-premerge-review"
PROOF = BUNDLE / "proof/SB09"
SHARED = ROOT.parent / "CanDoItAll.SharedInfo"

def git(root, *arguments):
    return subprocess.run(["git", "-C", str(root), *arguments], capture_output=True, check=True).stdout

def digest(data):
    return hashlib.sha256(data).hexdigest().upper()

def changed(root):
    tracked = git(root, "diff", "--name-only", "HEAD", "-z").decode().split("\0")
    added = git(root, "ls-files", "--others", "--exclude-standard", "-z").decode().split("\0")
    return sorted(set(filter(None, tracked + added)))

def snapshot(root, label):
    rows = []
    for relative in changed(root):
        path = root / relative
        if not path.is_file():
            continue
        if root == ROOT and ("/proof/" in relative or "/reviews/test-results/" in relative or relative.endswith(".log")):
            continue
        previous = subprocess.run(["git", "-C", str(root), "show", f"HEAD:{relative}"], capture_output=True)
        rows.append({
            "path": f"{label}://{relative}",
            "beforeSha256": digest(previous.stdout) if previous.returncode == 0 else None,
            "afterSha256": digest(path.read_bytes()),
        })
    return {"repository": label, "baselineCommit": git(root, "rev-parse", "HEAD").decode().strip(), "files": rows}

PROOF.mkdir(parents=True, exist_ok=True)
report = {
    "generatedUtc": datetime.now(timezone.utc).isoformat(),
    "note": "Working tree evidence; no commit-clean claim. Proof and raw transcripts are hashed separately to avoid recursive self-hashes.",
    "sources": [snapshot(ROOT, "repo"), snapshot(SHARED, "sharedinfo")],
    "dependencies": [{
        "repository": name,
        "commit": git(ROOT.parent / name, "rev-parse", "HEAD").decode().strip(),
        "status": git(ROOT.parent / name, "status", "--porcelain=v1").decode().splitlines(),
    } for name in ("CanDoItAll.Components", "CanDoItAll.FileTools")],
}
(PROOF / "changed-files.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
artifacts = []
for path in sorted((BUNDLE / "reviews").rglob("*")):
    if path.is_file() and path.suffix in (".log", ".trx", ".txt"):
        artifacts.append({"path": "bundle://" + path.relative_to(BUNDLE).as_posix(), "sha256": digest(path.read_bytes()), "bytes": path.stat().st_size})
for path in sorted(PROOF.rglob("*")):
    if path.is_file() and path.name not in ("artifacts.json", "failure.png"):
        artifacts.append({"path": "bundle://" + path.relative_to(BUNDLE).as_posix(), "sha256": digest(path.read_bytes()), "bytes": path.stat().st_size})
for path in sorted((ROOT / "artifacts/providers-shared-premerge/schema").glob("*.sql")):
    artifacts.append({"path": "repo://" + path.relative_to(ROOT).as_posix(), "sha256": digest(path.read_bytes()), "bytes": path.stat().st_size, "generatedIgnoredArtifact": True})
(PROOF / "artifacts.json").write_text(json.dumps(artifacts, indent=2) + "\n", encoding="utf-8")
print(f"Hashed {sum(len(item['files']) for item in report['sources'])} changed source/document files and {len(artifacts)} artifacts.")
