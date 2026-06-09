# Hard Constraints

- No broad Process Core runtime extraction.
- No production driver registry, runtime selector, provider host, dependency-injection registration, manager command, scheduler hook, workflow hook, shell execution, package restore, Graph/Office call, workspace/storage write, process mutation, claim mutation, transition mutation, finalizer application or retry scheduling.
- Transcript and runtime-evidence verification must consume supplied immutable payloads only.
- Driver packages must not reference Modules, Infrastructure, AgentFramework, EF, UI, workspace, storage or connector packages.
- Core must not reference driver abstractions or driver implementations.
- Process-module consumers must be allow-listed adapters only.
- No UI/browser/mobile/small/medium proof unless unexpected UI changes occur; such changes should fail the bundle.
