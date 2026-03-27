
# Specification

## Item identity

- **Item ID:** I12
- **Title:** .NET runtime, launch profile, and localhost nodes
- **Origin:** docx
- **Dependencies:** I01, I10

## Objective

Make .NET project runtime nodes truly useful by connecting them to launch profiles, project selection, localhost URLs, and run modes.

## Normalized scope

Implement .NET-related nodes that parse launchSettings, infer default addresses, expose localhost links, and support dotnet watch and release run variants.

### In scope

- Project selector and default launch profile parsing.
- Localhost URL discovery and click-to-open behavior.
- dotnet watch node settings.
- Release run node settings including http versus https.

### Out of scope

- A full IDE-grade debugger experience.

## Key implementation decisions

- Reuse the existing LaunchProfileSettingsResolver instead of rebuilding launch profile parsing.
- Treat dotnet watch and release run as runtime variants with shared project selection behavior.
- Expose URL choices clearly and make localhost addresses clickable from node details.

## Implementation tasks

- Create .NET runtime node subtypes and details editors.
- Add project selector and launch profile parsing integration.
- Surface inferred localhost URLs in node details as clickable links.
- Implement dotnet watch and release node variants with protocol options.

## Risks to control

- Hard-coded URL assumptions will fail across different project launch profiles.

## Covered original notes

- N083 — Dotnet related
- N084 — Project default launch profile
- N085 — Project selector
- N086 — Then auto parse of launchprofile to get default addresses
- N087 — localhost run in node details – click to open in new tab
- N088 — Dotnetwatch
- N089 — Command to run specific project in dotnetwatch
- N090 — Ideal would be project selector
- N091 — Specify http vs https
- N092 — Run Release
- N093 — Specify http vs https
- N094 — Ideal would be project selector
- N095 — Address of release localhost run in node details – click to open in new tab
