# SB06 Proof Manifest

## Status
- Result: Pass
- Completed: 2026-06-11
- Scope: Runtime-host manager readback and dry-run denial readback attached to real process automation run and step ids.

## Source Hashes
- `tests\CanDoItAll.Tests.Integration\ProcessDomainEvidenceReadOnlyAdapterTests.cs`
  - SHA256: `958C1D6119D511DE320F589626ED56DBE098FFF5AC626B7FAE7AA115C533A170`
- `src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessManagerReadOnlyVerificationReadback.cs`
  - SHA256: `CB9683246BE6C4ED7F9C6FB8901E61834627D04539C8963CE8D4E41D5FCE88E3`
- `src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessManagerRuntimeHostDryRunReadback.cs`
  - SHA256: `14E88600279C9E1FCD3C85ADEE91644DEE795ED161CBBB6A4C27303233AC03F1`
- `src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessDryRunExecutionPipeline.cs`
  - SHA256: `3A802F65888C24EDAF15FA541A0833779B1752FAA38C8579FEA407567B8CE8BE`
- `src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessDryRunExecutionHost.cs`
  - SHA256: `17E1E34EF408C36C88A2717911F8D0D74787848A34D3F1F9BA06453E8E9DE7A2`
- `src\CanDoItAll.Modules.Processes\Automation\Dispatch\ProcessVerificationRuntimeHost.cs`
  - SHA256: `9E1CDC4E10D652C0705051CCBA52B217F2F0B090003111246CC194B1C0ACA1BC`

## Transcripts
- Passing transcript: `bundle://proof/SB06/transcripts/focused-integration.txt`
- `bundle://proof/SB06/transcripts/focused-integration.txt`
- `bundle://proof/SB06/transcripts/source-assertions.txt`
- `bundle://proof/SB06/transcripts/side-effect-scan.txt`
- `bundle://proof/SB06/transcripts/anti-stub-audit.txt`
- `bundle://proof/SB06/transcripts/code-first-guard.txt`
- `bundle://proof/SB06/transcripts/failing-first-source-assertion.txt`
- `bundle://proof/SB06/transcripts/browser-gap.txt`

## Validation Commands
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Process_runtime_host_readback_SB06_INV_001"`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Process_runtime_host_codefirst_SB01_INV_007"`

## Browser Validation
- No run-detail Razor surface currently exposes runtime-host readback or dry-run denial details.
- SB06 is closed with API/facade proof and an explicit UI exposure gap recorded in `bundle://proof/SB06/transcripts/browser-gap.txt`.

