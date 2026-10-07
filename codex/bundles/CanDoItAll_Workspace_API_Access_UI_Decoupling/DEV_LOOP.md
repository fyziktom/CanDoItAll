# Development-loop and sandbox proof

## Baselines

Before API extraction (after bounded S0), capture the real API Access view in the owned Web
host and record the exact main/sibling revisions, OS/SDK, source/package replacement mode,
configuration, theme mode, fixture state and browser viewport. Preserve current Core sandbox
closure as a non-regression baseline. Do not reuse another module's timings as API measurements.

For before/after Web, the standalone API sandbox and existing Core sandbox record evaluated
project graph, public/runtime dependencies, native/package entries and `dotnet watch --list`.
Do not confuse a restored solution member with a runtime dependency. Follow actual
`Directory.Build.targets` substitutions and static assets. Incomplete/unresolved closure is
not proof of isolation; inspect the error rather than excluding that edge.

The previously reported Core sandbox has eight projects; this is implementer evidence, not
an independently reproduced reviewer benchmark. Require no newly introduced API edge there,
not an arbitrary forever-fixed count when unrelated checkout changes exist. EV03, WS12–WS14.

## Measurements

Use the same feature journey and warm/cold conditions per comparable host. Measure startup
to actual interactive readiness, not HTTP 200 alone. Collect at least three edit-to-visible
samples for meaningful Razor and executed C# edits; do scoped CSS/feature JS only when this
slice actually owns them. A C# edit must change executed behavior, not an unused marker.
Record publish/browser validation of shared dialog/copy assets even when feature JS is N/A.

Restore exact input bytes in `finally`, capture owned process identity, and stop only those
hosts. No global dotnet kill, normal app restart or unrelated source formatting. Keep Windows
path-length/output conflicts in mind without changing the repository's build policy to hide
proof setup errors. A safe configuration such as `ApiUiProof` is a suggestion, not a toolchain pin.

Report individual samples and their endpoint/conditions. Smaller sandbox closure is a useful
result; it does not establish faster full Web or a statistically robust speedup. Do not
present time spent in a different fixture/backend as an architectural gain.

## Scenario fidelity

The API sandbox uses genuine renderers and presentation policies, no production registration.
Fixture owners maintain bounded safe metadata and versions and expose controlled gates for
access, lists, writes, read-back and failures. Clearly label any nonusable synthetic token.
Never import JWT signing merely to make a fixture string look real.

Cover all existing nested overlays, list failures/empty/multi-page states, stale versions,
authority loss, unknown/known-warning outcomes and original-store completion after reset.
Do not log password-bearing requests in scenario history. A fixture should verify capture
and disposal through redacted facts or transient assertions, not retain plaintext forever.

## Source and published browser

At the supported large desktop viewport (currently exemplified by 1600×1000), inspect normal
status, issuance, user list/editor, scope picker, token list and nested destructive confirmation.
Check readable first viewport, reachable footers, keyboard focus, no horizontal overflow,
actual clipboard/copy behavior and removal of inactive sensitive DOM. Store only masked images.
A visually hidden credential remains retained data; unmount/retire it according to policy.

Publish the API sandbox, run the published DLL in Production with no production DB/vault/
registry configuration, and test the actual interactions. Verify generated CSS, font, scripts,
dialog assets and no unexpected network calls; production CSS content links must not create a
Web project dependency. Do not ship font files in this handoff.

The production fixture separately uses the real Settings host, control-plane owners, security
checks and ephemeral data. UI success/failure cannot be inferred from the synthetic host.
No live external account or actual local executable launch is needed for either proof.
