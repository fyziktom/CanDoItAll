# SB01 Proof Manifest

- Subbundle: SB01 Entry audit, branch hygiene, and provider seam smoke.
- Status: Completed.
- Owned requirements: RQ-001, RQ-002, RQ-013.
- Raw notes: preserve previous provider decoupling; do not start full Process Core split; do not run small/medium/mobile UI validation.
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`.
- Browser proof: N/A because SB01 changed no rendered UI route.

## Changed-File Hashes

| Path | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `042FAFAA73C70B37D74F7EAE1FE51E6CCCAFB3FD41C818622ECC0174E11B02E1` |
| `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs` | `CFD816FD0C045E07CD48ED83DA08E0E46D9601B4DF136BDAA0B882FB12F8C5FE` |
| `bundle://subbundles/01-01-entry-audit-branch-hygiene-and-provider-seam-smoke/README.md` | `0636E6DE732809D7A1F5CF0DEC0DA487DEECAFBA4F23F00E5533BCC4A02D57DA` |

## Command Transcripts

- Branch status: `bundle://proof/SB01/transcripts/git-status.txt`.
- Development diff surface: `bundle://proof/SB01/transcripts/development-diff-name-status.txt`.
- MAF product dependency scan: `bundle://proof/SB01/transcripts/maf-product-dependency-scan.txt`.
- Hash capture: `bundle://proof/SB01/transcripts/hashes.txt`.

## Failing-First And Passing Proof

- Failing-first: N/A - no production behavior changed in this process gate; the adversarial dependency scan would expose forbidden product-module references if present.
- Passing transcript: `bundle://proof/SB01/transcripts/maf-provider-composition-test.txt`.
- Test name: `MafAgentRuntimeToolProviderCompositionTests`.

## Source Assertions

- Provider boundary assertion: `bundle://proof/SB01/source-assertions/provider-boundary.md`.

## Anti-Stub Audit

- Anti-stub transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.
- Result: no production TODO, `NotImplemented`, `throw new NotImplementedException`, or fixture-specific markers were found in the scoped MAF/tooling/process-provider boundary files.
