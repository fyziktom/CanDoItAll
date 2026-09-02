# FileTools Impact Analysis

## Dependency boundary

No direct Components dependency was found in FileTools. Its package validator deliberately fails
if a package project contains a Components or main-application dependency.

This is the desired architecture:

```text
CanDoItAll host
  -> Components
  -> FileTools
```

not:

```text
FileTools -> Components
```

FileTools user-facing RCLs should remain host-style-neutral and consume only ASP.NET Core shared
framework plus FileTools contracts/core.

## Expected implementation impact

Expected FileTools UI implementation changes: **none**.

Required work:

- verify no hidden Components reference was introduced,
- verify no `.material-icons` implementation contract is used,
- normalize package version metadata,
- run all FileTools unit/package validation,
- run the maintained FileTools sandbox,
- prove FileBrowser and FileInteraction when hosted by the updated CanDoItAll.

If a visual issue appears only in the CanDoItAll host, fix host composition or Components styles,
not FileTools ownership.

## Version inconsistency

The repository central version and several project-local versions differ. Observed values include
`0.1.0`, `0.1.2`, `0.2.0`, and `0.2.1`.

Normalize as follows:

1. set one central `Version` to selected `V`,
2. let `PackageVersion` inherit centrally,
3. remove packable-project `Version` and `PackageVersion` overrides,
4. build all nine packages through the repository package manifest,
5. validate every `.nupkg` and `.snupkg` reports exactly `V`.

## Gate

A code-free FileTools compatibility result is acceptable and preferable. The execution report
must explicitly say that the repository was inspected and validated, rather than claiming it was
"updated" without evidence.
