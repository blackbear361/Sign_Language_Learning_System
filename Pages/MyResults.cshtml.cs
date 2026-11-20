using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Sign_Language_Learning_System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Pages;

public class MyResultsModel : PageModel
{
    private readonly MySqlConnectionFactory _connectionFactory;

    public MyResultsModel(MySqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public string? Topic { get; set; }

    public IList<QuizResult> Results { get; private set; } = new List<QuizResult>();
    public IList<string> Topics { get; private set; } = new List<string>();
    public int TotalAttempts { get; private set; }
    public int AveragePercent { get; private set; }
    public int BestPercent { get; private set; }
    public int TopicsPassed { get; private set; }

    public async Task OnGetAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId")
            ?? throw new InvalidOperationException("The session is missing the signed-in user.");
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand("""
            SELECT Id, UserId, Category, Score, TotalQuestions, AnswerSummary, AttemptedAtUtc
            FROM quiz_results WHERE UserId = @userId
            ORDER BY AttemptedAtUtc DESC, Id DESC
            """, connection);
        command.Parameters.AddWithValue("@userId", userId);

        var all = new List<QuizResult>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) all.Add(MySqlRowMapper.ReadQuizResult(reader));

        Topics = all.Select(result => result.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();
        Results = string.IsNullOrWhiteSpace(Topic)
            ? all
            : all.Where(result => string.Equals(result.Category, Topic, StringComparison.OrdinalIgnoreCase)).ToList();
        TotalAttempts = all.Count;
        if (all.Count > 0)
        {
            AveragePercent = (int)Math.Round(all.Average(result => result.Percent));
            BestPercent = all.Max(result => result.Percent);
        }
        TopicsPassed = all.Where(result => result.Passed)
            .Select(result => result.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }
}
