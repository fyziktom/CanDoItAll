# Original Request

The user reported that the live process running at `http://localhost:5032` looked worse after a workflow test. The process said artifacts were missing and seemed to retry the same step even though rerunning that downstream step could not create an artifact that should have been produced by a previous step.

The required behavior is:

- analyze the current development PostgreSQL DB and live process state
- map the real troubles
- keep process runtime generic
- when a downstream step is missing an upstream artifact, ask the producing previous step or process manager to use previous records and create the missing artifact
- after the missing artifact exists, retry the downstream step

The prior context also required browser/runtime proof hardening for multi-team software delivery: generated applications must be tested by agents, browser screenshots and console evidence must become process artifacts, and process core must remain generic.
