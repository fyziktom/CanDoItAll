# SB04 Proof Manifest

## Changed Files

| File | SHA256 |
| --- | --- |
| `repo://Templates/Agents/teams/dotnet-delivery/members/blazor-application-developer/settings.json` | `29EEF77C95A3C3A28B2551F412FFA2D2D652A5F89EEAE43AC73D6E21CAF4FF5E` |
| `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-application-developer/settings.json` | `5DB7B87996436483B903E96AEE6D868C59F6069BEF27DB903D7FE688C367A92B` |
| `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-qa-review-lead/settings.json` | `7EF7A137480388685AC5DECE3599479344198130C8565CBB169457BDBF204527` |
| `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-solution-architect/settings.json` | `701B97CBB736493BCAB542D29222091F91E61EFFC1900F626BAF7472929EDC8D` |
| `repo://Templates/Agents/teams/delivery-platform/members/hr-staffing-manager/settings.json` | `A4794BF9C4C20412A4850D243479DD3F47DDEB7F5F665EB70BF7C113FAA94242` |
| `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs` | `232F6A838E5F07A3A48D1526E3EE5FBE7FEAA12A9642DE1855CEFF8756F4FCE8` |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Explicit agent seed model `gpt-5.4-mini` | Managed agent template settings | Agent catalog seed sync and process launch assignment | Survives host restart and no longer falls back to provider default for assigned .NET/Blazor delivery agents | `bundle://proof/SB04/transcripts/agent-seed-tests.txt` verifies seed refresh |
| Agent readiness snapshot | Agent and Cognitive Memory HTTP API responses | Process operator and launch readiness gate | Confirms selected agents, model, permissions, and Cognitive Memory disabled before user rerun | `bundle://proof/SB04/transcripts/agent-readiness.json` |

## Validation

- `bundle://proof/SB04/transcripts/agent-seed-tests.txt`
- `bundle://proof/SB04/transcripts/agent-readiness.json`
- `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Failing-first transcript: N/A process-readiness proof; production behavior is validated by seed tests and runtime API records.
- Passing transcript: `bundle://proof/SB04/transcripts/agent-seed-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Runtime API verification after restart: selected `.NET QA Review Lead`, `.NET Solution Architect`, `Blazor Application Developer`, and `Delivery manager AI agent` all returned `model: gpt-5.4-mini`; Cognitive Memory returned `isEnabled: false`.

## Closure

SB04 is complete. The earlier startup seed reset was repaired by changing the managed agent template source and validating the seed refresh test.
