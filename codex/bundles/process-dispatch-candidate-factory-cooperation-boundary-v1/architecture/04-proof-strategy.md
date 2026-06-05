# Proof Strategy

Required proof categories:

1. **Source inventory**
   - all `new DispatchCandidate` call sites before/after,
   - line counts,
   - candidate field map.

2. **Architecture guardrails**
   - no Process Core,
   - no driver API,
   - no MAF back-dependency,
   - no UI/prohibited viewport proof paths,
   - factory has no EF/DbContext/executionClient/SaveAgentAsync.

3. **Candidate parity**
   - subprocess candidate field parity,
   - workflow candidate field parity,
   - direct-agent candidate field parity,
   - missing binding behavior,
   - project-structure access grant/no-op behavior,
   - recovery execution id and manual directive behavior.

4. **Runtime smoke**
   - focused unit architecture tests,
   - focused integration wrapper tests,
   - `dotnet build CanDoItAll.slnx`,
   - no stubs/TODO/NotImplemented in extracted helpers.
