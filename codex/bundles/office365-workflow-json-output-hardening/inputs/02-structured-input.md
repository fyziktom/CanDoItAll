# Structured Input

| Raw ID | Exact source wording or artifact | Normalized concern |
| --- | --- | --- |
| N001 | `LLM workflow node 'summarize-office365' ... returned invalid JSON: '+' is invalid after a value.` | Office365 summary workflow LLM output is not reliably constrained to valid JSON before runtime validation. |
| N002 | `The app is running on http://localhost:5032.` | Validation can include the live running app when API or browser proof is useful. |
| N003 | `It is office365 workflow.` | Scope is the Office365 summary workflow path, not unrelated workflow templates. |
| N004 | `connected email has available email with correct category` | Final validation should attempt the real Office365 category workflow path or record a concrete blocker from the live environment. |
