## Plugin-wave readiness

### Email connector(s)
- **Read-only / fetch-only mailbox inventory:** partially feasible once the plugin-first resource/provider editor flow is fixed.
- **Write-side send / sync / queue operations:** **not ready** until a durable connector-operation boundary exists.

### LinkedIn / social / remote API connectors
- The new connector manifest foundation is promising.
- The current resource/provider UIs still force legacy enum-driven flows, so these connectors are **not first-class yet**.
- Direct introduction now would either distort the model or force more core-page edits than a true plugin platform should need.

### Custom API connectors
- Best future fit: manifest-driven connectors with schema-defined config, secrets, health checks, agent exposure, and optional workbench hooks.
- Current state: **close in direction, not close enough in execution**.

## Conclusion
The repo is **closer** than before, but the next big plugin wave would still land on seams that are not fully stabilized.
