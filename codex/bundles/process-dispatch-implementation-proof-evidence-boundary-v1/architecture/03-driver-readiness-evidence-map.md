# Driver Readiness Evidence Map

Documentation only. Do not create production driver APIs.

| Future evidence family | Existing runtime meaning | Current helper target |
| --- | --- | --- |
| `ConcreteProductMutationEvidence` | Successful tool receipt mutated a concrete product file in current attempt. | Concrete product path + receipt timeline rules |
| `ConcreteProductReadEvidence` | Current attempt read a concrete product source/project/deliverable. | Implementation proof read receipt rules |
| `BuildValidationEvidence` | Build/test/publish receipt after relevant mutation. | Receipt timeline + quality validation |
| `RunnableHostEvidence` | Run receipt after mutation for detected runnable host. | Runnable app proof + dotnet host rules |
| `DotNetHostShapeEvidence` | `.csproj`/host shape is runnable, not library-only. | DotNet host evidence rules |
| `ProcessMockImplementationEvidence` | Process mock can satisfy implementation proof in controlled test modes. | Process mock bridge |
| `BusinessAnalysisDeliverableEvidence` | Future non-SW deliverable proof, not implemented here. | Documentation only |
| `SpreadsheetValidationEvidence` | Future Office/Excel proof, not implemented here. | Documentation only |
