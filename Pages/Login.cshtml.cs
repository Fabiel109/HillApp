using HillApp.Data;
using HillApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace HillApp.Pages;

public class LoginModel : PageModel
{
    private readonly Database _database;
    private readonly UserSessionService _sessions;

    public LoginModel(Database database, UserSessionService sessions)
    {
        _database = database;
        _sessions = sessions;
    }

    [BindProperty]
    public string Usuario { get; set; } = string.Empty;

    [BindProperty]
    public string Contrasena { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public bool RegistroOk => string.Equals(Request.Query["registro"], "ok", StringComparison.OrdinalIgnoreCase);
    public bool CuentaEliminada => string.Equals(Request.Query["cuenta"], "eliminada", StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            if (await _sessions.ValidateAndTouchAsync(HttpContext) is not null)
                return RedirectToPage("/Records");
        }
        catch (MySqlException)
        {
            // Si TiDB no responde, dejamos visible el formulario para poder mostrar el error al enviar.
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Usuario = Usuario.Trim();

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrEmpty(Contrasena))
        {
            ErrorMessage = "Por favor, llena todos los campos.";
            return Page();
        }

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                SELECT UsuarioId, Contrasena
                FROM Usuarios
                WHERE NombreUsuario = @login OR Correo = @login
                LIMIT 1;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@login", Usuario);

            int usuarioId;
            string storedPassword;

            await using (var reader = await command.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync())
                {
                    ErrorMessage = "Usuario/correo o contraseña incorrectos.";
                    return Page();
                }

                usuarioId = checked((int)reader.GetInt64(reader.GetOrdinal("UsuarioId")));
                storedPassword = reader.GetString(reader.GetOrdinal("Contrasena"));
            }

            if (!PasswordService.Verify(Contrasena, storedPassword))
            {
                ErrorMessage = "Usuario/correo o contraseña incorrectos.";
                return Page();
            }

            var started = await _sessions.TryStartSessionAsync(HttpContext, usuarioId);
            if (!started)
            {
                ErrorMessage = "Esta cuenta ya tiene una sesión activa en otro dispositivo. Cierra sesión allí o espera 45 minutos sin actividad.";
                return Page();
            }

            // Compatibilidad con usuarios antiguos: al acceder correctamente se actualiza su hash.
            if (PasswordService.NeedsUpgrade(storedPassword))
            {
                const string upgradeSql = "UPDATE Usuarios SET Contrasena = @hash WHERE UsuarioId = @id";
                await using var upgrade = new MySqlCommand(upgradeSql, connection);
                upgrade.Parameters.AddWithValue("@hash", PasswordService.Hash(Contrasena));
                upgrade.Parameters.AddWithValue("@id", usuarioId);
                await upgrade.ExecuteNonQueryAsync();
            }

            return RedirectToPage("/Records");
        }
        catch (OverflowException)
        {
            ErrorMessage = "El identificador del usuario no es válido.";
            return Page();
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            ErrorMessage = "Falta actualizar la base de datos. Ejecuta SQL/ACTUALIZAR_BASE_V3.sql en TiDB Cloud.";
            return Page();
        }
        catch (MySqlException)
        {
            ErrorMessage = "No fue posible conectar con TiDB Cloud. Revisa la conexión y que el clúster esté disponible.";
            return Page();
        }
    }
}
