from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
required = [
    'README.md',
    'audit/current-state-audit.md',
    'audit/evidence-map.md',
    'analysis/process-failure-retry-rework-analysis.md',
    'architecture/target-rework-recovery-architecture.md',
    'requirements/requirements.md',
    'shared-prompts/codex-master-prompt.md',
    'shared-prompts/codex-qa-prompt.md',
    'reviews/readiness-gate.md',
]
missing = [p for p in required if not (ROOT / p).exists()]
subbundles = sorted((ROOT / 'subbundles').glob('*/README.md'))
if missing:
    print('Missing required files:')
    for item in missing:
        print(f' - {item}')
    sys.exit(1)
if len(subbundles) < 10:
    print(f'Expected at least 10 subbundles, found {len(subbundles)}')
    sys.exit(1)
print(f'Bundle OK: {len(required)} required files and {len(subbundles)} subbundles present.')
