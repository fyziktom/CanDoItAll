# Process Module Adapter Boundary

## Transcript adapter decomposition
`ProcessTranscriptVerificationReadOnlyAdapter` must be split into smaller helpers while preserving public/internal behavior:
- request preflight policy,
- supplied evidence URI policy,
- SHA-256 hash policy,
- read-only operation policy,
- denied audit fact factory,
- observation envelope mapper.

## Runtime evidence adapter
The new runtime evidence adapter must:
- accept already-produced descriptor payloads and evidence references from callers,
- create verification request objects,
- call the runtime evidence verifier directly,
- return an immutable process-owned observation envelope,
- never persist observations in this bundle,
- never register itself in DI, manager commands, scheduler, workflow executor, or a generic runtime host.

## Allowed process-module references
Only explicitly named adapter files may reference driver packages. Architecture tests must fail if broader dispatch services, finalizers, transition services, or runtime orchestration import driver packages.

## Observation envelope
Observation envelopes are read-only and must include:
- process run id,
- step run id,
- optional artifact id,
- lane,
- accepted/denied,
- diagnostics,
- evidence references,
- audit facts,
- redaction descriptor,
- no-mutation flag,
- contract version,
- requested/observed timestamps.
