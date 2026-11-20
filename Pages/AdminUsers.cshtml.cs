using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Sign_Language_Learning_System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Pages;

public class AdminUsersModel : PageModel
{
    private readonly MySqlConnectionFactory _connectionFactory;
    public AdminUsersModel(MySqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public IList<ApplicationUser> Users { get; private set; } = new List<ApplicationUser>();
    public int? CurrentUserId { get; private set; }
    [TempData] public string StatusMessage { get; set; } = string.Empty;
    [TempData] public string ErrorMessage { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        CurrentUserId = HttpContext.Session.GetInt32("UserId");
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            "SELECT Id, FullName, Email, Role, Status FROM users ORDER BY Email", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var users = new List<ApplicationUser>();
        while (await reader.ReadAsync()) users.Add(MySqlRowMapper.ReadApplicationUser(reader));
        Users = users;
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
    {
        if (status is not ("Active" or "Deactive"))
            return BadRequest();

        if (status == "Deactive" && id == HttpContext.Session.GetInt32("UserId"))
        {
            ErrorMessage = "You cannot deactivate the admin account you are currently using.";
            return RedirectToPage();
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            "UPDATE users SET Status = @status WHERE Id = @id", connection);
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@id", id);

        if (await command.ExecuteNonQueryAsync() == 0)
        {
            await using var existsCommand = new MySqlCommand(
                "SELECT COUNT(*) FROM users WHERE Id = @id", connection);
            existsCommand.Parameters.AddWithValue("@id", id);
            if (Convert.ToInt32(await existsCommand.ExecuteScalarAsync()) == 0)
                return NotFound();
        }

        StatusMessage = $"User status updated to {status}.";
        return RedirectToPage();
    }
}
