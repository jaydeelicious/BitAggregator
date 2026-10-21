using BitAggregator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BitAggregator.Tests.Infrastructure;

public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public BitAggregatorDbContext Context { get; }

    private SqliteTestDatabase(SqliteConnection connection , BitAggregatorDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<BitAggregatorDbContext>().UseSqlite(connection).Options;

        var context = new BitAggregatorDbContext(options);

        await context.Database.EnsureCreatedAsync();

        return new SqliteTestDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}