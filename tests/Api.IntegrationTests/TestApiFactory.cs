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
                options.UseSqlite(_connectionKeeper.ConnectionString));

            var notificationSenderDescriptor = services.Single(d => d.ServiceType == typeof(INotificationSender));
            services.Remove(notificationSenderDescriptor);
            services.AddSingleton<INotificationSender, CapturingNotificationSender>();

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }
}

// Each DbContext scope opens its own SqliteConnection instance (via ConnectionString) rather than
// sharing one SqliteConnection object across threads. Microsoft.Data.Sqlite.SqliteConnection is not
// safe for concurrent command execution from multiple threads on the same connection instance, which
// caused spurious ArgumentOutOfRangeExceptions under concurrent requests (see ConcurrencyOrderTests).
// SQLite's "mode=memory&cache=shared" keeps all connections pointed at the same named in-memory
// database, and SQLite's own locking (not a shared C# object) serializes concurrent writes - which is
// what the concurrency tests are actually exercising. The keep-alive connection here just prevents the
// in-memory database from being dropped once the last per-request connection closes.
public class SqliteConnectionKeeper : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _keepAliveConnection;

    public string ConnectionString { get; }

    public SqliteConnectionKeeper()
    {
        ConnectionString = $"DataSource=file:{Guid.NewGuid()}?mode=memory&cache=shared";
        _keepAliveConnection = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
        _keepAliveConnection.Open();
    }

    public void Dispose() => _keepAliveConnection.Dispose();
}
