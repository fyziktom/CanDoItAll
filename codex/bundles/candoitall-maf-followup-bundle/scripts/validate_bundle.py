#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
required = [
    'README.md',
    'audit/current-state-audit.md',
    'audit/evidence-map.md',
    'requirements/requirements.md',
    'shared-prompts/codex-master-prompt.md',
    'shared-prompts/codex-qa-prompt.md',
    'reviews/readiness-gate.md',
    'subbundles/01-finalizer-runtime-mode-alignment/README.md',
    'subbundles/02-tool-policy-exception-boundary/README.md',
    'subbundles/03-provider-feature-consistency/README.md',
    'subbundles/04-hardening-test-suite-reconciliation/README.md',
    'subbundles/05-repair-service-contract/README.md',
    'subbundles/06-process-context-output-validation/README.md',
    'subbundles/07-tool-composition-approval-failfast/README.md',
    'subbundles/08-workflow-checkpoint-claims-and-roadmap/README.md',
    'subbundles/09-verification-document-truthfulness/README.md',
]

missing = [item for item in required if not (root / item).exists()]
if missing:
    print('Missing required bundle files:')
    for item in missing:
        print(f'- {item}')
    sys.exit(1)

for md in root.rglob('*.md'):
    text = md.read_text(encoding='utf-8')
    if 'TODO' in text:
        print(f'Unexpected TODO marker in {md.relative_to(root)}')
        sys.exit(1)

print('Bundle validation passed.')
