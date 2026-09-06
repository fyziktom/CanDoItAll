# Evidence retention erratum — 2026-09-06

The original closure execution remains historical. Its proof files were available locally but excluded from the pushed branch by the root `proof/` ignore rule. The earlier description of retained branch proof was therefore incomplete.

CDA-UI-SEAMS-CATALOG-HARDEN-02 verified every original manifest entry against the surviving bytes before making any change. All original proof was present and matched; no measured values were reconstructed and no old execution was relabeled as a new run. This follow-up retains the existing compact proof set, including compressed receipts and referenced screenshots, with a precise ignore exception. Raw working logs remain transient.

The updated manifest hashes the exact retained Git blob bytes. Bundle-local attributes preserve original proof bytes; maintained documentation uses LF. The original entry manifest is separately retained by the follow-up bundle. This is an artifact-retention repair, not a new product validation or benchmark.

See the [follow-up bundle](../../UI_AgentCatalog_Harden_02_Reload_Evidence_Bundle/README.md) and its [Git evidence validator](../../UI_AgentCatalog_Harden_02_Reload_Evidence_Bundle/tools/validate-evidence.py). The original execution times, negative runs, exclusions and limitations remain unchanged.
