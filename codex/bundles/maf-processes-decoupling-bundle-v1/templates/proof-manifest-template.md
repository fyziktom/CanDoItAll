# Proof Manifest Template

## Subbundle

- ID:
- Title:
- Status:
- Critical foundation:

## Changed Files With Hashes

| File | SHA-256 | Reason |
| --- | --- | --- |
| `repo://...` | `<sha256>` | |

## Commands

| Command | Transcript path | Exit code | Purpose |
| --- | --- | ---: | --- |
| `dotnet build CanDoItAll.slnx` | `bundle://proof/SBxx/transcripts/build.txt` | 0 | Build proof |

## Source Assertions

| Assertion | Source path | Result |
| --- | --- | --- |
| MAF does not reference Processes | `repo://src/CanDoItAll.AgentFramework.Maf/...` | Pass |

## Semantic Adequacy Gate

| Label | Evidence |
| --- | --- |
| Raw note owned | |
| Shipped behavior | |
| Source proof | |
| Test proof | |
| Shallow-pass trap | |
| Adversarial negative proof | |
| Semantic positive proof | |
| Anti-stub audit | |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A unless the subbundle introduces production state/signal/record/event | | | | |
