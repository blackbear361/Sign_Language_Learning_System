using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MySqlConnector;
using Sign_Language_Learning_System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Pages;

public class AdminQuizzesModel : PageModel
{
    private static readonly string[] AllowedImageTypes = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedVideoTypes = { ".mp4", ".webm" };
    private const long MaxVideoBytes = 25 * 1024 * 1024;

    private readonly MySqlConnectionFactory _connectionFactory;
    private readonly IWebHostEnvironment _env;

    public AdminQuizzesModel(MySqlConnectionFactory connectionFactory, IWebHostEnvironment env)
    {
        _connectionFactory = connectionFactory;
        _env = env;
    }

    public IList<QuizQuestion> Questions { get; private set; } = new List<QuizQuestion>();
    public IList<SelectListItem> LessonOptions { get; private set; } = new List<SelectListItem>();
    public IList<string> Topics { get; private set; } = new List<string>();
    public int TotalQuestions { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Topic { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty] public QuestionInput Input { get; set; } = new();
    [BindProperty] public IFormFile? ImageFile { get; set; }
    [BindProperty] public bool RemoveImage { get; set; }
    [BindProperty] public IFormFile? VideoFile { get; set; }
    [BindProperty] public bool RemoveVideo { get; set; }
    [BindProperty(SupportsGet = true)] public bool MissingVideo { get; set; }
    public int MissingVideoCount { get; private set; }
    [TempData] public string StatusMessage { get; set; } = string.Empty;

    public class QuestionInput
    {
        public int Id { get; set; }
        [Display(Name = "Lesson"), Range(1, int.MaxValue, ErrorMessage = "Choose the lesson this question belongs to.")]
        public int LessonId { get; set; }
        [Display(Name = "Question"), Required(ErrorMessage = "Enter the question."), StringLength(300, ErrorMessage = "Keep the question under 300 characters.")]
        public string QuestionText { get; set; } = string.Empty;
        [Display(Name = "Sign image link"), RegularExpression(@"^(https?://\S+|/uploads/quiz/[A-Za-z0-9\-]+\.(jpg|jpeg|png|gif|webp))$", ErrorMessage = "Enter a full link that starts with http:// or https://"), StringLength(500)]
        public string? ImageUrl { get; set; }
        [Display(Name = "Image credit"), StringLength(200, ErrorMessage = "Keep the credit under 200 characters.")]
        public string? ImageCredit { get; set; }
        [Display(Name = "Option A"), Required(ErrorMessage = "Option A is required."), StringLength(120)]
        public string OptionA { get; set; } = string.Empty;
        [Display(Name = "Option B"), Required(ErrorMessage = "Option B is required."), StringLength(120)]
        public string OptionB { get; set; } = string.Empty;
        [Display(Name = "Option C"), Required(ErrorMessage = "Option C is required."), StringLength(120)]
        public string OptionC { get; set; } = string.Empty;
        [Display(Name = "Option D"), Required(ErrorMessage = "Option D is required."), StringLength(120)]
        public string OptionD { get; set; } = string.Empty;
        [Display(Name = "Correct answer"), Required(ErrorMessage = "Tick the correct answer."), RegularExpression("^[ABCD]$", ErrorMessage = "Tick the correct answer.")]
        public string CorrectOption { get; set; } = string.Empty;
        [Display(Name = "YouTube link"), StringLength(500)]
        public string? VideoUrl { get; set; }
        [Display(Name = "Learners need the video to answer")]
        public bool RequiresVideo { get; set; }
    }

    public async Task OnGetAsync(int? editId)
    {
        await LoadAsync();
        if (editId is null) return;

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using var command = new MySqlCommand("""
            SELECT Id, LessonId, QuestionText, ImageUrl, ImageCredit, VideoUrl, RequiresVideo,
                   OptionA, OptionB, OptionC, OptionD, CorrectOption
            FROM quiz_questions WHERE Id = @id
            """, connection);
        command.Parameters.AddWithValue("@id", editId.Value);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return;
        Input = new QuestionInput
        {
            Id = Convert.ToInt32(reader["Id"]),
            LessonId = Convert.ToInt32(reader["LessonId"]),
            QuestionText = Convert.ToString(reader["QuestionText"]) ?? string.Empty,
            ImageUrl = reader["ImageUrl"] is DBNull ? null : Convert.ToString(reader["ImageUrl"]),
            ImageCredit = reader["ImageCredit"] is DBNull ? null : Convert.ToString(reader["ImageCredit"]),
            VideoUrl = reader["VideoUrl"] is DBNull ? null : Convert.ToString(reader["VideoUrl"]),
            RequiresVideo = Convert.ToBoolean(reader["RequiresVideo"]),
            OptionA = Convert.ToString(reader["OptionA"]) ?? string.Empty,
            OptionB = Convert.ToString(reader["OptionB"]) ?? string.Empty,
            OptionC = Convert.ToString(reader["OptionC"]) ?? string.Empty,
            OptionD = Convert.ToString(reader["OptionD"]) ?? string.Empty,
            CorrectOption = Convert.ToString(reader["CorrectOption"]) ?? string.Empty
        };
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var options = new[] { Input.OptionA, Input.OptionB, Input.OptionC, Input.OptionD }
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim().ToLowerInvariant())
            .ToList();
        if (options.Count == 4 && options.Distinct().Count() < 4)
            ModelState.AddModelError(string.Empty, "Each answer option must be different.");

        ValidateUpload(ImageFile, AllowedImageTypes, "image/", MaxImageBytes, nameof(ImageFile), "Upload a JPG, PNG, GIF or WEBP image.", "Keep the image under 5 MB.");
        ValidateUpload(VideoFile, AllowedVideoTypes, "video/", MaxVideoBytes, nameof(VideoFile), "Upload an MP4 or WEBM video.", "Keep the video under 25 MB.");

        if (!string.IsNullOrWhiteSpace(Input.VideoUrl)
            && SignVideo.YouTubeId(Input.VideoUrl) is null
            && !SignVideo.IsUploadedClip(Input.VideoUrl.Trim()))
            ModelState.AddModelError("Input.VideoUrl", "Paste a YouTube link, e.g. https://www.youtube.com/watch?v=… or https://youtu.be/…");

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        await using (var lessonCommand = new MySqlCommand("SELECT COUNT(*) FROM lessons WHERE Id = @id", connection))
        {
            lessonCommand.Parameters.AddWithValue("@id", Input.LessonId);
            if (Input.LessonId <= 0 || Convert.ToInt32(await lessonCommand.ExecuteScalarAsync()) == 0)
                ModelState.AddModelError("Input.LessonId", "Choose an existing lesson.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        string? oldImage = null;
        string? oldVideo = null;
        if (Input.Id != 0)
        {
            await using var existing = new MySqlCommand(
                "SELECT ImageUrl, VideoUrl FROM quiz_questions WHERE Id = @id", connection);
            existing.Parameters.AddWithValue("@id", Input.Id);
            await using var reader = await existing.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return NotFound();
            oldImage = reader["ImageUrl"] is DBNull ? null : Convert.ToString(reader["ImageUrl"]);
            oldVideo = reader["VideoUrl"] is DBNull ? null : Convert.ToString(reader["VideoUrl"]);
        }

        var image = RemoveImage ? null : string.IsNullOrWhiteSpace(Input.ImageUrl) ? null : Input.ImageUrl.Trim();
        var video = RemoveVideo ? null : string.IsNullOrWhiteSpace(Input.VideoUrl) ? null : Input.VideoUrl.Trim();
        if (ImageFile is { Length: > 0 }) image = await SaveUploadAsync(ImageFile);
        if (VideoFile is { Length: > 0 }) video = await SaveUploadAsync(VideoFile);

        var sql = Input.Id == 0
            ? """
              INSERT INTO quiz_questions
                  (LessonId, QuestionText, ImageUrl, ImageCredit, OptionA, OptionB, OptionC, OptionD,
                   CorrectOption, VideoUrl, RequiresVideo, CreatedAtUtc)
              VALUES
                  (@lessonId, @questionText, @imageUrl, @imageCredit, @optionA, @optionB, @optionC, @optionD,
                   @correctOption, @videoUrl, @requiresVideo, UTC_TIMESTAMP())
              """
            : """
              UPDATE quiz_questions SET
                  LessonId = @lessonId, QuestionText = @questionText, ImageUrl = @imageUrl,
                  ImageCredit = @imageCredit, OptionA = @optionA, OptionB = @optionB,
                  OptionC = @optionC, OptionD = @optionD, CorrectOption = @correctOption,
                  VideoUrl = @videoUrl, RequiresVideo = @requiresVideo
              WHERE Id = @id
              """;
        await using (var command = new MySqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("@lessonId", Input.LessonId);
            command.Parameters.AddWithValue("@questionText", Input.QuestionText.Trim());
            command.Parameters.AddWithValue("@imageUrl", (object?)image ?? DBNull.Value);
            command.Parameters.AddWithValue("@imageCredit", image is null || string.IsNullOrWhiteSpace(Input.ImageCredit) ? DBNull.Value : Input.ImageCredit.Trim());
            command.Parameters.AddWithValue("@optionA", Input.OptionA.Trim());
            command.Parameters.AddWithValue("@optionB", Input.OptionB.Trim());
            command.Parameters.AddWithValue("@optionC", Input.OptionC.Trim());
            command.Parameters.AddWithValue("@optionD", Input.OptionD.Trim());
            command.Parameters.AddWithValue("@correctOption", Input.CorrectOption);
            command.Parameters.AddWithValue("@videoUrl", (object?)video ?? DBNull.Value);
            command.Parameters.AddWithValue("@requiresVideo", Input.RequiresVideo);
            if (Input.Id != 0) command.Parameters.AddWithValue("@id", Input.Id);
            await command.ExecuteNonQueryAsync();
        }

        if (oldImage != image) DeleteUploadedFile(oldImage);
        if (oldVideo != video) DeleteUploadedFile(oldVideo);
        StatusMessage = $"Question {(Input.Id == 0 ? "added" : "updated")}."
            + (Input.RequiresVideo && video is null ? " Learners won't see it until you add its video." : "");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();
        string? image;
        string? video;
        await using (var read = new MySqlCommand(
            "SELECT ImageUrl, VideoUrl FROM quiz_questions WHERE Id = @id", connection))
        {
            read.Parameters.AddWithValue("@id", id);
            await using var reader = await read.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return RedirectToPage();
            image = reader["ImageUrl"] is DBNull ? null : Convert.ToString(reader["ImageUrl"]);
            video = reader["VideoUrl"] is DBNull ? null : Convert.ToString(reader["VideoUrl"]);
        }

        await using (var command = new MySqlCommand("DELETE FROM quiz_questions WHERE Id = @id", connection))
        {
            command.Parameters.AddWithValue("@id", id);
            await command.ExecuteNonQueryAsync();
        }
        DeleteUploadedFile(image);
        DeleteUploadedFile(video);
        StatusMessage = "Question deleted. Learners' past results are kept.";
        return RedirectToPage();
    }

    private void ValidateUpload(IFormFile? file, string[] extensions, string contentTypePrefix, long maxBytes, string field, string typeError, string sizeError)
    {
        if (file is not { Length: > 0 }) return;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!extensions.Contains(extension) || !file.ContentType.StartsWith(contentTypePrefix, StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(field, typeError);
        else if (file.Length > maxBytes)
            ModelState.AddModelError(field, sizeError);
    }

    private async Task<string> SaveUploadAsync(IFormFile file)
    {
        var folder = Path.Combine(_env.WebRootPath, "uploads", "quiz");
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        await using var stream = System.IO.File.Create(Path.Combine(folder, fileName));
        await file.CopyToAsync(stream);
        return $"/uploads/quiz/{fileName}";
    }

    private void DeleteUploadedFile(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/quiz/", StringComparison.Ordinal)) return;
        var path = Path.Combine(_env.WebRootPath, "uploads", "quiz", Path.GetFileName(url));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private async Task LoadAsync()
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync();

        var lessons = new List<Lesson>();
        await using (var command = new MySqlCommand("""
            SELECT Id, Title, Category, Description, VideoUrl, IsPublished, CreatedAtUtc
            FROM lessons ORDER BY Category, Title
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) lessons.Add(MySqlRowMapper.ReadLesson(reader));

        var groups = new Dictionary<string, SelectListGroup>(StringComparer.OrdinalIgnoreCase);
        LessonOptions = lessons.Select(lesson =>
        {
            if (!groups.TryGetValue(lesson.Category, out var group))
                groups[lesson.Category] = group = new SelectListGroup { Name = lesson.Category };
            return new SelectListItem(lesson.IsPublished ? lesson.Title : $"{lesson.Title} (draft)", lesson.Id.ToString()) { Group = group };
        }).ToList();
        Topics = groups.Keys.OrderBy(name => name).ToList();

        await using (var count = new MySqlCommand(
            "SELECT COUNT(*), SUM(CASE WHEN RequiresVideo = 1 AND VideoUrl IS NULL THEN 1 ELSE 0 END) FROM quiz_questions", connection))
        await using (var reader = await count.ExecuteReaderAsync())
        {
            await reader.ReadAsync();
            TotalQuestions = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
            MissingVideoCount = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
        }

        var filters = new List<string>();
        await using var listCommand = new MySqlCommand();
        listCommand.Connection = connection;
        if (MissingVideo) filters.Add("q.RequiresVideo = 1 AND q.VideoUrl IS NULL");
        if (!string.IsNullOrWhiteSpace(Topic))
        {
            filters.Add("l.Category = @topic");
            listCommand.Parameters.AddWithValue("@topic", Topic.Trim());
        }
        if (!string.IsNullOrWhiteSpace(Search))
        {
            filters.Add("""
                (q.QuestionText LIKE @search OR q.OptionA LIKE @search OR q.OptionB LIKE @search
                 OR q.OptionC LIKE @search OR q.OptionD LIKE @search OR l.Title LIKE @search
                 OR q.ImageCredit LIKE @search)
                """);
            listCommand.Parameters.AddWithValue("@search", $"%{Search.Trim()}%");
        }
        var where = filters.Count == 0 ? "" : $"WHERE {string.Join(" AND ", filters)}";
        listCommand.CommandText = $"""
            SELECT {MySqlRowMapper.QuizQuestionWithLessonColumns}
            FROM quiz_questions q INNER JOIN lessons l ON l.Id = q.LessonId
            {where}
            ORDER BY l.Category, l.Title, q.Id
            """;
        var questions = new List<QuizQuestion>();
        await using (var reader = await listCommand.ExecuteReaderAsync())
            while (await reader.ReadAsync()) questions.Add(MySqlRowMapper.ReadQuizQuestion(reader));
        Questions = questions;
    }
}
