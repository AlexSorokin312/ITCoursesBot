using ITCoursesBot.DB;
using ITCoursesBot.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class DataExtensions
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        IConfiguration config)
    {
        var conn = config.GetConnectionString("Sqlite")
                   ?? throw new InvalidOperationException("…");

        services.AddDbContext<BotDbContext>(opts =>
            opts.UseSqlite(conn)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
        );

        return services;
    }
}