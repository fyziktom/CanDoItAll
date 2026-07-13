# Structured Input

## Raw Notes

| ID | Raw Note | Normalized Meaning |
| --- | --- | --- |
| M001 | "lets get back to it after proper refactoring of the MafAgentRuntime" | Agent-specific Financial Strategist fixes are deferred until MAF runtime architecture is improved. |
| M002 | "MafAgentRuntime ... is our main trouble" | The bundle must focus on generic MAF runtime architecture, not one downstream agent feature. |
| M003 | "huge class with partial classes instead of proper architecture isolations" | Partial files are not adequate responsibility boundaries; split into real collaborators. |
| M004 | "hard to unittest" | Extracted parts must be directly unit-testable without constructing the full runtime or using private reflection. |
| M005 | "it keeps us in lots of troubles" | The refactor should reduce future change risk and support later agent-specific features. |
| M006 | "Remove those things" | Remove margin calculation, quotation extraction, document-domain, and project-structure writeback implementation work from this bundle. |
| M007 | "isolation of drivers, strategies, helpers" | Define driver/strategy/helper boundaries with typed requests/results and clear ownership. |
| M008 | "analyze how it can affect performance" | Plan baseline and after-change measurement for startup, capability composition, provider attachment, and tool descriptor creation. |
| M009 | "achieve better testability ... mock them in case of integration tests" | Add a test harness strategy with mock provider clients, mock tool providers, mock context contributors, and fake workspace/MCP boundaries. |
| M010 | "stable base for future more specific cases" | The outcome is a generic runtime platform foundation, not a Financial Strategist-specific patch. |
| M011 | "do not do implementation" | This turn prepares and repairs the bundle only. |

## Scope Decision

- This remains an `initiative` bundle because it is a staged architecture refactor across runtime composition, provider execution, tool drivers, tests, and performance proof.
- The Financial Strategist scenario is retained only as a deferred example of why the foundation matters.
- Success means another implementation agent can refactor MAF in safe phases without guessing which responsibility belongs where.
