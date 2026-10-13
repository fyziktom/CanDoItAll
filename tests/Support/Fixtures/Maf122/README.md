# Genuine MAF 1.22 retained state

These fixtures were captured through Microsoft.Agents.AI 1.22.0 before upgrading the application. They contain synthetic identifiers and no credentials. The producer asserted the loaded assembly version, captured ordinary/pending/settled native sessions for local/service-managed history and blocking/streaming calls, and required zero effects before approval and exactly one after approval. No private SDK state was constructed or relabeled.

The manifest records content hashes. `Maf122RetainedNativeStateTests` restores both the 1.20 and 1.22 fixtures on the retained 1.22 runtime, checks native approval binding, and verifies repeated approval responses cannot execute a completed effect again. Application journal protocol fingerprints remain exact and are tested separately; readable native session state does not authorize replay of a mismatched protocol checkpoint.

Capture source and transcript: local bundle `process-tool-latency-safe-cache-20261012/proof/SB05/capture-native.cs` and `transcripts/capture-native.txt`. Capture uses the .NET file-app standard build cache, without custom configurations or copied application output trees.

The attempted 1.24 validation passed native retained-session cases but failed both consecutive-approval/compaction cases. `compaction-regression.cs.txt` is a standalone reproducer using only Microsoft packages. Copy to a `.cs` file and run with `dotnet run --file`; 1.24 fails, while changing its exact package directive to 1.22 passes. Package activation was deferred; no workaround disables approval binding or compaction.
