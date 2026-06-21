# .NET solution setup subprocess

**Key:** `dotnet-solution-setup`
**Criticality:** Standard
**Autonomy level:** Assisted

Atomic child process for creating or validating a named .NET solution, app project, test project, project references, and first build proof using the app archetype grounded by the parent scope.

## Steps
- Capture solution scaffold contract.
- Create solution and .NET app project.
- Add test project and reference.
- Validate first build and test discovery.
- Hand off setup evidence.
