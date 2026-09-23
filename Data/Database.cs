using MySqlConnector;

namespace HillApp.Data;

public sealed class Database
{
    private readonly string _connectionString;

    public Database(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("TiDB") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            throw new InvalidOperationException(
                "No se encontró ConnectionStrings:TiDB. Copia tu appsettings.Local.json de la versión anterior o configura la variable ConnectionStrings__TiDB.");
        }
    }

    public MySqlConnection CreateConnection() => new(_connectionString);
}
