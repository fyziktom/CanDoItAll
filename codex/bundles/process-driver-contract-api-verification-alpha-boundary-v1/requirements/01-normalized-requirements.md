# Normalized Requirements

| ID | Requirement | Owner Subbundles | Proof |
| --- | --- | --- | --- |
| REQ-001 | Revalidate latest branch and prerequisite proof before implementation. | SB001-SB003 | Build, source scan, focused prerequisite tests |
| REQ-002 | Create contract-only driver abstractions without runtime behavior. | SB004-SB006 | Project dependency scan, API inventory, no runtime token scan |
| REQ-003 | Encode permission modes, capability scopes, and denial reasons as production contracts. | SB007-SB009 | Unit tests for allowed/denied operation matrix |
| REQ-004 | Define audit facts, redaction, and evidence reference descriptors. | SB010-SB012 | Redaction tests, secret/token leakage negative tests |
| REQ-005 | Define verification-only request/response contracts without mutation APIs. | SB013-SB015 | Mutation-denial architecture tests |
| REQ-006 | Keep driver registry, runtime selector, DI registration, manager command, and execution hooks absent. | SB016-SB018 | Forbidden production token scans |
| REQ-007 | Prepare `.NET/Rust transcript verifier` as test-only alpha rehearsal. | SB019-SB021 | Fixture tests, no shell/workspace/storage mutation tests |
| REQ-008 | Bridge Core descriptors to driver evidence vocabulary without reversing dependencies. | SB022-SB024 | Dependency direction tests |
| REQ-009 | Strengthen Office and business-analysis read-only lane denial tests. | SB025-SB027 | Graph/business mutation denial tests |
| REQ-010 | Produce compatibility/migration docs for future verification-only drivers. | SB028-SB030 | Docs + API snapshot gate |
| REQ-011 | Explicitly decide whether first production alpha is approved. | SB031-SB033 | Decision template + red-team gate |
| REQ-012 | Refresh stable Core/domain driver roadmap. | SB034-SB036 | Roadmap consistency tests |
| REQ-013 | Close with broad smoke, prepared validator, completed validator, and proof index. | SB037-SB042 | Build/unit/integration/source scans/validators |
