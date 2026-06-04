# Proof Strategy

Every migration step must use failing-first or source-scan proof.

Required proof classes:

- Contract neutrality tests: no references/packages in `CanDoItAll.Processes.Contracts`.
- Adapter-only AgentFramework type usage: allowed only in `ProcessAutomationExecutionClient` and tests.
- Dispatcher forbidden type scan after SB07.
- Runtime parity tests for execution result/detail/failure behavior.
- Receipt required-tool and artifact-lineage tests after SB09.
- Large-screen-only proof policy scan.
- Full solution build before final closure.
