from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = [
    'README.md',
    'audit/current-state-audit.md',
    'audit/evidence-map.md',
    'requirements/requirements.md',
    'shared-prompts/codex-master-prompt.md',
    'shared-prompts/codex-qa-prompt.md',
    'reviews/readiness-gate.md',
]
SUBBUNDLES = [
    '01-finalizer-mode-aware-runtime',
    '02-finalizer-response-format-instruction-consistency',
    '03-tool-policy-exception-boundary',
    '04-provider-capability-ui-and-db-truth',
    '05-finalizer-sequence-invariant',
    '06-typed-output-runasync-evaluation',
    '07-verification-and-test-depth',
]

def main() -> int:
    missing = []
    for rel in REQUIRED:
        if not (ROOT / rel).is_file():
            missing.append(rel)
    for sub in SUBBUNDLES:
        rel = f'subbundles/{sub}/README.md'
        if not (ROOT / rel).is_file():
            missing.append(rel)
    if missing:
        print('Missing required bundle files:')
        for item in missing:
            print(f' - {item}')
        return 1
    print('Bundle structure OK')
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
