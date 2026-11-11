using Microsoft.Extensions.DependencyInjection;
using Reconciliation.Application.Export;
using Reconciliation.Application.Interfaces;
using Reconciliation.Application.Operations;
using Reconciliation.Application.Services;

namespace Reconciliation.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICashierService, CashierService>();
        services.AddScoped<IVaultService, VaultService>();
        services.AddScoped<IRemittanceService, RemittanceService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IPdfExportService, PdfExportService>();
        services.AddScoped<IGerencialService, GerencialService>();
        services.AddScoped<IGerencialChartGenerator, GerencialChartGenerator>();
        services.AddScoped<IGerencialExcelExportService, GerencialExcelExportService>();
        services.AddScoped<IGerencialPdfExportService, GerencialPdfExportService>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.AddScoped<IOperationsExcelExportService, OperationsExcelExportService>();
        return services;
    }
}
