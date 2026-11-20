using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Mvc.RazorPages; 
using MySqlConnector; 
using Sign_Language_Learning_System.Data;

namespace Sign_Language_Learning_System.Pages
{
    public class LoginModel : PageModel 
    { 
        private readonly MySqlConnectionFactory _connectionFactory;

        public LoginModel(MySqlConnectionFactory connectionFactory) 
        { 
            _connectionFactory = connectionFactory;
        } 

        [BindProperty] 
        public string Email { get; set; } = ""; 

        [BindProperty] 
        public string Password { get; set; } = ""; 

        public string Message { get; set; } = ""; 
        [TempData] public string AccountMessage { get; set; } = "";

        public void OnGet() 
        { 
        } 

        public IActionResult OnPost()
{
    using MySqlConnection connection = _connectionFactory.Create();
    connection.Open();

    string sql = @"SELECT Id, FullName, PasswordHash, Role, Status
                   FROM users 
                   WHERE Email = @email
                   LIMIT 1";
    using MySqlCommand command = new MySqlCommand(sql, connection);
    command.Parameters.AddWithValue("@email", Email.Trim());

    using MySqlDataReader reader = command.ExecuteReader();

    if (reader.Read())
    {
        int userId = Convert.ToInt32(reader["Id"]);
        string fullName = reader["FullName"].ToString() ?? "";
        string storedHash = reader["PasswordHash"].ToString() ?? "";
        string userRole = reader["Role"].ToString() ?? "User";  
        string userStatus = reader["Status"].ToString() ?? "";
        bool passwordCorrect = BCrypt.Net.BCrypt.Verify(Password, storedHash);

        if (passwordCorrect && string.Equals(userStatus, "Deactive", StringComparison.OrdinalIgnoreCase))
        {
            Message = "Your account has been deactivated. Please contact an administrator to reactivate it.";
            return Page();
        }

        if (passwordCorrect && string.Equals(userStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            HttpContext.Session.SetInt32("UserId", userId);
            HttpContext.Session.SetString("FullName", fullName);
            bool isAdmin = UserRoles.IsAdministrator(userRole);
            HttpContext.Session.SetString("Role", isAdmin ? UserRoles.Administrator : userRole.Trim());

            return RedirectToPage(isAdmin ? "/AdminDashboard" : "/Dashboard");
        }
    }
    

    Message = "Invalid email address or password.";
    return Page();
}

    }
}