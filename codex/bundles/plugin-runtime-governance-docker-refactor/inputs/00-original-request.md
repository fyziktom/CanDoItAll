# Original Request

```text
you are senior C# architect.
Implementation agent added plugins module in candoitall based on "C:\repositories\CanDoItAll\codex\bundles\plugin-workflow-executors" bundle.
Use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) with [$analyzing-dotnet-performance](C:\\Users\\lucys\\.codex\\skills\\analyzing-dotnet-performance\\SKILL.md) and [$optimizing-ef-core-queries](C:\\Users\\lucys\\.codex\\skills\\optimizing-ef-core-queries\\SKILL.md) to analyze implementation and find whrere are weak points.
Prepare new bundle. Do not do implementation. Do only detailed bundle.

Use usecase of creating simple Docker plugin that can get info about running dockers, or pull and start some docker or get logs from some running docker. In workflows for use of this plugin it must have also steps like call LLM to do summary from log, etc.
Thinking about how to implement some exact plugin can show weak spots too. You must analyze it and prepare architecture refactoring. Still you must think that plugins must remain generic.
In case of the docker it will need access to powershell. Things like this we must have under some control. It means that user must explicitly allow in plugins settings access to tools like files or powershell.
```
