using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reconciliation.Application;
using Reconciliation.Infrastructure;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var boliviaCs = context.Configuration["SqlConnectionString"]
            ?? throw new InvalidOperationException("SqlConnectionString is not configured.");

        var chileCs = context.Configuration["ChileConnectionString"];
        var accountingCs = context.Configuration["AccountingConnectionString"];

        services.AddApplication();

        if (!string.IsNullOrEmpty(chileCs) && !string.IsNullOrEmpty(accountingCs))
        {
            services.AddInfrastructure(new Dictionary<string, string>
            {
                ["Bolivia"] = boliviaCs,
                ["Chile"] = chileCs,
                ["Accounting"] = accountingCs
            });
        }
        else
        {
            services.AddInfrastructure(boliviaCs);
        }
    })
    .ConfigureLogging(logging =>
    {
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

host.Run();
