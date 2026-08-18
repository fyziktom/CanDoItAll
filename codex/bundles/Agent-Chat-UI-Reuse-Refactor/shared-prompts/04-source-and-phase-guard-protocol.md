# Source and phase guard protocol

Run `scripts/check_repo_boundaries.py` from the bundle against the repository root and subbundle base SHA.

The guard is supporting evidence. Also inspect the actual diff.

Required assertions:

- neutral project contains no forbidden namespaces or project references;
- changed production UI does not reference `CanDoItAll.Modules.LlmChats`;
- no production Simple Chat route/client/filter/context feature is added;
- no Simple Chat backend file is changed;
- no new partial file expands the named large UI types;
- no `IServiceProvider` service location is added to the neutral project;
- no EF/persistence/runtime service enters the neutral project.

Any false positive must be documented and fixed through a narrow allowlist in the proof, not by disabling the guard.
