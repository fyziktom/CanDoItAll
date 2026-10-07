#!/usr/bin/env python3
"""Validate package/evidence consistency; never claims product execution or provenance authenticity."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote

sys.dont_write_bytecode = True
HEX40 = re.compile(r'[0-9a-f]{40}')
HEX64 = re.compile(r'[0-9a-f]{64}')
IMAGE = re.compile(r'sha256:[0-9a-f]{64}')
STATUSES = {'PASS', 'FAIL', 'BLOCKED', 'NOT_RUN', 'NOT_APPLICABLE'}
MAX_BYTES = 4 * 1024 * 1024


def unique_object(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate JSON property')
        result[key] = value
    return result


def load_json(path: Path) -> object:
    if path.stat().st_size > MAX_BYTES:
        raise ValueError('JSON input too large')
    return json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=unique_object)


def package_files(root: Path) -> list[Path]:
    return sorted(p for p in root.rglob('*') if p.is_file() and '__pycache__' not in p.parts and p.suffix != '.pyc')


def package_errors(root: Path, check_manifest: bool = True) -> tuple[list[str], dict]:
    errors = []
    metrics = {'files': 0, 'local_links': 0, 'groups': 0, 'sources': 0, 'shared_files': 0}
    files = package_files(root)
    metrics['files'] = len(files)
    try:
        meta = load_json(root / 'bundle.json')
        if not isinstance(meta, dict):
            raise ValueError('Metadata must be an object')
        if meta.get('review_commit_is_execution_pin') is not False:
            errors.append('Review commit must not pin execution')
        groups = meta['product_outcome_groups']
        metrics['groups'] = len(groups)
        if len(groups) != len(set(groups)) or len(groups) != 39:
            errors.append('Expected the original 38 unique groups plus local concurrency')
        for name in meta['required_documents']:
            if not isinstance(name, str) or not (root / name).is_file():
                errors.append('Missing required document')
        template = load_json(root / 'templates/evidence.json')
        if {row['id'] for row in template['groups']} != set(groups):
            errors.append('Template group set differs')
        if any(row['status'] != 'NOT_RUN' or row['evidence'] or row['attempts'] for row in template['groups']):
            errors.append('Sealed template must not prefill product passes')
        if any(template['gates'].values()):
            errors.append('Sealed template gates must start false')
        sources = load_json(root / 'sources.json')['records']
        metrics['sources'] = len(sources)
        if len(sources) != len({r['id'] for r in sources}):
            errors.append('Duplicate source IDs')
        for row in sources:
            if row['ref'] != meta['review_commit'] or not row['read_coverage'] or not row['purpose']:
                errors.append('Incomplete source provenance')
        shared = load_json(root / 'shared-provenance.json')['files']
        metrics['shared_files'] = len(shared)
        actual_shared = {p.relative_to(root).as_posix() for p in package_files(root / 'shared')}
        if set(shared) != actual_shared or len(shared) != 22:
            errors.append('Shared foundation file set differs')
        for name, digest in shared.items():
            path = root / name
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                errors.append('Shared foundation hash differs: ' + name)
        for path in files:
            if path.suffix != '.md':
                continue
            text = path.read_text(encoding='utf-8')
            for link in re.findall(r'(?<!!)\[[^\]\n]+\]\(([^)]+)\)', text):
                target = link.strip().split(' "', 1)[0].strip('<>')
                if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:', target) or target.startswith('#'):
                    continue
                target = unquote(target.split('#', 1)[0])
                if not target:
                    continue
                metrics['local_links'] += 1
                resolved = (path.parent / target).resolve()
                if not resolved.is_relative_to(root.resolve()) or not resolved.exists():
                    errors.append('Broken/escaping local link: ' + path.relative_to(root).as_posix() + ' -> ' + target)
        if check_manifest:
            listed = {}
            for line in (root / 'MANIFEST.sha256').read_text(encoding='utf-8').splitlines():
                digest, name = line.split('  ', 1)
                if name in listed or HEX64.fullmatch(digest) is None:
                    errors.append('Invalid manifest row')
                listed[name] = digest
            actual = {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                      for p in files if p != root / 'MANIFEST.sha256'}
            if listed != actual:
                errors.append('Manifest does not match file set/hashes')
    except (OSError, ValueError, KeyError, TypeError, UnicodeError):
        errors.append('Invalid or missing package input')
    return errors, metrics


def evidence_errors(meta: dict, evidence: object, require_complete: bool = False) -> list[str]:
    errors = []
    if not isinstance(evidence, dict):
        return ['Evidence must be an object']
    try:
        if evidence.get('schema_version') != 1 or evidence.get('bundle_id') != meta['id']:
            errors.append('Wrong evidence schema or bundle')
        rows = evidence['groups']
        ids = [row['id'] for row in rows]
        if len(ids) != len(set(ids)) or set(ids) != set(meta['product_outcome_groups']):
            errors.append('Evidence group set is incomplete or duplicated')
        for row in rows:
            if row.get('status') not in STATUSES:
                errors.append('Invalid group status')
            if not isinstance(row.get('evidence'), list) or not isinstance(row.get('attempts'), list):
                errors.append('Invalid evidence/attempt list')
            elif row['status'] == 'PASS' and (not row['evidence'] or not row['attempts']):
                errors.append('PASS requires evidence and an attempt')
        if evidence.get('all_provider_ui_complete') is not False or evidence.get('application_release_ready') is not False:
            errors.append('PP2 cannot claim all-provider or release completion')
        gates = evidence['gates']
        if any(type(value) is not bool for value in gates.values()):
            errors.append('Gate values must be boolean')
        if require_complete or gates.get('pp2_complete') is True:
            for name in ['local_settings_concurrency_resolved','pp2_rendering_complete','independent_sandbox_validated',
                         'final_multi_instance_validated','native_consumers_validated','static_gates_passed',
                         'final_stable_checkpoint_accounted','signed_delivery_verified','pp2_complete']:
                if gates.get(name) is not True:
                    errors.append('Incomplete gate: ' + name)
            if any(row['status'] != 'PASS' for row in rows):
                errors.append('Full completion requires current proof for every required group')
            source = evidence['final_source']
            for name in ['application_commit','components_commit','filetools_commit']:
                if not isinstance(source.get(name), str) or HEX40.fullmatch(source[name]) is None:
                    errors.append('Missing exact final source: ' + name)
            if not isinstance(source.get('worktree_digest'), str) or HEX64.fullmatch(source['worktree_digest']) is None:
                errors.append('Missing final worktree digest')
            if not isinstance(source.get('application_image_digest'), str) or IMAGE.fullmatch(source['application_image_digest']) is None:
                errors.append('Missing final application image digest')
            if not isinstance(source.get('source_manifest'), str) or not source['source_manifest']:
                errors.append('Missing final source manifest')
            commits = evidence['signed_commits']
            if not commits:
                errors.append('No signed delivery commits recorded')
            for row in commits:
                if not isinstance(row.get('sha'), str) or HEX40.fullmatch(row['sha']) is None or row.get('openpgp') is not True or type(row.get('verify_exit_code')) is not int or row['verify_exit_code'] != 0:
                    errors.append('Unverified signed commit metadata')
            if source.get('application_commit') not in {r.get('sha') for r in commits}:
                errors.append('Final application commit lacks recorded signature verification')
            if any(row.get('blocks_pp2') is True and row.get('status') != 'RESOLVED' for row in evidence.get('open_findings', [])):
                errors.append('Unresolved PP2 blocker')
    except (KeyError, TypeError, ValueError):
        errors.append('Malformed evidence structure')
    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--evidence', type=Path)
    parser.add_argument('--require-complete', action='store_true')
    args = parser.parse_args()
    errors, metrics = package_errors(args.root)
    if args.evidence:
        try:
            errors += evidence_errors(load_json(args.root/'bundle.json'), load_json(args.evidence), args.require_complete)
        except (OSError, ValueError, UnicodeError):
            errors.append('Evidence could not be read safely')
    elif args.require_complete:
        errors.append('Completion requires a separate evidence file')
    for error in errors:
        print('FAIL:', error, file=sys.stderr)
    print(json.dumps(metrics, sort_keys=True))
    print('Consistency checks only; no execution, signature or provenance authenticity is established.')
    return 1 if errors else 0


if __name__ == '__main__':
    raise SystemExit(main())
