using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace Sign_Language_Learning_System.Data;

public sealed class SessionAuthorizationFilter(MySqlConnectionFactory connectionFactory) : IAsyncPageFilter
{
    private static readonly HashSet<string> MemberPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Dashboard", "/Lessons", "/Quiz", "/QuizResult", "/MyResults", "/Profile"
    };

    private static readonly HashSet<string> AdministratorPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/AdminDashboard", "/AdminUsers", "/AdminContent", "/AdminQuizzes"
    };

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(
        PageHandlerExecutingContext context,
        PageHandlerExecutionDelegate next)
    {
        var page = context.ActionDescriptor.ViewEnginePath;
        if (!MemberPages.Contains(page) && !AdministratorPages.Contains(page))
        {
            await next();
            return;
        }

        var userId = context.HttpContext.Session.GetInt32("UserId");
        if (userId is null)
        {
            context.Result = new RedirectToPageResult("/Login");
            return;
        }

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand(
            "SELECT Status FROM users WHERE Id = @id", connection);
        command.Parameters.AddWithValue("@id", userId.Value);
        var status = Convert.ToString(await command.ExecuteScalarAsync());
        if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            context.HttpContext.Session.Clear();
            context.Result = new RedirectToPageResult("/Login");
            return;
        }

        if (AdministratorPages.Contains(page)
            && !UserRoles.IsAdministrator(context.HttpContext.Session.GetString("Role")))
        {
            context.Result = new RedirectToPageResult("/Dashboard");
            return;
        }

        await next();
    }
}
