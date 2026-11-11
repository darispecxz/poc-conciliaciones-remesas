using System.Data;
using MySqlConnector;

namespace Reconciliation.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
    IDbConnection CreateConnection(string name);
}

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly Dictionary<string, string> _connectionStrings;
    private const string DefaultName = "Bolivia";

    public DbConnectionFactory(string connectionString)
    {
        _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DefaultName] = connectionString
        };
    }

    public DbConnectionFactory(Dictionary<string, string> connectionStrings)
    {
        _connectionStrings = new Dictionary<string, string>(connectionStrings, StringComparer.OrdinalIgnoreCase);
    }

    public IDbConnection CreateConnection()
    {
        return CreateConnection(DefaultName);
    }

    public IDbConnection CreateConnection(string name)
    {
        if (!_connectionStrings.TryGetValue(name, out var cs))
            throw new InvalidOperationException($"Connection string '{name}' is not configured.");
        return new MySqlConnection(cs);
    }
}
