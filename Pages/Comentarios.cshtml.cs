using HillApp.Data;
using HillApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace HillApp.Pages;

public class ComentariosModel : PageModel
{
    private static readonly HashSet<string> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "Opinión", "Sugerencia", "Bug", "Otro"
    };

    private readonly Database _database;
    private readonly UserSessionService _sessions;
    private readonly EmailService _email;
    private readonly AdminAccessService _adminAccess;

    public ComentariosModel(Database database, UserSessionService sessions, EmailService email, AdminAccessService adminAccess)
    {
        _database = database;
        _sessions = sessions;
        _email = email;
        _adminAccess = adminAccess;
    }

    [BindProperty]
    public string Tipo { get; set; } = "Sugerencia";

    [BindProperty]
    public string Comentario { get; set; } = string.Empty;

    public CurrentUser? CurrentUser { get; private set; }
    public string NombreUsuario => CurrentUser?.Username ?? "Usuario";
    public string CorreoUsuario => CurrentUser?.Email ?? string.Empty;
    public bool EsAdministrador => _adminAccess.IsAdmin(CurrentUser);
    public string? Mensaje => TempData.Peek("ComentarioMensaje") as string;
    public bool MensajeEsAdvertencia => string.Equals(TempData.Peek("ComentarioAdvertencia") as string, "1", StringComparison.Ordinal);
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var currentUser = await _sessions.ValidateAndTouchAsync(HttpContext);
            if (currentUser is null)
                return RedirectToPage("/Login");
            CurrentUser = currentUser;
        }
        catch (MySqlException)
        {
            ErrorMessage = "No fue posible validar tu sesión con TiDB Cloud.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        int userId;
        try
        {
            var currentUser = await _sessions.ValidateAndTouchAsync(HttpContext);
            if (currentUser is null)
                return RedirectToPage("/Login");
            CurrentUser = currentUser;
            userId = currentUser.UserId;
        }
        catch (MySqlException)
        {
            ErrorMessage = "No fue posible validar tu sesión. Inténtalo de nuevo.";
            return Page();
        }

        Tipo = Tipo.Trim();
        Comentario = Comentario.Trim();

        if (!TiposPermitidos.Contains(Tipo))
        {
            ErrorMessage = "Selecciona un tipo de comentario válido.";
            return Page();
        }

        if (Comentario.Length < 10)
        {
            ErrorMessage = "Escribe al menos 10 caracteres para poder enviar el comentario.";
            return Page();
        }

        if (Comentario.Length > 2000)
        {
            ErrorMessage = "El comentario no puede superar 2000 caracteres.";
            return Page();
        }

        var username = NombreUsuario;
        var userEmail = CorreoUsuario;
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            ErrorMessage = "No se encontró el correo de tu sesión. Cierra sesión y vuelve a entrar.";
            return Page();
        }

        long comentarioId;

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            // Evita spam accidental: máximo un comentario por minuto por usuario.
            const string cooldownSql = @"
                SELECT COUNT(*)
                FROM Comentarios
                WHERE UsuarioId = @usuarioId
                  AND FechaCreacion > DATE_SUB(CURRENT_TIMESTAMP, INTERVAL 60 SECOND);";

            await using (var cooldown = new MySqlCommand(cooldownSql, connection))
            {
                cooldown.Parameters.AddWithValue("@usuarioId", userId);
                var recent = Convert.ToInt32(await cooldown.ExecuteScalarAsync());
                if (recent > 0)
                {
                    ErrorMessage = "Espera un minuto antes de enviar otro comentario.";
                    return Page();
                }
            }

            const string insertSql = @"
                INSERT INTO Comentarios (UsuarioId, CorreoUsuario, Tipo, Mensaje, EmailEnviado)
                VALUES (@usuarioId, @correo, @tipo, @mensaje, 0);";

            await using var insert = new MySqlCommand(insertSql, connection);
            insert.Parameters.AddWithValue("@usuarioId", userId);
            insert.Parameters.AddWithValue("@correo", userEmail);
            insert.Parameters.AddWithValue("@tipo", Tipo);
            insert.Parameters.AddWithValue("@mensaje", Comentario);
            await insert.ExecuteNonQueryAsync();
            comentarioId = insert.LastInsertedId;
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            ErrorMessage = "Falta actualizar la base. Ejecuta SQL/ACTUALIZAR_BASE_V3.sql en TiDB Cloud.";
            return Page();
        }
        catch (MySqlException)
        {
            ErrorMessage = "No fue posible guardar el comentario. Inténtalo de nuevo.";
            return Page();
        }

        var emailSent = await _email.SendFeedbackAsync(username, userEmail, Tipo, Comentario);

        if (emailSent)
        {
            try
            {
                await using var connection = _database.CreateConnection();
                await connection.OpenAsync();
                const string updateSql = "UPDATE Comentarios SET EmailEnviado = 1 WHERE ComentarioId = @id";
                await using var update = new MySqlCommand(updateSql, connection);
                update.Parameters.AddWithValue("@id", comentarioId);
                await update.ExecuteNonQueryAsync();
            }
            catch (MySqlException)
            {
                // El correo sí salió; este estado es solo informativo.
            }

            TempData["ComentarioMensaje"] = "¡Gracias! Tu comentario fue guardado y enviado por correo.";
            TempData["ComentarioAdvertencia"] = "0";
        }
        else
        {
            TempData["ComentarioMensaje"] = "Tu comentario quedó guardado en TiDB, pero el correo todavía no está configurado o falló el envío.";
            TempData["ComentarioAdvertencia"] = "1";
        }

        return RedirectToPage("/Comentarios");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _sessions.EndSessionAsync(HttpContext);
        return RedirectToPage("/Login");
    }
}
