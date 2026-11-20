namespace Sign_Language_Learning_System.Models;

public sealed class QuizQuestion
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageCredit { get; set; }
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string CorrectOption { get; set; } = string.Empty;
    public string? VideoUrl { get; set; }
    public bool RequiresVideo { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Lesson Lesson { get; set; } = new();

    public bool IsReady => !RequiresVideo || !string.IsNullOrWhiteSpace(VideoUrl);

    public string? OptionText(string? option) => option switch
    {
        "A" => OptionA,
        "B" => OptionB,
        "C" => OptionC,
        "D" => OptionD,
        _ => null
    };
}
