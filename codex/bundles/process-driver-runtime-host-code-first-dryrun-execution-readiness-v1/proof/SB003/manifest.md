# SB003 Proof Manifest

- Gate: Code-first baseline.
- Source proof: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs`
- Test proof: `dotnet build CanDoItAll.slnx --configuration Debug`; `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build -v minimal`
- Negative proof: bundle-path, Process Core dependency drift, reflection/selector drift, secret, and side-effect API scans were run.
- Changed-file SHA-256: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs` `042614E022756CC1A6D9E78EBB8F3EBA399EC1920C9439BB70980A69C27411E3`
- Result: Passed.
