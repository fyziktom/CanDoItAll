# Recovery And Finalization Boundary

This bundle may touch recovery/finalization only as a consumer/fact layer.

Allowed:

- collect retry facts needed by completion decision,
- normalize no-progress / missing-tool / critical-failure summaries,
- add helper methods that return immutable decision facts,
- preserve journal/rework packet persistence in dispatcher.

Not allowed:

- moving `PersistRecoveryJournalAsync`,
- moving rework packet creation as a production behavior change,
- moving provider fallback mutation,
- moving final step transition or closure logic,
- creating Process Core services.
