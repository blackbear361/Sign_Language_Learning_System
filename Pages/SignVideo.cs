using System.Text.RegularExpressions;

namespace Sign_Language_Learning_System.Pages;

public static class SignVideo
{
    public static string? EmbedUrl(string? url)
    {
        var id = YouTubeId(url);
        return id is null ? null : $"https://www.youtube-nocookie.com/embed/{id}";
    }

    public static bool IsUploadedClip(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith("/uploads/quiz/", StringComparison.Ordinal);

    public static string? YouTubeId(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (!uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("www.youtube.com", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)
            && !uri.Host.Equals("www.youtu.be", StringComparison.OrdinalIgnoreCase))
            return null;

        var id = uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)
            ? uri.AbsolutePath.Trim('/')
            : Regex.Match(uri.Query, @"(?:\?|&)v=([A-Za-z0-9_-]{11})(?:&|$)").Groups[1].Value;
        return Regex.IsMatch(id, @"^[A-Za-z0-9_-]{11}$") ? id : null;
    }
}
