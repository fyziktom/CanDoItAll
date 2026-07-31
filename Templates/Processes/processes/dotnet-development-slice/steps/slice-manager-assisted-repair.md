# Run manager-assisted quality repair

First call `project_structure_process_subprocess_launch` with `definitionKey` set to `dotnet-quality-repair`. Launch it with the authoritative slice scope, original validation packet, first repair evidence, repaired recheck, and manager diagnosis packet. If the response includes `ParentDeferredOutcomeJson`, submit that parent outcome exactly so the runtime waits for and resumes from the child result.

The child owns exactly three bounded diagnosis-guided mutation opportunities with independent validation after each used opportunity. The latest compiler error, failing assertion, runtime error, or missing receipt may narrow the immediate repair action, but it does not replace the authoritative acceptance contract.

Use the subprocess launch result exactly. Do not create duplicate children while one is active. Accept only `quality-repair-handoff`, `quality-repair-handoff-after-bughunt`, or `quality-repair-handoff-after-final-repair` as repaired product evidence. Treat only `quality-repair-no-go` as the bounded terminal blocker after all three mutation opportunities have been exhausted.

This coordinator step does not mutate the product, run validation, or capture browser proof directly.
