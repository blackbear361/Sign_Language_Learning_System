using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Sign_Language_Learning_System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Pages;

public class LessonsModel : PageModel
{
    private const int PageSize = 10;
    private readonly MySqlConnectionFactory _connectionFactory;

    public LessonsModel(MySqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public IList<Lesson> Lessons { get; private set; } = new List<Lesson>();
    public IList<string> Categories { get; private set; } = new List<string>();
    public HashSet<int> CompletedLessonIds { get; private set; } = new();
    public HashSet<string> QuizCategories { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
    public int TotalLessons { get; private set; }
    public int TotalPages { get; private set; }
    public int TotalPublishedLessons { get; private set; }
    public int CompletedLessons { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task OnGetAsync()
    {
        var userId = RequireUserId();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        await using (var categoriesCommand = new MySqlCommand(
            "SELECT DISTINCT Category FROM lessons WHERE IsPublished = 1 ORDER BY Category", connection))
        await using (var reader = await categoriesCommand.ExecuteReaderAsync())
        {
            var categories = new List<string>();
            while (await reader.ReadAsync())
                categories.Add(reader.GetString(0));
            Categories = categories;
        }

        var filters = new List<string> { "IsPublished = 1" };
        if (!string.IsNullOrWhiteSpace(Search))
            filters.Add("(Title LIKE @search OR Category LIKE @search OR Description LIKE @search)");
        if (!string.IsNullOrWhiteSpace(Category))
            filters.Add("Category = @category");
        var whereClause = $"WHERE {string.Join(" AND ", filters)}";
        var searchValue = $"%{Search?.Trim()}%";

        await using (var countCommand = new MySqlCommand($"SELECT COUNT(*) FROM lessons {whereClause}", connection))
        {
            AddFilterParameters(countCommand, searchValue);
            TotalLessons = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        }

        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalLessons / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        await using (var lessonsCommand = new MySqlCommand($"""
            SELECT Id, Title, Category, Description, VideoUrl, IsPublished, CreatedAtUtc
            FROM lessons
            {whereClause}
            ORDER BY Category, Title
            LIMIT @pageSize OFFSET @offset
            """, connection))
        {
            AddFilterParameters(lessonsCommand, searchValue);
            lessonsCommand.Parameters.AddWithValue("@pageSize", PageSize);
            lessonsCommand.Parameters.AddWithValue("@offset", (PageNumber - 1) * PageSize);
            await using var reader = await lessonsCommand.ExecuteReaderAsync();
            var lessons = new List<Lesson>();
            while (await reader.ReadAsync())
                lessons.Add(MySqlRowMapper.ReadLesson(reader));
            Lessons = lessons;
        }

        await using (var command = new MySqlCommand(
            "SELECT LessonId FROM lesson_progress WHERE UserId = @userId", connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) CompletedLessonIds.Add(reader.GetInt32(0));
        }

        await using (var progressCommand = new MySqlCommand("""
            SELECT
                (SELECT COUNT(*) FROM lessons WHERE IsPublished = 1),
                (SELECT COUNT(*)
                 FROM lesson_progress p
                 INNER JOIN lessons l ON l.Id = p.LessonId
                 WHERE p.UserId = @userId AND l.IsPublished = 1)
            """, connection))
        {
            progressCommand.Parameters.AddWithValue("@userId", userId);
            await using var reader = await progressCommand.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                TotalPublishedLessons = reader.GetInt32(0);
                CompletedLessons = reader.GetInt32(1);
            }
        }

        await using (var command = new MySqlCommand("""
            SELECT DISTINCT l.Category
            FROM quiz_questions q
            INNER JOIN lessons l ON l.Id = q.LessonId
            WHERE l.IsPublished = 1 AND (q.RequiresVideo = 0 OR q.VideoUrl IS NOT NULL)
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) QuizCategories.Add(reader.GetString(0));
    }

    private void AddFilterParameters(MySqlCommand command, string searchValue)
    {
        if (!string.IsNullOrWhiteSpace(Search))
            command.Parameters.AddWithValue("@search", searchValue);
        if (!string.IsNullOrWhiteSpace(Category))
            command.Parameters.AddWithValue("@category", Category.Trim());
    }

    public async Task<IActionResult> OnPostToggleCompleteAsync(int lessonId)
    {
        var userId = RequireUserId();
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var lessonCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM lessons WHERE Id = @lessonId AND IsPublished = 1", connection, transaction))
        {
            lessonCommand.Parameters.AddWithValue("@lessonId", lessonId);
            if (Convert.ToInt32(await lessonCommand.ExecuteScalarAsync()) == 0) return NotFound();
        }

        await using (var progressCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM lesson_progress WHERE UserId = @userId AND LessonId = @lessonId", connection, transaction))
        {
            progressCommand.Parameters.AddWithValue("@userId", userId);
            progressCommand.Parameters.AddWithValue("@lessonId", lessonId);
            var completed = Convert.ToInt32(await progressCommand.ExecuteScalarAsync()) > 0;
            progressCommand.CommandText = completed
                ? "DELETE FROM lesson_progress WHERE UserId = @userId AND LessonId = @lessonId"
                : "INSERT INTO lesson_progress (UserId, LessonId, CompletedAtUtc) VALUES (@userId, @lessonId, UTC_TIMESTAMP())";
            await progressCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return RedirectToPage(new { Search, Category, PageNumber });
    }

    private int RequireUserId() => HttpContext.Session.GetInt32("UserId")
        ?? throw new InvalidOperationException("The session is missing the signed-in user.");
}
