# SB01 Semantic Invariants

## Invariant SB01-RQ001

- Invariant ID: `SB01-RQ001`
- Source raw note: "The previous provider seam remains intact" and "MAF stays product-tool-neutral and does not regain direct `Processes`, `Projects`, or `Workbench` references."
- Expected behavior: MAF does not reference product modules and runtime tool composition still resolves through registered providers.
- Disallowed shallow implementation: Passing SB01 by inspecting only the csproj while provider composition is broken or product-module references remain in source files.
- Failing-first test: N/A - no production behavior changed in this process gate; the adversarial scan in `bundle://proof/SB01/transcripts/maf-product-dependency-scan.txt` rejects the shallow case by scanning MAF source and project files.
- Passing test: `bundle://proof/SB01/transcripts/maf-provider-composition-test.txt`; test name `MafAgentRuntimeToolProviderCompositionTests`.
- Changed source files: No production source files changed in SB01; source hashes are recorded in `bundle://proof/SB01/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB01/source-assertions/provider-boundary.md` cites the MAF csproj, the dependency scan, and provider composition test.
- Red-team negative case: A direct `CanDoItAll.Modules.Processes`, `CanDoItAll.Modules.Projects`, or `CanDoItAll.Modules.Workbench` reference in MAF would appear in `bundle://proof/SB01/transcripts/maf-product-dependency-scan.txt` and fail the invariant.
- Downstream dependency check: SB02 and later movement subbundles may rely on the provider seam only after SB01 closure cites `proof/SB01/manifest.md` and this contract.

## Invariant SB01-RQ002

- Invariant ID: `SB01-RQ002`
- Source raw note: "Do not start the full Process Core split yet."
- Expected behavior: SB01 records baseline proof only and does not move EF entities, dispatcher logic, or process runtime behavior into a core project.
- Disallowed shallow implementation: Creating a broad core split or driver-pack shape while claiming it is branch hygiene.
- Failing-first test: N/A - no production behavior changed in this process gate; `bundle://proof/SB01/transcripts/development-diff-name-status.txt` and `bundle://proof/SB01/transcripts/git-status.txt` record the branch surface for review.
- Passing test: `bundle://proof/SB01/transcripts/maf-provider-composition-test.txt`; test name `MafAgentRuntimeToolProviderCompositionTests`.
- Changed source files: No production source files changed in SB01; source hashes are recorded in `bundle://proof/SB01/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB01/source-assertions/provider-boundary.md` states SB01 is proof-only and no production code was changed.
- Red-team negative case: A new core project, EF move, or driver pack would show up in the branch/status transcripts and violate the SB01 scope exception.
- Downstream dependency check: SB03/SB12 must keep full Process Core extraction deferred until the execution boundary proof exists.

## Invariant SB01-RQ013

- Invariant ID: `SB01-RQ013`
- Source raw note: "Do not run small, medium, or mobile UI validation."
- Expected behavior: SB01 records browser validation as N/A because no UI changed, and produces no small/medium/mobile screenshots.
- Disallowed shallow implementation: Spending proof time on unrelated mobile or responsive screenshots.
- Failing-first test: N/A - no production behavior changed in this process gate; the browser analytics row records N/A rather than a small/medium/mobile viewport.
- Passing test: `bundle://proof/SB01/transcripts/git-status.txt` records no UI implementation change in SB01, and `bundle://proof/SB01/transcripts/hashes.txt` records the SB01 bundle file hash.
- Changed source files: No production source files changed in SB01; bundle proof/status files changed only.
- Production assertions: `bundle://proof/SB01/source-assertions/provider-boundary.md` states browser proof is N/A for SB01.
- Red-team negative case: Any mobile screenshot path or small/medium viewport entry would violate this invariant and must reopen SB01.
- Downstream dependency check: Every later subbundle must preserve the same large-screen-only policy unless it unexpectedly touches rendered UI.
