# Subbundle 04: Documentation and Merge Evidence

## Goal

Record exactly what changed and why it is safe to merge.

## Required evidence file

Create or update:

`docs/maf-1.13-update-evidence.md`

## Required contents

- Before/after package table.
- Restore/build/test command list.
- Result summary for each command.
- A2A preview package decision.
- Mem0 preview package decision.
- Any source API changes made for compatibility.
- Any tests skipped and exact reason.
- Confirmation that no direct process runtime tool provider was introduced.
- Confirmation that process API route scope was not expanded.
- Confirmation that no new MAF features were adopted in phase 1.

## Final checks

```powershell
git status --short
git diff --check
rg "Microsoft\.Agents\.AI\" Version=\"1\.8\.0|Microsoft\.Agents\.AI\.OpenAI\" Version=\"1\.8\.0|Microsoft\.Agents\.AI\.Workflows\" Version=\"1\.8\.0" src tests tools -g "*.csproj"
rg "ProcessAgentRuntimeToolProvider|/api/processes/definitions|/api/processes/templates|ProcessManagerTools" src tests docs -g "*.cs" -g "*.md" -g "*.json"
```

## Exit criteria

- Evidence doc is accurate.
- Final scans pass or intentional historical mentions are clearly marked.
- Diff is reviewable.
