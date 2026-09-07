# Owner delivery verification

This task creates no commit or push. The real order is:

1. Review, commit and push the exact Components patch listed in the final source receipt.
2. Update primary dependency pins to that available Components revision through the separately owned CI change, then commit/push primary source and retained proof. This task does not edit CI or invent a future SHA.
3. In a clean primary checkout, run the commands below against the actual pushed commit. Supply the explicitly recorded clean sibling roots for the direct Module/Web builds, as demonstrated by A04.

From the primary repository root:

```powershell
$deliveredCommit = (git ls-remote origin refs/heads/components-decoupling | ForEach-Object { ($_ -split "\s+")[0] })
git cat-file -e "${deliveredCommit}^{commit}"
python tools/Validation/bundles/test_delivery_tools.py
python tools/Validation/bundles/validate_bundle_delivery.py --git-ref $deliveredCommit --bundle codex/bundles/UI_AgentsOverview_01_State_Read_Seams_Bundle --bundle codex/bundles/UI_AgentGovernance_01_State_Read_Seams_Bundle --bundle codex/bundles/UI_AgentsOverview_01D_PostPush_Delivery_Closure_Bundle
```

Use a fresh clone or explicitly fetch the actual remote commit before this owner verification. If the observed commit is not available locally, stop; do not substitute a stale local remote-tracking ref. The actual-tree command reads Git blobs. Ignored local files cannot satisfy it. An absent receipt, unresolved relative Markdown link, unsealed member or mismatched hash fails the command. Run without `--git-ref` before commit to validate tracked plus non-ignored untracked proposed files. Neither mode stages files, mutates history or treats a missing tree file as present.

Historical proof and this follow-up's retained evidence use scoped `-text`; maintained text remains LF. Proposed validation also compares each sealed file with Git clean-filter output, so a future line-ending conversion fails before commit. Do not regenerate old child manifests as if historical execution happened again. Root manifests describe the current retained bundle. New receipts belong to this follow-up, with their actual source identity and timestamp.

Repository merge readiness remains blocked while required sibling bytes/pins or the inherited documentation gate are unresolved.
