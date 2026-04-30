# 2026-04-28 CalcApp Runtime Failure

The user reran `Implement main app / Multi-team software delivery and release governance` for a calculator app and the process wrote a Blazor application, but the app returned HTTP 500 at `/`.

Observed runtime exception:

```text
System.InvalidOperationException: Cannot find the fallback endpoint specified by route values: { page: /_Host, area:  }.
```

Concrete generated app inspected:

- `C:\programovani\dotnet\calculatorblazor\CalcApp\CalcApp.csproj`
- `C:\programovani\dotnet\calculatorblazor\CalcApp\Program.cs`
- `C:\programovani\dotnet\calculatorblazor\CalcApp\Components\Pages\Home.razor`

Initial proof showed `dotnet build` and `dotnet test` passed, but `dotnet run --no-build --project C:\programovani\dotnet\calculatorblazor\CalcApp\CalcApp.csproj --urls http://127.0.0.1:5019` returned HTTP 500. `Program.cs` had been rewritten to legacy Blazor Server/Razor Pages hosting with `AddRazorPages`, `AddServerSideBlazor`, `MapBlazorHub`, and `MapFallbackToPage("/_Host")` while the project shape was a modern .NET 10 Blazor Web App with `Components/App.razor`.
