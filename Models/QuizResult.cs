namespace Sign_Language_Learning_System.Models;

public sealed class QuizResult
{
    public const int PassMark = 70;
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Score { get; set; }
    public int TotalQuestions { get; set; }
    public string AnswerSummary { get; set; } = string.Empty;
    public DateTime AttemptedAtUtc { get; set; }
    public int Percent => TotalQuestions == 0 ? 0 : (int)Math.Round(Score * 100.0 / TotalQuestions);
    public bool Passed => Percent >= PassMark;

    public static IReadOnlyList<(int QuestionId, string Option)> ParseAnswers(string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return Array.Empty<(int, string)>();
        return summary.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Split(':', 2))
            .Where(parts => parts.Length == 2
                && int.TryParse(parts[0], out _)
                && parts[1] is "A" or "B" or "C" or "D")
            .Select(parts => (int.Parse(parts[0]), parts[1]))
            .ToArray();
    }
}
