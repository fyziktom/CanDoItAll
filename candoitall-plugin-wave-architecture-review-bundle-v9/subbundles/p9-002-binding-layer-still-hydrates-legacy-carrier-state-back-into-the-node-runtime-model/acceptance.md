# Acceptance
This subbundle closes only when:
- the active code no longer exhibits the forbidden patterns,
- the required tests exist and pass,
- the repo-wide hard gate passes,
- the closure proof matches the actual code.

Target acceptance:
ProjectNodeBindingStorage.Apply(...) no longer writes node.Route / node.ExternalArtifactKind / node.MediaRelativePath etc.; ResolveBinding(...) no longer falls back to node carrier fields; projection assembly no longer seeds binding from legacy node fields.
