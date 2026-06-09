# Gap Analysis Toward "Processes Work Like Before"

## Definition of "works like before"

A user should be able to:

1. Start the web app in a local dev profile.
2. Open a project or project structure node.
3. Choose a process/template from UI or project-structure context.
4. Create a launch plan or start a process run.
5. See a process run and step runs persist.
6. Let dispatch claim eligible steps.
7. Execute either a workflow-backed role or a direct-agent role.
8. Finalize steps and close the run when criteria are satisfied.
9. Save/project artifacts into the expected process/project structure.
10. Inspect diagnostics, evidence, manager chat/directives, and recovery/read-only driver verification without accidental mutation.
11. Run at least one software-development process and one non-software process.

## Current readiness

The latest branch appears close for deterministic/controlled tests, but not yet ready to declare live process runtime restored because the following need live proof:

- UI/project-structure E2E flow.
- Real provider smoke with OpenAI API credits/key.
- Worker/outbox run from actual hosted runtime lane.
- Artifact/output navigation in UI.
- Process restart/recovery after interruption.
- Non-software business-analysis process with real LLM or realistic provider stub.
- Regression guard proving no tests depend on transient bundle paths.

## Runtime host decision

Do not implement a generic process-driver runtime host in this bundle. It is not required to restore process execution. Instead:

- Use current `ProcessesService` launch paths.
- Use current MAF/workflow/direct-agent execution.
- Use existing process tools and dispatch services.
- Use read-only driver verification as diagnostic support only.
- Add a future runtime-host decision gate only after live process execution is stable again.
