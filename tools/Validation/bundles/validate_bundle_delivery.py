"""Validate bundle evidence against proposed files or immutable Git tree blobs."""

import argparse
import hashlib
import json
import posixpath
import re
import subprocess
from pathlib import Path, PurePosixPath
from urllib.parse import unquote, urlsplit


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args])


class Inventory:
    def __init__(self, root, ref=None):
        self.root = Path(root).resolve()
        self.ref = ref
        self.object_format = git(root, "rev-parse", "--show-object-format").decode().strip()
        if ref:
            self.ref = git(root, "rev-parse", "--verify", "--end-of-options", ref + "^{tree}").decode().strip()
            raw = git(root, "ls-tree", "-rz", "--name-only", self.ref)
        else:
            raw = git(root, "ls-files", "-z", "--cached", "--others", "--exclude-standard")
        self.paths = {p.decode("utf-8") for p in raw.split(b"\0") if p}
        if not ref:
            self.paths = {p for p in self.paths if (self.root / p).is_file()}

    def read(self, path):
        if path not in self.paths:
            raise ValueError(f"Not in Git inventory: {path}")
        if self.ref:
            return git(self.root, "show", f"{self.ref}:{path}")
        file = (self.root / path).resolve()
        if not file.is_relative_to(self.root):
            raise ValueError(f"Path escapes repository: {path}")
        return file.read_bytes()


    def preserves_retained_bytes(self, path, data):
        if self.ref:
            return True
        filtered = subprocess.check_output(
            ["git", "-C", str(self.root), "hash-object", "--path=" + path, "--stdin"],
            input=data, stderr=subprocess.PIPE).decode().strip()
        header = f"blob {len(data)}\0".encode()
        original = hashlib.new(self.object_format, header + data).hexdigest()
        return filtered == original


def relative_path(owner, target):
    target = unquote(target).replace("\\", "/")
    if target.startswith("/") or re.match(r"^[A-Za-z]:", target):
        raise ValueError(f"Absolute local target: {target}")
    result = posixpath.normpath(posixpath.join(posixpath.dirname(owner), target))
    if result == ".." or result.startswith("../"):
        raise ValueError(f"Target escapes repository: {target}")
    return result


def markdown_targets(text):
    text = re.sub(r"(?ms)^\s*(`{3,}|~{3,}).*?^\s*\1\s*$", "", text)
    targets = re.findall(r"!?\[[^\]\n]*\]\(\s*(<[^>]+>|[^\s)]+)(?:\s+[^)]*)?\)", text)
    targets += re.findall(r"(?m)^\s*\[[^\]\n]+\]:\s*(<[^>]+>|\S+)", text)
    for target in targets:
        target = target.strip("<>")
        if re.match(r"^[A-Za-z]:", target) or target.startswith("\\\\"):
            yield target
            continue
        parsed = urlsplit(target)
        if parsed.scheme or parsed.netloc or not parsed.path:
            continue
        yield parsed.path


def validate(inventory, bundle):
    bundle = PurePosixPath(bundle).as_posix().rstrip("/")
    manifest = bundle + "/MANIFEST.sha256"
    errors = []
    entries = {}
    try:
        lines = inventory.read(manifest).decode("utf-8-sig").splitlines()
    except (ValueError, UnicodeError) as error:
        return {"bundle": bundle, "errors": [str(error)], "entries": 0, "links": 0}
    for number, line in enumerate(lines, 1):
        if not line.strip():
            continue
        match = re.fullmatch(r"([0-9a-fA-F]{64})\s+\*?(.+)", line)
        if not match:
            errors.append(f"{manifest}:{number}: invalid SHA-256 entry")
            continue
        expected, target = match.groups()
        try:
            path = relative_path(manifest, target)
            if not path.startswith(bundle + "/") or path == manifest:
                raise ValueError(f"Manifest entry outside bundle or self-reference: {target}")
            if path in entries:
                raise ValueError(f"Duplicate manifest entry: {target}")
            entries[path] = expected.lower()
            data = inventory.read(path)
            if not inventory.preserves_retained_bytes(path, data):
                errors.append(f"Git clean filters change retained bytes: {path}")
            actual = hashlib.sha256(data).hexdigest()
            if actual != expected.lower():
                errors.append(f"Hash mismatch: {path}")
        except ValueError as error:
            errors.append(str(error))
    members = {p for p in inventory.paths if p.startswith(bundle + "/") and p != manifest}
    for path in sorted(members - entries.keys()):
        errors.append(f"Unsealed bundle file: {path}")
    links = 0
    for path in sorted(members):
        if not path.endswith(".md"):
            continue
        try:
            for target in markdown_targets(inventory.read(path).decode("utf-8-sig")):
                links += 1
                resolved = relative_path(path, target)
                if resolved not in inventory.paths and not any(p.startswith(resolved + "/") for p in inventory.paths):
                    errors.append(f"Unresolved Git link: {path} -> {target}")
        except (ValueError, UnicodeError) as error:
            errors.append(f"{path}: {error}")
    return {"bundle": bundle, "entries": len(entries), "links": links, "errors": errors}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--git-ref", help="Actual committed tree, e.g. origin/components-decoupling")
    parser.add_argument("--bundle", action="append", required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    inventory = Inventory(args.repo_root, args.git_ref)
    report = {"mode": "git-tree" if args.git_ref else "proposed", "tree": inventory.ref,
              "results": [validate(inventory, bundle) for bundle in args.bundle]}
    report["passed"] = all(not item["errors"] for item in report["results"])
    result = json.dumps(report, indent=2, ensure_ascii=True) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result, encoding="utf-8", newline="\n")
    print(result)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
