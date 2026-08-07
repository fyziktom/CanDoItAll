# Preparation scope and limitations

The bundle was prepared from GitHub source inspection of `fyziktom/CanDoItAll` branch `maf-refactor` at `9e47a332fa9d329422ff616a0e0b6a97a22933c9` and comparison against `26da0c55861e5d4e6ca325e561f3f4612aa93266`. The review inspected runtime ports, context/authority capture, execution options, MAF capability composition, recovery, script policy, workspace factory/lifetime, state envelope/restore, approvals, process recovery, lightweight LLM, workflow integration, project references, tests, and branch closure artifacts.

The preparation environment did not contain a local checkout and could not independently execute `dotnet build`, tests, CodeAnalytics MCP, or live application scenarios. GitHub reported no commit status or workflow runs for the reviewed head. `SB00` is therefore mandatory and must independently reproduce the current branch proof before production edits.
