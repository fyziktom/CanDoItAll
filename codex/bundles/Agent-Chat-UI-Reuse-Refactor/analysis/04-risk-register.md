# Risk register

| Risk | Likelihood | Impact | Control |
|---|---:|---:|---|
| neutral project accidentally references Agent/LlmChats types | medium | critical | CP1 dependency/source guards |
| new project creates a cycle | low | critical | CodeAnalytics before/after dependency proof |
| compatibility wrapper changes DOM/CSS behavior | high | high | baseline screenshots, selector inventory, bUnit + focused browser proof |
| AgentChatPanel grows through new partial files | high | high | explicit prohibition; independent adapters and no net responsibility growth |
| generic component becomes a boolean god component | high | high | focused models/slots; architecture review |
| agent-only approvals/tools leak into neutral contracts | high | high | adapter-owned adornments/slots |
| settings extraction accidentally changes save semantics | medium | high | markup-only extraction; existing code-behind retained |
| Process or contextual consumers break | medium | high | consumer inventory and SB08 migration gate |
| broad test loops consume hours | high | high | impacted-test protocol and final-gate budget |
| zero-discovery test filter is accepted | medium | high | fail proof on zero/unexpected discovery |
| Simple Chat integration begins early | medium | critical | source/diff phase guard and terminal status |
| CSS isolation changes visual output | medium | high | rendered baseline and new owner component tests |
| hidden context becomes visible or malformed | medium | high | adapter parser tests and transcript parity |
| floating close/hide/stop semantics change | medium | high | targeted component/browser scenarios |
