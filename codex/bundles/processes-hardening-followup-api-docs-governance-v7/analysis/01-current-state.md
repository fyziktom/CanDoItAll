# Current state

Phase6 is a good step forward. It moved several previously inferred concepts into persisted models:

- Process step allowed operations.
- Process step target scope.
- Process artifact projection lineage.
- Process artifact projection identity hash.
- Step block reason code.
- Step recovery options.
- Definition contract mode.
- Typed UI controls for operation contracts.

The next risk is not that the runtime has no contract. The risk is that the surrounding surfaces still allow stale or partial process definitions to enter the runtime:
- Processes API/tool calls may not require or expose the new fields.
- Imported templates may omit new fields.
- Skills may still tell Codex/agents to use older prose-only contracts.
- Docs may not explain the typed model.
- Manual/API transitions may still bypass finalizer-grade artifact validation.
