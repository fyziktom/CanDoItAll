# Runtime architecture decision records

## ADR-R01 — Direct typed execution is primary

**Decision:** Ordinary runtime/tool commands execute from typed plans. Terminal text is display/presentation only.

**Rejected:** PowerShell/POSIX shell as universal transport.

## ADR-R02 — One low-level process primitive

**Decision:** Reuse/harden the existing workspace process host or an extracted lower primitive. Tools/plugins do not implement divergent local runners.

**Rejected:** Separate process code per MCP/tool/plugin.

## ADR-R03 — Registry-first process ownership

**Decision:** Persist launched-process identity. WMI/proc/macOS discovery is bounded recovery evidence.

**Rejected:** Name-only or command-substring termination.

## ADR-R04 — Environment and executable semantics are host-correct

**Decision:** Preserve environment key semantics and resolve/authorize executable identity deterministically.

**Rejected:** Global `OrdinalIgnoreCase` and universal `.exe/.cmd/.bat` candidates.

## ADR-R05 — Terminal and elevation are optional capabilities

**Decision:** Direct headless execution does not require a terminal; Unix/macOS elevation is unavailable by default.

**Rejected:** Automatic sudo/pkexec/osascript mapping.

## ADR-R06 — Controlled Playwright MCP tool root

**Decision:** Production MCP uses a pinned managed installation, not global npx cache discovery.

**Rejected:** Newest recursive cache match.

## ADR-R07 — External dependency claims are quarantinable

**Decision:** FileTools/Docker/native capabilities can be disabled independently and are supported only for tested profiles/versions.

**Rejected:** Inferring support from package metadata or executable presence.

## ADR-R08 — Processes owns semantics

**Decision:** Host capabilities feed process strategies, but Processes owns eligibility, recovery, evidence, escalation, and failure meaning.

**Rejected:** Generic MAF/Infrastructure platform service deciding process outcomes.

## ADR-R09 — Reuse one execution mechanism through owner-specific adapters

**Decision:** B01 first hardens a narrow host-correct execution mechanism and lifecycle contract. Workbench, Manager, MCP, external-tool, and plugin surfaces retain their own typed plan compilers and domain results but adapt execution to that mechanism where dependency direction permits. Security native helpers remain separately gated because their redaction and credential boundary is materially different.

**Pattern:** Strategy for executable/environment/host profiles; Adapter for existing owner-specific launch contracts; Factory only where a scoped process/session lifetime must be created. No service-locator or process-domain facade is approved.

**Rejected:** A broad platform service, a new Infrastructure-owned process authority, direct construction in every consumer, or moving all domain launch logic into MAF.

## ADR-R10 — Existing runtime split satisfies size and ownership triggers

**Decision:** Retain B01–B07 as the execution units. B00 found more than eight ownership boundaries and a likely change set above 60 production files, but those concerns are already separated by executable gates and owners. B90/B91 are conditional recovery paths rather than mandatory work.

**Rejected:** A single runtime implementation phase or premature project extraction before dependency evidence requires it.

## ADR-R11 — B01 keeps one process primitive behind owner-specific adapters

**Decision:** `LocalWorkspaceProcessHost` remains the sole low-level `System.Diagnostics.Process` implementation for B01-owned production paths. Workspace commands use it directly; external-tool and Git contracts use narrow adapters at the AgentFramework composition boundary; Windows `subst` compatibility uses the same host with typed arguments and an async session lifetime. Managed `dotnet run` starts the `dotnet` executable directly through `IWorkspaceLongRunningProcessHost`; its typed session owns readiness, detach, disposal, and tree termination without a PowerShell `Start-Process` intermediary. Durable kept-alive leases terminate only a PID whose exact UTC start timestamp and kernel-reported executable fingerprint match the recorded owned identity. Per-execution workspace factories may construct one explicitly owned host for their aggregate, while dependency-injection consumers receive the configured host.

**Policy:** `WorkspaceExecutableLocator` owns host-correct executable identity and `WorkspaceCommandEnvironmentPolicy` owns host-correct inherited environment selection. Windows uses ordered `PATHEXT` and case-insensitive environment keys. Linux/macOS use exact executable names, execute permission, final symlink identity, and ordinal environment keys. Ambient credential variables are excluded; explicit recipe bindings may add them. Receipts and persisted previews pass through `SensitiveTextRedactor`, and external-tool diagnostics never copy process output.

**Native escalation decision:** Actual Windows and Linux descendant-process tests prove that `Process.Kill(entireProcessTree: true)` plus bounded exit confirmation satisfies B01. A Job Object or Unix process-group adapter is therefore not introduced. Any future unconfirmed termination is returned as `TerminationFailed` with `ResidualProcessPossible=true` rather than hidden.

**Deferred boundary:** MCP launch resolution remains B04, Manager supervision B03, and Docker plugin policy B05. Those owners may adapt to the same primitive but do not move their domain semantics into AgentFramework Core.

**Rejected:** Retaining `DefaultGitCommandExecutor` or `LocalExternalProcessRunner` as independent process implementations, shell reconstruction from display strings, blanket case-insensitive environment handling, silent residual-process success, and native adapters without failing actual-host evidence.

## ADR-R12 — B02 keeps runtime semantics in Workbench and makes presentation optional

**Decision:** `ProjectStructureRuntimePlanCompiler` remains the pure owner-specific
compiler. `ProjectStructureRuntimePathResolver` adapts Core path authority,
`ProjectStructureRuntimeExecutionAdapter` consumes the B01 long-running process host,
and terminal/elevation remain narrow Workbench presentation adapters. The existing
launcher is retained only as a compatibility facade over those responsibilities.

**Policy:** .NET, Docker, Python, Node/Tailwind, and other ordinary runtimes execute
directly. PowerShell and POSIX shell are explicit script kinds. Linux/macOS terminal
presentation requires explicit configuration, and elevation is unavailable by
default. Windows `runas` remains a separate explicit action.

**Rejected:** A new project or broad platform service, PowerShell/POSIX as universal
transport, automatic terminal discovery on Unix, automatic privilege escalation,
generic parsing of dynamic legacy shell text, or a Workbench-owned duplicate process
host.

## ADR-R13 — B03 uses registry-first Manager supervision over the B01 host

**Decision:** Manager-launched Watch, Tailwind, and tuning processes use one composition-owned `LocalWorkspaceProcessHost` through `ManagerProcessCoordinator`. Manager retains the durable non-secret registry, typed purpose/lease policy, recovery state, and supervisor semantics. A recovered PID is actionable only after exact registry-first verification of start identity and the available executable, owner, command, and workspace evidence. Parent identity must match the current Manager at launch and is persisted for audit, but recovery intentionally permits Unix reparenting after the original Manager exits.

**Platform policy:** Windows WMI is isolated in `WindowsManagerProcessDiscovery`; Linux reads bounded `/proc` records; macOS reads microsecond start/parent/owner identity through kernel `libproc`, then executes `/bin/ps` through the canonical host and strictly parses one invariant-locale executable/command record. Permission denial, interruption, missing data, malformed or oversized data, process races, and mismatches remain diagnostic/manual and never authorize termination.

**Lifecycle policy:** Live Manager sessions request graceful stop followed by bounded force-tree termination. Recovered identities use exact verified force-tree termination; the B01 host independently revalidates the exact recorded start timestamp and executable identity before killing. Watcher hints are never authoritative; generation/fingerprint rescans, duplicate suppression, overflow recovery, and polling fallback converge state. Physical path comparison uses the detected filesystem comparer.

**Rejected:** Name-only or substring-only process ownership, broad process enumeration, WMI in neutral code, raw argv persistence, duplicate `Process` runners, unbounded platform parsing, blanket case-insensitive physical paths, or substituting filesystem text before argv tokenization.

## ADR-R14 — B04 adapts interactive MCP stdio to the canonical process host

**Decision:** `LocalWorkspaceProcessHost` gains a narrow derived duplex-session contract. It continues to own process creation, exact process identity, stderr bounds, cancellation, disposal, and tree termination; the MCP runtime receives only stdin/stdout streams and never receives the underlying `Process`. The MCP project continues to own JSON-RPC framing and protocol semantics.

**Executable and environment policy:** Commands are resolved first by `WorkspaceExecutableLocator`, then the resolved executable file name is checked with host-correct capability-owned semantics. Windows extension and case rules are not projected onto Linux/macOS. MCP environment construction starts from `WorkspaceCommandEnvironmentPolicy`; runtime-resolved bindings are added only to the ephemeral launch request and are not retained in the owned process session or copied into diagnostics.

**Playwright policy:** Production accepts an exact `@playwright/mcp` version and installs it beneath a versioned `.agent-tools/npm/playwright-mcp/<version>` directory. A temporary sibling is verified against package metadata, a CLI SHA-256 marker is written, and publication uses an atomic no-replace directory move so a concurrent winner is preserved. Global npx cache scanning and `latest` are rejected as authorities.

**External-tool policy:** External JSON tools retain stdin JSON, exit interpretation, and output-schema ownership. Their composition adapter resolves and authorizes the final executable, then delegates timeout, cancellation, environment, bounded output, and tree cleanup to B01.

**Rejected:** A second process implementation in MCP, exposing `Process` through a protocol adapter, name-only pre-resolution authorization, global npm cache selection, non-versioned in-place package installs, raw secret values in receipts/diagnostics, or a new cross-project platform facade.

## ADR-R15 — B05 keeps plugin semantics local and makes host integrations explicitly optional

**Decision:** Docker retains recipe and dependency-state semantics in its plugin, but executable resolution, inherited environment selection, workspace scope, process execution, timeout, cancellation, bounded output, and tree termination are injected B01 authorities. One scoped Docker service implements both execution and capability-probe contracts. Executable, context, daemon, and endpoint state remain separate typed values.

**Environment and disclosure policy:** Only the named Docker environment contract is inherited with host-correct key semantics. Docker configuration, certificate, and socket paths pass through the physical path authority before execution. Endpoint/configuration values and shared secret-shaped text are redacted from results and diagnostics.

**Desktop policy:** FileTools remains the OS-delegation adapter. The application requires explicit feature enablement, an interactive runtime host profile, a package-reported desktop session, trusted/reparse-safe file authority, and a host-bound preferred application when configured. Service/headless profiles fail before delegation. Cancellation is guaranteed before desktop delegation; an application already accepted by the OS shell is not represented as recallable.

**Development dependency identity:** Package fallback pins remain intact, while this implementation run selects direct sibling Components/FileTools project references. Compatibility evidence binds the exact sibling commits and FileTools working-tree files. NuGet publication/re-pinning is a separate later operation.

**Rejected:** A plugin-local process host/resolver/environment stack, ambient Docker credential inheritance, executable-presence-only support claims, automatic GUI attempts from service/headless profiles, silent foreign executable rebinding, or claiming deterministic macOS fixtures as actual-host evidence.

## ADR-R16 — B06 consumes scoped typed host facts without transferring authority

**Decision:** Process drivers and strategies declare stable `ProcessHostCapabilityId` requirements. Scoped host adapters project only typed availability, reason, execution port, and a bounded host-profile ID. `Processes` owns driver/strategy eligibility, missing-capability diagnostics, alternate-strategy selection, immutable plan evidence, and failure meaning. Driver-wide requirements are checked during catalog matching; only active strategy requirements are checked during binding, so an unavailable unused alternative does not block a valid plan.

**Adapter policy:** AgentFramework execution composition reports generic runtime facts, application composition projects desktop/terminal profile facts, Docker projects its B05 dependency snapshot, and the injected Process execution adapter reports its own registered port. Duplicate facts for one stable ID fail closed. Workflow executor availability is interpreted by the Process launch resolver before binding, without copying adapter diagnostic messages.

**Authority and disclosure policy:** Capability availability may only block. Canonical agent execution authority, workspace/project scope, tool policy, approvals, and recipe grants remain independently mandatory. Process plans persist and hash only stable capability/profile fields; physical paths, endpoints, timestamps, probe messages, and secrets are excluded.

**Platform-layer policy:** `ProcessDriverLayer.Platform` is a Process strategy-composition layer. It cannot acquire OS detection, executable resolution, filesystem/secrets access, native process creation, terminal discovery, or plugin dependency probing.

**Rejected:** A process-global mutable capability registry, OS branching inside Process drivers, capability facts that grant authority, copying plugin/host messages into Process receipts, blocking valid strategies because an unused alternative is unavailable, or moving Process recovery/escalation meaning into MAF or Infrastructure.
