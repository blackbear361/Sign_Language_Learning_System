using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Sign_Language_Learning_System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Pages;

public class QuizModel : PageModel
{
    private static readonly string[] ValidOptions = { "A", "B", "C", "D" };
    private readonly MySqlConnectionFactory _connectionFactory;

    public QuizModel(MySqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    [BindProperty(SupportsGet = true)]
    public string? Topic { get; set; }

    [BindProperty]
    public Dictionary<int, string> Answers { get; set; } = new();

    public IList<TopicSummary> Topics { get; private set; } = new List<TopicSummary>();
    public IList<QuizQuestion> Questions { get; private set; } = new List<QuizQuestion>();
    public HashSet<int> UnansweredIds { get; private set; } = new();
    [TempData] public string StatusMessage { get; set; } = string.Empty;

    public record TopicSummary(string Category, int QuestionCount, int? BestPercent, int Attempts);

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            await LoadTopicsAsync();
            return Page();
        }

        Questions = await LoadQuestionsAsync(Topic);
        if (Questions.Count == 0)
        {
            StatusMessage = $"There's no quiz for \"{Topic}\" yet. Try another topic.";
            return RedirectToPage("/Quiz");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Topic)) return RedirectToPage("/Quiz");
        Questions = await LoadQuestionsAsync(Topic);
        if (Questions.Count == 0) return RedirectToPage("/Quiz");

        foreach (var question in Questions)
            if (!Answers.TryGetValue(question.Id, out var answer) || !ValidOptions.Contains(answer))
                UnansweredIds.Add(question.Id);

        if (UnansweredIds.Count > 0)
        {
            ModelState.AddModelError(string.Empty, UnansweredIds.Count == 1
                ? "One question still needs an answer."
                : $"{UnansweredIds.Count} questions still need an answer.");
            return Page();
        }

        var score = Questions.Count(question => Answers[question.Id] == question.CorrectOption);
        var answerSummary = string.Join(";", Questions.Select(question => $"{question.Id}:{Answers[question.Id]}"));
        var userId = HttpContext.Session.GetInt32("UserId")
            ?? throw new InvalidOperationException("The session is missing the signed-in user.");

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand("""
            INSERT INTO quiz_results (UserId, Category, Score, TotalQuestions, AnswerSummary, AttemptedAtUtc)
            VALUES (@userId, @category, @score, @total, @answers, UTC_TIMESTAMP())
            """, connection);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@category", Questions[0].Lesson.Category);
        command.Parameters.AddWithValue("@score", score);
        command.Parameters.AddWithValue("@total", Questions.Count);
        command.Parameters.AddWithValue("@answers", answerSummary);
        await command.ExecuteNonQueryAsync();
        var resultId = checked((int)command.LastInsertedId);
        return RedirectToPage("/QuizResult", new { id = resultId });
    }

    private async Task<IList<QuizQuestion>> LoadQuestionsAsync(string topic)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand($"""
            SELECT {MySqlRowMapper.QuizQuestionWithLessonColumns}
            FROM quiz_questions q
            INNER JOIN lessons l ON l.Id = q.LessonId
            WHERE l.IsPublished = 1 AND l.Category = @category
              AND (q.RequiresVideo = 0 OR q.VideoUrl IS NOT NULL)
            ORDER BY l.Title, q.Id
            """, connection);
        command.Parameters.AddWithValue("@category", topic);
        await using var reader = await command.ExecuteReaderAsync();
        var questions = new List<QuizQuestion>();
        while (await reader.ReadAsync()) questions.Add(MySqlRowMapper.ReadQuizQuestion(reader));
        return questions;
    }

    private async Task LoadTopicsAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId")
            ?? throw new InvalidOperationException("The session is missing the signed-in user.");
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        var topicCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new MySqlCommand("""
            SELECT l.Category, COUNT(*) AS QuestionCount
            FROM quiz_questions q
            INNER JOIN lessons l ON l.Id = q.LessonId
            WHERE l.IsPublished = 1 AND (q.RequiresVideo = 0 OR q.VideoUrl IS NOT NULL)
            GROUP BY l.Category ORDER BY l.Category
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) topicCounts[reader.GetString("Category")] = reader.GetInt32("QuestionCount");

        var resultGroups = new Dictionary<string, List<QuizResult>>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new MySqlCommand("""
            SELECT Id, UserId, Category, Score, TotalQuestions, AnswerSummary, AttemptedAtUtc
            FROM quiz_results WHERE UserId = @userId
            """, connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var result = MySqlRowMapper.ReadQuizResult(reader);
                if (!resultGroups.TryGetValue(result.Category, out var list))
                    resultGroups[result.Category] = list = new List<QuizResult>();
                list.Add(result);
            }
        }

        Topics = topicCounts.Select(topic =>
        {
            resultGroups.TryGetValue(topic.Key, out var attempts);
            return new TopicSummary(topic.Key, topic.Value,
                attempts is { Count: > 0 } ? attempts.Max(result => result.Percent) : null,
                attempts?.Count ?? 0);
        }).ToList();
    }
}
