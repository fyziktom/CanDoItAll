from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
from uuid import UUID

REPO = Path(__file__).resolve().parents[6]
HELPER = REPO / 'codex/bundles/agent-startup-performance/proof/SB03/ui/Extract-UiRunEvidence.py'
EXPECTED_HELPER_HASH = '65ef0b4e059d8b20370cc364da4dd27b6d892b9aa37bc31b1dd9a05a732254de'
REFERENCE = HELPER.parent / 'reference-facts.json'
TOOLS = frozenset(('load_skill', 'workspace_list_directory', 'workspace_read_file', 'workspace_spreadsheet_summary', 'workspace_read_spreadsheet_range', 'workspace_convert_document'))
CALL_KEYS = ('callId', 'CallId', 'call_id')
NAME_KEYS = ('name', 'Name', 'functionName', 'FunctionName')
RESULT_KEYS = ('result', 'Result', 'output', 'Output')


def first(node, keys):
    values = [node[key] for key in keys if key in node]
    if len(values) > 1 and any(value != values[0] for value in values[1:]):
        raise ValueError('Conflicting structured protocol fields.')
    return values[0] if values else None


def prop(node, name):
    return first(node, (name, name[0].upper() + name[1:]))


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


def decode(value, evidence):
    if isinstance(value, str):
        if len(value.encode('utf-8')) > evidence.MAX_FILE_BYTES:
            raise ValueError('Structured payload exceeds the bound.')
        return json.loads(value, object_pairs_hook=evidence.reject_duplicate_keys)
    return value


def asset_index(value, assets):
    if not isinstance(value, str):
        return None
    matches = [index for index, asset in enumerate(assets) if value == asset['workspaceRelativePath']]
    if len(matches) > 1:
        raise ValueError('Ambiguous approved asset reference.')
    return matches[0] if matches else None


def argument_evidence(arguments, assets, evidence):
    arguments = decode(arguments, evidence)
    if not isinstance(arguments, dict):
        return {'structuredArgumentsPresent': False}
    source = first(arguments, ('path', 'Path', 'workbookPath', 'WorkbookPath'))
    index = asset_index(source, assets)
    result = {'structuredArgumentsPresent': True, 'sourceMatchesApprovedAssetIndex': index}
    for name in ('previewCharacters', 'maxCharacters', 'maxRows', 'maxColumns'):
        value = prop(arguments, name)
        if value is not None:
            result[name] = evidence.number(value)
    worksheet = prop(arguments, 'worksheetName')
    if worksheet is not None:
        result['worksheetMatchesPricing'] = worksheet == 'Pricing'
    address = prop(arguments, 'rangeAddress')
    if address is not None:
        result['rangeAddress'] = address if isinstance(address, str) and re.fullmatch(r'[A-Z]{1,3}[1-9][0-9]{0,6}:[A-Z]{1,3}[1-9][0-9]{0,6}', address) else 'UnclassifiedRedacted'
    if 'outputPath' in arguments or 'OutputPath' in arguments:
        result['defaultOutputPathRequested'] = prop(arguments, 'outputPath') in (None, '')
    return result


def result_evidence(value, assets, evidence):
    try:
        result = decode(value, evidence)
    except (ValueError, json.JSONDecodeError):
        return {'structuredJsonResult': False}
    if not isinstance(result, dict):
        return {'structuredJsonResult': False}
    output = {'structuredJsonResult': True}
    for name in ('succeeded', 'isTruncated'):
        found = prop(result, name)
        if found is not None:
            output[name] = evidence.boolean(found)
    source = first(result, ('path', 'Path', 'sourcePath', 'SourcePath', 'workbookPath', 'WorkbookPath'))
    index = asset_index(source, assets)
    output['resultSourceMatchesApprovedAssetIndex'] = index
    content = prop(result, 'content')
    if isinstance(content, str):
        digest = hashlib.sha256(content.encode('utf-8')).hexdigest()
        output['contentUtf8Sha256'] = digest
        output['contentCharacterCount'] = len(content)
        output['contentExactlyMatchesApprovedAssetSha256'] = index is not None and digest == assets[index]['sha256']
    preview = prop(result, 'markdownPreview')
    if preview is not None:
        if not isinstance(preview, str):
            raise ValueError('Markdown preview has an unexpected type.')
        output['markdownPreviewCharacterCount'] = len(preview)
        output['markdownPreviewNonempty'] = bool(preview)
    path = prop(result, 'outputPath')
    if path is not None:
        if not isinstance(path, str):
            raise ValueError('Output path has an unexpected type.')
        output['outputDiffersFromAllApprovedSourceAssets'] = all(path != asset['workspaceRelativePath'] for asset in assets)
        output['outputMarkdownExtension'] = path.lower().endswith('.md')
    receipt = prop(result, 'receipt')
    if isinstance(receipt, dict):
        receipt_id = prop(receipt, 'id')
        if receipt_id is not None:
            output['receiptId'] = evidence.guid(receipt_id)
        run_id = prop(receipt, 'executionRunId')
        if run_id is not None:
            output['receiptExecutionRunId'] = evidence.guid(run_id)
        for name in ('startedAtUtc', 'completedAtUtc'):
            found = prop(receipt, name)
            if found is not None:
                output['receipt' + name[0].upper() + name[1:]] = evidence.timestamp(found)
        for name in ('mutatesWorkspace', 'succeeded'):
            found = prop(receipt, name)
            if found is not None:
                output['receipt' + name[0].upper() + name[1:]] = evidence.boolean(found)
        status = first(receipt, ('outcome', 'Outcome', 'exitSummary', 'ExitSummary'))
        if isinstance(status, str):
            output['receiptOutcome'] = evidence.receipt_outcome(status)
    values = prop(result, 'values')
    if isinstance(values, list):
        expected = (('ZM-x5600', '35000', '39900', '42000'), ('ZM-x6600', '41500', '46000', '49000'), ('ZM-x6600A', '66000', '73000', '78000'))
        rows = [[str(cell).strip() for cell in row] for row in values if isinstance(row, list)]
        output['referencePriceRowsMatched'] = [list(row) for row in expected if any(all(cell in actual for cell in row) for actual in rows)]
    return output


def extract(run, assets, evidence):
    serialized = run.get('serializedSessionStateJson')
    if serialized is None:
        return {'serializedStatePresent': False, 'calls': [], 'resultPairs': []}
    state = decode(serialized, evidence)
    envelope = isinstance(state, dict) and 'payloadJson' in state
    if envelope:
        state = decode(state['payloadJson'], evidence)
    calls = {}
    results = []
    for node in walk(state):
        call_id = first(node, CALL_KEYS)
        name = first(node, NAME_KEYS)
        args = first(node, ('arguments', 'Arguments'))
        function = node.get('function')
        if isinstance(function, dict):
            name = first(function, NAME_KEYS)
            args = first(function, ('arguments', 'Arguments'))
            if call_id is None and node.get('type') == 'function':
                call_id = node.get('id')
        if call_id is not None and name is not None:
            if name not in TOOLS:
                raise ValueError('An unexpected tool needs separate scope review.')
            key = evidence.identifier(call_id)
            item = {'callId': key, 'toolName': name, **argument_evidence(args, assets, evidence)}
            if key in calls and calls[key] != item:
                raise ValueError('Conflicting call records.')
            calls[key] = item
        if call_id is not None and any(key in node for key in RESULT_KEYS):
            results.append((evidence.identifier(call_id), first(node, RESULT_KEYS)))
    pairs = []
    for call_id, result in results:
        if call_id in calls:
            item = {'callId': call_id, 'toolName': calls[call_id]['toolName'], **result_evidence(result, assets, evidence)}
            if item not in pairs:
                pairs.append(item)
    return {'serializedStatePresent': True, 'runtimeEnvelopePayloadDecoded': envelope, 'calls': sorted(calls.values(), key=lambda item: item['callId']), 'resultPairs': sorted(pairs, key=lambda item: item['callId'])}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--host', choices=('native', 'client'), required=True)
    parser.add_argument('--run-id', action='append', required=True)
    args = parser.parse_args()
    if not 1 <= len(args.run_id) <= 6 or len(set(args.run_id)) != len(args.run_id):
        raise ValueError('Supply one to six distinct root-authorized run IDs.')
    helper_hash = hashlib.sha256(HELPER.read_bytes()).hexdigest()
    if helper_hash != EXPECTED_HELPER_HASH:
        raise ValueError('Reviewed helper identity mismatch.')
    spec = importlib.util.spec_from_file_location('reviewed_ui_evidence', HELPER)
    evidence = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(evidence)
    reference = json.loads(REFERENCE.read_text(encoding='utf-8-sig'), object_pairs_hook=evidence.reject_duplicate_keys)
    snapshot = evidence.Snapshot(evidence.EXECUTION_ROOTS[args.host])
    snapshot.assert_no_journal()
    extracted = []
    for requested in args.run_id:
        run_id = evidence.guid(requested)
        path = snapshot.root / 'runs' / UUID(run_id).hex / 'run.json'
        run = snapshot.read(path)
        if evidence.guid(run['id']) != run_id:
            raise ValueError('Run identity mismatch.')
        extracted.append({'runId': run_id, 'sessionId': evidence.guid(run['chatSessionId']), 'runSourceSha256': snapshot.files[path], **extract(run, reference[args.host]['assets'], evidence)})
    snapshot.verify_unchanged()
    print(json.dumps({'host': args.host, 'capturedUtc': datetime.now(timezone.utc).isoformat(), 'reviewedHelperSha256': helper_hash, 'referenceFactsSha256': hashlib.sha256(REFERENCE.read_bytes()).hexdigest(), 'sourceRunFileCount': len(snapshot.files), 'sourceRecordsUnchangedAtRecheck': True, 'appCalls': False, 'sourceWrites': False, 'rawArgumentsOrResultsPublished': False, 'runs': extracted, 'limits': 'Only explicitly supplied run.json files and existing proof helpers/reference facts read. Structured calls/results are decoded from the known runtime envelope, never arbitrary message strings. Runtime snapshots may retain or omit prior calls; call IDs do not independently persist an executionRunId. Result receipts with IDs can be joined to exact-run receipt records. No raw text, prompt, source path, provider data or content is emitted. Exact string hashes intentionally do not normalize line endings/BOMs or silently treat mismatches as equivalent.'}, indent=2))


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print('File evidence extraction refused: bounded schema, identity, scope, snapshot or parsing check failed; no source content emitted.', file=sys.stderr)
        sys.exit(1)

