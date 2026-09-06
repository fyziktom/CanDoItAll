import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
from urllib.parse import unquote, urlsplit

parser = argparse.ArgumentParser(description="Validate retained bundle evidence against Git index or a revision.")
parser.add_argument("bundles", nargs="*", default=["UI_Providers_02E_Verification_Delivery_Hardening_Bundle", "UI_AgentCatalog_Harden_01_Development_Loop_Bundle"])
parser.add_argument("--revision", help="Git revision; omitted means current index.")
parser.add_argument("--output", type=Path)
args = parser.parse_args()
repo = Path(__file__).resolve().parents[4]
command = ["git", "ls-tree", "-r", "-z", "--name-only", args.revision] if args.revision else ["git", "ls-files", "--cached", "-z"]
tracked = set(filter(None, subprocess.check_output(command, cwd=repo).decode().split("\0")))
proc = subprocess.Popen(["git", "cat-file", "--batch"], cwd=repo, stdin=subprocess.PIPE, stdout=subprocess.PIPE)
cache = {}
def content(path):
    if path not in cache:
        ref = f"{args.revision}:{path}" if args.revision else ":" + path
        proc.stdin.write((ref + "\n").encode())
        proc.stdin.flush()
        header = proc.stdout.readline().decode().rstrip()
        if header.endswith(" missing"):
            cache[path] = None
        else:
            cache[path] = proc.stdout.read(int(header.split()[-1]))
            proc.stdout.read(1)
    return cache[path]
errors = []
results = []
for name in args.bundles:
    prefix = "codex/bundles/" + name + "/"
    manifest_path = prefix + "MANIFEST.sha256"
    manifest = content(manifest_path)
    if manifest is None:
        errors.append(f"{name}: manifest absent from Git")
        continue
    entries = {}
    for line in manifest.decode("utf-8-sig").splitlines():
        if not line.strip():
            continue
        fields = line.split(maxsplit=1)
        if len(fields) != 2 or not re.fullmatch("[0-9a-fA-F]{64}", fields[0]):
            errors.append(f"{name}: invalid manifest row")
            continue
        relative = fields[1].lstrip("*")
        if relative in entries or PurePosixPath(relative).is_absolute() or ".." in PurePosixPath(relative).parts:
            errors.append(f"{name}: duplicate or escaping manifest path: {relative}")
            continue
        entries[relative] = fields[0].lower()
    for relative, expected in entries.items():
        path = prefix + relative
        data = content(path)
        if data is None or path not in tracked:
            errors.append(f"{name}: manifest entry absent from Git: {relative}")
        elif hashlib.sha256(data).hexdigest() != expected:
            errors.append(f"{name}: Git checksum mismatch: {relative}")
        if not (repo / path).is_file():
            errors.append(f"{name}: manifest entry absent from checkout: {relative}")
    covered = {p[len(prefix):] for p in tracked if p.startswith(prefix)} - {"MANIFEST.sha256"}
    for relative in sorted(covered - entries.keys()):
        errors.append(f"{name}: tracked file not covered by manifest: {relative}")
    links = 0
    for relative in sorted(covered):
        if not relative.endswith(".md"):
            continue
        path = prefix + relative
        text = content(path).decode("utf-8-sig")
        targets = re.findall(r"!?\[[^]\n]*\]\(([^)\n]+)\)", text)
        targets += re.findall(r"^\s*\[[^]\n]+\]:\s*(\S+)", text, flags=re.MULTILINE)
        for target in targets:
            target = target.strip()
            target = target[1:target.index(">")] if target.startswith("<") and ">" in target else target.split(' "', 1)[0]
            uri = urlsplit(target)
            if uri.scheme or target.startswith("//") or not uri.path:
                continue
            resolved = ((repo / path).parent / unquote(uri.path)).resolve()
            try:
                linked = resolved.relative_to(repo).as_posix()
            except ValueError:
                errors.append(f"{path}: relative link escapes repository: {target}")
                continue
            links += 1
            if linked.startswith(".mcp-state/"):
                errors.append(f"{path}: evidence link relies on transient local state: {target}")
            if linked not in tracked and not any(p.startswith(linked.rstrip("/") + "/") for p in tracked):
                errors.append(f"{path}: relative link absent from Git: {target}")
            if not resolved.exists():
                errors.append(f"{path}: relative link absent from checkout: {target}")
    results.append({"bundle": name, "manifestEntries": len(entries), "trackedCoveredFiles": len(covered), "relativeLinks": links})
proc.stdin.close()
proc.wait()
report = {"source": args.revision or "index", "bundles": results, "errors": errors, "result": "FAIL" if errors else "PASS", "hashContract": "SHA-256 of exact Git blob bytes; historical proof uses -text to preserve original bytes."}
if args.output:
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="\n")
print(json.dumps(report, indent=2))
raise SystemExit(1 if errors else 0)
