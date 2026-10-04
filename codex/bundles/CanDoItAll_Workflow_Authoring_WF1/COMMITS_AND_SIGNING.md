# Coherent signed checkpoints

Arrange signing at entry while the user can unlock the existing key through native pinentry. Use the
configured signing identity; do not generate a replacement key. Preserve the same host user, shell,
GNUPGHOME and gpg-agent across checkpoints where supported. Cache lifetime is finite; retaining a shell
alone is not an unlimited unlock. Ask for native unlock again if required. Do not silently weaken cache
or secret policy, pass a passphrase in arguments/environment, export a private key, copy it into a
container or use unsigned commits as fallback.

Recommended logical commits: S0 setup attribution; lossless document/state boundary; canvas/settings
renderers and native composition; page-owned dialogs and sandbox; native regression/final documentation.
Combine tightly related pieces when they are only correct together. Avoid dozens of artificial atomics,
but do not leave the whole long implementation as one opaque final commit.

Before each commit inspect staged paths, secret scan scope and generated artifacts. Do not stage private
fixture state, original logs/transcripts, synthetic discovery dumps or unrelated work. Retain historical
bundles; their normal final pre-merge cleanup is not part of this task. Verify each new signature and
record source equivalence when final commits contain only tests/docs. Do not rewrite already reviewed
history just to produce the suggested grouping.

No push, merge, release or remote dependency publication is authorized here. Deliver a clear summary of
local signed commits and any still-unpublished sibling needed to reproduce the result. A signing problem
is a delivery blocker, not a reason to conceal unfinished commits or discard implementation work.
