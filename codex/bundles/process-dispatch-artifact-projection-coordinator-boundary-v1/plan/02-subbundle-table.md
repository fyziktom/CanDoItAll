# Subbundle Table

| ID | Phase | Title | Prerequisites |
| --- | --- | --- | --- |
| SB01 | Inventory | Entry baseline, branch hygiene, and proof inventory | No production source movement |
| SB02 | Inventory | ArtifactProjection source-path and source-family inventory | SB01 |
| SB03 | Design | Projection coordinator cutline and source ownership design | SB01-SB02 |
| SB04 | Gate | Gate A - architecture guardrails before source movement | SB01-SB03 |
| SB05 | Foundation | Projection context snapshot model | SB04 |
| SB06 | Foundation | Projection IO and source-read boundary design | SB05 |
| SB07 | Foundation | Projection write outcome application helper | SB05-SB06 |
| SB08 | Gate | Gate B - context and outcome parity | SB05-SB07 |
| SB09 | ExecutionArtifact | Execution artifact source facts | SB08 |
| SB10 | ExecutionArtifact | Execution artifact file resolver and content reader | SB09 |
| SB11 | ExecutionArtifact | Execution artifact projection coordinator | SB09-SB10 |
| SB12 | ExecutionArtifact | Migrate execution artifact path into coordinator | SB11 |
| SB13 | ExecutionArtifact | Execution artifact focused parity tests | SB12 |
| SB14 | Gate | Gate C - execution artifact projection proof | SB09-SB13 |
| SB15 | ProcessMock | Process mock projection facts | SB14 |
| SB16 | ProcessMock | Process mock content reader boundary | SB15 |
| SB17 | ProcessMock | Process mock projection coordinator | SB15-SB16 |
| SB18 | ProcessMock | Migrate process mock projection path | SB17 |
| SB19 | ProcessMock | Process mock negative/parity tests | SB18 |
| SB20 | Gate | Gate D - process mock proof | SB15-SB19 |
| SB21 | WorkspaceWritten | Workspace-written artifact source facts | SB20 |
| SB22 | WorkspaceWritten | Workspace-written path resolver and file reader | SB21 |
| SB23 | WorkspaceWritten | Workspace-written projection coordinator | SB21-SB22 |
| SB24 | WorkspaceWritten | Migrate workspace-written projection path | SB23 |
| SB25 | WorkspaceWritten | Workspace-written focused parity tests | SB24 |
| SB26 | Gate | Gate E - workspace-written proof | SB21-SB25 |
| SB27 | ExistingManaged | Existing-managed artifact source facts | SB26 |
| SB28 | ExistingManaged | Existing-managed path candidate resolver | SB27 |
| SB29 | ExistingManaged | Existing-managed projection coordinator | SB27-SB28 |
| SB30 | ExistingManaged | Migrate existing-managed projection path | SB29 |
| SB31 | ExistingManaged | Existing-managed focused parity tests | SB30 |
| SB32 | Gate | Gate F - existing-managed proof | SB27-SB31 |
| SB33 | ResponseText | Response-text projection facts | SB32 |
| SB34 | ResponseText | Response-text projection content builder | SB33 |
| SB35 | ResponseText | Response-text projection coordinator | SB33-SB34 |
| SB36 | ResponseText | Migrate response-text projection path | SB35 |
| SB37 | ResponseText | Response-text focused parity tests | SB36 |
| SB38 | Gate | Gate G - response-text proof | SB33-SB37 |
| SB39 | BrowserNative | Provider-native browser projection facts | SB38 |
| SB40 | BrowserNative | Provider-native browser safe path resolver | SB39 |
| SB41 | BrowserNative | Provider-native browser projection coordinator | SB39-SB40 |
| SB42 | BrowserNative | Migrate provider-native browser projection path | SB41 |
| SB43 | BrowserNative | Provider-native browser focused parity tests | SB42 |
| SB44 | Gate | Gate H - provider-native browser proof | SB39-SB43 |
| SB45 | DecisionRecord | Completed-decision record-only facts | SB44 |
| SB46 | DecisionRecord | Completed-decision record-only coordinator cleanup | SB45 |
| SB47 | DecisionRecord | Migrate completed-decision path | SB46 |
| SB48 | Gate | Gate I - decision artifact proof | SB45-SB47 |
| SB49 | Facade | Projection orchestrator facade | SB48 |
| SB50 | Facade | ArtifactProjection wrapper slimming pass | SB49 |
| SB51 | Facade | Side-effect ownership scan and helper naming cleanup | SB50 |
| SB52 | Gate | Gate J - orchestrator and side-effect proof | SB49-SB51 |
| SB53 | DriverReadiness | Documentation-only driver-readiness projection map | SB52 |
| SB54 | Validation | Broad focused regression matrix | SB52-SB53 |
| SB55 | Validation | Final hardening scans and known-failure ledger | SB54 |
| SB56 | Final | Final red-team, completed validator, and next cutline | SB55 |
