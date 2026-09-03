# Components Impact Analysis

## Upstream blocker

At discovery, the first CI run after merging `ui-refactoring` into Components main failed:

1. `StandardPublicApiMetadataMatchesFreezeSnapshot`
2. `StandardSourcePackageInputsMatchFreezeSnapshot`
3. `CanvasPackageStaticAssetsMatchExpectedManifest`

JavaScript restoration, Tailwind generation, asset verification, .NET restore, and build had
already passed. This indicates governance baselines were not reconciled rather than an immediate
compiler failure. Nevertheless, snapshots must be reviewed, not blindly regenerated.

## Public API review targets

The observed merged branch includes additive surfaces such as:

- Material Symbols variation properties on `Icon`,
- public Material icon aliases,
- deterministic `RoboAvatar`,
- deterministic `HomoAvatar`,
- extensive XML documentation.

Codex must inspect the actual approval diff at execution time and confirm that there are no
unintended removals, namespace changes, or signature changes.

## Static assets

The host contract now uses:

```text
_content/CanDoItAll.Components.BaseLib/css/material-symbols.css
_content/CanDoItAll.Components.BaseLib/css/output.css
```

The old `material-icons.css` no longer exists.

## Generated BaseLib CSS defect

Current Components main:

- ignores `output.css`,
- has no checked-in BaseLib output,
- has no MSBuild target that generates it,
- expects npm to generate it in Components CI.

This works for Components CI and package creation but not for a clean sibling project-reference
consumer, because normal CanDoItAll restore/build and Docker publish do not implicitly execute
the Components Tailwind build.

### Required fix

1. Regenerate BaseLib output from `Tailwind/input-base.css`.
2. Add an explicit `.gitignore` exception for only the distributed BaseLib output.
3. Commit that output.
4. Add a deterministic check to Components CI:
   - generate assets,
   - fail if the committed BaseLib output changed.
5. Keep sandbox output CSS ignored.
6. Document the policy in Components Tailwind/build documentation.

Do not duplicate the Components Tailwind source into CanDoItAll. Do not require hidden npm state
for a normal source-reference .NET build.

## Preflight/reset risk

The merged BaseLib input includes Tailwind preflight. This can change global element defaults in
the host. Removing or redesigning preflight is not part of this bundle unless concrete browser
proof demonstrates a product regression. Treat it as a visual-proof hotspot and fix only
specific proven problems in the owning layer.

## Release gate

Components must pass:

- generated asset verification,
- reviewed approval baselines,
- full .NET tests,
- pack of every publishable project,
- package static-asset inspection,
- clean-source rebuild with no untracked/missing BaseLib stylesheet.
