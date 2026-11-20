using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Sign_Language_Learning_System.Data;

namespace Sign_Language_Learning_System.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ProfileModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [BindProperty]
        public string FullName { get; set; } = "";

        public string Email { get; set; } = "";

        [BindProperty]
        public string Phone { get; set; } = "";

        [BindProperty]
        public string Bio { get; set; } = "";

        [BindProperty]
        public DateTime? DateOfBirth { get; set; }

        [BindProperty]
        public string SkillLevel { get; set; } = "Beginner";

        public string Message { get; set; } = "";
        public bool CanDeactivateAccount =>
            !UserRoles.IsAdministrator(HttpContext.Session.GetString("Role"));

        public IActionResult OnGet()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            string sql = @"
                SELECT u.FullName, u.Email, p.Phone, p.Bio, p.DateOfBirth, p.SkillLevel
                FROM users u
                LEFT JOIN user_profiles p ON u.Id = p.UserId
                WHERE u.Id = @userId";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@userId", userId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                FullName = reader["FullName"].ToString() ?? "";
                Email = reader["Email"].ToString() ?? "";
                Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() ?? "" : "";
                Bio = reader["Bio"] != DBNull.Value ? reader["Bio"].ToString() ?? "" : "";
                DateOfBirth = reader["DateOfBirth"] != DBNull.Value ? Convert.ToDateTime(reader["DateOfBirth"]) : null;
                SkillLevel = reader["SkillLevel"] != DBNull.Value ? reader["SkillLevel"].ToString() ?? "Beginner" : "Beginner";
            }

            return Page();
        }
          
        public IActionResult OnPost()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            string connectionString = _configuration.GetConnectionString("DefaultConnection") ?? "";
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            string updateUsersSql = "UPDATE users SET FullName = @fullName WHERE Id = @userId";
            using (var cmd = new MySqlCommand(updateUsersSql, connection))
            {
                cmd.Parameters.AddWithValue("@fullName", FullName);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.ExecuteNonQuery();
            }
            string upsertProfileSql = @"
                INSERT INTO user_profiles (UserId, Phone, Bio, DateOfBirth, SkillLevel)
                VALUES (@userId, @phone, @bio, @dob, @skill)
                ON DUPLICATE KEY UPDATE
                    Phone = VALUES(Phone),
                    Bio = VALUES(Bio),
                    DateOfBirth = VALUES(DateOfBirth),
                    SkillLevel = VALUES(SkillLevel)";

            using (var cmd = new MySqlCommand(upsertProfileSql, connection))
            {
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@phone", Phone ?? "");
                cmd.Parameters.AddWithValue("@bio", Bio ?? "");
                cmd.Parameters.AddWithValue("@dob", (object?)DateOfBirth ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@skill", SkillLevel ?? "Beginner");
                cmd.ExecuteNonQuery();
            }

            HttpContext.Session.SetString("FullName", FullName);

            Message = "Profile updated successfully!";
            return Page();
        }
        public IActionResult OnPostDeactivateAccount()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToPage("/Login");

            if (!CanDeactivateAccount)
                return Forbid();

            string connectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            string sql = "UPDATE users SET Status = 'Deactive' WHERE Id = @userId";
            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@userId", userId);
            command.ExecuteNonQuery();

            HttpContext.Session.Clear();
            TempData["AccountMessage"] = "Your account is deactivated. Contact an administrator if you want to reactivate it.";
            return RedirectToPage("/Login");
        }
    }
}