# Workaround Decision Register

A workaround is not removed because an upstream release note sounds related. It is removed only when a characterization test demonstrates that MAF 1.15 provides the required behavior on the real CanDoItAll path.

| ID | Current mechanism | Decision | Reason | Proof required |
|---|---|---|---|---|
| W01 | `HandoffDepthGuardAgent.RunCoreAsync` streams and calls `updates.ToAgentResponse()` | **Rewrite** | It bypasses upstream non-streaming terminal-output projection | Direct MAF vs wrapper vs full-runtime output fixture |
| W02 | Main runtime collects all `AgentResponseUpdate` and calls `ToAgentResponse()` | **Keep temporarily; redesign projection for workflows if needed** | Correct for ordinary agents; potentially wrong for workflow terminal selection | Ordinary agent, handoff, reasoning, multi-response, tool adjacency matrix |
| W03 | Any custom update sorting/grouping/synthetic message IDs in snapshot code | **Audit; remove only duplicative logic** | Can reintroduce fixed merge/order defects | Failing-first fixture for each removed transform |
| W04 | Custom required-finalizer tool and validation | **Keep** | It enforces application-owned typed output and tool sequence, not merely workflow output selection | Compare trigger rate before/after handoff fix; no semantic removal |
| W05 | Finalizer repair/fallback after provider completion | **Keep until isolated proof** | May handle provider and model failures unrelated to MAF workflow merge | Failure taxonomy and targeted fixtures |
| W06 | Process-local raw approval-request cache | **Keep only as optimization** | Persistent state must be authoritative across restart | Restart and multi-instance tests |
| W07 | Rehydrate `ToolApprovalRequestContent` from custom record | **Require restored native 1.15 binding; reject legacy reconstruction** | The exact serialized session is authoritative; a 1.13 record cannot fabricate binding state | Function/MCP/restart and legacy drain/reissue tests |
| W08 | Generate random approval ID when request ID is missing | **Remove** | Breaks exact binding and can authorize a different logical request | Missing ID fails closed |
| W09 | Apply one `approved` boolean without proving the pending snapshot is unchanged | **Constrain to the complete current snapshot** | The existing application decision remains valid only when atomically bound to the exact server-held snapshot | Snapshot-change and mixed pending tests |
| W10 | `ShouldReplayTranscriptAfterApproval()` always returns `false` | **Remove or implement intentionally** | Dead compatibility hook obscures behavior | Static and behavioral proof |
| W11 | Opaque JSON `conversationId` sniff | **Keep in compatibility pass; consider typed metadata later** | It selects provider/framework history before deserialize and is application policy | Cross-version session matrix |
| W12 | Five-second session serialization timeout | **Keep initially** | Bounded persistence is operational policy | Timeout test and telemetry |
| W13 | Catch-all session serialization failure returning `null` | **Rewrite diagnostics** | Hides defects during migration | Typed result/structured logs for timeout, JSON, incompatibility, provider error |
| W14 | Request-scoped attachment scrubber | **Keep** | Privacy/lifecycle requirement not replaced by MAF | Ensure new state-bag approval data survives scrub |
| W15 | Governed process step uses isolated session | **Keep** | Process state is persisted through typed outcomes/artifacts | Process regression |
| W16 | Custom workspace file/command/artifact tools | **Keep** | Rich application security and integration boundary | Full file/capability security suite |
| W17 | `DefaultAgentToolInvocationPolicy` and script inspection | **Keep** | Application authorization and governance | Mutation/read/external alias tests |
| W18 | Filter approval-required mutation tools on unsupported transports | **Keep** | Provider capability and fail-closed behavior | Provider matrix |
| W19 | `AllowMultipleToolCalls = !hasApprovalTools` | **Keep for parity; reassess after mixed-call adoption** | Limits ambiguous approval batches; new bypass may make a different strategy viable | One/multiple/mixed tool-call matrix |
| W20 | Project-wide `MAAI001` / `MAAIW001` suppression | **Narrow after warning audit** | Some APIs stabilized; other experimental use remains | Unsuppressed warning inventory |
| W21 | Custom workflow/checkpoint assembly compatibility code, if discovered | **Candidate removal** | 1.15 fixes type identity matching | 1.13 checkpoint resumed under 1.15 |
| W22 | Custom tool-call/result reorder or dedupe code, if discovered | **Candidate removal** | 1.15 fixes workflow-hosted message ordering | Stored history and response adjacency |
| W23 | Custom approval substitution checks, if discovered | **Keep defense-in-depth or simplify carefully** | MAF binding is necessary but application policy may add ownership/session checks | Threat-model mapping |
| W24 | Immutable preparation/preload snapshots | **Keep unchanged** | Correct architecture; unrelated to upstream defects | Concurrency and stale-revision tests |
