using System.Data;
using Sign_Language_Learning_System.Models;

namespace Sign_Language_Learning_System.Data;

internal static class MySqlRowMapper
{
    public static Lesson ReadLesson(IDataRecord row) => new()
    {
        Id = Convert.ToInt32(row["Id"]),
        Title = Convert.ToString(row["Title"]) ?? string.Empty,
        Category = Convert.ToString(row["Category"]) ?? string.Empty,
        Description = Convert.ToString(row["Description"]) ?? string.Empty,
        VideoUrl = row["VideoUrl"] is DBNull ? null : Convert.ToString(row["VideoUrl"]),
        IsPublished = Convert.ToBoolean(row["IsPublished"]),
        CreatedAtUtc = Convert.ToDateTime(row["CreatedAtUtc"])
    };

    public static QuizQuestion ReadQuizQuestion(IDataRecord row) => new()
    {
        Id = Convert.ToInt32(row["Id"]),
        LessonId = Convert.ToInt32(row["LessonId"]),
        QuestionText = Convert.ToString(row["QuestionText"]) ?? string.Empty,
        ImageUrl = row["ImageUrl"] is DBNull ? null : Convert.ToString(row["ImageUrl"]),
        ImageCredit = row["ImageCredit"] is DBNull ? null : Convert.ToString(row["ImageCredit"]),
        OptionA = Convert.ToString(row["OptionA"]) ?? string.Empty,
        OptionB = Convert.ToString(row["OptionB"]) ?? string.Empty,
        OptionC = Convert.ToString(row["OptionC"]) ?? string.Empty,
        OptionD = Convert.ToString(row["OptionD"]) ?? string.Empty,
        CorrectOption = Convert.ToString(row["CorrectOption"]) ?? string.Empty,
        VideoUrl = row["VideoUrl"] is DBNull ? null : Convert.ToString(row["VideoUrl"]),
        RequiresVideo = Convert.ToBoolean(row["RequiresVideo"]),
        CreatedAtUtc = Convert.ToDateTime(row["CreatedAtUtc"]),
        Lesson = new Lesson
        {
            Id = Convert.ToInt32(row["JoinedLessonId"]),
            Title = Convert.ToString(row["LessonTitle"]) ?? string.Empty,
            Category = Convert.ToString(row["LessonCategory"]) ?? string.Empty,
            Description = Convert.ToString(row["LessonDescription"]) ?? string.Empty,
            VideoUrl = row["LessonVideoUrl"] is DBNull ? null : Convert.ToString(row["LessonVideoUrl"]),
            IsPublished = Convert.ToBoolean(row["LessonIsPublished"]),
            CreatedAtUtc = Convert.ToDateTime(row["LessonCreatedAtUtc"])
        }
    };

    public static QuizResult ReadQuizResult(IDataRecord row) => new()
    {
        Id = Convert.ToInt32(row["Id"]),
        UserId = Convert.ToInt32(row["UserId"]),
        Category = Convert.ToString(row["Category"]) ?? string.Empty,
        Score = Convert.ToInt32(row["Score"]),
        TotalQuestions = Convert.ToInt32(row["TotalQuestions"]),
        AnswerSummary = Convert.ToString(row["AnswerSummary"]) ?? string.Empty,
        AttemptedAtUtc = Convert.ToDateTime(row["AttemptedAtUtc"])
    };

    public static ApplicationUser ReadApplicationUser(IDataRecord row) => new()
    {
        Id = Convert.ToInt32(row["Id"]),
        FullName = Convert.ToString(row["FullName"]) ?? string.Empty,
        Email = Convert.ToString(row["Email"]) ?? string.Empty,
        Role = Convert.ToString(row["Role"]) ?? "User",
        Status = Convert.ToString(row["Status"]) ?? "Active"
    };

    public const string QuizQuestionWithLessonColumns = """
        q.Id, q.LessonId, q.QuestionText, q.ImageUrl, q.ImageCredit,
        q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.CorrectOption,
        q.VideoUrl, q.RequiresVideo, q.CreatedAtUtc,
        l.Id AS JoinedLessonId, l.Title AS LessonTitle, l.Category AS LessonCategory,
        l.Description AS LessonDescription, l.VideoUrl AS LessonVideoUrl,
        l.IsPublished AS LessonIsPublished, l.CreatedAtUtc AS LessonCreatedAtUtc
        """;
}
