using Application.Notifications;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

public class TestApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnectionKeeper _connectionKeeper = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connectionKeeper.Connection));

            var notificationSenderDescriptor = services.Single(d => d.ServiceType == typeof(INotificationSender));
            services.Remove(notificationSenderDescriptor);
            services.AddSingleton<INotificationSender, CapturingNotificationSender>();

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

public class SqliteConnectionKeeper : IDisposable
{
    public Microsoft.Data.Sqlite.SqliteConnection Connection { get; }

    public SqliteConnectionKeeper()
    {
        Connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        Connection.Open();
    }

    public void Dispose() => Connection.Dispose();
}
