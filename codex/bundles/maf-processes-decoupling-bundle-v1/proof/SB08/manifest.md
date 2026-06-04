# SB08 Proof Manifest

## Subbundle

- ID: SB08
- Title: Documentation and operator handoff
- Status: Completed
- Critical foundation: No
- Owned requirements: RQ-012
- Raw notes: "rozplest ty zavislosti"; "po mensich krocich"; "nesmi veci zjednodusit nebo neco vynechat"
- Semantic invariant contract: `bundle://proof/SB08/semantic-invariants.md`

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://README.md` | `BB7E5B0EAC5917790FE293AE84CF1EDA89AAD987679B0B993BD20E8D9BC63BA8` | Documents that process tools are contributed by the Processes provider and MAF runs without them when Processes is absent. |
| `repo://docs/agent-runtime-tool-surface.md` | `7EDCFFE1EE3EAC1C55D80208AA7BC854F0984B121DAFD6700BE7DF3D4F75A9EF` | Replaces stale MAF process-tool partial references with provider seam sources and direct-tool rules. |
| `repo://docs/api-control-plane.md` | `22F458D39D0EEE3DFCDA0BC0EDAB44C11941D1E00B9A504D643765E3E17F2ACF` | Clarifies that internal direct process tools are provider-owned and narrower than HTTP APIs. |
| `repo://docs/architecture-beta.md` | `C8F7F7E65DB22BAAFD6A087B32A7C7885DE1C08FF26E8E645E96F5100ADA010D` | Adds architecture-level runtime provider seam, source links, diagrams, troubleshooting, and next-phase boundary. |
| `repo://src/CanDoItAll.AgentFramework.Maf/README.md` | `81E7DDF012321B31E0F279EAAAAFCAF4C220A6BCF437D3938DA6A84C7CD738C3` | Documents MAF-side provider troubleshooting and forbids recoupling as a fix. |
| `repo://src/CanDoItAll.Modules.Processes/README.md` | `A112FF2250A09F99B3D24860AC9035C76C3BF134E472B5443729F92961214666` | Documents Processes ownership of `ProcessAgentRuntimeToolProvider` and operator troubleshooting. |
| `bundle://README.md` | `AEBBDF068D4B7DF7C9F7D78BAC3915B8639D28673F8BC3175C526CE637B6EDBA` | Updates bundle overview/status wording so it does not describe the old process-tool partial as current. |
| `bundle://subbundles/08-documentation-and-operator-handoff/README.md` | `4298112629C629216567E8661302B78C5444055645F9AA021189EE058F3F5E57` | Marks SB08 acceptance and closure state. |
| `bundle://reviews/01-execution-report.md` | `29F600BCB6E2E64D2AAF275BA5363DCBAD3E86BCFA30B116F743A9EEC8311A40` | Records SB08 gate, validation, browser N/A, and raw-note closure progress. |
| Changed file hash transcript | `bundle://proof/SB08/source-assertions/changed-file-hashes.txt` | Full hash evidence for SB08 documentation files. |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `rg -n "ProcessToolBuilder|MafAgentRuntime\.ProcessTools" README.md docs src -g "*.md" -g "*.cs" -g "*.razor"` | `bundle://proof/SB08/transcripts/stale-reference-scan-live-docs.txt` | 0 | Proves no stale deleted-process-builder references remain in live README/docs/src content; no-match `rg` exit 1 was normalized to proof success. |
| `rg -n "ProcessToolBuilder|MafAgentRuntime\.ProcessTools" README.md docs src codex -g "*.md" -g "*.cs" -g "*.razor"` | `bundle://proof/SB08/transcripts/stale-reference-scan-with-bundle-history.txt` | 0 | Required broader scan; remaining matches are historical bundle inputs, prior subbundle instructions, and proof records. |
| `git diff --check` | `bundle://proof/SB08/transcripts/git-diff-check.txt` | 0 | Proves documentation edits have no whitespace errors. |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SB08/transcripts/solution-build.txt` | 0 | Standard validation build after documentation updates. |

## Validator Proof Citations

- Adversarial negative proof: N/A process/non-production documentation-only subbundle; the live stale-reference scan is the maintained regression proof.
- Passing transcript: `bundle://proof/SB08/transcripts/stale-reference-scan-live-docs.txt`.
- Anti-stub audit transcript: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`.

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| Documentation source assertion passed. | `bundle://proof/SB08/source-assertions/documentation-source-assertion.txt` | Repo overview, tool-surface doc, API doc, architecture doc, MAF README, and Processes README all describe the provider seam and operator troubleshooting. |
| Historical reference classification recorded. | `bundle://proof/SB08/source-assertions/historical-reference-classification.txt` | Broader `codex` matches are baseline, proof, and instruction records, not live stale documentation. |

## Closure Gate

| Label | Evidence |
| --- | --- |
| Stale documentation removed | Live `README.md docs src` scan has no `ProcessToolBuilder` or `MafAgentRuntime.ProcessTools` matches. |
| Provider seam documented | `README.md`, `docs/architecture-beta.md`, `docs/agent-runtime-tool-surface.md`, MAF README, and Processes README name `IAgentRuntimeToolProvider` and `ProcessAgentRuntimeToolProvider`. |
| Next phase bounded | `docs/architecture-beta.md` states this is not a completed process-core extraction and that future driver/core splits need a separate migration. |
| Operator handoff | MAF and Processes READMEs explain how to debug missing process tools without reintroducing a MAF -> Processes dependency. |
| Browser validation | N/A; documentation-only change with no rendered UI route exercised. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB08 changes documentation only; it introduces no persisted production state, signal, record, or event. | N/A | N/A | N/A |
