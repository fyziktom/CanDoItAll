using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Support;

public enum PostcommitOwner { Resource, TestPlan }
public enum PostcommitFault { Search, SearchCancellation, Activity }

public sealed class OwnerPostcommitTestProbe {
    public const string ResourceLocation = "https://example.invalid/retained-resource";
    public Func<PostcommitOwner, Guid, bool, CancellationToken, Task>? AfterSearch { get; set; }
    public Func<PostcommitOwner, ActivityWriteRequest, CancellationToken, Task>? AfterActivity { get; set; }
    public Func<PostcommitOwner, CancellationToken, Task>? BeforeSave { get; set; }
    public Func<PostcommitOwner, CancellationToken, Task>? BeforeRead { get; set; }
    public int SearchReturns { get; private set; }
    public int ActivityReturns { get; private set; }

    public void ConfigureServices(IServiceCollection services) {
        var activity = services.Last(descriptor => descriptor.ServiceType == typeof(IActivityStream));
        if (activity.ImplementationType != typeof(NullActivityStream)) {
            throw new InvalidOperationException("This fixture must wrap the application's actual NullActivityStream registration.");
        }
        services.AddScoped<ISearchIndexService>(provider =>
            new SearchBoundary(provider.GetRequiredService<SearchIndexService>(), this));
        services.AddSingleton<IActivityStream>(new ActivityBoundary(new NullActivityStream(), this));
        var saveBoundary = new SaveBoundary(this);
        var readBoundary = new ReadBoundary(this);
        services.AddSingleton<IDbContextFactory<ResourcesDbContext>>(provider =>
            new PooledDbContextFactory<ResourcesDbContext>(new DbContextOptionsBuilder<ResourcesDbContext>(
                provider.GetRequiredService<DbContextOptions<ResourcesDbContext>>())
                .AddInterceptors(saveBoundary, readBoundary).Options));
        services.AddSingleton<IDbContextFactory<TestLabDbContext>>(provider =>
            new PooledDbContextFactory<TestLabDbContext>(new DbContextOptionsBuilder<TestLabDbContext>(
                provider.GetRequiredService<DbContextOptions<TestLabDbContext>>())
                .AddInterceptors(saveBoundary, readBoundary).Options));
    }

    public void ArmFault(PostcommitOwner owner, PostcommitFault fault, Exception failure,
        CancellationTokenSource? cancellation = null) {
        if (fault == PostcommitFault.Activity) {
            AfterActivity = (actual, _, _) => {
                if (actual != owner) {
                    return Task.CompletedTask;
                }
                AfterActivity = null;
                throw failure;
            };
            return;
        }
        AfterSearch = (actual, _, _, _) => {
            if (actual != owner) {
                return Task.CompletedTask;
            }
            AfterSearch = null;
            if (fault == PostcommitFault.SearchCancellation) {
                (cancellation ?? throw new InvalidOperationException("The cancellation case requires its original source.")).Cancel();
            }
            throw failure;
        };
    }

    public void FailNextRead(PostcommitOwner owner, Exception failure) {
        var remaining = 1;
        BeforeRead = (actual, _) => {
            if (actual == owner && Interlocked.Exchange(ref remaining, 0) == 1) {
                throw failure;
            }
            return Task.CompletedTask;
        };
    }

    public static async Task<ProjectWriteAdmission> CreateProjectAsync(IServiceProvider services, string name) {
        var result = await services.GetRequiredService<ProjectsService>().SaveAsync(new ProjectEditorModel { Name = name });
        if (!result.IsSuccess) {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Message)));
        }
        return await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(result.Value)
            ?? throw new InvalidOperationException("The created project must have its own admission.");
    }

    public static ResourceEditorModel Resource(IServiceProvider services, ProjectWriteAdmission admission) {
        var plugin = services.GetRequiredService<ResourceConnectorPluginRegistry>().Resolve(null, ResourceKind.WebLink);
        var model = new ResourceEditorModel {
            ProjectId = admission.ProjectId,
            ExpectedProjectAdmission = admission,
            Name = "Retained resource",
            Description = "Committed owner data",
            ConnectorPluginKey = plugin.Manifest.PluginKey,
            SupportsPreview = true
        };
        plugin.ApplyConfig(model, JsonSerializer.Serialize(new WebLinkResourceConfig(ResourceLocation, "Retained title")));
        return model;
    }

    public static TestPlanEditorModel Plan(ProjectWriteAdmission? admission) => new() {
        ProjectId = admission?.ProjectId,
        ExpectedProjectAdmission = admission,
        Title = "Retained test plan",
        Phase = "Verification",
        CoverageGoal = "Preserve every committed child identity",
        PlaywrightSpecPath = "tests/retained.spec.ts",
        Cases = [new() { Name = "First scenario", StoryOrFeature = "Owner write", Status = TestCaseStatus.Passed, Notes = "Case evidence" }],
        Evidence = [new() { EvidenceLabel = "First artifact", ArtifactPath = "artifacts/retained.png", EvidenceKind = "Screenshot", Notes = "Evidence notes" }],
        Runs = [new() { ExecutedAtUtc = new(2026, 9, 11, 10, 11, 12, TimeSpan.Zero), Runner = "Playwright", Result = TestCaseStatus.Passed, Summary = "Original run" }]
    };

    private static PostcommitOwner? Owner(DbContext? context) => context switch {
        ResourcesDbContext => PostcommitOwner.Resource,
        TestLabDbContext => PostcommitOwner.TestPlan,
        _ => null
    };

    private static PostcommitOwner? Owner(string? sourceType) => sourceType switch {
        "resource" => PostcommitOwner.Resource,
        "test-plan" => PostcommitOwner.TestPlan,
        _ => null
    };

    private sealed class SearchBoundary(SearchIndexService actual, OwnerPostcommitTestProbe probe) : ISearchIndexService {
        public async Task UpsertAsync(SearchDocumentInput input, CancellationToken cancellationToken = default) {
            await actual.UpsertAsync(input, cancellationToken);
            await ReturnedAsync(input.SourceType, input.SourceKey, false, cancellationToken);
        }

        public async Task UpsertForMutationAsync(SearchDocumentInput input, CancellationToken cancellationToken = default) {
            await actual.UpsertForMutationAsync(input, cancellationToken);
            await ReturnedAsync(input.SourceType, input.SourceKey, false, cancellationToken);
        }

        public async Task DeleteAsync(string sourceType, string sourceKey, CancellationToken cancellationToken = default) {
            await actual.DeleteAsync(sourceType, sourceKey, cancellationToken);
            await ReturnedAsync(sourceType, sourceKey, true, cancellationToken);
        }

        public Task<IReadOnlyList<SearchResult>> SearchAsync(string query, int take = 12, CancellationToken cancellationToken = default)
            => actual.SearchAsync(query, take, cancellationToken);

        private async Task ReturnedAsync(string sourceType, string sourceKey, bool deleted, CancellationToken cancellationToken) {
            if (Owner(sourceType) is { } owner) {
                probe.SearchReturns++;
                if (probe.AfterSearch is { } callback) {
                    await callback(owner, Guid.Parse(sourceKey), deleted, cancellationToken);
                }
            }
        }
    }

    private sealed class ActivityBoundary(NullActivityStream actual, OwnerPostcommitTestProbe probe) : IActivityStream {
        public async Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            await actual.RecordAsync(request, cancellationToken);
            if (Owner(request.ArtifactKind) is { } owner) {
                probe.ActivityReturns++;
                if (probe.AfterActivity is { } callback) {
                    await callback(owner, request, cancellationToken);
                }
            }
        }
    }

    private sealed class SaveBoundary(OwnerPostcommitTestProbe probe) : SaveChangesInterceptor {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Owner(eventData.Context) is { } owner && probe.BeforeSave is { } callback) {
                await callback(owner, cancellationToken);
            }
            return result;
        }
    }

    private sealed class ReadBoundary(OwnerPostcommitTestProbe probe) : DbCommandInterceptor {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (Owner(eventData.Context) is { } owner && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                && probe.BeforeRead is { } callback) {
                await callback(owner, cancellationToken);
            }
            return result;
        }
    }
}
