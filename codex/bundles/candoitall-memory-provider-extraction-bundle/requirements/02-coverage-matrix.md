# Coverage Matrix

| Original demand | Requirement IDs | Owning subbundles |
| --- | --- | --- |
| Separate Cognitive Memory core into standalone service. | R14, R15, R16, R17, R18 | SB24-SB31 |
| Create generic module for multiple memory providers. | R01, R02, R03, R04, R05 | SB01-SB10 |
| Base app should not need Qdrant. | R17 | SB07, SB24, SB27, SB30, SB33 |
| Memory module remains as UI and interfacing wrapper. | R12, R13 | SB20-SB23 |
| Support simple query-response memories. | R01, R03 | SB01, SB07, SB32 |
| Support living/eventful memories. | R04, R05, R06 | SB03, SB09, SB27, SB28 |
| Add generic MAF tool and workflow executor with provider selection. | R09, R10, R11 | SB15-SB19 |
| Native memory may depend on MAF but MAF must not depend on native memory. | R11, R15 | SB18, SB19, SB28, SB30 |
| Native memory owns its own DB and EF. | R14, R15, R18 | SB24, SB25, SB26, SB29, SB31 |
| Ingestion from projects, processes, CRM, resources. | R07, R08 | SB04, SB11-SB14 |
| Protocol includes structured context metadata. | R01, R07 | SB01, SB04 |
| Remove current memory hard links in MAF. | R11 | SB15-SB19, SB30 |
| Handle network timeouts and multi-minute requests. | R03, R04 | SB03, SB07, SB09, SB10 |
| Delayed feedback and outcome tracking. | R06 | SB03, SB09, SB21, SB33 |
| Optional IPFS snapshots and unpin on forget. | R06 | SB03, SB06, SB09, SB31 |
| Future native features must remain possible. | R01, R05, R13, R16 | SB01, SB03, SB22, SB27-SB29 |
| Mocking and testability. | R19, R20 | SB05, SB10, SB14, SB19, SB23, SB29, SB32-SB34 |
| Current MAF refactor and live native repo state must be reflected before implementation. | R21 | SB04, SB05, SB11-SB19, SB24-SB30, SB33-SB34 |
| Provider has not been configured yet, but the app must still work. | R02, R10, R12, R17, R21 | SB02, SB06, SB15-SB18, SB20, SB23, SB30, SB33 |
| Repair the live implementation rather than accepting historical bundle completion labels. | R20, R21, R27 | SB35, SB40 |
| Configure whether an agent uses memory automatically, only through an explicit prompt directive, or not at all. | R22, R24 | SB37, SB40 |
| Let one agent use multiple memory providers selected by strongly typed settings and aliases. | R02, R10, R23, R24 | SB36, SB37, SB40 |
| Support `/mem:memory1` without leaking routing syntax into the provider or allowing an agent to escape its bindings. | R24, R25 | SB37, SB40 |
| Never select an implicit first provider and never expose operation status or cancellation across requester/runtime boundaries. | R02, R25 | SB36, SB40 |
| Preserve project, workspace, process, workflow, agent, and session identity across MAF and provider protocol calls. | R01, R10, R26 | SB37, SB38, SB39, SB40 |
| Replace capability-grouping partial classes and misplaced helpers with cohesive, testable project/folder/namespace ownership. | R20, R27 | SB35, SB36, SB37, SB38, SB40 |
| Preserve driver configuration and secrets safely while making HTTP, MCP, native, and mock registration truthful. | R03, R28 | SB38, SB40 |
| Secure the external Cognitive Memory service and prove it works through the real main-app driver. | R16, R19, R26, R29 | SB39, SB40 |
