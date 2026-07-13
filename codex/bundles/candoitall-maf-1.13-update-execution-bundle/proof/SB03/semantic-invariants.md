# SB03 Semantic Invariants

## INV-SB03-MAF-113-SKILL-APPROVAL-COMPATIBILITY

Raw note owned: update MAF packages while preserving current CanDoItAll agent behavior.

Expected behavior: ordinary skill capabilities expose the MAF skills provider without requiring approval for read-only skill operations or script execution when script approval was not configured; capabilities that explicitly require script approval still mark script execution as approval-required.

Disallowed shallow implementation: deleting the retired `UseScriptApproval` call and accepting MAF 1.13 defaults. That would compile, but it would silently approval-gate all skills provider tools and change chat behavior.

Semantic positive proof: `bundle://proof/SB03/transcripts/maf-composition-tests.md` shows focused tests pass for both non-approval and script-approval skill capability cases through `RuntimeCapabilityComposer`.

Adversarial negative proof: `bundle://proof/SB03/transcripts/build-failing-first.md` captured the package-induced compile break on the removed `UseScriptApproval` API before the fix.

Production assertions: `bundle://proof/SB03/transcripts/source-assertions.md` proves no direct process runtime provider or process route expansion was introduced, and the compatibility code uses MAF 1.13 provider options.

Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub.md` found no placeholder, stub, or silent fallback patterns in the changed SB03 source files.

Downstream dependency check: `SB04` may start because the adapter compatibility diff is bounded and whole-solution Release build proof exists.

## Production Behavior Artifact Matrix

No new production signal, state record, external route, process tool provider, or workflow surface was introduced in `SB03`.
