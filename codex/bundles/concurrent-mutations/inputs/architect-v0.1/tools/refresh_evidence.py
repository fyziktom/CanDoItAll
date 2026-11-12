#!/usr/bin/env python3
"""Read-only Git evidence refresh; no checkout, fetch, build or application changes.

An identical source blob does not prove unchanged callers or architectural validity.
The output must be outside the repository and must not already exist.
"""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import sys


def git(repo: Path, *args: str, raw: bool = False) -> str | bytes:
    command = ['git', '-C', str(repo), *args]
    result = subprocess.run(command, check=False, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=60)
    if result.returncode:
        raise ValueError(f'Git read failed ({args[0]}): {result.stderr.decode("utf-8", errors="replace").strip()}')
    return result.stdout if raw else result.stdout.decode('utf-8', errors='replace').strip()


def refresh(repo: Path, ref: str, source_ledger: Path, output: Path) -> dict:
    repo = Path(str(git(repo.resolve(), 'rev-parse', '--show-toplevel'))).resolve()
    output = output.resolve()
    if output.is_relative_to(repo):
        raise ValueError('Output must be outside the application repository')
    if output.exists():
        raise ValueError('Output already exists; choose a new file to preserve prior evidence')
    commit = str(git(repo, 'rev-parse', '--verify', '--end-of-options', f'{ref}^{{commit}}'))
    branch = str(git(repo, 'symbolic-ref', '--short', '-q', 'HEAD')) if str(git(repo, 'rev-parse', '--abbrev-ref', 'HEAD')) != 'HEAD' else '(detached)'
    dirty = bool(git(repo, 'status', '--porcelain'))
    tree_raw = git(repo, 'ls-tree', '-r', '-z', '--full-tree', commit, raw=True)
    assert isinstance(tree_raw, bytes)
    paths: dict[str, str] = {}
    by_blob: dict[str, list[str]] = {}
    for record in tree_raw.split(b'\x00'):
        if not record:
            continue
        header, name = record.split(b'\t', 1)
        _mode, kind, blob = header.decode('ascii').split(' ')
        if kind != 'blob':
            continue
        path = name.decode('utf-8', errors='replace')
        paths[path] = blob
        by_blob.setdefault(blob, []).append(path)
    ledger = json.loads(source_ledger.read_text(encoding='utf-8'))
    rows = []
    for source in ledger:
        if source.get('kind') != 'repository':
            continue
        path = source['path']
        expected = source.get('git_blob_sha')
        actual = paths.get(path)
        if actual is None:
            candidates = by_blob.get(expected, []) if expected else []
            state = 'missing-relocation-candidate' if candidates else 'missing-needs-discovery'
        else:
            candidates = []
            state = 'present-baseline-hash-unrecorded' if not expected else 'same-blob-review-callers' if actual == expected else 'changed-reread-required'
        rows.append(dict(source_id=source['id'], path=path, evidence_ref=source['ref'],
                         expected_blob=expected, current_blob=actual, status=state,
                         possible_relocations=candidates, semantic_review_completed=False))
    result = dict(schema_version=1, generated_utc=datetime.now(timezone.utc).isoformat(),
                  repository_root=str(repo), checked_ref=ref, resolved_commit=commit,
                  active_branch=branch, working_tree_dirty=dirty,
                  uncommitted_content_inspected=False,
                  warning='Only committed tree blob identities compared. Inspect dirty content and all affected callers/composition/tests manually. No application proof produced.',
                  sources=rows)
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('x', encoding='utf-8') as stream:
        json.dump(result, stream, indent=2, ensure_ascii=False)
        stream.write('\n')
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--ref', default='HEAD')
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--sources', type=Path, default=Path(__file__).resolve().parents[1] / 'registers' / 'sources.json')
    args = parser.parse_args()
    try:
        result = refresh(args.repo, args.ref, args.sources, args.out)
        print(json.dumps({'status': 'written', 'resolved_commit': result['resolved_commit'],
                          'source_count': len(result['sources']), 'output': str(args.out.resolve())}, indent=2))
        return 0
    except (OSError, ValueError, KeyError, subprocess.TimeoutExpired) as exc:
        print(f'Evidence refresh failed: {exc}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
