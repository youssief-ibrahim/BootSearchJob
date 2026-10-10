
using DotNetJobHunter;
using DotNetJobHunter.Data;
using DotNetJobHunter.Services;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<JobDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection")));

builder.Services.AddHttpClient<TelegramService>();
builder.Services.AddHttpClient<RssJobSource>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var runOnce = builder.Configuration
    .GetValue<bool>("JobHunter:RunOnce");

if (runOnce)
{
    await host.StartAsync();

    var lifetime = host.Services
        .GetRequiredService<IHostApplicationLifetime>();

    var completion = new TaskCompletionSource();

    lifetime.ApplicationStopped.Register(
        () => completion.TrySetResult());

    await completion.Task;
}
else
{
    await host.RunAsync();
}
