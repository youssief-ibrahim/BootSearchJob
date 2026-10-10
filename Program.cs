
using DotNetJobHunter.Services;
using DotNetJobHunter;
using DotNetJobHunter.Data;
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

await host.RunAsync();
