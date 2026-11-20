using Microsoft.AspNetCore.Mvc; 
using Microsoft.AspNetCore.Mvc.RazorPages; 
using MySqlConnector; 
using Sign_Language_Learning_System.Data;

namespace Sign_Language_Learning_System.Pages
{
    public class SignupModel : PageModel 
    { 
        private readonly MySqlConnectionFactory _connectionFactory;

        public SignupModel(MySqlConnectionFactory connectionFactory) 
        { 
            _connectionFactory = connectionFactory;
        } 

        [BindProperty] 
        public string FullName { get; set; } = ""; 

        [BindProperty] 
        public string Email { get; set; } = ""; 

        [BindProperty] 
        public string Password { get; set; } = ""; 

        [BindProperty] 
        public string ConfirmPassword { get; set; } = ""; 

        public string Message { get; set; } = ""; 
        public bool RegistrationSuccessful { get; set; }

        public void OnGet() 
        { 
        } 

        public IActionResult OnPost() 
        { 

            if (Password != ConfirmPassword) 
            { 
                Message = "Password and confirm password do not match."; 
                return Page(); 
            } 
            

            using MySqlConnection connection = _connectionFactory.Create();

            connection.Open(); 


            string checkSql = "SELECT COUNT(*) FROM Users WHERE Email = @email"; 
            using MySqlCommand checkCommand = new MySqlCommand(checkSql, connection); 
            checkCommand.Parameters.AddWithValue("@email", Email.Trim()); 
            
            int existingUserCount = Convert.ToInt32(checkCommand.ExecuteScalar()); 
            if (existingUserCount > 0) 
            { 
                Message = "This email address is already registered."; 
                return Page(); 
            } 

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(Password); 

            string sql = @"INSERT INTO Users (FullName, Email, PasswordHash) 
                           VALUES (@full_name, @email, @password_hash)"; 

            using MySqlCommand command = new MySqlCommand(sql, connection); 
            command.Parameters.AddWithValue("@full_name", FullName.Trim()); 
            command.Parameters.AddWithValue("@email", Email.Trim()); 
            command.Parameters.AddWithValue("@password_hash", passwordHash); 
            command.ExecuteNonQuery(); 

            FullName = "";
            Email = "";
            Message = "Registration successful! You can now go to the Login page."; 
            RegistrationSuccessful = true;
            
            return Page(); 
        } 
    }
}