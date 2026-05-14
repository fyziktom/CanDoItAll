# Original Request

Date: 2026-05-14

```text
Use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to improve implementation of our plugins. 
Right now I see that plugins are part of plugin module. We must test that we can install modules from runtime as kind of package that user select and install. It means they must be able to be added without additional compilation. Some of them might require restart. Then it must provide smooth way for user for restart. Right now you would have to kill the process in task manager if you want restart.

Create in src folder plugins (part of sln too) where you will move implementations of each plugin we have. You must validate that they work as before. 
You must add way to add plugins in UI. There will be two ways. One way is download from our plugin catalogue, second will be impload of zip with plugin (it must have libs, some manifest, icon, etc).
```

## Raw Notes

| Note | Exact request fragment |
| --- | --- |
| `N001` | Use `candoitall-bundle-workflow` to improve implementation of plugins. |
| `N002` | Plugins are currently part of the plugin module. |
| `N003` | Test that modules can be installed from runtime as a package that user selects and installs. |
| `N004` | Packages must be able to be added without additional compilation. |
| `N005` | Some packages might require restart. |
| `N006` | Provide a smooth user restart path; do not require killing the process in Task Manager. |
| `N007` | Create a `src` folder `plugins` area, part of the solution, and move implementations of each existing plugin there. |
| `N008` | Validate moved plugins work as before. |
| `N009` | Add UI support for adding plugins from plugin catalogue. |
| `N010` | Add UI support for upload of a zip with plugin libs, manifest, icon, etc. |
