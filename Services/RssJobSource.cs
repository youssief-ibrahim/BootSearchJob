
using System.Xml.Linq;
using DotNetJobHunter.Models;

namespace DotNetJobHunter.Services;

public class RssJobSource
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RssJobSource> _logger;

    private static readonly TimeSpan MaxJobAge =
        TimeSpan.FromHours(24);

    public RssJobSource(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RssJobSource> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<JobPosting>> GetJobsAsync(
        CancellationToken cancellationToken)
    {
        var feeds = _configuration
            .GetSection("JobSources:GoogleNewsFeeds")
            .Get<Dictionary<string, string>>()
            ?? new Dictionary<string, string>();

        if (feeds.Count == 0)
        {
            _logger.LogWarning("No RSS feeds configured.");
            return [];
        }

        var jobs = new List<JobPosting>();
        var cutoffTime = DateTime.UtcNow - MaxJobAge;

        foreach (var feed in feeds)
        {
            var sourceName = feed.Key;
            var feedUrl = feed.Value;

            if (string.IsNullOrWhiteSpace(feedUrl))
                continue;

            try
            {
                _logger.LogInformation(
                    "Checking RSS source: {Source}",
                    sourceName);

                using var response = await _httpClient.GetAsync(
                    feedUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                await using var stream =
                    await response.Content.ReadAsStreamAsync(
                        cancellationToken);

                var document = await XDocument.LoadAsync(
                    stream,
                    LoadOptions.None,
                    cancellationToken);

                var channel = document.Root?
                    .Elements()
                    .FirstOrDefault(e =>
                        e.Name.LocalName == "channel");

                if (channel is null)
                {
                    _logger.LogWarning(
                        "RSS channel not found for {Source}",
                        sourceName);
                    continue;
                }

                var items = channel.Elements()
                    .Where(e => e.Name.LocalName == "item");

                var sourceJobsCount = 0;

                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var title = GetElementValue(item, "title");
                    var link = GetElementValue(item, "link");
                    var pubDate = GetElementValue(item, "pubDate");

                    if (string.IsNullOrWhiteSpace(title) ||
                        string.IsNullOrWhiteSpace(link))
                        continue;

                    
                    if (!DateTimeOffset.TryParse(
                        pubDate, out var parsedDate))
                        continue;

                    var publishedAt = parsedDate.UtcDateTime;

                    if (publishedAt < cutoffTime ||
                        publishedAt > DateTime.UtcNow.AddMinutes(5))
                        continue;

                    bool isLinkedInPost =
                        sourceName.Equals(
                            "LinkedInPosts",
                            StringComparison.OrdinalIgnoreCase);

                    bool isRelevant = isLinkedInPost
                        ? IsRelevantLinkedInPost(title)
                        : IsRelevantJob(title);

                    if (!isRelevant)
                        continue;

                    jobs.Add(new JobPosting
                    {
                        Title = title,
                        Url = link,
                        Source = sourceName,
                        PublishedAt = publishedAt
                    });

                    sourceJobsCount++;
                }

                _logger.LogInformation(
                    "Source {Source}: {Count} matching recent results.",
                    sourceName,
                    sourceJobsCount);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to retrieve RSS source {Source}.",
                    sourceName);
            }
        }

        // ãäÚ ÊßÑÇÑ ÇáÑÇÈØ äÝÓå.
        var uniqueJobs = jobs
            .GroupBy(
                job => job.Url.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        _logger.LogInformation(
            "Total unique recent results: {Count}",
            uniqueJobs.Count);

        return uniqueJobs;
    }

    private static string? GetElementValue(
        XElement parent,
        string elementName)
    {
        return parent.Elements()
            .FirstOrDefault(e =>
                e.Name.LocalName == elementName)
            ?.Value
            .Trim();
    }

    private static bool IsRelevantLinkedInPost(string title)
    {
        var text = title.ToLowerInvariant();

        bool hasBackend =
            text.Contains("backend") ||
            text.Contains("back-end") ||
            text.Contains("back end");

        bool hasDotNet =
            text.Contains(".net") ||
            text.Contains("asp.net") ||
            text.Contains("c#") ||
            text.Contains("dotnet");

        bool hasDeveloper =
            text.Contains("developer") ||
            text.Contains("development");

        bool hasHiring =
            text.Contains("hiring") ||
            text.Contains("hire") ||
            text.Contains("vacancy") ||
            text.Contains("vacancies") ||
            text.Contains("job opening") ||
            text.Contains("open position");

        return hasBackend &&
               hasDotNet &&
               hasDeveloper &&
               hasHiring;
    }

    private static bool IsRelevantJob(string title)
    {
        var text = title.ToLowerInvariant();

        bool hasDotNet =
            text.Contains(".net") ||
            text.Contains("asp.net") ||
            text.Contains("c#") ||
            text.Contains("dotnet");

        if (!hasDotNet)
            return false;

        string[] seniorKeywords =
        [
            "senior",
            "sr.",
            "lead developer",
            "tech lead",
            "team lead",
            "principal engineer",
            "staff engineer",
            "architect",
            "engineering manager",
            "director of engineering"
        ];

        if (seniorKeywords.Any(text.Contains))
            return false;

        string[] unwantedKeywords =
        [
            "tutorial",
            "course",
            "certification",
            "webinar",
            "podcast",
            "top 10",
            "top 5",
            "essential skills",
            "getting started",
            "what is ",
            "how to ",
            "release notes",
            "product launch"
        ];

        if (unwantedKeywords.Any(text.Contains))
            return false;

        string[] jobKeywords =
        [
            "developer",
            "backend",
            "back-end",
            "back end",
            "software engineer",
            "software developer",
            "programmer",
            "engineer",
            "job",
            "hiring",
            "vacancy",
            "vacancies",
            "career",
            "careers"
        ];

        return jobKeywords.Any(text.Contains);
    }
}
