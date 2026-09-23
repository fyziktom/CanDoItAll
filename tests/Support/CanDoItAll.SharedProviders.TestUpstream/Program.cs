using CanDoItAll.SharedProviders.TestUpstream;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = FixtureLimits.MaximumRequestBodyBytes;
    options.Limits.MaxConcurrentConnections = FixtureLimits.MaximumConcurrentConnections;
});
builder.Services.ConfigureHttpJsonOptions(options =>
    FixtureJson.Configure(options.SerializerOptions));
builder.Services.AddSingleton(FixtureAuthenticationOptions.Load(
    builder.Configuration,
    builder.Environment.ContentRootPath));
builder.Services.AddSingleton<TestControlState>();
builder.Services.AddSingleton<RequestCaptureStore>();
builder.Services.AddSingleton<ComfyUiFixtureState>();

var app = builder.Build();
app.UseMiddleware<RequestCaptureMiddleware>();
app.UseMiddleware<FixtureTokenAuthorizationMiddleware>();
app.MapFixtureEndpoints();
app.Run();

public partial class Program;
