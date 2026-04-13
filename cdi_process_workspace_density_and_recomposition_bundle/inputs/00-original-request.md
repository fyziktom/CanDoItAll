# Original Request

```text
great. I need you to improve this:
1) when I unzoom a little window, content is not using whole width. It must use full widht.
2) add SummaryTile prop, that can make it looking like kind of badge. It means it will all be on one row and value will be smaller in size. So we save another of height.
3) You must improve processes canvas. We need something similar as in project structure canvas to recompose the nodes on the canvas. Right now they are overlaying/overlaping each other. They should not have colisions on the canvs, otherwise it is not clear to read them. In case of processes there is usually some main line from start to end with some branches, etc, but still it has more kind of fishbone style of mindmap. The recompose algorithm should consider it. There must be few more types of "recomposition" buttons in canvas toolbar. One, "Colisions" will be just to solve colisions. It will move nodes just good enough to do not have colision with any of the neighbours. Second will be "Add Space Around" and it will just increase space around each node. Third will be more smarter "Recomposition". They all will be in toolbar menu under common button with just some propriet icon and when I mouseover that icon, it will roll down the dropdown with those three options. This might have lots of common parts with recomposition in the project structure. Think about how to make it modular, so we can use it across different use of CanvasLib. Those calculations are usually difficult, so they must happen in C# side (we can use parallism, etc).
Apply it for processes in "C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\529c12060808489fad29feb5bc60dda1\db\candoitall.db" db.
It is very complex task, so use [$candoitall-bundle-workflow](C:\\Users\\lucys\\.codex\\skills\\candoitall-bundle-workflow\\SKILL.md) to prepare detailed bundle with subbundles.
```
