# Original Request

> I see in the visual studio solution explorer in wwwroot folder css and css-src and same for js, but they contains same things. I see also those folders/files in explorer too.
>
> Analyze it and assure we have just one valid copy of folders/files in repo. Analyze other parts of the repo for potential duplicities like this. Our main goal is to stabilize codebase, refactor for better maintanance and improvements. So any duplicities, too large files, too many files in one folder are not ok.
>
> Then organize components in Components folder in CanvasLib to sub folders, so we have easier maintanance. Same for Canvas.Graph folder. all those classes should be better organized in folders that groups them based on common topic.
>
> Split also some larger files into own classes (for example [CanvasWorkbenchContracts.cs](src/CanDoItAll.Components.CanvasLib/Canvas/CanvasWorkbenchContracts.cs) .
>
> use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve all this. Assure that all functions are always preserved all is working as before.
