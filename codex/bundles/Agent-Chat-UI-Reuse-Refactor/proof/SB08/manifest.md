# SB08 governed proof manifest

- Status: completed
- Proof tier: Governed
- Owned requirements: UIR-004, UIR-012, UIR-014, UIR-016, UIR-017, UIR-018, UIR-019, UIR-024, UIR-025, UIR-031, UIR-033, UIR-044, UIR-045, UIR-046, UIR-054, UIR-061, UIR-064, UIR-073, UIR-075, UIR-077
- Raw note: migrate every live Agent consumer through the neutral presentation boundary, remove superseded duplication, prove dependency direction, and do not activate Simple Chats.

## Portable evidence

- Semantic contract: `bundle://proof/SB08/semantic-invariants.md`
- Consumer inventory/closure: `bundle://proof/SB08/consumer-migration.md`
- Changed files/ranges: `bundle://proof/SB08/changed-files-and-ranges.json`
- SHA-256 manifest: `bundle://proof/SB08/changed-file-hashes.json`
- Impact request/response: `bundle://proof/SB08/impacted-tests-request.json`, `bundle://proof/SB08/impacted-tests-response.json`
- Architecture review: `bundle://proof/SB08/architecture-review.md`
- Source assertions/anti-stub audit: `bundle://proof/SB08/source-guards.md`
- Command transcript: `bundle://proof/SB08/transcripts/validation.txt`
- Supporting UI proof: `bundle://proof/SB05/browser-parity.md`, `bundle://proof/SB07/browser-parity.md`
- Supporting downstream proof: 81/81 contextual/Process/Agent consumer filter and the Processes build in `bundle://proof/SB08/transcripts/validation.txt`

## Negative and positive proof

SB08 introduced no production behavior after SB07, so a new failing-first test would be artificial. The architecture closure reuses the behavior-changing subbundle tests and adds a fresh cross-consumer regression. Negative coverage includes invalid opaque-key rejection, fail-closed selection, and stale contextual completion. Positive coverage includes participant selection, thread/workspace rendering, manager chat, floating lifecycle/settings, and the real Agent responses captured at CP2/CP3.

## Anti-stub decision

Pass. No `TODO`, `FIXME`, `NotImplementedException`, fixture-specific branch, template-only production output, or second presentation implementation exists in the new neutral/adapter path.

## Production behavior artifact matrix

See `bundle://proof/SB08/semantic-invariants.md`. Presentation artifacts are produced by Agent mappers, consumed by neutral components, recreated from live state, and protected by fail-closed negative tests.

## Progression

CP4 passes to SB09. Simple Chat UI remains inactive.
