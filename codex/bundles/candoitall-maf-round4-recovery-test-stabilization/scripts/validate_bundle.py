#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = [
    'README.md',
    'audit/current-state-audit.md',
    'audit/evidence-map.md',
    'analysis/codex-report-vs-actual-snapshot.md',
    'analysis/process-failure-retry-rework-analysis.md',
    'analysis/test-suite-failure-taxonomy.md',
    'architecture/target-rework-recovery-architecture.md',
    'architecture/test-suite-stabilization-architecture.md',
    'requirements/requirements.md',
    'traceability/requirement-traceability.md',
    'shared-prompts/codex-master-prompt.md',
    'shared-prompts/codex-qa-prompt.md',
    'reviews/readiness-gate.md',
]
REQUIRED_SUBBUNDLES = [
    '00-snapshot-integrity-and-secret-emergency',
    '01-process-mutation-tool-policy-and-approval',
    '02-typed-recovery-decision-and-rework-packets',
    '03-efficient-context-selection-and-session-policy',
    '04-qa-return-rework-loop',
    '05-proof-fingerprints-and-receipt-reuse',
    '06-retry-ledger-backoff-loop-control',
    '07-finalizer-sequence-trace-hardening',
    '08-test-suite-taxonomy-and-default-green-gate',
    '09-playwright-release-no-build-fixtures',
    '10-mcp-stdio-path-and-configuration-fixes',
    '11-projectstructure-api-test-host-stabilization',
    '12-component-canvas-test-modernization',
    '13-storage-and-projectstructure-integration-stabilization',
    '14-dotnetwatch-live-process-test-gating',
    '15-documentation-truthfulness-and-execution-report-validation',
]
SECRET_PATTERNS = [
    re.compile(r'sk-proj-[A-Za-z0-9_-]{20,}'),
    re.compile(r'ghp_[A-Za-z0-9]{20,}'),
    re.compile(r'github_pat_[A-Za-z0-9_]{20,}'),
]

def main() -> int:
    missing = [p for p in REQUIRED if not (ROOT / p).is_file()]
    for sb in REQUIRED_SUBBUNDLES:
        path = ROOT / 'subbundles' / sb / 'README.md'
        if not path.is_file():
            missing.append(str(path.relative_to(ROOT)))
    if missing:
        print('Missing required files:')
        for item in missing:
            print(f' - {item}')
        return 1

    offenders = []
    for path in ROOT.rglob('*'):
        if not path.is_file():
            continue
        text = path.read_text(encoding='utf-8', errors='ignore')
        for pattern in SECRET_PATTERNS:
            if pattern.search(text):
                offenders.append(str(path.relative_to(ROOT)))
                break
    if offenders:
        print('Potential raw secret pattern found inside bundle:')
        for item in offenders:
            print(f' - {item}')
        return 1

    print('Bundle validation passed.')
    return 0

if __name__ == '__main__':
    sys.exit(main())
