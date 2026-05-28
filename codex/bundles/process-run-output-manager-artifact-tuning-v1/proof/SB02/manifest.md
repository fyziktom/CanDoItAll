# SB02 Proof Manifest

- Subbundle: `SB02`
- Proof type: selected-run manager resolution and live manager-chat smoke.
- Portable source references: `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs`, `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs`, `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Semantic invariant contract: `proof/SB02/semantic-invariants.md`.
- Browser proof: `bundle://reviews/proof/manager-chat-smoke.png`.
- Passing command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessManagerAgentResolver_uses_assigned_manager_before_ambiguous_manager_options|FullyQualifiedName=CanDoItAll.Tests.Integration.ProcessRunAutomationDispatchServiceTests.ProcessManagerAgentResolver_rejects_ambiguous_assigned_managers"`.
- Passing transcript: `bundle://proof/SB02/transcripts/passing.md`.
- Anti-stub transcript: `bundle://proof/SB02/transcripts/anti-stub.md`.
- Failing-first: N/A process runtime resolver proof; adversarial negative proof is the ambiguity regression test.
- Changed-file SHA-256: `5F9C5A2618F5DFD073F02CBADDF1D1283340BBE896A5ACD69F14D7EACE6A7A19` for `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerAgentResolver.cs`.
- Changed-file SHA-256: `4D7BCFF61591823831AB1E983CDC3990E5363E96C16B263DCF0CFB1717C382F0` for `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs`.
- Changed-file SHA-256: `CAFB9A9ACE8C78938697B2D438B22CD33A781450079616F767571144AB0527F3` for `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.ManagerChat.cs`.
