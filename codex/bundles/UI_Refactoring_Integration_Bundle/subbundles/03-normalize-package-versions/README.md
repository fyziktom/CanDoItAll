# SB03 — Normalize Package Versions

**Status:** Blocked until SB02 is green  
**Outcome:** One coordinated, unused version `V` across all fallback package families  
**Proof tier:** Governed

## Scope

- Components central package version,
- FileTools central package version and project overrides,
- CanDoItAll fallback package properties,
- local pack proof,
- version decision record.

## Version selection

1. Enumerate all package IDs produced by Components and FileTools.
2. Enumerate all configured NuGet feeds.
3. Query each feed for the recommended candidate `0.3.0`.
4. If unused everywhere, select `V=0.3.0`.
5. Otherwise select the next coordinated unused stable version.
6. Record feed URLs by name, package IDs checked, and decision. Do not record credentials.

Use `scripts/resolve-package-version.ps1` as a helper; verify against private feeds with the
repository's authenticated tooling when applicable.

## Components changes

Set:

```xml
<CanDoItAllPackageBaseVersion>V</CanDoItAllPackageBaseVersion>
```

Keep the existing proof/prerelease suffix mechanism.

## FileTools changes

1. Set central:

```xml
<Version>V</Version>
<PackageVersion>$(Version)</PackageVersion>
```

2. Remove local `<Version>` and `<PackageVersion>` elements from all packable project files.
3. Do not change package IDs or dependency boundaries.
4. Build all nine packages and inspect every nuspec.

## CanDoItAll changes

On the post-development-merge branch in SB05/SB06, set:

```xml
<CanDoItAllComponentsPackageVersion>V</CanDoItAllComponentsPackageVersion>
<CanDoItAllFileToolsPackageVersion>V</CanDoItAllFileToolsPackageVersion>
```

If SB03 executes before SB05, record `V` and apply the CanDoItAll properties in SB06 rather than
editing the stale branch early.

## Validation

Run `scripts/check-version-consistency.ps1` after all repositories contain the changes.

Pack both families into separate clean directories, then copy them to one temporary local feed
for SB08.

## Acceptance

- all publishable Components packages report `V`,
- all nine FileTools packages report `V`,
- no FileTools packable project overrides central version,
- CanDoItAll fallback properties report `V`,
- no package was published,
- selected `V` was verified unused at decision time.

## Progression gate

Version decision and package manifests are attached to the execution report.

## Reopen triggers

- package version appears on a feed before publication,
- package manifest gains/loses IDs,
- any project still emits a different version.
