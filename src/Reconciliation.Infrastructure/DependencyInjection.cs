using Microsoft.Extensions.DependencyInjection;
using Reconciliation.Domain.Interfaces;
using Reconciliation.Domain.Operations;
using Reconciliation.Infrastructure.Data;
using Reconciliation.Infrastructure.Repositories;

namespace Reconciliation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(new DbConnectionFactory(connectionString));
        services.AddScoped<ICashierRepository, CashierRepository>();
        services.AddScoped<IVaultRepository, VaultRepository>();
        services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();
        services.AddScoped<IGerencialRepository, GerencialRepository>();
        services.AddScoped<IOperationsRepository, OperationsRepository>();
        return services;
    }

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Dictionary<string, string> connectionStrings)
    {
        services.AddSingleton<IDbConnectionFactory>(new DbConnectionFactory(connectionStrings));

        // Existing repositories
        services.AddScoped<ICashierRepository, CashierRepository>();
        services.AddScoped<IVaultRepository, VaultRepository>();
        services.AddScoped<IExchangeRateRepository, ExchangeRateRepository>();

        // Gerencial repository
        services.AddScoped<IGerencialRepository, GerencialRepository>();

        // Remittance repositories
        services.AddScoped<IRemittanceRepository, RemittanceRepository>();
        services.AddScoped<IChileRemittanceRepository, ChileRemittanceRepository>();
        services.AddScoped<IAccountingRepository, AccountingRepository>();

        // Operations repository
        services.AddScoped<IOperationsRepository, OperationsRepository>();

        return services;
    }
}
