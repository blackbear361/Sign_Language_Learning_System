namespace Sign_Language_Learning_System.Models;

public sealed class ApplicationUser
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public string Status { get; set; } = "Active";
}
