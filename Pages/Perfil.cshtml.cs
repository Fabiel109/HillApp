using HillApp.Data;
using HillApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace HillApp.Pages;

public class PerfilModel : PageModel
{
    private readonly Database _database;
    private readonly UserSessionService _sessions;
    private readonly UserAccountService _accounts;
    private readonly AdminAccessService _adminAccess;

    public PerfilModel(
        Database database,
        UserSessionService sessions,
        UserAccountService accounts,
        AdminAccessService adminAccess)
    {
        _database = database;
        _sessions = sessions;
        _accounts = accounts;
        _adminAccess = adminAccess;
    }

    [BindProperty]
    public string NuevoNombre { get; set; } = string.Empty;

    [BindProperty]
    public string ContrasenaActual { get; set; } = string.Empty;

    [BindProperty]
    public string NuevaContrasena { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmarContrasena { get; set; } = string.Empty;

    [BindProperty]
    public string ContrasenaEliminar { get; set; } = string.Empty;

    public CurrentUser? CurrentUser { get; private set; }
    public string NombreUsuario => CurrentUser?.Username ?? "Usuario";
    public string Correo => CurrentUser?.Email ?? string.Empty;
    public bool EsAdministrador => _adminAccess.IsAdmin(CurrentUser);
    public DateTime? FechaRegistro { get; private set; }
    public DateTime? UltimoAcceso { get; private set; }
    public string? Mensaje => TempData.Peek("PerfilMensaje") as string;
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var auth = await AuthenticateAsync();
        if (auth is not null)
            return auth;
        if (CurrentUser is null)
            return Page();

        NuevoNombre = NombreUsuario;
        await LoadDatesAsync(CurrentUser.UserId);
        return Page();
    }

    public async Task<IActionResult> OnPostNombreAsync()
    {
        var auth = await AuthenticateAsync();
        if (auth is not null)
            return auth;
        if (CurrentUser is null)
            return Page();

        NuevoNombre = NuevoNombre.Trim();
        if (NuevoNombre.Length is < 3 or > 100)
        {
            ErrorMessage = "El nombre de usuario debe tener entre 3 y 100 caracteres.";
            await LoadDatesAsync(CurrentUser!.UserId);
            return Page();
        }

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            const string checkSql = @"
                SELECT COUNT(*)
                FROM Usuarios
                WHERE NombreUsuario = @nombre AND UsuarioId <> @id;";
            await using (var check = new MySqlCommand(checkSql, connection))
            {
                check.Parameters.AddWithValue("@nombre", NuevoNombre);
                check.Parameters.AddWithValue("@id", CurrentUser!.UserId);
                var exists = Convert.ToInt32(await check.ExecuteScalarAsync());
                if (exists > 0)
                {
                    ErrorMessage = "Ese nombre de usuario ya está en uso.";
                    await LoadDatesAsync(CurrentUser.UserId);
                    return Page();
                }
            }

            const string updateSql = "UPDATE Usuarios SET NombreUsuario = @nombre WHERE UsuarioId = @id;";
            await using var update = new MySqlCommand(updateSql, connection);
            update.Parameters.AddWithValue("@nombre", NuevoNombre);
            update.Parameters.AddWithValue("@id", CurrentUser!.UserId);
            await update.ExecuteNonQueryAsync();

            TempData["PerfilMensaje"] = "Nombre de usuario actualizado correctamente.";
            return RedirectToPage("/Perfil");
        }
        catch (MySqlException)
        {
            ErrorMessage = "No se pudo actualizar el nombre. Inténtalo de nuevo.";
            await LoadDatesAsync(CurrentUser!.UserId);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        var auth = await AuthenticateAsync();
        if (auth is not null)
            return auth;
        if (CurrentUser is null)
            return Page();

        if (string.IsNullOrEmpty(ContrasenaActual))
        {
            ErrorMessage = "Escribe tu contraseña actual.";
            await LoadDatesAsync(CurrentUser!.UserId);
            return Page();
        }

        if (NuevaContrasena.Length < 8)
        {
            ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres.";
            await LoadDatesAsync(CurrentUser!.UserId);
            return Page();
        }

        if (!string.Equals(NuevaContrasena, ConfirmarContrasena, StringComparison.Ordinal))
        {
            ErrorMessage = "La nueva contraseña y su confirmación no coinciden.";
            await LoadDatesAsync(CurrentUser!.UserId);
            return Page();
        }

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            var storedPassword = await GetStoredPasswordAsync(connection, CurrentUser!.UserId);
            if (storedPassword is null || !PasswordService.Verify(ContrasenaActual, storedPassword))
            {
                ErrorMessage = "La contraseña actual no es correcta.";
                await LoadDatesAsync(CurrentUser.UserId);
                return Page();
            }

            const string updateSql = "UPDATE Usuarios SET Contrasena = @hash WHERE UsuarioId = @id;";
            await using var update = new MySqlCommand(updateSql, connection);
            update.Parameters.AddWithValue("@hash", PasswordService.Hash(NuevaContrasena));
            update.Parameters.AddWithValue("@id", CurrentUser.UserId);
            await update.ExecuteNonQueryAsync();

            TempData["PerfilMensaje"] = "Contraseña cambiada correctamente.";
            return RedirectToPage("/Perfil");
        }
        catch (MySqlException)
        {
            ErrorMessage = "No se pudo cambiar la contraseña. Inténtalo de nuevo.";
            await LoadDatesAsync(CurrentUser!.UserId);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEliminarCuentaAsync()
    {
        var auth = await AuthenticateAsync();
        if (auth is not null)
            return auth;
        if (CurrentUser is null)
            return Page();

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            var storedPassword = await GetStoredPasswordAsync(connection, CurrentUser!.UserId);
            if (storedPassword is null || !PasswordService.Verify(ContrasenaEliminar, storedPassword))
            {
                ErrorMessage = "La contraseña actual no es correcta. La cuenta no fue eliminada.";
                await LoadDatesAsync(CurrentUser.UserId);
                return Page();
            }

            var userId = CurrentUser.UserId;
            await _accounts.DeleteUserAsync(userId);
            await _sessions.EndSessionAsync(HttpContext);
            return RedirectToPage("/Login", new { cuenta = "eliminada" });
        }
        catch (MySqlException)
        {
            ErrorMessage = "No se pudo eliminar la cuenta. Inténtalo de nuevo.";
            await LoadDatesAsync(CurrentUser!.UserId);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _sessions.EndSessionAsync(HttpContext);
        return RedirectToPage("/Login");
    }

    private async Task<IActionResult?> AuthenticateAsync()
    {
        try
        {
            var currentUser = await _sessions.ValidateAndTouchAsync(HttpContext);
            if (currentUser is null)
                return RedirectToPage("/Login");

            CurrentUser = currentUser;
            return null;
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            ErrorMessage = "Falta actualizar la base de datos. Ejecuta SQL/ACTUALIZAR_BASE_V3.sql en TiDB Cloud.";
            return null;
        }
        catch (MySqlException)
        {
            ErrorMessage = "No fue posible validar tu sesión con TiDB Cloud.";
            return null;
        }
    }

    private async Task LoadDatesAsync(int userId)
    {
        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();
            const string sql = @"
                SELECT FechaRegistro, UltimoAcceso
                FROM Usuarios
                WHERE UsuarioId = @id
                LIMIT 1;";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", userId);
            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                FechaRegistro = reader.IsDBNull(reader.GetOrdinal("FechaRegistro"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("FechaRegistro"));
                UltimoAcceso = reader.IsDBNull(reader.GetOrdinal("UltimoAcceso"))
                    ? null
                    : reader.GetDateTime(reader.GetOrdinal("UltimoAcceso"));
            }
        }
        catch (MySqlException)
        {
            ErrorMessage ??= "No fue posible cargar los datos del perfil.";
        }
    }

    private static async Task<string?> GetStoredPasswordAsync(MySqlConnection connection, int userId)
    {
        const string sql = "SELECT Contrasena FROM Usuarios WHERE UsuarioId = @id LIMIT 1;";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", userId);
        return (await command.ExecuteScalarAsync()) as string;
    }
}
