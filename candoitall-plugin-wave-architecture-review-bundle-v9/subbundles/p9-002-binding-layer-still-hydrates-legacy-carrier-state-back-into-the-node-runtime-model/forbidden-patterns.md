# Forbidden patterns
- ProjectNodeBindingStorage.Apply(...) writes binding data back into node.Route / node.ExternalArtifactKind / node.MediaRelativePath etc.
- ResolveBinding(...) falls back from binding state to legacy node carrier fields
- Projection assembly seeds binding state from legacy carrier fields
