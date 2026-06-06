# Side Effect Map

| Side effect | Current area | Must remain explicit |
| --- | --- | --- |
| `EnsureStepDispatchClaimHeldAsync` | Execution artifact projection and parent facade | Projection orchestrator/host should expose this as an explicit operation |
| `File.ReadAllBytesAsync` | Multiple source coordinators | Keep in source-specific coordinator or file IO helper named as side-effectful |
| `File.Copy` | Provider-native browser projection | Keep in browser projection coordinator or explicit file IO helper |
| `Directory.CreateDirectory` | Provider-native browser projection and directory preflight | Keep explicit and workspace-safe |
| `WriteCoordinator.WriteAsync` | Storage-backed projection writes | Keep write request fields identical |
| `RecordOnlyCoordinator.RecordAsync` | Completed decision artifacts | Keep record-only path separate from storage-backed writes |
| Candidate `ExternalReferenceKeys` mutation | Candidate state helper | Must go through candidate-state helper only |
| Candidate `RecordedArtifactExpectationIds` mutation | Candidate state helper | Must go through candidate-state helper only |
