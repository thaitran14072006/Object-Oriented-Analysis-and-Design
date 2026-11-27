using Microsoft.Data.SqlClient;

namespace CarRental.Web.Shared.Data;

public sealed class CarRentalDatabase(IConfiguration configuration)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var settings = configuration.GetConnectionString("CarRental");
        if (string.IsNullOrWhiteSpace(settings))
            throw new DatabaseUnavailableException("Cần cấu hình kết nối SQL Server ConnectionStrings:CarRental.");

        var connection = new SqlConnection(settings);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (SqlException)
        {
            await connection.DisposeAsync();
            throw new DatabaseUnavailableException("Không kết nối được cơ sở dữ liệu RentalPro.");
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<T> ReadAsync<T>(Func<SqlWork, Task<T>> read, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await read(new SqlWork(connection, null, cancellationToken));
    }

    public async Task<T> WriteAsync<T>(string resource, Func<SqlWork, Task<T>> write, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var work = new SqlWork(connection, transaction, cancellationToken);
        var lockResult = await work.ValueAsync<int>("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive',
                 @LockOwner='Transaction', @LockTimeout=5000;
            SELECT @result;
            """, ("resource", resource));
        if (lockResult < 0)
            throw new BusinessException("Dữ liệu đang được xử lý. Hãy thử lại.", 409);

        var result = await write(work);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}

public sealed class DatabaseUnavailableException(string message) : Exception(message);

public sealed class BusinessException(string message, int status = 400, object? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public object? Details { get; } = details;
}
