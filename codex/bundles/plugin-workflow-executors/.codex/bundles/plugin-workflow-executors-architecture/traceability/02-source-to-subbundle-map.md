# Source To Subbundle Map

| Source Ids | Subbundles | Reason |
| --- | --- | --- |
| S001-S004 | SB02, SB03, SB12, SB17 | Executor contracts/models/validator must become plugin-ready. |
| S005-S008 | SB02, SB12, SB17 | Built-in executor descriptors/DI must avoid collisions and preserve compatibility. |
| S009-S012 | SB04, SB11, SB12, SB17 | API and workflow UI must display plugin executors and settings without hard-coded branches. |
| S013-S018 | SB05, SB11, SB13, SB17 | Secret vault/runtime resolver/UI become plugin secret broker foundation. |
| S019-S024 | SB06, SB13, SB17 | Storage/workspace/project structure services become capability-gated facades. |
| S025-S029 | SB03, SB04, SB09, SB11 | Connector schema/registry concepts are reused for plugin settings/catalog. |
| S030-S036 | SB10, SB11, SB15, SB17 | Settings/composition/nav/API surfaces receive plugin module integration. |
| S037-S039 | All | Existing bundle style and prior workflow/vault constraints shape this bundle. |
| S040-S043 | SB17 | Tests and browser proof use existing test project surfaces. |
| S044-S046 | SB03, SB04, SB05, SB11, SB12, SB17 | Canonical configuration, trusted renderer registration, and secret broker foundations are reused by plugin settings and execution. |
| S047-S051 | SB06, SB07, SB12, SB13, SB17 | Plugin-safe workspace/storage/project-structure facades and execution observability support bundled plugin executor runtime access. |
| S052-S056 | SB09, SB11, SB12, SB13, SB15, SB16, SB17 | Plugin abstraction contracts define manifest, capability, settings, connection, execution, package, and OAuth metadata. |
| S057-S064 | SB10, SB11, SB12, SB13, SB14, SB17 | Plugins module catalog, persistence, API, composition, tests, and startup-query repair form the bundled plugin MVP foundation. |
