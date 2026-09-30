# Exact Components consumption and remote delivery

## Reviewed observation

The closure report binds locally tested application production source
`3d7c88f384b7920744464a7c7570530ca825325a` to Components
`22d5b21afdf80c2bca74c1c598f0b1bb72c86f9e`, including actually served JS and package hashes.
It explicitly describes remote publication as blocked. [R02]

During this review the connected Components branch list still showed main
`f258ab6a959a97fa16c01d0858e7dc122728a11a` and development
`ff5289746573a6dd6456754a7764844627ddd49f`; fetching DialogInterop at the reported repaired
SHA returned 'No commit found for the ref'. This proves non-retrievability through that
repository connection at review time, not that no local fix exists. Recheck before action.
The app push does not itself publish a sibling commit. [R13, R14]

## Required procedure

1. Identify the actual local Components checkout, dirty paths and commit. Verify the bounded
   Dialog/interop/JS changes and their signed provenance. Do not overwrite unrelated work.
2. Record source-mode/package-mode explicitly. Compare real loaded assemblies and served
   assets, not just a version string such as 0.3.0 or a matching directory name.
3. Read current `.github/workflows/ci.yml`. Its matching-branch dependency resolution must
   receive the fix on the branch actually selected for the target PR/build, then record the
   immutable resolved SHA. A repair on an unrelated branch is not delivered to development.
4. With no publish permission, prepare the exact repository/ref/commit and ordering for the
   operator, continue safe local tests, and keep delivery BLOCKED or LOCAL_SOURCE_PAIR_VERIFIED.
   Do not manufacture remote verification from a local package or copy Dialog into the app.
5. Once genuinely authorized/published, verify reachability, target branch resolution, clean
   restore/build and the same consumer/source/asset semantics. Mark delivery VERIFIED only
   against that intended consumer, with evidence. No force push, merge or release is implicit.

## FileTools is a different situation

The reviewed local `3a080ecd31068a77c1e1bd639f7a78e21c93db85` and CI pin
`498b36825bd5a5222429972af120b04becf4b3f6` compare as two commits ahead with **no changed files**.
Do not declare a functional incompatibility merely because these SHA values differ. Record
exact tree/effective-build equivalence and any revision-derived assembly/package metadata;
rebuild the intended pair when relevant. No FileTools source upgrade is demanded by this
observation. [R15]

Protect all eight Workspace/Configuration sandbox closures. Reassess a changed shared
Components edge/assets across actual consuming modules, including source and published modes,
without inventing a fixed project-count target. Metadata equality alone is not binary proof.
