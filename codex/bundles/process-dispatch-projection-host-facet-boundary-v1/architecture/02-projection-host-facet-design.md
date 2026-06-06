# Projection host facet design

Recommended module-local facets:

| Facet | Owns | Must not own |
| --- | --- | --- |
| `IProcessProjectionClaimGuard` | claim-held checks and cancellation guard | artifact matching or storage writes |
| `IProcessProjectionPathResolver` | full path resolution, scope paths, workspace safety | artifact kind decisions |
| `IProcessProjectionFileIo` | read/copy/write side effects with explicit names | candidate mutation |
| `IProcessProjectionArtifactClassifier` | content type, process artifact kind, storage content kind | file system writes |
| `IProcessProjectionExpectationMatcher` | expectation matching and expectation id resolution | storage placement |
| `IProcessProjectionProjectStructureMatcher` | governed project-structure path matching | source-family orchestration |
| `IProcessProjectionSessionObservationSource` | session file writes, browser outputs, result text | driver APIs |
| `IProcessProjectionResponseTextRules` | response artifact path and content eligibility | file writes |
| `IProcessProjectionBrowserOutputRules` | provider-native browser output mapping | driver APIs |
| `IProcessProjectionDecisionArtifactRules` | completed decision trust/provenance/review | source-family orchestration |
| `IProcessProjectionLineageFactory` | recovery lineage and external ref helpers | storage writes |
| `IProcessProjectionCandidateState` | consistent candidate mutation | projection planning |

Do not export these outside the module in this bundle.
