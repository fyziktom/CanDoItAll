
# Original Request

```text
you are senior C# architect with deep knowledge of different storage systems. You must prepare execution-grade bundle with detailed instruction for codex to add Storage Driver in our CanDoItAll solution.

Actual state:
- We have simple driver in WorkspaceStorage.cs. It solves just small part of what we need

Main goals:
- flexible robust and safe storage driver capable to work with different types of storages:
    a. File system
    b. IPFS (local node or remote node)
    c. FTP (local or remote)
    d. i do not know, but we will add another types of storages later for sure
- must be possible to persist settings/configuration of what storage will be used for what in default.
- when I upload for example some file, it will recommend some default option (for example for docx or txt is more probable filesystem because I want to edit file, but for pdf or image is ipfs better because do not expect changes)
- we will need also UI section of all this settings (it include addingg new storage, ideal as steps/wizard to lead me based on type, then adding connection info, then test, etc)
- We must have some set of generic UI components for storage driver (like list of available storages) that we can use on different pages, but from same codebase to keep it maintainable.

Notes from architect:
- you must split it to phases and for each has own folder with subbundles.
    a. first phase will be creating of the defining models and interfaces.
    b. then factories, main classes for services, implementing interfaces etc.
    c. test coverage with unit tests
    d. then implementation into other projects
- think about some details like batch loading of files (for example to ftp, ipfs, etc where some pipeline/buffer might be handy). We need to have it prepared for those operations (like migration of folders with content to ipfs, etc). think it through.
- it might be good to have possibility to add reference to some node of project structure (it might be project or some specific node, that points to some storage system...like local deployment folders for project releases, etc). In one project structure I can have for example multiple references to storages. Actually each storage must have also own node. If I am adding that node I can do also setting of new storage driver connection (that I can then see also in management of all storages).

- this might touch lots of places in the app (all uploads and views/downloads/use of files). you must map all those situtations and assure we have subbundle with instructions how to implement new driver there. best is to do it as xlxs and then based on that continue to create all subbundles.
- assure that codex will have all checklists and will not skip anything.
- codex must be forced to do real validation with playwright mcp and screenshots.
- when you think you have all, do detailed validation as senior QA inspector. Take those xlsx as inputs to check if every identified thing to refactor has proper subbundle and it is in main checklist for codex. If something is missing or raising concerns it must be added/improved otherwise bundle is not ready.

Final zip must be detailed with all instructions (checklists, prompts, tests, validations criterias, etc) for Codex to run without skipping anything or faking tests, etc.
It must test everything with playwright mcp and screenshots to confirm, that all functions are working well and in UI there are no overlay of components, overflows of components/texts/images, etc. all best practice.
```
