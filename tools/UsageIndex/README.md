# Usage index maintenance

This optional operator executable initializes or repairs the persistence-owned Agent
usage locator index. It references the existing persistence owner and requires no new
hosted service. Ordinary Overview/API reads never execute its backfill.

```powershell
dotnet build ./tools/UsageIndex/CanDoItAll.UsageIndex.csproj --configuration Release /m:1
dotnet run --project ./tools/UsageIndex --configuration Release -- <workspace-root> organization <profile-guid-N>
```

Omit the scope kind/key only for an explicitly Sandbox workspace. The other supported
kinds are `project`, `tenant` and `process`. Use the actual configured root and scope;
the command does not discover profiles or read credentials.

The first pass discovers historical canonical file locators. Subsequent lock leases
process at most 100 payloads each. Ctrl+C saves completed progress; repeat the command
to resume. `--rebuild` starts a new generation from canonical evidence. For pre-split
storage, `--migrate-legacy` explicitly runs the existing canonical migration first.
Upgrade all writers before initializing coverage; older writers cannot maintain it.

See [the feature contract and validation record](../../docs/architecture/agents-overview-usage-window.md)
for coverage states, recovery, compatibility, test filters and performance measurements.
