# Original Request

The user requested a complete detailed architecture proposal for a new Process module in `C:\repositories\CanDoItAll`.

The request requires a critical architecture and rewrite-preparation task, not a quick patch. The proposal must not omit, simplify, or compress away the listed requirements. It must prepare a strong foundation for a major Process module rewrite.

The current processes are unreliable, slow, and not generic enough, but they reveal important areas that a proper process system must solve: sharing artifacts between steps, supporting process steps as subprocesses, recovering and resupplying artifacts, process runtime orchestration, process monitoring, templates, branching and switch steps, and manager-driven recovery and escalation.

The current Process UI/UX is a useful reference point and anchor. The UI/UX direction should be preserved, but everything underneath it can be fundamentally refactored. Existing drivers may contain useful shape but must not be assumed correct. It is acceptable to refactor drivers significantly to improve stability, flexibility, and genericity.

The module must be thought of as an operating system. This iteration prepares only the architecture proposal. The architecture must clearly separate the generic process core, runtime execution, dispatcher responsibilities, process instance construction, process templates, domain-specific drivers, domain-specific strategies, process manager behavior, subprocess management, artifact management, monitoring and snapshots, and UI-facing projections and live/history views.

The core must remain generic but support layered domain drivers. Example layering for Blazor WASM: general software-development driver, .NET driver, Blazor driver, Blazor WASM sub-driver. Broad and specific drivers must compose without leaking domain terms into the generic runtime or dispatcher.

A high-quality process builder and appropriate factories are required. A process instance must be assembled for a specific run. The builder must compose process definition, process instance, roles, artifacts, steps, subprocess instances, selected drivers, selected strategies, recovery behavior, branch/switch behavior, manager behavior, and monitoring/snapshot configuration. If a step is another process, the subprocess must have its own builder executed, and nested subprocesses must be supported.

The Strategy pattern is required for runtime behavior, manager behavior, and especially step execution. A step may be a normal step, another process, a workflow, an agent, collaboration of multiple agents, or an automatic handoff flow such as Microsoft Agent Framework handoffs. The builder must assign the correct execution strategy to each step, and the assigned strategy is part of instance composition.

Completed steps cannot be discarded. Later steps may need artifacts from earlier steps. The architecture must model artifact ownership, sharing, availability, dependencies, recovery, resupply, parent/child references, branch inputs, manager inputs, and driver-specific recovery.

The architecture must include detailed exception and error handling, not only the happy path. Errors may be too detailed or too domain-specific for users, so the process manager must preprocess them into useful information. Users may configure manager-driven automatic resolution of certain error types. Generic mechanisms must isolate domain-specific concepts behind drivers and strategies. Parent and subprocess managers must communicate, with domain-specific communication variants behind strategies.

The process manager must oversee runs, understand step and subprocess results, preprocess errors, decide automatic resolution versus escalation, communicate with subprocess managers, request artifact recovery/resupply, invoke drivers and strategies, communicate useful UI/user information, prevent uncontrolled loops, and enforce recovery/escalation limits without hardcoding domain concepts into the core.

Processes require monitoring. Many processes may run concurrently. Runtime information must be emitted through an event/observer system. Live Processes UI should behave like a snapshot cache and should not reload everything from scratch unless the user requests a longer period or history. Time range filtering must work correctly. The architecture must define event emission, observer/subscriber mechanism, snapshots, live snapshot cache, historical persistence, filtering, UI projections, runtime isolation, and live/history behavior.

Templates must be modular. Global roles, artifacts, and steps must be reusable across processes. The architecture must support editing global components, editing local overrides, publishing global updates to usages, detecting conflicts, and manually resolving conflicts. This is similar to Git. Template data may be better stored as files with database indexing.

Templates need version markings and migration strategy. JSON is the source of truth. Markdown and Mermaid projections are questionable because the UI already shows process flow; the architecture must decide whether to store or generate them. If stored, migrations must handle them.

A Git wrapper is unavoidable. It must support versioning text-based configuration/instruction files, process system usage, agent usage, process manager usage, tracking changes during a process run, and checking whether agents modified unauthorized files. Generic Git UI components are needed for changes, diffs, commits, merges, conflict display/resolution, and status.

Switch/branch steps are important. They are often domain-dependent. The architecture must support generic branch/switch behavior, domain-specific branch definitions, selection from available definitions, user overrides/customization, and routes backward to previous steps. Loop protection must exist; repeated passes must trigger escalation when limits are exceeded.

Later implementation should happen on a new branch. The first implementation step should copy the old Process implementation into bundle/reference material, then remove the original Process implementation, including projects and tests, before rebuilding from the ground up. Useful existing parts should be identified for adaptation.

Because several architecture iterations are expected, bundles must be versioned. The first repository change must update `.gitignore` so bundles are versionable.

Expected deliverable: architecture bundle/proposal including current-state analysis, current runtime/dispatcher insufficiency, target architecture, generic/core versus driver boundaries, builder/factory design, strategy design, artifact lifecycle and recovery design, error handling and recovery strategy design, parent/subprocess manager communication, monitoring snapshots, template modularization/versioning/migration, JSON source-of-truth decision, Git wrapper and Git UI proposals, switch/branch design, loop protection/escalation, phased rewrite plan, reusable current pieces, test strategy, and `.gitignore` update.

