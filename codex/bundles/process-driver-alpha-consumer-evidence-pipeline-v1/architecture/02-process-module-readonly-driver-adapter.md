# Process Module Read-Only Driver Adapter

## Purpose
Create a process-owned adapter that can call the transcript verifier alpha from tests and controlled evidence/proof flows without creating a generic runtime driver subsystem.

## Required Shape
- Name should communicate narrowness, e.g. `ProcessTranscriptVerificationReadOnlyAdapter` or equivalent.
- Accept only already-supplied transcript content and evidence references.
- Validate hash before invoking the verifier.
- Return immutable observation/envelope data.
- Never write process state, artifacts, workspace files, storage, claims, transitions, finalizer state, or retry schedules.
- Do not register a generic driver registry/selector/host/manager command.

## Expected Inputs
- Process run id, step run id, artifact id/reference ids where available.
- Caller context.
- Permission mode and capability scope.
- Evidence reference(s).
- Transcript reference and transcript text.
- Optional Core descriptor family tag.

## Expected Outputs
- Accepted/denied state.
- Diagnostic list.
- Evidence references with content hashes.
- Audit facts.
- Redaction descriptor.
- NoMutationPerformed flag.
- Source-lane metadata (`DotNetRustTranscriptVerification`).
