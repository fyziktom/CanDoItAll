#!/usr/bin/env python3
from pathlib import Path
import sys
root = Path(__file__).resolve().parents[1]
required = [
    'README.md',
    'audit/current-state-audit.md',
    'audit/evidence-map.md',
    'analysis/codex-report-vs-snapshot.md',
    'analysis/process-failure-retry-rework-analysis.md',
    'analysis/process-ui-control-monitoring-analysis.md',
    'analysis/escalation-approval-model-analysis.md',
    'architecture/target-architecture.md',
    'requirements/requirements.md',
    'traceability/requirement-traceability.md',
    'shared-prompts/codex-master-prompt.md',
    'shared-prompts/codex-qa-prompt.md',
    'reviews/readiness-gate.md',
]
missing = [p for p in required if not (root / p).exists()]
sub_root = root / 'subbundles'
subs = sorted(p for p in sub_root.iterdir() if p.is_dir()) if sub_root.exists() else []
if len(subs) < 10:
    missing.append('at least 10 subbundles')
for sub in subs:
    if not (sub / 'README.md').exists():
        missing.append(str(sub.relative_to(root) / 'README.md'))
if missing:
    print('Bundle validation failed:')
    for item in missing:
        print(f'- {item}')
    sys.exit(1)
print(f'Bundle validation passed with {len(subs)} subbundles.')
