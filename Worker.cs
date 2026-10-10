
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
    private readonly bool _runOnce;

    public Worker(
        RssJobSource jobSource,
        TelegramService telegram,
        IServiceScopeFactory scopeFactory,
        ILogger<Worker> logger,
        IConfiguration configuration)
    {
        _jobSource = jobSource;
        _telegram = telegram;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _runOnce = configuration.GetValue<bool>("JobHunter:RunOnce");
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("DotNet Job Hunter started.");

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
                _logger.LogError(ex, "Failed to check jobs.");

                if (_runOnce)
                    throw;
            }

            if (_runOnce || stoppingToken.IsCancellationRequested)
                break;

            await Task.Delay(
                TimeSpan.FromMinutes(5),
                stoppingToken);

        } while (!stoppingToken.IsCancellationRequested);

        _logger.LogInformation("DotNet Job Hunter finished.");
    }

    private async Task CheckJobsAsync(
        CancellationToken cancellationToken)
    {
        var jobs = await _jobSource.GetJobsAsync(cancellationToken);

        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<JobDbContext>();

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var exists = await db.Jobs
                .AnyAsync(j => j.Url == job.Url, cancellationToken);

            if (exists)
                continue;

            db.Jobs.Add(job);
            await db.SaveChangesAsync(cancellationToken);

            var message =
                $"🆕 New .NET Job Found!\n\n" +
                $"💼 {job.Title}\n" +
                $"🌐 Source: {job.Source}\n" +
                $"📅 Published: {job.PublishedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Unknown"}\n\n" +
                $"🔗 Apply:\n{job.Url}";

            try
            {
                await _telegram.SendMessageAsync(
                    message, cancellationToken);

                _logger.LogInformation(
                    "Notification sent for {Title}", job.Title);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Failed to notify about {Title}", job.Title);
            }
        }

        _logger.LogInformation(
            "Job check completed. Results: {Count}", jobs.Count);
    }
}
