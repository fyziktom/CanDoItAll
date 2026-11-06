using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework.Pages.Authoring;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowCatalogReaderTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_filtered_page_keeps_exact_selected_metadata_without_loading_a_graph(bool selectedVisible) {
        var selected = Item("Selected");
        var query = new WorkflowCatalogSearchQuery("filter", WorkflowLifecycleStatus.Active, 2, 2);
        var ports = new ReadPorts {
            Search = request => {
                Assert.Same(query, request);
                return new(selectedVisible ? [selected, Item("Other")] : [Item("Other"), Item("Another")], 2, 2, 8);
            },
            Lookup = request => {
                Assert.Equal([selected.Id], request.WorkflowIds);
                return [selected];
            }
        };
        var result = await new WorkflowCatalogReader(ports, ports).ReadAsync(query, selected.Id, default);
        Assert.Equal(selected, result.Selected);
        Assert.Equal(2, result.Page.Items.Count);
        Assert.Equal(8, result.Page.TotalCount);
        Assert.Equal(selectedVisible ? 0 : 1, ports.LookupCalls);
        Assert.Equal(1, ports.SearchCalls);
    }

    [Fact]
    public async Task Deletion_of_the_last_page_clamps_once_and_keeps_the_filter() {
        var queries = new List<WorkflowCatalogSearchQuery>();
        var ports = new ReadPorts {
            Search = query => {
                queries.Add(query);
                return query.PageIndex == 3 ? new([], 3, 2, 3) : new([Item("Remaining")], 1, 2, 3);
            }
        };
        var result = await new WorkflowCatalogReader(ports, ports).ReadAsync(new("same filter", WorkflowLifecycleStatus.Draft, 3, 2), null, default);
        Assert.Equal(1, result.Page.PageIndex);
        Assert.Equal(2, queries.Count);
        Assert.All(queries, query => {
            Assert.Equal("same filter", query.Text);
            Assert.Equal(WorkflowLifecycleStatus.Draft, query.Status);
            Assert.Equal(2, query.PageSize);
        });
        Assert.Equal(0, ports.LookupCalls);
    }

    [Fact]
    public async Task Failed_selected_metadata_read_does_not_return_a_partial_catalog_snapshot() {
        var ports = new ReadPorts {
            Search = _ => new([Item("Visible")], 0, 25, 1),
            Lookup = _ => throw new InvalidOperationException("Read unavailable")
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new WorkflowCatalogReader(ports, ports).ReadAsync(new(), WorkflowId.New(), default));
        Assert.Equal(1, ports.SearchCalls);
        Assert.Equal(1, ports.LookupCalls);
    }

    private static WorkflowCatalogItem Item(string name)
        => new(WorkflowId.New(), WorkflowVersionId.New(), name, "Metadata", WorkflowLifecycleStatus.Draft,
            WorkflowRuntimeBackendKind.InProcess, DateTimeOffset.UnixEpoch);

    private sealed class ReadPorts : IWorkflowCatalogSearchService, IWorkflowCatalogLookupService {
        public required Func<WorkflowCatalogSearchQuery, WorkflowCatalogSearchPage> Search { get; init; }
        public Func<WorkflowCatalogLookupQuery, IReadOnlyList<WorkflowCatalogItem>> Lookup { get; init; } = _ => [];
        public int SearchCalls { get; private set; }
        public int LookupCalls { get; private set; }

        public Task<WorkflowCatalogSearchPage> SearchDefinitionsAsync(WorkflowCatalogSearchQuery query, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            SearchCalls++;
            return Task.FromResult(Search(query));
        }

        public Task<IReadOnlyList<WorkflowCatalogItem>> LookupDefinitionsAsync(WorkflowCatalogLookupQuery query, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            LookupCalls++;
            return Task.FromResult(Lookup(query));
        }
    }
}
