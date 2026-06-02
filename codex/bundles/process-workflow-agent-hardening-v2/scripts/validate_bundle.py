#!/usr/bin/env python3
from pathlib import Path
import argparse
import sys

REQUIRED_ROOTS = [
    'README.md',
    'inputs',
    'analysis',
    'requirements',
    'architecture',
    'plan',
    'traceability',
    'shared-prompts',
    'subbundles',
    'reviews',
]

REQUIRED_SUBBUNDLE_SECTIONS = [
    '## Status',
    '## Objective',
    '## Covered Inputs',
    '## Prerequisites',
    '## Exact Source References',
    '## Deliverables',
    '## Dependency Impact',
    '## Validation Depth',
    '## Implementation Steps',
    '## Scope Exceptions',
    '## Do Not Do',
    '## Acceptance Checklist',
    '## Proof Required',
    '## Browser Validation Logging',
    '## Progression Gate',
    '## Suggested Agent Prompt',
]

PLAN_REQUIRED = [
    '## Subbundle Dependency Map',
    '## Critical Subbundles',
    '## Phase Gates',
    '```mermaid',
]

ANALYSIS_REQUIRED = [
    '## Critical Path Risks',
    '## Validation Risks',
    '## Reopen Triggers',
]


def fail(message: str) -> None:
    print(f'FAIL: {message}')
    sys.exit(1)


def require_path(root: Path, relative: str) -> None:
    if not (root / relative).exists():
        fail(f'Missing required path: {relative}')


def require_contains(path: Path, needles: list[str]) -> None:
    if not path.exists():
        fail(f'Missing required file: {path}')
    text = path.read_text(encoding='utf-8')
    missing = [needle for needle in needles if needle not in text]
    if missing:
        fail(f'{path} is missing required section(s): {missing}')


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument('--stage', choices=['prepared', 'completed'], default='prepared')
    parser.add_argument('--root', default='.')
    args = parser.parse_args()

    root = Path(args.root).resolve()
    for relative in REQUIRED_ROOTS:
        require_path(root, relative)

    require_contains(root / 'plan' / '01-phase-plan.md', PLAN_REQUIRED)
    require_contains(root / 'analysis' / '02-assumptions-and-risks.md', ANALYSIS_REQUIRED)

    subbundle_roots = sorted((root / 'subbundles').glob('SB*'))
    if len(subbundle_roots) < 1:
        fail('No subbundles found.')

    for subbundle in subbundle_roots:
        readme = subbundle / 'README.md'
        require_contains(readme, REQUIRED_SUBBUNDLE_SECTIONS)

    if args.stage == 'completed':
        proof_root = root / 'proof'
        if not proof_root.exists():
            fail('Completed validation requires proof directory.')
        for subbundle in subbundle_roots:
            sb_id = subbundle.name.split('-', 1)[0]
            require_path(root, f'proof/{sb_id}/manifest.md')
            require_path(root, f'proof/{sb_id}/semantic-invariants.md')

    print(f'PASS: bundle validation succeeded for stage {args.stage}')


if __name__ == '__main__':
    main()
