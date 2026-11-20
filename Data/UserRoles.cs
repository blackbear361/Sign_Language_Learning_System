namespace Sign_Language_Learning_System.Data;

public static class UserRoles
{
    public const string Administrator = "Administrator";

    public static bool IsAdministrator(string? role)
    {
        var normalizedRole = role?.Trim();
        return string.Equals(normalizedRole, Administrator, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedRole, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
