# Structured Input

```json
{
  "branch": "maf-processes-refactor",
  "reviewed_head": "df98a1066e59baa014f05799cfedd80db6ac0aee",
  "previous_bundle": "process-dispatch-artifact-validation-rule-boundary-v1",
  "next_bundle": "process-dispatch-tool-validation-recovery-boundary-v1",
  "core_policy": "Do not create or extract CanDoItAll.Processes.Core in this bundle.",
  "driver_policy": "Create driver-readiness inventories only. Do not create driver-pack APIs or implementations.",
  "viewport_policy": "No small, medium, mobile, tablet, phone, Android, iPhone, or responsive proof. Runtime refactor proof should be N/A; if UI proof unexpectedly becomes necessary, use large desktop/PC only.",
  "primary_hotspot": "ProcessRunAutomationDispatchService.ToolValidation.cs",
  "secondary_hotspots": [
    "ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs",
    "ProcessRunAutomationDispatchService.RecoveryDirective.cs",
    "ProcessRunAutomationDispatchService.RecoveryPackets.cs"
  ]
}
```
