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
