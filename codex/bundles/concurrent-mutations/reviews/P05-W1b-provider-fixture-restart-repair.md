# Workspace settings restart fixture repair

Source-only follow-up to the frozen 330-case candidate. The Workspace owner lane
executed 83 cases: 82 passed and one failed during application restart. No production
change or successful repaired execution is claimed here.

The earlier fixture correction supplied a retained provider ID so bootstrap would
preserve the user's explicit default. Its synthetic provider omitted the connector
identity, so the real runtime projection correctly refused it on restart. Populate
that one provider with the existing ScenarioHarness connector and base/model defaults,
plus its supported configuration version. This needs no network call or secret.

Only `WorkspaceSettingsOwnerPersistenceTests.cs` changes. Keep the exact serialized
settings comparison, original ID/revision/time, ordinary owner save, both application
restarts and independent profile-isolation assertions. The class remains two Facts.
The parent will transfer this repair after the frozen run, then build, discover and
execute it on the next candidate with final portability enforcement.
