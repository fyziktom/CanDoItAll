#!/usr/bin/env python3
"""Validate the architecture package, not CanDoItAll application behavior."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote


def validate(root: Path, verify_hashes: bool) -> dict:
    errors: list[str] = []
    checks: list[str] = []
    catalogs: dict[str, list[dict]] = {}
    for file in sorted((root / 'catalogs').glob('*.json')):
        try:
            data = json.loads(file.read_text(encoding='utf-8'))
            if not isinstance(data, list) or not all(isinstance(row, dict) for row in data):
                raise ValueError('Catalog must be an array of objects.')
            catalogs[file.stem] = data
        except (OSError, ValueError) as exc:
            errors.append(f'{file.relative_to(root)}: {exc}')
    required = {'modules', 'features', 'contracts', 'qa-scenarios', 'invariants',
                'glossary', 'gaps', 'sources', 'requirements', 'baseline-inventory',
                'translation-coverage', 'module-operation-matrix', 'runtime-surfaces',
                'managed-agent-profiles', 'feature-proof-candidates',
                'invariant-proof-plan', 'contract-proof-plan', 'persistence-discovery',
                'inherited-source-anchors'}
    if required - catalogs.keys():
        errors.append(f'Missing catalogs: {sorted(required - catalogs.keys())}')
        return {'status': 'FAIL', 'errors': errors}
    ids: dict[str, str] = {}
    for name, rows in catalogs.items():
        seen: set[str] = set()
        for row in rows:
            identity = row.get('id')
            if identity:
                if identity in seen or identity in ids:
                    errors.append(f'Duplicate record ID: {identity}')
                seen.add(identity)
                ids[identity] = name
    checks.append('JSON shape and record identity uniqueness')

    manifest = json.loads((root / 'manifest.json').read_text(encoding='utf-8'))
    actual_counts = {name: len(rows) for name, rows in catalogs.items()}
    if manifest.get('catalog_counts') != actual_counts:
        errors.append('Manifest catalog counts do not match files.')
    if manifest.get('language') != 'en' or manifest.get('application_validation') != 'NOT_RUN':
        errors.append('Manifest language/evidence status is invalid.')
    checks.append('Manifest counts, language, and evidence status')

    for entry in catalogs['baseline-inventory']:
        name = entry['catalog']
        key = entry.get('identity_field')
        current = catalogs.get(name, [])
        if key:
            present = {row.get(key) for row in current}
            missing = set(entry['identities']) - present
            if missing:
                errors.append(f'Original identities lost from {name}: {sorted(missing)}')
        elif len(current) < entry['record_count']:
            errors.append(f'Original records lost from {name}.')
    coverage = catalogs['translation-coverage']
    if len(coverage) != manifest.get('original_file_count'):
        errors.append('Translation coverage count does not match original file inventory.')
    if len({row['original_path'] for row in coverage}) != len(coverage):
        errors.append('Duplicate original path in translation coverage.')
    for row in coverage:
        target = root / row['replacement_path']
        if not target.is_file():
            errors.append(f'Missing replacement for {row["original_path"]}: {row["replacement_path"]}')
    checks.append('All original catalog IDs and file replacements retained')

    reference_fields = {'source_ids', 'primary_source_ids', 'module_ids', 'caller_modules',
                        'implementation_modules', 'extension_implementers', 'delegated_data_owners',
                        'contract_ids', 'read_contract_ids', 'command_contract_ids',
                        'planned_qa_ids', 'qa_ids', 'candidate_qa_ids', 'feature_ids',
                        'automation_callers'}
    single_fields = {'module_id', 'owner_module_id', 'contract_owner', 'registration_owner',
                     'parent_module_id', 'business_policy_owner', 'technical_identity_owner',
                     'source_id', 'input_id', 'anchor_id', 'feature_id', 'invariant_id',
                     'contract_id'}
    for name, rows in catalogs.items():
        for row in rows:
            for key, value in row.items():
                refs = value if key in reference_fields else [value] if key in single_fields else []
                for ref in refs:
                    if ref is None and key == 'parent_module_id':
                        continue
                    if ref not in ids:
                        errors.append(f'{name} {row.get("id", "mapping")}: unknown {key} {ref}')
    checks.append('Catalog cross-references resolve')

    module_ids = {row['id'] for row in catalogs['modules']}
    matrix_ids = {row['module_id'] for row in catalogs['module-operation-matrix']}
    if module_ids != matrix_ids or len(matrix_ids) != len(catalogs['module-operation-matrix']):
        errors.append('Operation matrix must cover every module exactly once.')
    for module in module_ids:
        if not (root / 'modules' / f'{module}.md').is_file():
            errors.append(f'Missing module card {module}.')
    for row in catalogs['module-operation-matrix']:
        if row.get('new_automatic_grants') is not False:
            errors.append(f'Matrix row grants authority implicitly: {row["id"]}')
    for row in catalogs['contracts']:
        if 'BND-CONVERSATIONS' in row['caller_modules']:
            errors.append(f'Neutral conversation UI is a direct domain caller in {row["id"]}.')
        if row['id'] in {'CON-072', 'CON-073'}:
            if 'BND-SIMPLECHATS' in row['extension_implementers']:
                errors.append('Simple Chats product must not implement runtime executor ports.')
    checks.append('Module coverage, explicit grants, and neutral UI/ordinary-product boundaries')

    scenario_ids = {row['id'] for row in catalogs['qa-scenarios']}
    for row in catalogs['qa-scenarios']:
        if row.get('execution_status') != 'NOT_RUN':
            errors.append(f'Unperformed application proof is not NOT_RUN: {row["id"]}')
        for field in ['title', 'given', 'when', 'then', 'fault_injection', 'required_evidence']:
            if not row.get(field):
                errors.append(f'Missing {field} in {row["id"]}')
    for row in catalogs['features']:
        if row.get('runtime_proof') != 'NOT_RUN':
            errors.append(f'Unexpected runtime-proof claim: {row["id"]}')
    maps = [('feature-proof-candidates', 'feature_id', 'features'),
            ('invariant-proof-plan', 'invariant_id', 'invariants'),
            ('contract-proof-plan', 'contract_id', 'contracts')]
    for mapping, key, source in maps:
        mapped = [row[key] for row in catalogs[mapping]]
        expected = {row['id'] for row in catalogs[source]}
        if len(mapped) != len(set(mapped)) or set(mapped) != expected:
            errors.append(f'Incomplete or duplicate {mapping}.')
    for row in catalogs['contract-proof-plan']:
        expected = {q['id'] for q in catalogs['qa-scenarios'] if row['contract_id'] in q['contract_ids']}
        if set(row['qa_ids']) != expected:
            errors.append(f'Contract proof mapping drift: {row["contract_id"]}')
    checks.append('Scenario completeness, truthful proof status, and proof mapping consistency')

    # Unicode escapes keep this validator itself English-only.
    czech_diacritics = re.compile('[\u011b\u0161\u010d\u0159\u017e\u00fd\u00e1\u00ed\u00e9\u00fa\u016f\u010f\u0165\u0148\u011a\u0160\u010c\u0158\u017d\u00dd\u00c1\u00cd\u00c9\u00da\u016e\u010e\u0164\u0147]')
    known_old_instruction = re.compile(r'documentation\s+(?:and\s+communication\s+)?(?:is|must be)\s+in\s+Czech', re.I)
    markdown_link = re.compile(r'(?<!!)\[[^\]\n]*\]\(([^)\n]+)\)')
    text_files = [p for p in root.rglob('*') if p.is_file() and p.suffix in {'.md', '.json', '.py', '.txt'}]
    for file in text_files:
        text = file.read_text(encoding='utf-8')
        if czech_diacritics.search(text):
            errors.append(f'Potential untranslated Czech text: {file.relative_to(root)}')
        if file.name != 'validate_package.py' and known_old_instruction.search(text):
            errors.append(f'Obsolete language instruction: {file.relative_to(root)}')
        if file.suffix == '.md':
            for match in markdown_link.finditer(text):
                destination = match.group(1).split(' "')[0].strip('<>')
                if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:', destination) or destination.startswith('#'):
                    continue
                local = unquote(destination.split('#')[0])
                if local and not (file.parent / local).resolve().is_file():
                    errors.append(f'Broken Markdown link: {file.relative_to(root)} -> {destination}')
    checks.append('English-remnant scan and local Markdown links')

    # Source IDs mentioned in prose are useful even when not formatted as hyperlinks.
    record_reference = re.compile(r'\b(?:CON|QA|FEAT|INV|TERM|GAP|REQ|SRC|EXT|INPUT|USER|MAP|POL|SURF|MAT|ACTOR|ANCH|DATA)-\d+\b')
    for file in (p for p in text_files if p.suffix == '.md'):
        for reference in set(record_reference.findall(file.read_text(encoding='utf-8'))):
            if reference not in ids:
                errors.append(f'Unknown prose reference {reference} in {file.relative_to(root)}')
    checks.append('Catalog identifiers used in Markdown resolve')

    hash_status = 'NOT_REQUESTED'
    if verify_hashes:
        lines = (root / 'checksums.sha256').read_text(encoding='utf-8').splitlines()
        declared: dict[str, str] = {}
        for line in lines:
            if not line.strip():
                continue
            try:
                digest, relative = line.split('  ', 1)
            except ValueError:
                errors.append('Malformed checksum line.')
                continue
            path = (root / relative).resolve()
            if root.resolve() not in path.parents or relative in declared:
                errors.append(f'Unsafe/duplicate checksum entry: {relative}')
                continue
            declared[relative] = digest
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                errors.append(f'Checksum mismatch: {relative}')
        actual_files = {str(p.relative_to(root)) for p in root.rglob('*') if p.is_file() and p.name != 'checksums.sha256'}
        if set(declared) != actual_files:
            errors.append(f'Incomplete checksum coverage: {sorted(actual_files ^ set(declared))}')
        hash_status = 'PASS' if not any('checksum' in error.lower() for error in errors) else 'FAIL'
        checks.append('SHA-256 integrity and complete delivered-file coverage')

    return {'status': 'PASS' if not errors else 'FAIL', 'validator_scope': 'PACKAGE_ONLY_NOT_APPLICATION_TESTS',
            'application_validation': 'NOT_RUN', 'catalog_counts': actual_counts,
            'checked_groups': checks, 'hash_verification': hash_status,
            'errors': errors, 'notes': ['Language scanning is a remnant detector, not complete automated linguistic verification.',
                                      'Retained identity checks do not by themselves prove semantic translation quality.',
                                      'Source claims and runtime behavior require the recorded human/implementation review gates.']}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify-hashes', action='store_true')
    parser.add_argument('--write-report', type=Path, help='Write package-only report; regenerate hashes afterward if report is inside the package.')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    try:
        report = validate(root, args.verify_hashes)
    except (OSError, ValueError, KeyError, TypeError) as exc:
        report = {'status': 'FAIL', 'validator_scope': 'PACKAGE_ONLY_NOT_APPLICATION_TESTS', 'errors': [str(exc)]}
    output = json.dumps(report, indent=2) + '\n'
    print(output, end='')
    if args.write_report:
        args.write_report.parent.mkdir(parents=True, exist_ok=True)
        args.write_report.write_text(output, encoding='utf-8')
    return 0 if report['status'] == 'PASS' else 1


if __name__ == '__main__':
    sys.exit(main())
