using MySqlConnector;

namespace Sign_Language_Learning_System.Data;

public static class AdminAccountSeeder
{
    public static async Task SeedAsync(MySqlConnectionFactory connectionFactory, IConfiguration configuration)
    {
        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();

        await using var checkCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM users WHERE Email = @email", connection);
        checkCommand.Parameters.AddWithValue("@email", email);
        if (Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0)
        {
            return;
        }

        // The unique email key also prevents duplicates during concurrent startup.
        await using var command = new MySqlCommand("""
            INSERT INTO users (FullName, Email, PasswordHash, Role, Status)
            VALUES (@fullName, @email, @passwordHash, @role, 'Active')
            ON DUPLICATE KEY UPDATE Id = Id
            """, connection);
        command.Parameters.AddWithValue("@fullName", "Administrator");
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@passwordHash", BCrypt.Net.BCrypt.HashPassword(password));
        command.Parameters.AddWithValue("@role", UserRoles.Administrator);
        await command.ExecuteNonQueryAsync();
    }
}
