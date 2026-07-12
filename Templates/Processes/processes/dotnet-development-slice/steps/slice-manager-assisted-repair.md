# Run manager-assisted quality repair

First call `project_structure_process_subprocess_launch` with `definitionKey` set to `dotnet-quality-repair`. Launch it with the authoritative slice scope, original validation packet, first repair evidence, repaired recheck, and manager diagnosis packet. If the response includes `ParentDeferredOutcomeJson`, submit that parent outcome exactly so the runtime waits for and resumes from the child result.

The child owns a fresh diagnosis-guided implementation strategy, independent validation, and one specialist bughunt/re-repair lane. The latest compiler error, failing assertion, browser error, or missing receipt may narrow the immediate repair action, but it does not replace the authoritative acceptance contract.

Use the subprocess launch result exactly. Do not create duplicate children while one is active. Accept only `quality-repair-handoff` or `quality-repair-handoff-after-bughunt` as repaired product evidence. Treat `quality-repair-no-go` as the bounded final blocker after the internal manager and bughunt lanes have been exhausted.

This coordinator step does not mutate the product, run validation, or capture browser proof directly.
