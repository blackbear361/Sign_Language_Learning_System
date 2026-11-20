using Microsoft.AspNetCore.Mvc.RazorPages;
using Sign_Language_Learning_System.Models;
using Sign_Language_Learning_System.Data;
using MySqlConnector;

namespace Sign_Language_Learning_System.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly MySqlConnectionFactory _connectionFactory;

        public DashboardModel(MySqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public int TotalLessons { get; private set; }
        public int CompletedLessons { get; private set; }
        public string UserName { get; private set; } = string.Empty;
        public int ProgressPercent => TotalLessons == 0 ? 0 : (int)Math.Round(CompletedLessons * 100.0 / TotalLessons);
        public QuizResult? LatestQuiz { get; private set; }

        public async Task OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId")
                ?? throw new InvalidOperationException("The session is missing the signed-in user.");
            UserName = HttpContext.Session.GetString("FullName") ?? string.Empty;
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync();

            await using (var command = new MySqlCommand(
                "SELECT COUNT(*) FROM lessons WHERE IsPublished = 1", connection))
                TotalLessons = Convert.ToInt32(await command.ExecuteScalarAsync());

            await using (var command = new MySqlCommand("""
                SELECT COUNT(*)
                FROM lesson_progress p
                INNER JOIN lessons l ON l.Id = p.LessonId
                WHERE p.UserId = @userId AND l.IsPublished = 1
                """, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                CompletedLessons = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            await using var latestCommand = new MySqlCommand("""
                SELECT Id, UserId, Category, Score, TotalQuestions, AnswerSummary, AttemptedAtUtc
                FROM quiz_results WHERE UserId = @userId
                ORDER BY AttemptedAtUtc DESC, Id DESC LIMIT 1
                """, connection);
            latestCommand.Parameters.AddWithValue("@userId", userId);
            await using var reader = await latestCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync()) LatestQuiz = MySqlRowMapper.ReadQuizResult(reader);
        }
    }
}
