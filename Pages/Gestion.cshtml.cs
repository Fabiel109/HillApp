using HillApp.Data;
using HillApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace HillApp.Pages;

public sealed record ManagedUser(
    int UserId,
    string Username,
    string Email,
    DateTime CreatedAt,
    DateTime? LastAccess,
    int RecordCount,
    int CommentCount);

public class GestionModel : PageModel
{
    private readonly Database _database;
    private readonly UserSessionService _sessions;
    private readonly UserAccountService _accounts;
    private readonly AdminAccessService _adminAccess;

    public GestionModel(
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

    [BindProperty(SupportsGet = true)]
    public string Orden { get; set; } = "creacion";

    public CurrentUser? CurrentUser { get; private set; }
    public string NombreUsuario => CurrentUser?.Username ?? "Usuario";
    public List<ManagedUser> Usuarios { get; } = new();
    public string? Mensaje => TempData.Peek("GestionMensaje") as string;
    public string? ErrorMessage { get; set; }
    public int TotalUsuarios => Usuarios.Count;
    public int Activos30Dias => Usuarios.Count(u => GetInactiveDays(u) <= 30);

    public async Task<IActionResult> OnGetAsync()
    {
        var auth = await AuthorizeAsync();
        if (auth is not null)
            return auth;
        if (CurrentUser is null)
            return Page();

        await LoadUsersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostEliminarAsync(int usuarioId)
    {
        var auth = await AuthorizeAsync();
        if (auth is not null)
            return auth;
        if (CurrentUser is null)
            return Page();

        if (usuarioId <= 0)
        {
            TempData["GestionMensaje"] = "No se recibió un usuario válido.";
            return RedirectToPage("/Gestion", new { orden = Orden });
        }

        if (usuarioId == CurrentUser!.UserId)
        {
            TempData["GestionMensaje"] = "Tu propia cuenta se elimina desde Perfil, donde se pide la contraseña actual.";
            return RedirectToPage("/Gestion", new { orden = Orden });
        }

        try
        {
            var deleted = await _accounts.DeleteUserAsync(usuarioId);
            TempData["GestionMensaje"] = deleted
                ? "Cuenta y datos asociados eliminados correctamente."
                : "La cuenta ya no existe.";
        }
        catch (MySqlException)
        {
            TempData["GestionMensaje"] = "No se pudo eliminar la cuenta. Revisa la conexión con TiDB Cloud.";
        }

        return RedirectToPage("/Gestion", new { orden = Orden });
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _sessions.EndSessionAsync(HttpContext);
        return RedirectToPage("/Login");
    }

    public int GetInactiveDays(ManagedUser user)
    {
        var reference = user.LastAccess ?? user.CreatedAt;
        return Math.Max(0, (int)Math.Floor((DateTime.UtcNow - DateTime.SpecifyKind(reference, DateTimeKind.Utc)).TotalDays));
    }

    private async Task<IActionResult?> AuthorizeAsync()
    {
        try
        {
            var currentUser = await _sessions.ValidateAndTouchAsync(HttpContext);
            if (currentUser is null)
                return RedirectToPage("/Login");

            CurrentUser = currentUser;
            if (!_adminAccess.IsAdmin(currentUser))
                return NotFound();

            return null;
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            ErrorMessage = "Falta actualizar la base de datos. Ejecuta SQL/ACTUALIZAR_BASE_V3.sql en TiDB Cloud.";
            return null;
        }
        catch (MySqlException)
        {
            ErrorMessage = "No fue posible validar la sesión con TiDB Cloud.";
            return null;
        }
    }

    private async Task LoadUsersAsync()
    {
        if (CurrentUser is null)
            return;

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            var orderBy = string.Equals(Orden, "acceso", StringComparison.OrdinalIgnoreCase)
                ? "COALESCE(u.UltimoAcceso, u.FechaRegistro) DESC, u.UsuarioId DESC"
                : "u.FechaRegistro DESC, u.UsuarioId DESC";

            var sql = $@"
                SELECT
                    u.UsuarioId,
                    u.NombreUsuario,
                    u.Correo,
                    u.FechaRegistro,
                    u.UltimoAcceso,
                    (SELECT COUNT(*) FROM Records r WHERE r.UsuarioId = u.UsuarioId) AS RecordCount,
                    (SELECT COUNT(*) FROM Comentarios c WHERE c.UsuarioId = u.UsuarioId) AS CommentCount
                FROM Usuarios u
                ORDER BY {orderBy};";

            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                Usuarios.Add(new ManagedUser(
                    checked((int)reader.GetInt64(reader.GetOrdinal("UsuarioId"))),
                    reader.GetString(reader.GetOrdinal("NombreUsuario")),
                    reader.GetString(reader.GetOrdinal("Correo")),
                    reader.GetDateTime(reader.GetOrdinal("FechaRegistro")),
                    reader.IsDBNull(reader.GetOrdinal("UltimoAcceso")) ? null : reader.GetDateTime(reader.GetOrdinal("UltimoAcceso")),
                    Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("RecordCount"))),
                    Convert.ToInt32(reader.GetInt64(reader.GetOrdinal("CommentCount")))));
            }
        }
        catch (MySqlException)
        {
            ErrorMessage = "No se pudo cargar la lista de cuentas.";
        }
    }
}
