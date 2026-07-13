# QA Prompt

Review the completed subbundle against the bundle, not only against passing tests.

Check:

- every raw input owned by the subbundle has a closure result;
- branch outcome behavior is proved for accepted, repair, blocked, and no-route cases when applicable;
- generic runtime/application code has no new domain hardcodes;
- templates and artifact templates are migrated or explicitly exempted;
- browser/host proof is captured for user-visible or runtime lifecycle behavior;
- proof manifests and semantic invariant contracts exist for critical subbundles;
- referenced `repo://` and `bundle://` paths resolve;
- downstream dependencies were checked before the next subbundle starts.

Reject closure if:

- proof is prose-only;
- tests assert only counts/non-empty output;
- a repair branch passes without concrete defect evidence;
- missing proof caused by QA omission is treated as product repair;
- a new abstraction delegates all behavior back to the old adapter or builder.
