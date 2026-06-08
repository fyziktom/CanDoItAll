# Source Hotspots Inventory

| Area | Current source | Concern | Planned owner |
| --- | --- | --- | --- |
| Transcript verifier | `repo://src/CanDoItAll.Processes.Drivers.TranscriptVerification/TranscriptVerificationAlphaVerifier.cs` | Too many responsibilities in one class | SB004-SB006 |
| Process transcript adapter | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs` | Safe but concentrated policy/observation/audit logic | SB010-SB012 |
| Evidence hash/URI policy | Transcript verifier + process adapter | Duplication and inconsistent denial risk | SB013-SB015 |
| Audit/redaction | Transcript verifier + adapter | Must be shared in behavior, not necessarily shared package | SB016-SB018 |
| Runtime evidence descriptors | `repo://src/CanDoItAll.Processes.Core` | Ready for a second verifier alpha | SB019-SB027 |
| Core consumer allow-list | architecture tests | Must remain exact; no broad dispatch import | SB028-SB030 |
| Office/business lanes | docs/tests | Must remain read-only denial lanes | SB034-SB036 |
