# Manager Readonly Command Surface

## Goal
Expose a controlled manager/operator diagnostic command that can invoke the verification-only host over already-loaded process evidence and return diagnostics.

## Requirements
- Requires manager identity.
- Requires process run id and optional step/artifact scope.
- Reads existing process/runtime/artifact evidence only.
- Calls verification-only host with explicit lane payloads.
- Returns diagnostics, evidence references, audit ids, redaction descriptor, no-mutation flag, and host lane summaries.
- Does not apply transitions, finalizers, recovery packets, retry scheduling, artifact writes, workspace writes, provider repair, or external calls.

## UI/API proof
At minimum the bundle must provide API/service proof. UI proof is needed only if a browser-visible manager command surface is added.
