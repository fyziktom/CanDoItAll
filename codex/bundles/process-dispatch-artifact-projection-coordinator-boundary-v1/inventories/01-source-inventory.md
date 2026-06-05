# Source Inventory

Primary hotspot:

- `ProcessRunAutomationDispatchService.ArtifactProjection.cs`

Existing helper foundations:

- `ProcessArtifactProjectionPlanner.cs`
- `ProcessArtifactProjectionSourceAdapters.cs`
- `ProcessArtifactProjectionWriteCoordinator.cs`
- `ProcessArtifactProjectionLineageBuilder.cs`
- `ProcessArtifactSatisfactionSnapshot.cs`
- `ProcessResponseTextArtifactSatisfactionRules.cs`
- `ProcessAutomationSessionObservation.cs`
- `ProcessProviderNativeBrowserOutputFacts.cs`

Source families to preserve:

1. execution artifacts
2. process mock artifacts
3. workspace-written artifacts
4. existing managed artifacts
5. response text artifacts
6. provider-native browser artifacts
7. completed decision artifacts

Codex must update this inventory during SB02 with current line counts, method names, and exact tests.
