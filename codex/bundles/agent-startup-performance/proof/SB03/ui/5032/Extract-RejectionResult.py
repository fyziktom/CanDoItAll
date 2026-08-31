import hashlib
import importlib.util
import json
from datetime import datetime, timezone
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
RUN_ID = '3908f2a2-35f6-4009-80f5-f2d69e619bcd'
CALL_ID = 'call_3Ut7txS4EYKYA65Ifg8JSDC7'


def main():
    helper = HERE.parent / 'Extract-FileRunEvidence.py'
    spec = importlib.util.spec_from_file_location('file_evidence', helper)
    file_evidence = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(file_evidence)
    if hashlib.sha256(file_evidence.HELPER.read_bytes()).hexdigest() != file_evidence.EXPECTED_HELPER_HASH:
        raise ValueError('Reviewed helper changed.')
    spec = importlib.util.spec_from_file_location('ui_evidence', file_evidence.HELPER)
    evidence = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(evidence)
    snapshot = evidence.Snapshot(evidence.EXECUTION_ROOTS['native'])
    snapshot.assert_no_journal()
    path = snapshot.root / 'runs' / RUN_ID.replace('-', '') / 'run.json'
    run = snapshot.read(path)
    if evidence.guid(run['id']) != RUN_ID:
        raise ValueError('Run identity mismatch.')
    envelope = file_evidence.decode(run['serializedSessionStateJson'], evidence)
    state = file_evidence.decode(envelope['payloadJson'], evidence)
    results = []
    for node in file_evidence.walk(state):
        if file_evidence.first(node, file_evidence.CALL_KEYS) != CALL_ID or not any(key in node for key in file_evidence.RESULT_KEYS):
            continue
        result = file_evidence.first(node, file_evidence.RESULT_KEYS)
        if not isinstance(result, str):
            raise ValueError('Rejection result has unexpected type.')
        lower = result.lower()
        item = {'callId': CALL_ID, 'resultIsString': True, 'characterCount': len(result), 'utf8Sha256': hashlib.sha256(result.encode('utf-8')).hexdigest(), 'containsRejected': 'rejected' in lower, 'containsDeclined': 'declined' in lower, 'containsNotApproved': 'not approved' in lower, 'containsNotExecuted': 'not executed' in lower, 'rawResultPublished': False}
        if item not in results:
            results.append(item)
    if len(results) != 1:
        raise ValueError('Expected exactly one paired rejection result.')
    snapshot.verify_unchanged()
    print(json.dumps({'capturedUtc': datetime.now(timezone.utc).isoformat(), 'runId': RUN_ID, 'runSourceSha256': snapshot.files[path], 'results': results, 'sourceUnchangedAtRecheck': True, 'appCalls': False, 'sourceWrites': False, 'limits': 'Only the exact authorized rejection run read. Lexical rejection markers corroborate the paired scalar result; absence of execution is established separately by rejected durable approval and zero before/after execution receipts, not by the Invoking progress title.'}, indent=2))


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print('Rejection extraction refused; no source content emitted.', file=sys.stderr)
        sys.exit(1)
