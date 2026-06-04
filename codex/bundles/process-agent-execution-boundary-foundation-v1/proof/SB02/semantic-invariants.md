# SB02 Semantic Invariants

## Invariant SB02-RQ003

- Invariant ID: `SB02-RQ003`
- Source raw note: "Inventory process boundary" and "Produce source-backed inventory of dispatcher partials and AgentFramework usages."
- Expected behavior: The bundle records direct AgentFramework usage by file, line count, usage kind, dispatcher status, and proposed owner before production movement starts.
- Disallowed shallow implementation: Listing only one execution file or only high-level prose without source-backed line counts and direct-call scan proof.
- Failing-first test: N/A - no production behavior changed in this process inventory gate; `bundle://proof/SB02/transcripts/direct-execution-call-scan.txt` is the adversarial scan that exposes missed direct calls.
- Passing test: `bundle://proof/SB02/transcripts/direct-execution-call-scan.txt` and `bundle://proof/SB02/transcripts/dispatcher-partial-line-counts.txt`.
- Changed source files: No production source files changed in SB02; inventory hash is recorded in `bundle://proof/SB02/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB02/source-assertions/inventory-closure.md`.
- Red-team negative case: A dispatcher direct call omitted from `bundle://inventories/02-agentframework-usage-in-processes.md` would appear in `bundle://proof/SB02/transcripts/direct-execution-call-scan.txt` and must reopen SB02.
- Downstream dependency check: SB03/SB06 must use the inventory cutline before defining or moving the execution facade.

## Invariant SB02-RQ013

- Invariant ID: `SB02-RQ013`
- Source raw note: "Do not run small, medium, or mobile UI validation."
- Expected behavior: SB02 is service/source inventory only and records browser validation as N/A.
- Disallowed shallow implementation: Producing unrelated viewport screenshots while inventorying service coupling.
- Failing-first test: N/A - no production behavior changed in this process inventory gate.
- Passing test: `bundle://proof/SB02/transcripts/hashes.txt`.
- Changed source files: No production source files changed in SB02; bundle inventory/proof files changed only.
- Production assertions: `bundle://proof/SB02/source-assertions/inventory-closure.md`.
- Red-team negative case: Any small/medium/mobile screenshot or viewport row would violate the large-screen-only policy.
- Downstream dependency check: Later subbundles inherit the same browser N/A policy unless UI changes.
