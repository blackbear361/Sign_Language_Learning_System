using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Sign_Language_Learning_System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Pages;

public class QuizResultModel : PageModel
{
    private readonly MySqlConnectionFactory _connectionFactory;

    public QuizResultModel(MySqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public QuizResult Result { get; private set; } = null!;
    public IList<ReviewItem> Review { get; private set; } = new List<ReviewItem>();
    public int AttemptNumber { get; private set; }
    public int BestPercent { get; private set; }

    public record ReviewItem(int Number, string Question, string? YourAnswer, string? CorrectAnswer, bool IsCorrect, string LessonTitle, string? ImageUrl);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId")
            ?? throw new InvalidOperationException("The session is missing the signed-in user.");
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        await using (var command = new MySqlCommand("""
            SELECT Id, UserId, Category, Score, TotalQuestions, AnswerSummary, AttemptedAtUtc
            FROM quiz_results WHERE Id = @id AND UserId = @userId
            """, connection))
        {
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@userId", userId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return NotFound();
            Result = MySqlRowMapper.ReadQuizResult(reader);
        }

        var answers = QuizResult.ParseAnswers(Result.AnswerSummary);
        var review = new List<ReviewItem>();
        var number = 0;
        foreach (var (questionId, option) in answers)
        {
            number++;
            await using var command = new MySqlCommand($"""
                SELECT {MySqlRowMapper.QuizQuestionWithLessonColumns}
                FROM quiz_questions q
                INNER JOIN lessons l ON l.Id = q.LessonId
                WHERE q.Id = @id
                """, connection);
            command.Parameters.AddWithValue("@id", questionId);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) continue;
            var question = MySqlRowMapper.ReadQuizQuestion(reader);
            review.Add(new ReviewItem(
                number,
                question.QuestionText,
                question.OptionText(option),
                question.OptionText(question.CorrectOption),
                option == question.CorrectOption,
                question.Lesson.Title,
                question.ImageUrl));
        }
        Review = review;

        await using (var command = new MySqlCommand("""
            SELECT COUNT(*), COALESCE(MAX(ROUND(Score * 100.0 / NULLIF(TotalQuestions, 0))), 0)
            FROM quiz_results WHERE UserId = @userId AND Category = @category
            """, connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@category", Result.Category);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            BestPercent = Convert.ToInt32(reader[1]);
        }

        await using (var command = new MySqlCommand("""
            SELECT COUNT(*) + 1
            FROM quiz_results
            WHERE UserId = @userId AND Category = @category
              AND (AttemptedAtUtc < @attemptedAt OR (AttemptedAtUtc = @attemptedAt AND Id < @id))
            """, connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@category", Result.Category);
            command.Parameters.AddWithValue("@attemptedAt", Result.AttemptedAtUtc);
            command.Parameters.AddWithValue("@id", Result.Id);
            AttemptNumber = Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        return Page();
    }
}
