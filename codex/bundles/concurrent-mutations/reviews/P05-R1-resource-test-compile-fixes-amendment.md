# Resource fixture compile corrections

Parent Release compilation found two C# fixture errors after the cohesive Resource/stage1/stage2 transfer. Both parent-authored corrections are now mirrored byte-for-byte in managed; no production or assertion behavior changed.

- `StorageObjectResourceConnectorTests.cs`: the existing `Prepare` call now names `ReplaceResourceMetadataCommand`, resolving the new-overload target-typed `new` ambiguity. The file remains six Facts plus one four-row theory (10 rows).
- `ResourceTestLabAdmissionIntegrationTests.cs`: the initial application local and its two uses now say `initialResources`, avoiding the C# scope collision with the later restart local. The file remains four Facts plus two theories containing six inline rows (10 rows).

The complete shared-test chain is original MAIN -> Resource B5E148 -> stage2 ACA188 -> compile EEFB00. The Unit change is original Resource 3B122 -> explicit-command 44AB450. Exact images, parent repair receipts, the original Resource inventory, stage2 A0A8 inventory and the immutable Unit 05E45 overlay are retained. This successor combines the two corrections without rewriting those records.

Ordinary V3 must change exactly these two selected source identities and its explicit provenance binding. All six lanes, 254 methods, 346 ordinary rows, four separate browser rows, stage1-first 14-row prerequisite, exclusions and runtime helpers remain unchanged. The earlier EF API correction CBF1/26D538 remains a separate inherited binding.

Parent reports the final eight standard Release builds passed. This note records source correspondence only; it does not claim discovery, runtime, PostgreSQL migration, physical cleanup or capacity proof. Parent retains the failed build logs and owns final static/build/runtime evidence. No MAIN source, build, test or database operation was performed by this agent for this correction.
