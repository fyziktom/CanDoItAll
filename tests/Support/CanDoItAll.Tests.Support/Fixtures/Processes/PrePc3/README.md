# Pre-PC3 process fixture

These are actual predecessor serializer outputs, not current plans with their captured
definition property cleared. The retained `ProcUiProofPC2` Builder, Persistence,
Application and Unit assemblies all report source commit
`8951ffdf7d35aaf0b00b7fe8220f1aad8d9e3d59`. The reviewed predecessor shape at
`095c418b7981511ba9c96a75b7d2950ff4f326a6` has no intervening changes to these process
sources or distributed templates. Binary SHA-256 values are in `provenance.json`.

`generator.cs.txt` invokes the predecessor `ProcessLaunchAtomicCommitTests` fixture's
real application launch preparation with execution disabled. Its test runtime store
rejects the initial create event after capturing the complete initial commit, so no
external effects run. The predecessor persistence mapper serializes the resulting plan;
the same predecessor serializer options serialize the initial commit. The generator
refuses a binary whose plan type has an `ExecutableDefinitions` property and verifies
that the emitted plan JSON omits it.

The stored payload represents the launch's initial transaction, before acceptance. Tests
may populate isolated predecessor storage from it and exercise accepted-state recovery.
They must identify that storage setup separately from the original fixture capture. They
must retain these plan bytes and typed plan hash, rather than recompute a convenient hash
or infer authority from the fixture's launch variables. The optional project admission
was already supported by this predecessor format; admission variants in tests change the
runtime origin record, never this immutable plan.

The `parent-` files are a second independent predecessor launch capture, with different
run/plan/step identities and its own byte provenance. Native recovery tests import these
records as isolated historical snapshots using the native persistence mappers. Status,
runtime root linkage, source variables and optional admission are explicit fixture
parameters; this setup does not claim that a provider executed the historical steps.
Neither plan is reconstructed or rewritten. This storage-import fixture is separate from
the populated-schema upgrade and real workflow execution proof lanes.

To regenerate, copy `generator.cs.txt` to `Program.cs` in an isolated scratch .NET 10
executable project with implicit usings, nullable enabled, and a framework reference to
`Microsoft.AspNetCore.App`. Run it with the absolute predecessor Unit output directory
and a new output directory as its two arguments. Keep the predecessor outputs and their
distributed templates together. Run the generator from the reviewed source checkout so
the template loader sees that source version. New runs allocate new run/plan/step IDs and
timestamps, so byte hashes differ; record their observed binary versions and hashes.

Generation used SDK 10.0.303. No private signing material, provider credentials, ordinary
database content or developer-machine paths are part of these payloads. The source
generator is retained as text so it is not compiled into the test-support assembly.
