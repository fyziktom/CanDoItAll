# Add test project and reference

Create the test project using the test framework grounded by the parent contract or existing repository convention, add it to the solution, and add the required project reference to keep test work available from the first slice.

Do not hardcode xUnit, MSTest, or NUnit in this generic process. Use the named test framework when the current run provides one; otherwise use the existing repository convention and record the assumption. Escalate when a test project cannot safely reference the app project, such as UI-only projects that require tests to target a domain/application library not yet present.

This step creates and connects the test project only. Do not implement feature-specific tests, run `dotnet build`, `dotnet test`, `dotnet run`, launch a browser, or capture runtime proof here. Build/test discovery belongs to the validation step, and feature-specific tests belong to the feature implementation slice.

The required change-set artifact must list the test project file, solution membership evidence, project reference evidence when applicable, representative file readbacks, and a short statement that build/runtime validation was intentionally deferred.
