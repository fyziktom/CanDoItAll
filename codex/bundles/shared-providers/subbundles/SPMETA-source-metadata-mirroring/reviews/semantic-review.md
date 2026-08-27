# SPMETA adversarial semantic verifier

Reviewer: primary agent. This is an explicit re-read of final production diff, raw input
mapping, proof manifest, failing and passing TRX results, runtime transcript and visible
screenshots; it is not independent-agent sign-off.

## Challenges and results

1. Pretty labels could conceal broken routing. The final source catalog retains opaque IDs,
   mapper carries separate names, component test asserts emitted IDs, integration/runtime
   evidence identifies the intended upstream models. No hash decoding or ID replacement.
2. A plausible price table could still be OpenAI defaults. The UI compares the exact published
   source model set and all nine rate fields; Ollama has one row, real source rate 0.12,
   and private=true. Empty imported prices stay empty in production mapper/editor tests.
   Central price-only rows are intentionally not published as models.
3. Both UIs could agree on an incorrect private value. This challenge invalidated preliminary
   image-2 passes. Actual failing-first tests reproduced both toggle directions; the final
   UI reopens source settings and asserts the operator-requested value after save before
   comparing client. Final source/client screenshots show checked after resync.
4. Import-only proof could miss stale caches. Final UI changes rate 1.23 -> 9.87, false -> true,
   removes a secondary model, resynchronizes and sees one model. Canonical tests change every
   price field independently; imported IDs/default routing IDs remain stable in real DB reads.
5. An incompatible snapshot could take down management. Strict snapshot validation is shared
   by management/runtime; old schema is explicit IncompatibleContract/SnapshotInvalid, never
   guessed. Source controls remain reachable and final UI synchronization succeeds.
6. Green UI text could be seeded. The test starts real chats and tool approval through UI.
   Central records are read-only queried, not inserted; repeat asserts eight complete successes
   and eight upstream 200 responses. A new on-disk PNG has the correct signature and a run-time
   modification timestamp. Vision capture contains image content. No fixture branch exists
   in changed production code.
7. A broad test claim could conceal incomplete tooling. 161+217+46+38 selected test executions
   pass; the exact filters/discovery are in transcripts. No CodeAnalytics selection result was
   obtained. No whole-repo, full graph, live-provider, paid-provider or billing claim is made.
8. A successful local build could leave stale containers. Both running engines identify image
   spmeta-20260827-3; final Release Docker build history records the matching image digest.
   Both final UI passes and runtime proof were collected after deploying that image.

## Negative/positive artifact adequacy

The three initial metadata tests failed before implementation and pass in the final unit
lane; the rerender test failed before its correction and passes in final components; both
private-toggle cases failed before the save fix and pass afterward. Validate-Closure.ps1
matches failed test names to final passing results and verifies counts, required paths,
hashes, runtime assertions and original SB07 status.

Fixture records used by pure parser/materializer unit tests are boundary inputs, not forged
runtime completion signals. Production usage records and image artifacts are emitted by
real application paths. Screenshots alone are not treated as execution proof.

## Decision and explicit limits

PASS the SPMETA metadata repair, subject to the accompanying machine artifact check.
Original SB07 remains blocked. The retained two-instance databases are upgrade/resync fixtures,
not newly wiped empty-client installations. Test model responses are deterministic; the 68-byte
PNG is a fixture image. Ledger token/image usage is complete, but billed pricing remains
Unavailable. None of those limitations is disguised as a completed live-provider gate.

Reopen if the manifest/hash check fails, a requested source value does not survive reload,
labels leak route IDs, remote prices are replaced with driver defaults, or a real downstream
operation fails. Earlier failing/preliminary artifacts are chronology, not alternate proof.
