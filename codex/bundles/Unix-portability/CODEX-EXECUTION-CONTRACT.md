# Codex execution contract

## Executor

Primary target: Codex 5.6 Sol xhigh, deepest available reasoning.

The prompts are model-neutral, but the executor must not compress multiple gates merely because it can process a large context.

## Session entry

At every session:

1. Read this file, the active bundle README, the active subbundle README/prompt/tasks, source anchor, findings, and relevant ADRs.
2. Run `git status --short --branch`, `git rev-parse HEAD`, and `dotnet --info`.
3. Read the previous session handoff and gate report.
4. Verify that prerequisite evidence exists and still matches HEAD.
5. Stop if unrelated changes would be overwritten or if the active subbundle is not eligible.

## Implementation discipline

- One mandatory subbundle at a time.
- Keep changes cohesive enough for one review/PR; split before implementation when the subbundle's own triggers fire.
- Add failing-first tests or explicit characterization before changing behavior.
- Preserve current public/data contracts unless the subbundle includes versioned migration.
- Prefer extending an existing authoritative owner to adding a facade over duplicates.
- Do not create broad `*PlatformService`, `*OsHelper`, `*RuntimeUtils`, or miscellaneous helper projects.
- Do not scatter `OperatingSystem.Is*` branches through business/domain/process code.
- Do not use conditional compilation merely to avoid correct runtime composition.
- Do not use shell command strings for ordinary direct execution.
- Do not log secret values, complete sensitive environment maps, tokens, or unredacted tool output.
- Do not weaken symlink, path, approval, workspace, TLS, tool, or process ownership policy to make another OS pass.
- Keep source-code comments in English.

## Source-reference drift

When a prepared path no longer exists:

1. use git history/search to find the replacement;
2. compare responsibilities, not only names;
3. update the source manifest and every affected task/requirement;
4. invalidate dependent evidence;
5. do not silently omit the scope.

When HEAD differs from the prepared anchor, follow `shared/rebase-protocol.md`.

## Evidence contract

Every task records:

- changed files;
- design decision and rejected alternatives;
- failing-first/characterization evidence;
- exact focused commands and exit codes;
- actual OS/profile/RID/dependency versions;
- test/CI links and log paths;
- migration backup/checksum/rollback evidence when relevant;
- residual risks and follow-up;
- redaction scan result.

Screenshots are required only for browser/desktop capability behavior, not as a substitute for code/test evidence.

## Commit and publication policy

Do not commit, push, create branches, or open pull requests unless the operator explicitly requests it. When requested, keep one intentional scope per commit and include the active subbundle ID in the message.

## Stop conditions

Stop and invoke the named conditional subbundle when:

- a gate is NO-GO;
- existing encrypted/path/storage state becomes unreadable;
- a migration is partially committed or ambiguous;
- a second process/path/secret stack appears;
- MAF starts owning process-domain semantics;
- an external dependency cannot substantiate its support claim;
- an insecure fallback, automatic privilege elevation, name-only process kill, or link escape would be required;
- actual-host evidence contradicts the bundle architecture.

## Completion

Only `C4` can close the core bundle. Only `R4` can close the full program. A green build alone is never a portability support claim.
