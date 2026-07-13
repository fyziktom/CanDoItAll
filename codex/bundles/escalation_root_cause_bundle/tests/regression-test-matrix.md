# Regression test matrix

## Matrix A – The exact incident

| Given | Expected |
|---|---|
| `Calculator.slnx` exists but contains only `<Solution></Solution>` | Product readback gate fails |
| `src/Calculator/Calculator.csproj` exists | Required path gate passes for app project |
| receipts include `workspace_dotnet_new template=sln` | scaffold receipt gate passes for solution scaffold |
| receipts include `workspace_dotnet_new template=blazorwasm` | scaffold receipt gate passes for app scaffold |
| receipts do not include `workspace_pwsh_run_script` | required tool receipt gate fails |
| diagnostic metadata safe/idempotent | recovery is `SafeRetry/CurrentStepRetry` |
| first attempt | no manager escalation |
| same fingerprint exceeds budget | manager escalation with concrete root cause |

## Matrix B – Placeholder resolution

| Given | Expected |
|---|---|
| `CurrentProcessRunId=ab4...` and `DotNetCreateProjectScriptRef=artifacts/process-runs/{CurrentProcessRunId}/scripts/x.ps1` | resolved ref `artifacts/process-runs/ab4.../scripts/x.ps1` |
| unresolved `{Unknown}` in `*ScriptRef` | launch/template validation error |
| cycle `A={B}`, `B={A}` | resolution issue, no agent dispatch |
| unresolved placeholder in non-tool prose allowlisted | warning or unchanged according to options |

## Matrix C – Subprocess propagation

| Given | Expected |
|---|---|
| parent runtime-owned subprocess step has no exact MAF execution run | UI/projection does not imply missing evidence; it points to child run |
| child run is Blocked with latest receipt diagnostics | parent diagnostic contains child code and summary |
| child physical artifact exists but `ProducedArtifactsJson=[]` | parent bridge does not accept it as child output |
| child completed with accepted slot in ledger | parent bridge synthesizes accepted parent artifact |
| child completed with no-go slot | parent propagates no-go, not blind retry |

## Matrix D – Recovery routing

| Diagnostic | Metadata | Expected route |
|---|---|---|
| `process.adapter.product_required_file_content_missing` | safe/idempotent | `SafeRetry/CurrentStepRetry` |
| `process.adapter.product_required_tool_receipt_missing` | safe/idempotent | `SafeRetry/CurrentStepRetry` |
| denied tool or policy violation | unsafe or policy | `ManagerAction` or `TemplateRepair` |
| missing upstream artifact with responsible upstream step | safe/idempotent | `UpstreamStepRework` |
| child no-go | n/a | `ChildRunPropagation` or manager depending policy |
| repeated same safe retry fingerprint beyond budget | safe/idempotent | `ManagerRequired` |

## Matrix E – Managed artifacts

| Given | Expected |
|---|---|
| structured finalizer valid, product gates fail | artifact is staged/rejected, not accepted slot |
| product gates pass | artifact slot is produced and parent bridge can use it |
| rejected artifact has `Status: Completed` from agent | runtime rejection is visible and prevents bridge acceptance |
| appendix text before gates | must not say full completion accepted |

## Matrix F – Agent/template readiness

| Given | Expected |
|---|---|
| step execution class `DeterministicToolPlan` | runtime executor/guard selected |
| step requires `workspace.script.run-pwsh-product-mutation` | assigned agent or executor must have explicit capability |
| tool name exists but capability/path preflight fails | launch/assignment repair or template repair, not vague agent blocker |
| template subprocess accepted child output step missing | template validation error |
