from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def stamp(value):
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


def save(path, value):
    if path.exists():
        raise ValueError('Refusing to overwrite immutable summary.')
    path.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')


for host, port, persisted in (('native', '5032', 'files-two-run-persisted.json'), ('client', '5214', 'files-three-run-persisted.json')):
    folder = ROOT / port
    runtime = read(folder / persisted)
    structured = read(folder / 'files-structured-calls.json')
    by_id = {run['runId']: run for run in runtime['runs']}
    joined = []
    for calls in structured['runs']:
        run = by_id[calls['runId']]
        if run['state'] != 'Completed' or run['outcome'] != 'Succeeded' or run['pendingApprovalCount'] != 0:
            raise ValueError('Expected a completed source-file test run.')
        pairs = []
        for pair in calls['resultPairs']:
            if 'receiptExecutionRunId' not in pair:
                continue
            if pair['receiptExecutionRunId'] != run['runId']:
                raise ValueError('Structured result belongs to a different run.')
            receipts = [receipt for receipt in run['receipts'] if receipt['toolName'] == pair['toolName'] and stamp(receipt['startedAtUtc']) == stamp(pair['receiptStartedAtUtc']) and stamp(receipt['completedAtUtc']) == stamp(pair['receiptCompletedAtUtc'])]
            if len(receipts) != 1 or pair['receiptOutcome'] != 'Succeeded':
                raise ValueError('Expected a unique matching successful execution receipt.')
            pairs.append({'callId': pair['callId'], 'toolName': pair['toolName'], 'executionRunId': pair['receiptExecutionRunId'], 'durableReceiptId': receipts[0]['id'], 'receiptOutcomeFromStructuredResult': pair['receiptOutcome'], 'mutatesWorkspace': pair['receiptMutatesWorkspace'], 'joinedBy': 'Exact executionRunId, toolName, startedAtUtc and completedAtUtc'})
        approved_calls = [item for item in calls['calls'] if item['toolName'] in ('workspace_read_file', 'workspace_convert_document', 'workspace_spreadsheet_summary', 'workspace_read_spreadsheet_range')]
        if any(item['sourceMatchesApprovedAssetIndex'] is None for item in approved_calls):
            raise ValueError('A source-file tool argument does not match an approved source.')
        content_pairs = [item for item in calls['resultPairs'] if item['toolName'] == 'workspace_read_file']
        if any(not item.get('contentExactlyMatchesApprovedAssetSha256') or item.get('isTruncated') is not False or item.get('succeeded') is not True for item in content_pairs):
            raise ValueError('An actual read did not return the exact approved source.')
        joined.append({'runId': run['runId'], 'sessionId': run['chatSessionId'], 'state': run['state'], 'outcome': run['outcome'], 'recordCounts': run['recordCounts'], 'actualSerializedCallCount': len(calls['calls']), 'structuredResultPairCount': len(calls['resultPairs']), 'approvedSourceFileCallCount': len(approved_calls), 'allSourceFileCallArgumentsMatchApprovedAssets': True, 'exactWholeFileReadContentHashMatchCount': len(content_pairs), 'joinedResultReceipts': pairs, 'unjoinedSkillCalls': len([item for item in calls['calls'] if item['toolName'] == 'load_skill']), 'sessionCurrentMessageCount': run['sessionSnapshot']['messageCount']})
    save(folder / 'files-verification-summary.json', {'capturedUtc': datetime.now(timezone.utc).isoformat(), 'host': host, 'status': 'Passed', 'source': [persisted, 'files-structured-calls.json', '../reference-facts.json', '../file-fixtures-final.json'], 'runs': joined, 'limits': 'Persisted supporting evidence, not a substitute for root-observed UI proof. Session counts describe the whole session at capture. Original asset integrity comes from final SHA256/size verification; lexical output-path inequality alone is not a canonical-path or no-write guarantee. The native conversion intentionally creates a derived Markdown artifact. Metrics and usage overlap and are not added together. No live app/provider requests, builds, tests or source changes were made by this summary.'})

folder = ROOT / '5032'
before = read(folder / 'files-before-approval-persisted.json')['runs'][0]
after = read(folder / 'files-after-approval-persisted.json')['runs'][0]
if before['runId'] != after['runId'] or before['approvals'][0]['status'] != 'Pending' or after['approvals'][0]['status'] != 'Approved':
    raise ValueError('Approval transition mismatch.')
approval = after['approvals'][0]
conversion = [receipt for receipt in after['receipts'] if receipt['toolName'] == 'workspace_convert_document']
if any(receipt['toolName'] == 'workspace_convert_document' for receipt in before['receipts']) or len(conversion) != 1 or stamp(conversion[0]['startedAtUtc']) <= stamp(approval['decidedAtUtc']):
    raise ValueError('Conversion execution ordering mismatch.')
save(folder / 'approval-verification-summary.json', {'capturedUtc': datetime.now(timezone.utc).isoformat(), 'status': 'Passed', 'runId': after['runId'], 'sessionId': after['chatSessionId'], 'approvalId': approval['approvalId'], 'callId': approval['callId'], 'beforeState': before['state'], 'beforeApprovalStatus': 'Pending', 'beforeConversionReceiptCount': 0, 'rootSingleApproveClickUtc': '2026-08-31T17:42:36.514Z', 'durableDecisionUtc': approval['decidedAtUtc'], 'afterApprovalStatus': 'Approved', 'afterState': after['state'], 'afterOutcome': after['outcome'], 'afterConversionReceiptCount': 1, 'conversionReceiptId': conversion[0]['id'], 'conversionStartedAfterDecision': True, 'decisionToConversionStartSeconds': (stamp(conversion[0]['startedAtUtc']) - stamp(approval['decidedAtUtc'])).total_seconds(), 'pendingApprovalDurationSeconds': (stamp(approval['decidedAtUtc']) - stamp(approval['requestedAtUtc'])).total_seconds(), 'totalRunElapsedSeconds': (stamp(after['completedAtUtc']) - stamp(after['createdAtUtc'])).total_seconds(), 'excludedFromStartupPerformanceSamples': True, 'intentionalReviewerApprovalWaitIncluded': True, 'initialAndResumedMetrics': [{'outcome': metric['outcome'], 'toolCalls': metric['toolCalls'], 'durationMs': metric['durationMs']} for metric in sorted(after['metrics'], key=lambda value: stamp(value['createdAtUtc']))], 'afterApprovalSessionMessageCount': after['sessionSnapshot']['messageCount'], 'beforeSnapshotSha256': hashlib.sha256((folder / 'files-before-approval-persisted.json').read_bytes()).hexdigest(), 'limits': 'The initial metric records Cancelled at approval suspension; the final durable run is Completed/Succeeded. This is not proof of a user-cancelled run. A UI busy-handle Stop action did not establish persisted cancellation: root reopened the same pending approval and later explicitly approved it. Low-level receipt approval mode is distinct from the outer durable Approved decision. Conversion call/result and receipt joins are in files-structured-calls.json and files-verification-summary.json.'})

before = read(folder / 'rejection-before-persisted.json')['runs'][0]
after = read(folder / 'rejection-after-persisted.json')['runs'][0]
paired = read(folder / 'rejection-paired-result.json')
if before['runId'] != after['runId'] or before['approvals'][0]['status'] != 'Pending' or after['approvals'][0]['status'] != 'Rejected' or before['receipts'] or after['receipts']:
    raise ValueError('Rejection transition mismatch.')
if paired['results'][0]['callId'] != after['approvals'][0]['callId'] or not paired['results'][0]['containsRejected']:
    raise ValueError('Rejection call/result identity mismatch.')
save(folder / 'rejection-verification-summary.json', {'capturedUtc': datetime.now(timezone.utc).isoformat(), 'status': 'Passed', 'runId': after['runId'], 'sessionId': after['chatSessionId'], 'approvalId': after['approvals'][0]['approvalId'], 'callId': after['approvals'][0]['callId'], 'beforeState': before['state'], 'beforeApprovalStatus': 'Pending', 'beforeReceiptCount': 0, 'rootSingleRejectClickUtc': '2026-08-31T17:55:54.497Z', 'durableDecisionUtc': after['approvals'][0]['decidedAtUtc'], 'afterApprovalStatus': 'Rejected', 'afterState': after['state'], 'afterOutcome': after['outcome'], 'afterPendingApprovalCount': after['pendingApprovalCount'], 'afterReceiptCount': 0, 'pairedScalarResultContainsRejected': True, 'afterLogCount': after['recordCounts']['logs'], 'afterSessionMessageCount': after['sessionSnapshot']['messageCount'], 'beforeSnapshotSha256': hashlib.sha256((folder / 'rejection-before-persisted.json').read_bytes()).hexdigest(), 'limits': 'The Invoking progress title describes the wrapper path; it does not prove that conversion ran. Rejected durable approval, the same-call rejection result, and zero before/after execution receipts support enforcement. The agent conversation completed successfully after declining the tool: this is not a Failed provider/run or actual running-turn cancellation test. No raw result, prompt or source contents are published.'})
print(json.dumps({'summaryFiles': ['5032/files-verification-summary.json', '5214/files-verification-summary.json', '5032/approval-verification-summary.json', '5032/rejection-verification-summary.json'], 'status': 'Passed', 'source': 'Existing sanitized proof only; no live reads or calls.'}))
