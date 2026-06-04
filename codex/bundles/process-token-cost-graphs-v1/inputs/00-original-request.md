# Original Request

Source: user prompt on 2026-06-01.

```text
Main goal:
Improve used tokens usage and price and statistic calculations.

Architect notes:
I see that some process costs amounts like 0.08USD, consumed if I remember correctly it was about 100k tokens what I saw in ui in live procses, but in openai billing usage I see about milions of tokens consumed.
Analyze how do we calculate it. We must improve it. Assure we are counting also outptut and cached tokens for openai provider. For example ollama, will not have cached tokens, but if provider has it we must calc it correctly.

Then, when process finished and I refreshed live processes page and selected for example 1 day history, I cannot see prices graph.

Then on processes page there must be new tab in selected process to show graphs like we have on live processes page to show merged info about all runs of that process and in specic selected process run we need also own tab for graphs for that specific process run only.
Those are lots of loading of the data, so it must load them only when that tab is selected. For all process run there might be button "Show graphs of all runs of process" and with preselected option like last 1 month (with other options like 1day, 1 week, 1 month, 3 months, 1 year, all), so when user will click on that tab by accident it will not start loading it until that click on button.
```
