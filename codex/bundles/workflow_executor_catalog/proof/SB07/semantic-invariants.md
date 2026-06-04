# SB07 Semantic Invariants

## Invariant SB07-NETWORK-WORKSPACE-GUARDRAILS

- Source raw note: RN03 and R9 require HTTP/download workflows without bypassing network or workspace safety.
- Expected behavior: HTTP fetch blocks private network targets by default, masks secret-bearing headers, writes downloads only to workspace output paths, and source ingestion consumes typed output paths.
- Disallowed shallow implementation: treating downloaded content as an unscoped host path or silently accepting arbitrary private network access.
- Positive proof: `HttpFetchDownloadsToWorkspaceAndSourceIngestionReadsOutputPath` and `HttpFetchBlocksPrivateNetworkTargetsByDefault` in `bundle://proof/SB10/transcripts/dotnet-test-unit-workflow-executor-catalog.txt`
- Source proof: `bundle://proof/SB10/transcripts/source-assertions-executor-implementations.txt`
