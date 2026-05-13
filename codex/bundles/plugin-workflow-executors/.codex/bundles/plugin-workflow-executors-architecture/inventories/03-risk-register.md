# Risk Register

| Id | Risk | Impact | Likelihood | Mitigation | Owned By |
| --- | --- | --- | --- | --- | --- |
| RK001 | Plugin settings duplication | High | High | Extract/adapt canonical settings schema and renderer host before plugin module. | SB03, SB04 |
| RK002 | Raw secret leakage through plugin settings or workflow JSON | Critical | Medium | Consumer-bound secret broker; no raw secret persistence; sanitized logs and tests. | SB05, SB07, SB17 |
| RK003 | Dynamic plugin code trust problem | Critical | Medium | MVP supports bundled plugins only; shop contract separates catalog/install from code loading; signatures/hashes required later. | SB10, SB15 |
| RK004 | Workflow catalog collision | High | Medium | Unique plugin ids, executor ids, renderer keys; duplicate tests and startup failure semantics. | SB02, SB09, SB17 |
| RK005 | Project structure concrete service leakage | High | High | Extract canonical gateway before plugins can use project structure. | SB06 |
| RK006 | OAuth2 arrives too late and causes breaking changes | High | Medium | Add OAuth2 broker extension point and connection auth model now, implement providers later. | SB09, SB16 |
| RK007 | Plugin module depends on too many feature modules | Medium | Medium | Put contracts in abstraction projects; expose capability facades instead of referencing implementation modules. | SB06, SB09 |
| RK008 | Non-Windows vault instability | Medium | Medium | Review Auto provider fallback and require explicit supported provider for plugin deployment targets. | SB05 |
| RK009 | Plugin renderer becomes arbitrary remote UI execution | Critical | Low in MVP | Bundled renderers only in MVP; schema fallback for remote catalog; signed UI packages later. | SB04, SB15 |
| RK010 | Codex implementation drift | High | Medium | Mandatory review gates and execution report updates every few subbundles. | SB08, SB14, SB18 |
| RK011 | Runtime helper scattering | Medium | High | Require helper extraction review in review gates; no page-local service logic. | SB08, SB14 |
| RK012 | External service plugin outputs too large or unsafe | Medium | Medium | Payload limits, artifact capture policy, sanitized failures, tests. | SB07, SB13, SB17 |
