# Original Request

```text
You are senior C# architect.
You must prepare detailed execution-grade bundle for Codex to solve this new feature in our app:

Main goal:
- allow to select the database that I will work with
- allow to switch the database that I am working with during runtime
- add SQLite support

Actual state:
- analyze code
- primarily postgresql now
- loading automatically during start, cannot switch between databases

You must
- analyze existing codebase and things related to db
- design architecture changes that will lead to main goals.
- create detailed plan and subbundles for each step of the implementation
- validate whole bundle if it contains all information for codex to fluently implement all and truly validate it
- sometimes happens that codex is faking validations, skipping some subbundles, etc. assure it will not happen and codex will do all things correctly and trully.
- this is important to cover with unit tests and e2e tests. It is critical thing. It will be necessary to assure that during switch of db in runtime it will correctly reload all running modules/services with new data.

Notes around this feature:
- when app starts it can start with last setup of db (if available) and still popup info modal if user wants co continue with this db or switch to different one
- source of db (sqlite) can be from some openfile dialog, setup of path, selection of existing dbs in some AppData app folder, or ipfs (local or remote node), probably it will be good to have some more sophisticated driver for db connection. 
- source of db (postgresql) can be from localhost (process or docker), remote server.
- it should allow to create new db (for both sqlite and postgresql). So user can always do new db and ideally with optional clone of all data option. so you can practically do snapshot of db like kind of branch. We are building own ipfs node (already working, just tuning/testing now) so you will be able to pin some version of db and then do some large changes with ai, if they will go well, you can keep it, if not you can go back to old snapshot. It is kind of versioning tool for whole db. Since the files will be on ipfs or storage the db as itself will not be superlarge. 

when you finish and you are prepared to create final zip, do last validation with view of senior QA C# architect if this bundle will lead to successful implementation and solving main goals and notes I  mentioned. if something is missing or raising concerns or it can be improved you must repair/add/improve it and then revalidate it. 
It is large thing, so take your time to do best work you can do.
```
