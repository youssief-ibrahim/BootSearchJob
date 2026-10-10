
using DotNetJobHunter.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DotNetJobHunter;

public class JobDbContextFactory
    : IDesignTimeDbContextFactory<JobDbContext>
{
    public JobDbContext CreateDbContext(string[] args)
    {
        var basePath = AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile(
                "appsettings.json",
                optional: true,
                reloadOnChange: false)
            .AddJsonFile(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "appsettings.json"),
                optional: true,
                reloadOnChange: false)
            .Build();

        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DefaultConnection was not found in appsettings.json.");
        }

        var options = new DbContextOptionsBuilder<JobDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new JobDbContext(options);
    }
}
