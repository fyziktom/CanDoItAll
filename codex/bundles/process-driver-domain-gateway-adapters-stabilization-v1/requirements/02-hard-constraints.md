# Hard Constraints

- No generic runtime driver host.
- No runtime driver registry.
- No runtime selector.
- No DI registration or service collection extension.
- No manager command.
- No scheduler/workflow hook.
- No shell command execution.
- No package restore or tool invocation by drivers.
- No Office/Graph call.
- No workspace/storage write.
- No process mutation.
- No claim/transition/finalizer/retry mutation.
- No broad Process Core runtime extraction.
- No reverse dependency from Core to driver packages.
- No driver package dependency on Modules, Infrastructure, AgentFramework, EF, UI, workspace, storage, or connector packages.
- No UI/mobile/small/medium proof unless UI/media files changed; UI/media drift should fail this bundle.
