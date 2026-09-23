using System.Globalization;
using System.Security.Cryptography;
using HillApp.Data;
using MySqlConnector;

namespace HillApp.Services;

public sealed record CurrentUser(int UserId, string Username, string Email, string SessionToken);

public sealed class UserSessionService
{
    private const int ActiveSessionMinutes = 45;
    private const string UserIdCookie = ".HillApp.User";
    private const string TokenCookie = ".HillApp.Token";
    private readonly Database _database;

    public UserSessionService(Database database)
    {
        _database = database;
    }

    public async Task<bool> TryStartSessionAsync(HttpContext httpContext, int userId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        // Solo permite iniciar si no hay otra sesión activa o si la anterior
        // lleva 45 minutos sin actividad. El UPDATE es atómico.
        var sql = $@"
            UPDATE Usuarios
            SET ActiveSessionToken = @token,
                SessionLastSeen = UTC_TIMESTAMP(6),
                UltimoAcceso = UTC_TIMESTAMP(6)
            WHERE UsuarioId = @usuarioId
              AND (
                    ActiveSessionToken IS NULL
                    OR SessionLastSeen IS NULL
                    OR SessionLastSeen < DATE_SUB(UTC_TIMESTAMP(6), INTERVAL {ActiveSessionMinutes} MINUTE)
                  );";

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@token", token);
        command.Parameters.AddWithValue("@usuarioId", userId);

        var affected = await command.ExecuteNonQueryAsync();
        if (affected == 0)
            return false;

        WriteAuthCookies(httpContext, userId, token);
        return true;
    }

    public async Task<CurrentUser?> ValidateAndTouchAsync(HttpContext httpContext)
    {
        if (!TryReadAuthCookies(httpContext, out var userId, out var token))
        {
            ClearAuthCookies(httpContext);
            return null;
        }

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        const string selectSql = @"
            SELECT NombreUsuario, Correo, ActiveSessionToken
            FROM Usuarios
            WHERE UsuarioId = @usuarioId
            LIMIT 1
            FOR UPDATE;";

        string username;
        string email;
        string? activeToken;

        await using (var select = new MySqlCommand(selectSql, connection, transaction))
        {
            select.Parameters.AddWithValue("@usuarioId", userId);
            await using var reader = await select.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                await transaction.RollbackAsync();
                ClearAuthCookies(httpContext);
                return null;
            }

            username = reader.GetString(reader.GetOrdinal("NombreUsuario"));
            email = reader.GetString(reader.GetOrdinal("Correo"));
            activeToken = reader.IsDBNull(reader.GetOrdinal("ActiveSessionToken"))
                ? null
                : reader.GetString(reader.GetOrdinal("ActiveSessionToken"));
        }

        if (!string.Equals(activeToken, token, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync();
            ClearAuthCookies(httpContext);
            return null;
        }

        const string touchSql = @"
            UPDATE Usuarios
            SET SessionLastSeen = UTC_TIMESTAMP(6),
                UltimoAcceso = UTC_TIMESTAMP(6)
            WHERE UsuarioId = @usuarioId
              AND ActiveSessionToken = @token;";

        await using (var touch = new MySqlCommand(touchSql, connection, transaction))
        {
            touch.Parameters.AddWithValue("@usuarioId", userId);
            touch.Parameters.AddWithValue("@token", token);
            await touch.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return new CurrentUser(userId, username, email, token);
    }

    public async Task EndSessionAsync(HttpContext httpContext)
    {
        if (TryReadAuthCookies(httpContext, out var userId, out var token))
        {
            try
            {
                await using var connection = _database.CreateConnection();
                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Usuarios
                    SET ActiveSessionToken = NULL,
                        SessionLastSeen = NULL
                    WHERE UsuarioId = @usuarioId
                      AND ActiveSessionToken = @token;";

                await using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@usuarioId", userId);
                command.Parameters.AddWithValue("@token", token);
                await command.ExecuteNonQueryAsync();
            }
            catch (MySqlException)
            {
                // Aunque TiDB esté temporalmente inaccesible, se borran las cookies locales.
            }
        }

        ClearAuthCookies(httpContext);
    }

    private static bool TryReadAuthCookies(HttpContext httpContext, out int userId, out string token)
    {
        token = httpContext.Request.Cookies[TokenCookie] ?? string.Empty;
        var rawUserId = httpContext.Request.Cookies[UserIdCookie];

        return int.TryParse(rawUserId, NumberStyles.None, CultureInfo.InvariantCulture, out userId)
               && userId > 0
               && token.Length == 64;
    }

    private static void WriteAuthCookies(HttpContext httpContext, int userId, string token)
    {
        var options = BuildCookieOptions(httpContext);
        httpContext.Response.Cookies.Append(UserIdCookie, userId.ToString(CultureInfo.InvariantCulture), options);
        httpContext.Response.Cookies.Append(TokenCookie, token, options);
    }

    private static CookieOptions BuildCookieOptions(HttpContext httpContext)
    {
        var host = httpContext.Request.Host.Host;
        var isLocal = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                      || host == "127.0.0.1"
                      || host == "::1";

        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = !isLocal,
            Path = "/",
            MaxAge = TimeSpan.FromDays(30)
        };
    }

    private static void ClearAuthCookies(HttpContext httpContext)
    {
        var options = BuildCookieOptions(httpContext);
        httpContext.Response.Cookies.Delete(UserIdCookie, options);
        httpContext.Response.Cookies.Delete(TokenCookie, options);
    }
}
