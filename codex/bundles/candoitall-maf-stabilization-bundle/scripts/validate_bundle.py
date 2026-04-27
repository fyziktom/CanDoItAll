#!/usr/bin/env python3
from pathlib import Path

root = Path(__file__).resolve().parents[1]
required = [
    'README.md',
    'analysis/current-state-audit.md',
    'analysis/repository-evidence-map.md',
    'analysis/maf-feature-gap-matrix.md',
    'requirements/requirements.md',
    'architecture/target-architecture.md',
    'plan/execution-plan.md',
    'traceability/matrix.md',
    'shared-prompts/codex-master-prompt.md',
    'shared-prompts/codex-qa-prompt.md',
    'reviews/readiness-gate.md',
]
for rel in required:
    path = root / rel
    if not path.exists():
        raise SystemExit(f'Missing required file: {rel}')

subbundles = sorted((root / 'subbundles').glob('*'))
if len(subbundles) < 10:
    raise SystemExit(f'Expected at least 10 subbundles, found {len(subbundles)}')
for bundle in subbundles:
    for name in ['README.md', 'codex-prompt.md', 'tests.md', 'file-map.md']:
        path = bundle / name
        if not path.exists():
            raise SystemExit(f'Missing {name} in {bundle.name}')

print(f'Bundle validation passed: {len(subbundles)} subbundles.')
