# C2 readiness classification repair

Source-only repair after the frozen 330-case candidate reported the readiness assertion failure; no production behavior changes or validation execution in this lane.

`Matching_active_readiness_and_admission_refuse_missing_or_inconsistent_issued_metadata` deliberately removes one pool state while retaining its issued epoch. The installed-set control-plane audit now rejects that orphan epoch as `InconsistentMetadata` before per-pool readiness. The former `MissingState` coordinator expectation therefore describes the superseded call order. This is an intentional stronger readiness check, not a reason to weaken the global audit.

The repaired test requires `InconsistentMetadata` from EnsureReady, Attach and Resume. A new arrangement in the same Fact independently enters the foreground admission path and requires its original `MissingState` classification. All existing no-healing, unchanged file binding, later inconsistent-head admission, unsupported protocol and missing retained epoch assertions remain.

Only `tests/Integration/CanDoItAll.Tests.Integration/FiniteOperationPersistenceTests.cs` changes. Its frozen SHA256 is `D5D3EE8F3D491849908E9C80DD0BF2D3E7BAB9ECC80320A40DA475E63DA91212`. The class still has exactly **12 Facts / 12 expected discovery / 12 expected runtime rows**, without theories, dynamic data or exclusions. Parent owns transfer and execution after the frozen candidate completes. Earlier inventories remain historical.
