# Original Request

```text
Use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to solve this:
Main goal:
we must be able to run workflow from project structure canvas.

Notes from architect:
we have similar system for starting processes. First user will add process node in project structure and then they start with via right click opiton.
Here it will be almost same. First user will add workflow node under some node. During adding it opens dialog where is possible to select specific workflow. In this dialog the settings must be little more advanced because we must specify what we want to provide as input for the workflow.
For sure there will be prefilled info about parent node with all details. It must always provide also information about what project is it.
Then it can click in right click menu to start. It must open confirmation dialog to confirm start.
Workflows are defined more hard so it does not need "matching resources" dialog during start as we have in processes.
When user starts workflow like this. We must somehow inform user about that it is running. Worklfows will be usually done faster than processes. For sure it is good to setup progress of worflow node in project structure to "started" and when it is done to "100%". We also have markers on nodes, so if it fails, pause,etc we can add proper marker too.
I have one another general idea about show status. If I click in project structure on workflow node it can show in selection floating window actual status in little more detail. At least what step from how much is it now and some generic info about status, like running, failed, finished, etc.
When workflow is adding some nodes with results it must add them under yourself node in project structure.
Each workflow should provide execution summary in the project structure too. It must contains basic results. I case it created some new files it must contains list of them. Not everytime workflow must create files that will be directly added as asset nodes, so it will be better in case of file operations to provide also path in this summary. I am not sure if we have summary like that now. If not, analyze how to add it. Especially in case of those file savings.

It is lots of tasks. First assure that you have all necessary basic functions in backend. Then go to the UI layer.
You must test it on real cases (use gpt-5-mini and gptoss20b64k local ollama). Use at least 20 different real world cases. Assure that workflows contains proper real world instructions and cases makes sense. You will have to synthetize data for those tests (for example emails, or some simple business plan as md, etc). I also added some files here "C:\programovani\testdata\testworkflows".
there is some order from mouser.com (xlsx and pdf of same order), then some sample complex financial plan for new project, and also folder SEAMARK with pdf files about some xray devices and their pricelist (this might be good simulations of case for folder fetch and do summary). In that case workflow will take folder as input from the parent node info. User should see in the add workflow dialog what will be feed into workflow.
During those real tests you must validate results of the workflows to see it did true work as you would expect from the workflow. If you will see any troubles you must add subbundle to repair it on the fly.
use same db postgresql as we have now in visual studio instance. It has zero projects now, so it is ready. It need just improvements of those workflows . Some of them are generic, so you can fit cases on it, few will be new because I think we do not have workflosws for cases like SEAMARK, or that mouser order (it can check if items match in pdf and xlsx, or do some summary about order, so there are multiple possible workflows for same data).

All those instructions you must store in bundle, but start with doing better structure of this input. You must preserve all, just improve structure.
```
