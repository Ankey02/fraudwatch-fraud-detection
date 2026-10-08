using Dapper;
using Npgsql;

namespace FraudWatch.Api.Data;

public class Db(string connectionString)
{
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(Schema.Sql, cancellationToken: ct));
    }
}
