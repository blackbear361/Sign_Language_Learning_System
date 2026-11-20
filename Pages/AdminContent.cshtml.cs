using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;
using Sign_Language_Learning_System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Pages;

public class AdminContentModel : PageModel
{
    private const int PageSize = 10;
    private readonly MySqlConnectionFactory _connectionFactory;
    public AdminContentModel(MySqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public IList<Lesson> Lessons { get; private set; } = new List<Lesson>();
    public IList<string> Categories { get; private set; } = new List<string>();
    public int TotalLessons { get; private set; }
    public int TotalPages { get; private set; }
    public bool ShowForm { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty] public LessonInput Input { get; set; } = new();
    [TempData] public string StatusMessage { get; set; } = string.Empty;

    public class LessonInput
    {
        public int Id { get; set; }
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(120)] public string Title { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)] public string Category { get; set; } = "Basics";
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(4000)] public string Description { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Url, System.ComponentModel.DataAnnotations.StringLength(500)] public string? VideoUrl { get; set; }
        public bool IsPublished { get; set; }
    }

    public async Task OnGetAsync(int? editId)
    {
        await LoadLessonsAsync();
        if (editId is null) return;

        ShowForm = true;
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand("""
            SELECT Id, Title, Category, Description, VideoUrl, IsPublished, CreatedAtUtc
            FROM lessons WHERE Id = @id
            """, connection);
        command.Parameters.AddWithValue("@id", editId.Value);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return;
        var lesson = MySqlRowMapper.ReadLesson(reader);
        Input = new LessonInput
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Category = lesson.Category,
            Description = lesson.Description,
            VideoUrl = lesson.VideoUrl,
            IsPublished = lesson.IsPublished
        };
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadLessonsAsync();
            ShowForm = true;
            return Page();
        }

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        if (Input.Id != 0)
        {
            await using var exists = new MySqlCommand("SELECT COUNT(*) FROM lessons WHERE Id = @id", connection);
            exists.Parameters.AddWithValue("@id", Input.Id);
            if (Convert.ToInt32(await exists.ExecuteScalarAsync()) == 0) return NotFound();
        }
        var sql = Input.Id == 0
            ? """
              INSERT INTO lessons (Title, Category, Description, VideoUrl, IsPublished, CreatedAtUtc)
              VALUES (@title, @category, @description, @videoUrl, @isPublished, UTC_TIMESTAMP())
              """
            : """
              UPDATE lessons
              SET Title = @title, Category = @category, Description = @description,
                  VideoUrl = @videoUrl, IsPublished = @isPublished
              WHERE Id = @id
              """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@title", Input.Title.Trim());
        command.Parameters.AddWithValue("@category", Input.Category.Trim());
        command.Parameters.AddWithValue("@description", Input.Description.Trim());
        command.Parameters.AddWithValue("@videoUrl", string.IsNullOrWhiteSpace(Input.VideoUrl) ? DBNull.Value : Input.VideoUrl.Trim());
        command.Parameters.AddWithValue("@isPublished", Input.IsPublished);
        if (Input.Id != 0) command.Parameters.AddWithValue("@id", Input.Id);
        await command.ExecuteNonQueryAsync();

        StatusMessage = "Lesson saved.";
        return RedirectToPage(new { Search, Category, PageNumber });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand("DELETE FROM lessons WHERE Id = @id", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
        StatusMessage = "Lesson deleted.";
        return RedirectToPage(new { Search, Category, PageNumber });
    }

    private async Task LoadLessonsAsync()
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        await using (var categoriesCommand = new MySqlCommand(
            "SELECT DISTINCT Category FROM lessons ORDER BY Category", connection))
        await using (var reader = await categoriesCommand.ExecuteReaderAsync())
        {
            var categories = new List<string>();
            while (await reader.ReadAsync())
                categories.Add(Convert.ToString(reader["Category"]) ?? string.Empty);
            Categories = categories;
        }

        var filters = new List<string>();
        await using var countCommand = new MySqlCommand();
        countCommand.Connection = connection;

        if (!string.IsNullOrWhiteSpace(Search))
        {
            filters.Add("(Title LIKE @search OR Category LIKE @search OR Description LIKE @search)");
            countCommand.Parameters.AddWithValue("@search", $"%{Search.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(Category))
        {
            filters.Add("Category = @category");
            countCommand.Parameters.AddWithValue("@category", Category.Trim());
        }

        var whereClause = filters.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", filters)}";
        countCommand.CommandText = $"SELECT COUNT(*) FROM lessons {whereClause}";
        TotalLessons = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalLessons / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        await using var lessonsCommand = new MySqlCommand($"""
            SELECT Id, Title, Category, Description, VideoUrl, IsPublished, CreatedAtUtc
            FROM lessons
            {whereClause}
            ORDER BY CreatedAtUtc DESC, Id DESC
            LIMIT @pageSize OFFSET @offset
            """, connection);

        if (!string.IsNullOrWhiteSpace(Search))
            lessonsCommand.Parameters.AddWithValue("@search", $"%{Search.Trim()}%");
        if (!string.IsNullOrWhiteSpace(Category))
            lessonsCommand.Parameters.AddWithValue("@category", Category.Trim());
        lessonsCommand.Parameters.AddWithValue("@pageSize", PageSize);
        lessonsCommand.Parameters.AddWithValue("@offset", (PageNumber - 1) * PageSize);

        await using var lessonsReader = await lessonsCommand.ExecuteReaderAsync();
        var lessons = new List<Lesson>();
        while (await lessonsReader.ReadAsync())
            lessons.Add(MySqlRowMapper.ReadLesson(lessonsReader));
        Lessons = lessons;
    }
}
