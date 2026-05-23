# SB02 Proof Manifest

## Status

- `Not started`

## Required Evidence

- Changed-file hashes for prompt/policy/validation changes.
- Test transcript proving server-hosted Blazor output is rejected when contract selects WASM/static/no backend.
- Test or source assertion proving output root must match the contract unless a contract revision exists.
- Anti-stub audit transcript.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Required Negative Test |
| --- | --- | --- | --- | --- |
| Delivery contract mode/root constraint | Contract step artifact and grounding service | Implementation prompt, validation prompt, proof validation | Produced by contract step, carried to downstream steps, verified against actual output | `Microsoft.NET.Sdk.Web` server app under alternate root fails for WASM/static contract. |
| Contract revision requirement | Process runtime/prompt | Executor and manager | Required before changing selected mode/root | Silent SSR switch fails. |

## Planned Transcript Paths

- `bundle://proof/SB02/transcripts/failing-first.txt`
- `bundle://proof/SB02/transcripts/passing-tests.txt`
- `bundle://proof/SB02/transcripts/source-assertions.txt`
- `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
