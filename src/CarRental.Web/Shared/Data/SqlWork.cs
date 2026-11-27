using System.Data;
using Microsoft.Data.SqlClient;

namespace CarRental.Web.Shared.Data;

public sealed class SqlWork(SqlConnection connection, SqlTransaction? transaction, CancellationToken cancellationToken)
{
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map,
        params (string Key, object? Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(map(reader));
        return rows;
    }

    public async Task<T?> OneAsync<T>(string sql, Func<SqlDataReader, T> map,
        params (string Key, object? Value)[] parameters) where T : class =>
        (await QueryAsync(sql, map, parameters)).SingleOrDefault();

    public async Task<T> ValueAsync<T>(string sql, params (string Key, object? Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        return (T)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("SQL query returned no value."));
    }

    public async Task<int> ExecuteAsync(string sql, params (string Key, object? Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqlCommand Command(string sql, (string Key, object? Value)[] parameters)
    {
        var command = new SqlCommand(sql, connection, transaction);
        foreach (var (key, value) in parameters)
        {
            var parameter = command.Parameters.AddWithValue("@" + key, value ?? DBNull.Value);
            if (value is DateTime)
                parameter.SqlDbType = SqlDbType.DateTime2;
            if (value is DateOnly date)
            {
                parameter.SqlDbType = SqlDbType.Date;
                parameter.Value = date.ToDateTime(TimeOnly.MinValue);
            }
        }
        return command;
    }
}
