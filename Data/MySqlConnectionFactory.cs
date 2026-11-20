using MySqlConnector;

namespace Sign_Language_Learning_System.Data;

public sealed class MySqlConnectionFactory(IConfiguration configuration)
{
    private readonly string _connectionString =
        configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

    public MySqlConnection Create() => new(_connectionString);
}
