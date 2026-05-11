# Input Coverage Matrix

| Raw note | Normalized requirement coverage | Owning subbundles |
| --- | --- | --- |
| RN-001 Add AI workflows into application using Microsoft Agent Framework. | RQ-001, RQ-002 | 01, 02, 03, 07, 08 |
| RN-002 Workflows are a possible substitute for AI agents for some work. | RQ-003, RQ-004 | 01, 02, 06 |
| RN-003 Workflows do not replace processes; processes are above workflows and agents. | RQ-003 | 01, 06, 08 |
| RN-004 A process role can be filled by an AI agent or workflow. | RQ-004, RQ-020 | 06 |
| RN-005 Phase 1 improves MAF wrapper libraries for workflow models/helpers/wrappers. | RQ-005 | 01 |
| RN-006 Phase 2 improves Agents module with its own workflow page. | RQ-019 | 04 |
| RN-007 Another phase integrates into web app. | RQ-008, RQ-009, RQ-010 | 03, 04, 05, 07 |
| RN-008 Architecture reviews after each phase, detailed after phase 1. | RQ-006, RQ-007, RQ-021 | 01, 02, 04, 05, 06, 08 |
| RN-009 Workflows need own settings and testing like processes. | RQ-008, RQ-009 | 03, 07 |
| RN-010 Workflow needs own canvas editor similar to processes. | RQ-010 | 05 |
| RN-011 Workflow has artifacts, human-in-loop, agent step usage. | RQ-011, RQ-012, RQ-013 | 01, 02, 05 |
| RN-012 Workflow mostly calls LLM with strict instructions, captures result, passes to next step, triages, or runs strict logic. | RQ-014 | 01, 03, 05 |
| RN-013 Prepared LLM Call Component library. | RQ-015, RQ-016 | 01, 03, 05 |
| RN-014 Analyze MAF runtime; use core if possible. | RQ-017 | 01, 02 |
| RN-015 If MAF does not provide enough management, add wrapper/additional library for parallelism and observations. | RQ-018 | 01, 02 |
| RN-016 Official durable workflow article recommends DurableTask/DTS for durable, observable, long-running workflows. | RQ-022, RQ-023, RQ-024 | 01, 02, 07, 08 |
| RN-017 Article describes Azure Functions hosting, generated HTTP endpoints, RequestPort response/status endpoints, and optional MCP tools. | RQ-025 | 02, 07, 08 |
| RN-018 Performance skill requires runtime/API hot-path scan and validation gates. | RQ-026 | 01, 02, 03, 07, 08 |
