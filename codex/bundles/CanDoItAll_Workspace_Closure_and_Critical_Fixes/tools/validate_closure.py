#!/usr/bin/env python3
"""Check closure bookkeeping and optional artifact hashes, never execution authenticity."""
from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from typing import Any

STATUSES = {'PASSED', 'FAILED', 'BLOCKED', 'REHEARSAL', 'NOT_RUN'}
MODES = {'static', 'deterministic-owner', 'production-ui', 'live-agent'}
EXECUTIONS = {'executed', 'not-run', 'rehearsal', 'live'}
FIX_GROUPS = {'FIX-DS', 'FIX-C1', 'FIX-C2', 'FIX-C3'}
HASH40 = re.compile(r'[0-9a-f]{40}')
HASH64 = re.compile(r'[0-9a-f]{64}')


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def integer(value: Any, name: str, minimum: int = 0) -> int:
    require(type(value) is int and value >= minimum, f'Invalid integer: {name}')
    return value


def text(value: Any, name: str) -> str:
    require(isinstance(value, str) and bool(value.strip()), f'Missing text: {name}')
    return value


def digest(value: Any, pattern: re.Pattern[str], name: str) -> None:
    require(isinstance(value, str) and pattern.fullmatch(value) is not None, f'Invalid hash: {name}')


def sha256_file(path: Path) -> str:
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(chunk)
    return value.hexdigest()


def evidence(items: Any, root: Path | None) -> set[str]:
    require(isinstance(items, list), 'Evidence must be a list.')
    kinds: set[str] = set()
    for item in items:
        require(isinstance(item, dict), 'Invalid evidence object.')
        kinds.add(text(item.get('kind'), 'evidence kind'))
        name = text(item.get('path'), 'evidence path')
        path = PurePosixPath(name)
        require(not path.is_absolute() and '..' not in path.parts and '\\' not in name
                and ':' not in name and name == path.as_posix() and bool(path.parts),
                f'Unsafe evidence path: {name}')
        digest(item.get('sha256'), HASH64, name)
        if root is not None:
            target = root
            for part in path.parts:
                target /= part
                require(not target.is_symlink(), f'Evidence symlink: {name}')
            require(target.resolve().is_relative_to(root), f'Evidence escaped root: {name}')
            require(target.is_file(), f'Evidence missing: {name}')
            require(sha256_file(target) == item['sha256'], f'Evidence hash mismatch: {name}')
    return kinds


def checkpoint(value: Any) -> None:
    require(isinstance(value, dict), 'Missing checkpoint object.')
    for field in ('app_commit', 'components_commit', 'filetools_commit'):
        digest(value.get(field), HASH40, field)
    digest(value.get('source_manifest_sha256'), HASH64, 'source_manifest_sha256')


def times(attempt: dict[str, Any]) -> None:
    try:
        start = datetime.fromisoformat(attempt['started_at_utc'].replace('Z', '+00:00'))
        end = datetime.fromisoformat(attempt['ended_at_utc'].replace('Z', '+00:00'))
    except (KeyError, TypeError, AttributeError, ValueError) as exc:
        raise ValueError('Invalid attempt timestamps.') from exc
    require(start.utcoffset() is not None and end.utcoffset() is not None and start <= end,
            'Invalid timestamp order or missing offset.')


def validate(plan: dict[str, Any], result: dict[str, Any], root: Path | None = None,
             require_ready: bool = False) -> dict[str, Any]:
    require(plan.get('schema_version') == result.get('schema_version') == 1, 'Unsupported schema.')
    require(bool(plan.get('bundle_id')) and plan['bundle_id'] == result.get('bundle_id'), 'Bundle IDs differ.')
    if root is not None:
        require(not root.is_symlink() and root.is_dir(), 'Invalid evidence root.')
        root = root.resolve()
    for field in ('workspace_rendering_complete', 'product_fixes_closed', 'ready_for_next_module'):
        require(type(result.get(field)) is bool, f'Expected boolean: {field}')
    ready = result['ready_for_next_module']
    require(not require_ready or ready, 'Closure is not ready.')
    require(not (ready or require_ready) or root is not None, 'Ready validation requires an evidence root.')

    expected: dict[str, Any] = {}
    for case in plan.get('cases', []):
        cid = text(case.get('id'), 'group ID')
        require(cid not in expected and case.get('required_mode') in MODES, 'Duplicate/invalid plan group.')
        for field in ('required', 'needs_ui', 'needs_owner'):
            require(type(case.get(field)) is bool, f'Invalid plan boolean: {cid}/{field}')
        expected[cid] = case
    require(bool(expected), 'Empty closure plan.')
    actual: dict[str, Any] = {}
    for group in result.get('groups', []):
        cid = text(group.get('id'), 'result group ID')
        require(cid not in actual, 'Duplicate result group.')
        actual[cid] = group
    require(set(actual) == set(expected), 'Group inventory differs from plan.')
    critical = result.get('open_critical_findings')
    require(isinstance(critical, list) and all(isinstance(x, str) and x.strip() for x in critical),
            'Invalid critical finding inventory.')
    require(len(set(critical)) == len(critical), 'Duplicate critical finding.')

    authorization = result.get('live_authorization', {})
    require(type(authorization.get('authorized')) is bool, 'Invalid live authorization.')
    total_limit = integer(authorization.get('max_requests_total'), 'total authorization')
    execution_limit = integer(authorization.get('max_requests_per_execution'), 'execution authorization')
    policy = plan.get('live_policy', {})
    if authorization['authorized']:
        text(authorization.get('campaign_id'), 'authorized campaign')
        text(authorization.get('operator_reference'), 'operator authorization reference')
        require(0 < total_limit <= policy['maximum_if_separately_authorized'], 'Unauthorized campaign limit.')
        require(0 < execution_limit <= policy['per_execution_maximum_if_separately_authorized'], 'Unauthorized execution limit.')
        require('authorization' in evidence(authorization.get('evidence'), root), 'Missing authorization artifact.')
    else:
        require(total_limit == execution_limit == 0, 'Closed authorization must allow zero requests.')

    executions: dict[str, Any] = {}
    total = 0
    for execution in result.get('live_executions', []):
        eid = text(execution.get('id'), 'live execution ID')
        require(eid not in executions, 'Duplicate live execution.')
        require(authorization['authorized'], 'Live execution without new authorization.')
        reserved = integer(execution.get('reserved_requests'), 'reserved requests')
        journal = integer(execution.get('provider_journal_requests'), 'provider journal requests')
        require(reserved <= execution_limit, 'Per-execution budget exceeded.')
        total += reserved
        checkpoint(execution.get('checkpoint'))
        require('budget-journal' in evidence(execution.get('evidence'), root), 'Missing budget journal.')
        executions[eid] = execution
    require(total <= total_limit, 'Campaign budget exceeded.')

    statuses: dict[str, str] = {}
    attempt_ids: set[str] = set()
    selected: dict[str, Any] = {}
    for cid, group in actual.items():
        attempts = group.get('attempts')
        require(isinstance(attempts, list), 'Attempts must be a list.')
        choice = group.get('selected_attempt_id')
        statuses[cid] = 'NOT_RUN'
        for attempt in attempts:
            aid = text(attempt.get('attempt_id'), 'attempt ID')
            require(aid not in attempt_ids, 'Duplicate attempt ID.')
            attempt_ids.add(aid)
            status = attempt.get('status')
            mode = attempt.get('mode')
            run = attempt.get('execution')
            require(status in STATUSES and mode in MODES and run in EXECUTIONS, f'Invalid outcome: {aid}')
            if status != 'PASSED':
                text(attempt.get('reason'), 'non-passing reason')
            if run != 'not-run':
                checkpoint(attempt.get('checkpoint'))
                times(attempt)
                text(attempt.get('command'), 'attempt command')
            kinds = evidence(attempt.get('evidence', []), root)
            if run == 'live':
                require(mode == 'live-agent', 'Live execution must use the live mode.')
                live_ids = attempt.get('live_execution_ids', [])
                require(isinstance(live_ids, list) and bool(live_ids), 'Missing live execution references.')
                require(len(live_ids) == len(set(live_ids)), 'Duplicate live execution reference.')
                for eid in live_ids:
                    require(eid in executions, 'Unknown live execution reference.')
                    require(executions[eid]['reserved_requests'] > 0, 'Live execution has no reserved requests.')
                    require(executions[eid]['checkpoint'] == attempt['checkpoint'], 'Live execution source mismatch.')
            if status == 'PASSED':
                require(run in {'executed', 'live'}, 'Gated-off or rehearsal pass is not executed proof.')
                require(mode == expected[cid]['required_mode'], f'Wrong proof mode: {cid}')
                require(bool(kinds), 'Passing attempt has no evidence.')
                if expected[cid]['needs_ui']:
                    require('ui' in kinds, f'Missing actual UI evidence: {cid}')
                if expected[cid]['needs_owner']:
                    require('owner' in kinds, f'Missing actual owner evidence: {cid}')
                if mode == 'live-agent':
                    require(run == 'live', 'Live group lacks live execution.')
                    ids = attempt.get('live_execution_ids', [])
                    require(isinstance(ids, list) and bool(ids), 'Missing live execution references.')
                    for eid in ids:
                        require(eid in executions, 'Unknown live execution reference.')
                        used = executions[eid]
                        require(used['reserved_requests'] > 0 and used['provider_journal_requests'] > 0,
                                'Live pass needs positive outbound and provider journal proof.')
                        require(used['checkpoint'] == attempt['checkpoint'], 'Live execution source mismatch.')
            if 'test_counts' in attempt:
                counts = attempt['test_counts']
                executed = integer(counts.get('executed'), 'test executed')
                passed = integer(counts.get('passed'), 'test passed')
                failed = integer(counts.get('failed'), 'test failed')
                skipped = integer(counts.get('skipped'), 'test skipped')
                require(executed == passed + failed + skipped, 'Inconsistent executed test counts.')
                if status == 'PASSED':
                    require(executed > 0 and failed == skipped == 0, 'Non-green test counts cannot pass.')
            if aid == choice:
                selected[cid] = attempt
                statuses[cid] = status
        require(choice is None and not attempts or choice in {x['attempt_id'] for x in attempts}, 'Invalid selected attempt.')
        if choice is not None:
            require(attempts[-1]['attempt_id'] == choice, 'Cannot select an old pass over a later attempt.')

    if result['product_fixes_closed']:
        require(not critical and all(statuses.get(x) == 'PASSED' for x in FIX_GROUPS), 'Product fixes remain open.')
    delivery = result.get('dependency_delivery', {})
    require(delivery.get('status') in {'NOT_RUN', 'BLOCKED', 'LOCAL_SOURCE_PAIR_VERIFIED', 'VERIFIED'}, 'Invalid delivery status.')
    if delivery['status'] in {'LOCAL_SOURCE_PAIR_VERIFIED', 'VERIFIED'}:
        digest(delivery.get('consumer_components_commit'), HASH40, 'consumer Components commit')
        text(delivery.get('intended_consumer'), 'intended dependency consumer')
        require('dependency' in evidence(delivery.get('evidence'), root), 'Missing dependency manifest.')
    if ready:
        require(result['workspace_rendering_complete'] and result['product_fixes_closed'] and not critical,
                'Workspace/finding closure incomplete.')
        checkpoint(result.get('accepted_checkpoint'))
        for cid, case in expected.items():
            if case['required']:
                require(statuses[cid] == 'PASSED', f'Required group is not passing: {cid}')
                require(selected[cid]['checkpoint'] == result['accepted_checkpoint'], f'Stale final source: {cid}')
        require(delivery['status'] == 'VERIFIED', 'Dependency delivery not verified.')
        require(delivery['consumer_components_commit'] == result['accepted_checkpoint']['components_commit'],
                'Consumer dependency differs from tested source.')
    return {'groups': len(actual), 'attempts': len(attempt_ids), 'statuses': statuses,
            'live_executions': len(executions), 'reserved_requests': total, 'ready_for_next_module': ready}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--evidence-root', type=Path)
    parser.add_argument('--require-ready', action='store_true')
    args = parser.parse_args()
    try:
        result = validate(json.loads(args.plan.read_text(encoding='utf-8')),
                          json.loads(args.results.read_text(encoding='utf-8')),
                          args.evidence_root, args.require_ready)
    except (OSError, ValueError, TypeError, KeyError) as exc:
        print(f'FAIL: {exc}')
        return 1
    print(json.dumps(result, indent=2))
    print('Bookkeeping/hash check only; authorization and product execution authenticity are not certified.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
