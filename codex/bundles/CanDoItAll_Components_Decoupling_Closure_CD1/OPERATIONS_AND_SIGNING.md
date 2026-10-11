# Local signing, worktree safety and bounded resources

## PGP at entry and during the task

Ask the operator to unlock the existing local PGP signing identity at the beginning. Inspect
the configured signing program/key, commit signing settings and repository identity. Keep the
same GPG installation, user context, GNUPGHOME when configured, and dedicated commit environment.
Use the existing gpg-agent; do not create a replacement identity or expose a secret.

Create coherent tested checkpoints with actual signed commits (`git commit -S` under the
repository configuration) and verify each (`git verify-commit <sha>` or the documented trusted
verification equivalent). Record verified fingerprints/status without exporting key material.
A Signed-off-by footer is not a cryptographic signature. Do not disable signing or hooks,
rewrite user history, reset the worktree or include unrelated operator edits. No automatic
push/merge/deploy is authorized by this package.

An open terminal does not guarantee permanent cache unlock. If the agent's idle/maximum cache
lifetime expires, request local re-unlock; never fall back to an unsigned commit. Do not modify
cache security policy, collect passphrases in scripts/environment/logs, or bypass pinentry.
Separate source/test changes from report-only closure where helpful. Keep the tested source
and final signed report ancestry explicit; do not insert a commit's own unknown hash into itself.

## Reuse instead of accumulation

Read current AGENTS/Testing rules before provisioning. Use only Debug and Release, reusable
standard bin/obj and bounded existing framework/runtime/asset-mode suffixes. No configuration,
full output copy or Docker stack named for each bundle/stage/attempt. Small result directories
are not build-output trees. Serialize writers to common outputs and release owned file locks
before rebuild; reuse dotnetwatch where useful.

Use an existing explicitly isolated PostgreSQL 18 test endpoint and native lease mechanism
when suitable. Provision one owned temporary fixture only if none exists or a real destructive/
platform-specific case requires one. Protect the ordinary app/database on 5032, manual demos,
retained provider resources and installed MCP/tool shadow outputs. An ignored .env is not safe
to print; use private process input for fixture credentials.

Record ownership before creation: exact processes/start times, container IDs/labels, exclusive
volumes/networks, ports and canonical temp paths, with intended last use. Put disposal in
finally and verify after the last lane. On resume reconcile the old ownership ledger before
creating any replacement. Retain the minimal proof needed to understand a failure or completed
coverage; never delete the entire .artifacts tree or prune Docker globally.

## Existing operational qualification

PC4 reports two inactive retained paths whose deletion approval was denied:

- `.artifacts/mcp-installs/CanDoItAll.Mcp.CodeAnalytics/failed/failed-20261010-101825372`
- `.artifacts/pc4-recovery-20261010/codeanalytics-smoke`

Do not retry through another mechanism to bypass policy. Recheck and record their disposition
only if relevant. They are not permission to delete an active `current` tool installation and
not a component-extraction blocker. If newly owned cleanup is blocked, report exact remaining
resources and impact; do not claim successful cleanup or accumulate extra fixtures to avoid it.
