from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
from uuid import UUID

REPO = Path(__file__).resolve().parents[7]
HELPER = REPO / '.artifacts/agent-startup-performance/diagnostics/Extract-UiRunEvidence.py'
REVIEWED = REPO / 'codex/bundles/agent-startup-performance/proof/SB03/ui/Extract-UiRunEvidence.py'
EXPECTED_TOOL = 'workspace_stat_path'
CALL_KEYS = ('callId', 'CallId', 'call_id')
NAME_KEYS = ('name', 'Name', 'functionName', 'FunctionName')
RESULT_KEYS = ('result', 'Result', 'output', 'Output')


def first(node, keys):
    values = [node[key] for key in keys if key in node]
    if len(values) > 1 and any(value != values[0] for value in values[1:]):
        raise ValueError('Conflicting structured protocol fields.')
    return values[0] if values else None


def walk(root):
    pending = [(root, 0)]
    count = 0
    while pending:
        node, depth = pending.pop()
        count += 1
        if count > 100000 or depth > 64:
            raise ValueError('Runtime state exceeds the bounded traversal.')
        if isinstance(node, dict):
            yield node
            pending.extend((value, depth + 1) for value in node.values() if isinstance(value, (dict, list)))
        elif isinstance(node, list):
            pending.extend((value, depth + 1) for value in node if isinstance(value, (dict, list)))


def classify_result(result, evidence):
    if isinstance(result, str):
        try:
            result = json.loads(result, object_pairs_hook=evidence.reject_duplicate_keys)
        except ValueError:
            return {'structuredJsonResult': False, 'missingPathTextMatched': 'does not exist' in result.lower()}
    if not isinstance(result, (dict, list)):
        return {'structuredJsonResult': False, 'missingPathTextMatched': False}
    flags = {'structuredJsonResult': True, 'succeededFalse': False, 'existsFalse': False, 'pathKindMissing': False, 'failedReceiptOrOutcome': False, 'missingPathTextMatched': False}
    for node in walk(result):
        flags['succeededFalse'] |= first(node, ('succeeded', 'Succeeded')) is False
        flags['existsFalse'] |= first(node, ('exists', 'Exists')) is False
        flags['pathKindMissing'] |= first(node, ('pathKind', 'PathKind')) == 'missing'
        for key in ('status', 'Status', 'outcome', 'Outcome', 'exitSummary', 'ExitSummary'):
            value = node.get(key)
            if isinstance(value, str):
                flags['failedReceiptOrOutcome'] |= value == 'Failed' or value.startswith('Failed: ')
        for key in ('message', 'Message', 'exitSummary', 'ExitSummary'):
            value = node.get(key)
            if isinstance(value, str):
                flags['missingPathTextMatched'] |= 'does not exist' in value.lower()
    return flags


def extract_state(run, evidence):
    serialized = run.get('serializedSessionStateJson')
    if serialized is None:
        return {'serializedStatePresent': False, 'calls': [], 'resultPairs': []}
    if not isinstance(serialized, str) or len(serialized.encode('utf-8')) > evidence.MAX_FILE_BYTES:
        raise ValueError('Runtime state is outside the reviewed size/type bound.')
    state = json.loads(serialized, object_pairs_hook=evidence.reject_duplicate_keys)
    calls = {}
    result_nodes = []
    for node in walk(state):
        call_id = first(node, CALL_KEYS)
        name = first(node, NAME_KEYS)
        function = node.get('function')
        if isinstance(function, dict):
            function_name = first(function, NAME_KEYS)
            if function_name == EXPECTED_TOOL:
                name = function_name
                if call_id is None and node.get('type') == 'function':
                    call_id = node.get('id')
        if name == EXPECTED_TOOL and call_id is not None:
            call_id = evidence.identifier(call_id)
            calls[call_id] = {'callId': call_id, 'toolName': EXPECTED_TOOL}
        if call_id is not None and any(key in node for key in RESULT_KEYS):
            result_nodes.append((evidence.identifier(call_id), first(node, RESULT_KEYS)))
    pairs = []
    for call_id, result in result_nodes:
        if call_id in calls:
            item = {'callId': call_id, **classify_result(result, evidence)}
            if item not in pairs:
                pairs.append(item)
    return {'serializedStatePresent': True, 'calls': sorted(calls.values(), key=lambda item: item['callId']), 'resultPairs': pairs}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--host', choices=('native', 'client'), required=True)
    parser.add_argument('--run-id', action='append', required=True)
    args = parser.parse_args()
    if len(args.run_id) != 2 or len(set(args.run_id)) != 2:
        raise ValueError('Supply exactly the two root-authorized run IDs in conversation order.')
    helper_hash = hashlib.sha256(HELPER.read_bytes()).hexdigest()
    if helper_hash != hashlib.sha256(REVIEWED.read_bytes()).hexdigest():
        raise ValueError('Reviewed helper identity mismatch.')
    spec = importlib.util.spec_from_file_location('reviewed_ui_evidence', HELPER)
    evidence = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(evidence)
    snapshot = evidence.Snapshot(evidence.EXECUTION_ROOTS[args.host])
    snapshot.assert_no_journal()
    results = []
    preceding_calls = set()
    expected_session = None
    for requested in args.run_id:
        run_id = evidence.guid(requested)
        run = snapshot.read(snapshot.root / 'runs' / UUID(run_id).hex / 'run.json')
        if evidence.guid(run['id']) != run_id:
            raise ValueError('Run identity mismatch.')
        session_id = evidence.guid(run['chatSessionId'])
        if expected_session is not None and session_id != expected_session:
            raise ValueError('The authorized pair does not share a session.')
        expected_session = session_id
        extracted = extract_state(run, evidence)
        observed_ids = {item['callId'] for item in extracted['calls']}
        new_ids = sorted(observed_ids - preceding_calls)
        results.append({'runId': run_id, 'sessionId': session_id, **extracted, 'newCallIdsRelativeToPrecedingAuthorizedRun': new_ids, 'precedingCallIdsRetainedInThisState': preceding_calls <= observed_ids, 'callIdAvailable': bool(observed_ids), 'rawArgumentsOrResultsPublished': False})
        preceding_calls = observed_ids
    snapshot.verify_unchanged()
    print(json.dumps({'host': args.host, 'capturedUtc': datetime.now(timezone.utc).isoformat(), 'reviewedHelperSha256': helper_hash, 'sourceRunFileCount': len(snapshot.files), 'sourceRecordsUnchangedAtRecheck': True, 'appCalls': False, 'sourceWrites': False, 'runs': results, 'limits': 'Only the two specified run.json files were read. Runtime state may retain earlier calls; new IDs are a set difference against the preceding authorized run, not an independently persisted executionRunId on the call. No message, prompt, argument, path or raw result text is emitted. Receipt association remains separately tied to the exact run and single-call metric.'}, indent=2))


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print('Structured extraction refused: bounded schema, identity, snapshot or parsing check failed; no source content emitted.', file=sys.stderr)
        sys.exit(1)