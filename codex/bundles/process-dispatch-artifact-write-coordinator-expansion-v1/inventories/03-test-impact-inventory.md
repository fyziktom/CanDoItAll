# Test Impact Inventory

Expected test surfaces:

- `tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Existing artifact projection and lineage slices from previous bundles

Recommended focused commands:

```powershell
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter FullyQualifiedName~ProcessAgentExecutionBoundaryArchitectureTests -v minimal
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Artifact" -v minimal
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests&FullyQualifiedName~Projection" -v minimal
dotnet build CanDoItAll.slnx -v minimal
```

If a broad process-filtered integration test times out, record the exact timeout and run narrower artifact/projection slices. Do not claim final closure from timed-out proof alone.
