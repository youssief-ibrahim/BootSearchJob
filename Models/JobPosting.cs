
namespace DotNetJobHunter.Models;

public class JobPosting
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTime? PublishedAt { get; set; }

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
}
