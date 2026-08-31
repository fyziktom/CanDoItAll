# Docker candidate build and preflight proof

The candidate image built successfully from frozen, sanitized snapshots. This proof does not claim client replacement or published-image CSS verification.

- Tag: `candoitall-app:agent-startup-performance-20260831`.
- Immutable image ID: `sha256:a9b6165bf88a2ecbf62884ca76f1af1b7622fbc0b6861eec5c9c57603149bfc6`.
- Full included-input fingerprint: `0879aa10acbd798c2de6d7ebed4c4892201fb7a679ad39b2246f6ff38c82920d`.
- Source HEAD: `3d5def561cd06635a2676c4b86afcb6b49ad169b`; the exact thirteen-file source/test freeze is `../../frozen-integration/source-freeze.json` and matched before staging/build. Included source/snapshot hashes matched again after the completed build.
- Actual input inventory: 3,922 app files, 1,122 Components files and 274 FileTools files. All three repository HEAD/status records and content hashes are in `source-input-manifest.json`. Its source/snapshot paths are repository-relative; no source context contents are copied into this proof.
- 581 static project-reference targets, the application Templates directory and FileBrowser scoped stylesheet were present. The canonical Dockerfile was copied verbatim, retaining both named-context `**/[Bb]in` and `**/[Oo]bj` exclusions and SDK10.0.302/runtime10.0.10 pins.

The initial deeply nested snapshot path exceeded Windows MAX_PATH while copying a project manifest; no Docker build started from that incomplete snapshot. A fresh short owned `.artifacts/aspi-20260831` root was used successfully. The incomplete snapshot is not a build input and was not reused. Root/sibling `.dockerignore` files were not edited. The root ignore file does not exclude every nested bundle proof directory and FileTools lacks its own ignore file, so task-owned snapshots explicitly omit proof/private/generated/artifact/secret paths before transmission. Main-context docs/tests/tools/Tailwind exclusions remain applied. Only Git-governed tracked and eligible untracked current files enter the snapshots; new SB02 production source is included.

`image-build.command.json` retains the actual executed argument array, time bounds, cwd and exit code. Those historical absolute paths are evidence, not a portable launch contract. The reproducible command shape is:

```text
docker build --progress=plain --file <app-snapshot>/src/App/CanDoItAll.Web/Dockerfile --tag candoitall-app:agent-startup-performance-20260831 --iidfile <artifact-image-id> --build-context components=<components-snapshot> --build-context filetools=<filetools-snapshot> --build-arg BUILD_DATE=<UTC> --build-arg BUILD_REVISION=<frozen-head> --build-arg BUILD_SOURCE_FINGERPRINT=<included-input-hash> --build-arg BUILD_VERSION=agent-startup-performance-<hash-prefix> <app-snapshot>
```

## Read-only verification and CSS boundary

`Verify-StartupClientCandidate.ps1` locates the owning repository or accepts `-RepositoryRoot`. It performs only Git/file reads and Docker context/image/container inspection. It checks thirteen frozen inputs, the candidate immutable ID/labels, the restart draft hash, source CSS hash, and both original containers' baseline metadata. It never invokes the restart helper or creates/stops/starts a container. Its pre-replacement identity checks deliberately fail after authorized replacement instead of silently retargeting a new client.

The restart helper's `-WhatIf` initially rejected the network comparison because its actual projection omitted baseline aliases. Including actual aliases fixed the projection while retaining exact names, IDs and aliases. The corrected WhatIf completed successfully and reached ShouldProcess without a stop or report write. This is read-only preflight evidence, not shutdown/rollback execution proof.

The source scoped stylesheet hash is proven, and the Docker log records successful FileBrowser component restore/build and application publish. Actual published `CanDoItAll.Web.styles.css`, its FileBrowser scoped CSS import, and correct rendered file-browser styles remain pending real5214 static/UI verification after authorized replacement. No image archive export was run during the integration quiet gate.

No raw environment, Docker Config/HostConfig, credentials, private files or source snapshots were copied. The manifest contains only source path/status/content-hash metadata. `verification-contract.json` defines the preservation boundary and records the restart helper hash; root must independently accept it before any mutation. The running client and publisher were not changed during this build/proof work.
