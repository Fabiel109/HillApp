using HillApp.Data;
using MySqlConnector;

namespace HillApp.Services;

public sealed class UserAccountService
{
    private readonly Database _database;

    public UserAccountService(Database database)
    {
        _database = database;
    }

    public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await DeleteChildrenAsync(connection, transaction, userId, cancellationToken);

            const string deleteUserSql = "DELETE FROM Usuarios WHERE UsuarioId = @usuarioId;";
            await using var deleteUser = new MySqlCommand(deleteUserSql, connection, transaction);
            deleteUser.Parameters.AddWithValue("@usuarioId", userId);
            var affected = await deleteUser.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return affected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> DeleteInactiveUsersAsync(int inactiveDays, CancellationToken cancellationToken = default)
    {
        var cutoffUtc = DateTime.UtcNow.AddDays(-inactiveDays);

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var staleIds = new List<int>();
            const string selectSql = @"
                SELECT UsuarioId
                FROM Usuarios
                WHERE COALESCE(UltimoAcceso, FechaRegistro) < @cutoff
                FOR UPDATE;";

            await using (var select = new MySqlCommand(selectSql, connection, transaction))
            {
                select.Parameters.AddWithValue("@cutoff", cutoffUtc);
                await using var reader = await select.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    staleIds.Add(checked((int)reader.GetInt64(0)));
                }
            }

            foreach (var userId in staleIds)
            {
                await DeleteChildrenAsync(connection, transaction, userId, cancellationToken);

                const string deleteSql = @"
                    DELETE FROM Usuarios
                    WHERE UsuarioId = @usuarioId
                      AND COALESCE(UltimoAcceso, FechaRegistro) < @cutoff;";

                await using var delete = new MySqlCommand(deleteSql, connection, transaction);
                delete.Parameters.AddWithValue("@usuarioId", userId);
                delete.Parameters.AddWithValue("@cutoff", cutoffUtc);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return staleIds.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task DeleteChildrenAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int userId,
        CancellationToken cancellationToken)
    {
        const string deleteCommentsSql = "DELETE FROM Comentarios WHERE UsuarioId = @usuarioId;";
        await using (var deleteComments = new MySqlCommand(deleteCommentsSql, connection, transaction))
        {
            deleteComments.Parameters.AddWithValue("@usuarioId", userId);
            await deleteComments.ExecuteNonQueryAsync(cancellationToken);
        }

        const string deleteRecordsSql = "DELETE FROM Records WHERE UsuarioId = @usuarioId;";
        await using (var deleteRecords = new MySqlCommand(deleteRecordsSql, connection, transaction))
        {
            deleteRecords.Parameters.AddWithValue("@usuarioId", userId);
            await deleteRecords.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
