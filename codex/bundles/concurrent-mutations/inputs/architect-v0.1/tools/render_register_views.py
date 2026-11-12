#!/usr/bin/env python3
"""Regenerate standalone readable register views inside this architecture package.

Narrative architecture documents are not rewritten. Review their wording separately.
"""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import sys


def render(root: Path) -> list[str]:
    root = root.resolve()
    output = root / 'registers' / 'views'
    output.mkdir(parents=True, exist_ok=True)
    schemas = {
        'decisions': ['id', 'title', 'status', 'recommendation', 'decision_gate'],
        'risks': ['id', 'title', 'trigger', 'impact', 'mitigation', 'phase', 'tests'],
        'mutation_inventory': ['id', 'owner', 'operation', 'evidence_level', 'proposed_boundary', 'phase'],
        'test_scenarios': ['id', 'phase', 'layer', 'title', 'setup_and_interleaving', 'required_assertion', 'status'],
        'phases': ['id', 'title', 'depends_on', 'goal', 'stop', 'status'],
        'performance_scenarios': ['id', 'title', 'workload', 'metrics', 'candidate_gate', 'status'],
        'open_questions': ['id', 'question', 'review_owner', 'decision_gate', 'recommended_default_or_action', 'status'],
    }
    written = []
    for name, fields in schemas.items():
        rows = json.loads((root / 'registers' / f'{name}.json').read_text(encoding='utf-8'))
        lines = [f'# {name.replace("_", " ").title()}', '',
                 'Generated from the adjacent JSON register. Narrative documents may also require review after decisions change.', '',
                 '| ' + ' | '.join(fields) + ' |', '| ' + ' | '.join('---' for _ in fields) + ' |']
        def cell(value: object) -> str:
            if isinstance(value, list):
                value = '; '.join(str(item) for item in value)
            return str(value if value is not None else 'Not recorded').replace('|', '\\|').replace('\n', '<br>')
        lines.extend('| ' + ' | '.join(cell(row.get(field)) for field in fields) + ' |' for row in rows)
        path = output / f'{name}.md'
        path.write_text('\n'.join(lines) + '\n', encoding='utf-8')
        written.append(path.relative_to(root).as_posix())
    return written


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('package_root', nargs='?', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        for path in render(args.package_root):
            print(path)
    except (OSError, ValueError, KeyError) as exc:
        print(f'Register rendering failed: {exc}', file=sys.stderr)
        sys.exit(1)
