# Process Module Adapter Boundary

## Purpose
Process module adapters are allowed only as explicit, read-only consumers of supplied evidence payloads.

## Allowed
- Construct verification request from already-resolved content and references.
- Validate URI, hash, content type, size, permission, scope, and operation.
- Call one explicit verifier.
- Return immutable observation envelope.
- Include diagnostics, audit facts, redaction, evidence references, no-mutation proof.

## Denied
- File reads.
- Workspace/storage lookup.
- Database writes.
- Process state mutation.
- Artifact creation.
- Finalizer application.
- Transition/claim/retry mutation.
- Runtime registration or selector.
