# Product Output Snapshot

Product root copied from: `C:\programovani\dotnet\output`

The snapshot is under `product-output-snapshot/files`. It excludes bin/obj by design and copies selected project files, Razor pages, layout files, CSS, sample data, and tests.

## Scaffold Scan

`product-output-snapshot/forbidden-scaffold-scan.txt` contains these matches:

```text
C:\programovani\dotnet\output\src\TetrisGame\Layout\MainLayout.razor:9:            <a href="https://learn.microsoft.com/aspnet/core/" target="_blank">About</a>
C:\programovani\dotnet\output\src\TetrisGame\Pages\Counter.razor:1:@page "/counter"
C:\programovani\dotnet\output\src\TetrisGame\Pages\Weather.razor:1:@page "/weather"
C:\programovani\dotnet\output\src\TetrisGame\Pages\Weather.razor:40:    private WeatherForecast[]? forecasts;
C:\programovani\dotnet\output\src\TetrisGame\Pages\Weather.razor:44:        forecasts = await Http.GetFromJsonAsync<WeatherForecast[]>("sample-data/weather.json");
C:\programovani\dotnet\output\src\TetrisGame\Pages\Weather.razor:47:    public class WeatherForecast
```

This confirms that the generated product still contains default Blazor scaffold content at capture time.

## Important Product Files

- `product-output-snapshot/files/src/TetrisGame/Pages/Home.razor`: appears to contain the custom Tetris UI.
- `product-output-snapshot/files/src/TetrisGame/Pages/Counter.razor`: default counter scaffold still present.
- `product-output-snapshot/files/src/TetrisGame/Pages/Weather.razor`: default weather scaffold still present.
- `product-output-snapshot/files/src/TetrisGame/Layout/MainLayout.razor`: default framework docs link still present.
- `product-output-snapshot/files/src/TetrisGame/wwwroot/sample-data/weather.json`: default sample data still present.

Pro should distinguish product correctness from process correctness. The process should not escalate just because QA found this defect; it should route to repair in a controlled way.
