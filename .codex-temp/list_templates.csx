using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using CanDoItAll.Tests.Support;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Activity;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Automation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Web.Infrastructure;
using System.Linq;

var loader = new ProcessTemplatePackLoader("C:/repositories/CanDoItAll/Templates/Processes");
var projection = new ProcessTemplateProjectionService(loader);
var service = new ProcessTemplateLibraryService(loader, projection);
foreach (var item in service.ListItems(ProcessTemplateLibraryCategory.Processes).Take(5))
{
    Console.WriteLine(item.Title);
}
