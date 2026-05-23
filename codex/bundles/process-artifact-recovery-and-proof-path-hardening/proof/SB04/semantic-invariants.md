# SB04 Semantic Invariants

## Invariants

- Invariant ID: `SB04-I001`
- Source raw note: User required `gpt-5.4-mini` for all agents used by the process and asked to verify HR-selected agents have the needed tools and permissions.
- Expected behavior: Managed delivery agents used for Blazor work keep `gpt-5.4-mini` after seed refresh and host restart.
- Disallowed shallow implementation: Updating agent rows through API only, because startup seed sync can reset model fields back to blank.
- Failing-first test: `AgentFrameworkWorkspaceSeedIntegrationTests` initially failed because expected blank model no longer matched the updated seed behavior.
- Passing test: `bundle://proof/SB04/transcripts/agent-seed-tests.txt` records 23 passing seed tests.
- Changed source files: `repo://Templates/Agents/teams/dotnet-delivery/members/*/settings.json`, `repo://Templates/Agents/teams/delivery-platform/members/hr-staffing-manager/settings.json`, `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkWorkspaceSeedIntegrationTests.cs`.
- Production assertions: Runtime API returned `gpt-5.4-mini` for the selected process agents after host restart.
- Red-team negative case: Restarting the host previously reset blank model values; after the seed fix, the same restart keeps the model pin.
- Downstream dependency check: SB06/SB07 live process execution can rely on stable agent model selection.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Managed agent model setting | Agent template pack | Seed sync and process launch | Written into catalog on startup for .NET/Blazor delivery agents | `bundle://proof/SB04/transcripts/agent-seed-tests.txt` |
| Cognitive Memory disabled setting | Cognitive Memory settings API | Agent context contribution path | Prevents unstable memory context from participating in process runs | `bundle://proof/SB04/transcripts/agent-readiness.json` |
