# Original Request

> Use [$candoitall-bundle-workflow](C:\\Users\\dell\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this:
> we still have lots of nodes in project structure, that does not work well. Especially those:
> - runtime nodes -> they must start the process. It must offer in doubleclick in dialog run normally or as admin (especially all realted to powershell). It means for example docker runtime nodes too and same python. They all must be able to start powershell with that specific command in folder that is specified as path if some is specified.
> - openning some folder with file or folder as itself does not work. it opened explorer, but in home path. We must have Folder node that allows to select some path of folder and in doubleclick dialog offers option to open folder in explorer. and all files nodes if file is on drive, then it must offer open file location in explorer too.
> - repository and link nodes must recognize that it is pointing to github or gitlab links.
> - tools of agents for accessing project structure must have information about how to add links, runtime scripts, folders and files of all types. Especially those runtime nodes are important. typical situation is that I will just add some folder node and then tell agent to add runtime nodes for starting app in some different modes/settings, etc. so I can then just click on node to start some app we are building.
>
> you must validate that it is working and validate it with playwright mcp and screenshots.
