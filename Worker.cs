
using DotNetJobHunter.Data;
using DotNetJobHunter.Models;
using DotNetJobHunter.Services;
using Microsoft.EntityFrameworkCore;

namespace DotNetJobHunter;

public class Worker : BackgroundService
{
    private readonly RssJobSource _jobSource;
    private readonly TelegramService _telegram;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly bool _runOnce;

    public Worker(
        RssJobSource jobSource,
        TelegramService telegram,
        IServiceScopeFactory scopeFactory,
        ILogger<Worker> logger,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime)
    {
        _jobSource = jobSource;
        _telegram = telegram;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _applicationLifetime = applicationLifetime;

        _runOnce = configuration.GetValue<bool>(
            "JobHunter:RunOnce");
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("DotNet Job Hunter started.");

        try
        {
            do
            {
                try
                {
                    await CheckJobsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to check jobs.");

                    if (_runOnce)
                        break;
                }

                if (_runOnce || stoppingToken.IsCancellationRequested)
                    break;

                await Task.Delay(
                    TimeSpan.FromMinutes(10),
                    stoppingToken);

            } while (!stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
        finally
        {
            _logger.LogInformation(
                "DotNet Job Hunter finished.");

            if (_runOnce)
                _applicationLifetime.StopApplication();
        }
    }

    private async Task CheckJobsAsync(
        CancellationToken cancellationToken)
    {
        var jobs = await _jobSource.GetJobsAsync(
            cancellationToken);

        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<JobDbContext>();

        var newJobsCount = 0;

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedUrl = job.Url.Trim().TrimEnd('/');

            // منع تكرار الرابط حتى لو اختلفت حالة الأحرف
            // أو كان الرابط ينتهي بشرطة /.
            var exists = await db.Jobs.AnyAsync(
                j => j.Url == normalizedUrl ||
                     j.Url == job.Url,
                cancellationToken);

            if (exists)
            {
                _logger.LogInformation(
                    "Duplicate job skipped: {Title}",
                    job.Title);

                continue;
            }

            job.Url = normalizedUrl;

            // نسجل الوظيفة أولاً حتى لا تتكرر
            // في التشغيل التالي إذا فشل إرسال Telegram.
            db.Jobs.Add(job);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not save job: {Title}",
                    job.Title);

                db.Entry(job).State = EntityState.Detached;
                continue;
            }

            var message =
                $"🆕 New .NET Job Found!\n\n" +
                $"💼 {job.Title}\n" +
                $"🌐 Source: {job.Source}\n" +
                $"📅 Published: {job.PublishedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown"}\n\n" +
                $"🔗 Apply:\n{job.Url}";

            try
            {
                await _telegram.SendMessageAsync(
                    message,
                    cancellationToken);

                newJobsCount++;

                _logger.LogInformation(
                    "Notification sent for {Title}",
                    job.Title);
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
                    "Failed to notify about {Title}",
                    job.Title);
            }
        }

        _logger.LogInformation(
            "Job check completed. Found {Total} results; sent {New} notifications.",
            jobs.Count,
            newJobsCount);
    }
}
