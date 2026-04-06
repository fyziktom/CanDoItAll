# Anti-evasion rules

- Do not rename required symbols or tests just to satisfy string searches indirectly.
- Do not satisfy runtime-plane requirements with only in-memory mediator/events.
- Do not keep singular `IAutomationSignalProvider` consumption in the automation workspace.
- Do not execute plugin business logic inline in Quartz jobs.
- Do not keep connector outbox draining manual-only.
- Do not reintroduce write-on-read cleanup into Workbench reads.
- Do not remove or weaken the phase10 proof tests while implementing phase11.
- Do not remove generic test ids / secret wiring from the shared connector field editor just to make component tests easier to rewrite.
- Do not treat MQTT as the canonical internal transport.
