using System.Net.Mail;
using HillApp.Data;
using HillApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace HillApp.Pages;

public class RegistroModel : PageModel
{
    private readonly Database _database;

    public RegistroModel(Database database)
    {
        _database = database;
    }

    [BindProperty]
    public string Usuario { get; set; } = string.Empty;

    [BindProperty]
    public string Correo { get; set; } = string.Empty;

    [BindProperty]
    public string Contrasena { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Usuario = Usuario.Trim();
        Correo = Correo.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(Usuario) || string.IsNullOrWhiteSpace(Correo) || string.IsNullOrEmpty(Contrasena))
        {
            ErrorMessage = "Todos los campos son obligatorios.";
            return Page();
        }

        if (Usuario.Length is < 3 or > 100)
        {
            ErrorMessage = "El usuario debe tener entre 3 y 100 caracteres.";
            return Page();
        }

        if (!MailAddress.TryCreate(Correo, out _))
        {
            ErrorMessage = "Ingresa un correo electrónico válido.";
            return Page();
        }

        if (Contrasena.Length < 8)
        {
            ErrorMessage = "La contraseña debe tener al menos 8 caracteres.";
            return Page();
        }

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            const string checkSql = @"
                SELECT NombreUsuario, Correo
                FROM Usuarios
                WHERE NombreUsuario = @usuario OR Correo = @correo
                LIMIT 1;";

            await using (var check = new MySqlCommand(checkSql, connection))
            {
                check.Parameters.AddWithValue("@usuario", Usuario);
                check.Parameters.AddWithValue("@correo", Correo);
                await using var reader = await check.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var existingUser = reader.GetString(reader.GetOrdinal("NombreUsuario"));
                    var existingEmail = reader.GetString(reader.GetOrdinal("Correo"));
                    ErrorMessage = string.Equals(existingUser, Usuario, StringComparison.OrdinalIgnoreCase)
                        ? "El nombre de usuario ya está en uso. Elige otro."
                        : string.Equals(existingEmail, Correo, StringComparison.OrdinalIgnoreCase)
                            ? "Ese correo ya está vinculado a una cuenta."
                            : "El usuario o correo ya está en uso.";
                    return Page();
                }
            }

            const string insertSql = @"
                INSERT INTO Usuarios (NombreUsuario, Correo, Contrasena, FechaRegistro, UltimoAcceso)
                VALUES (@usuario, @correo, @contrasena, UTC_TIMESTAMP(), UTC_TIMESTAMP(6));";

            await using var insert = new MySqlCommand(insertSql, connection);
            insert.Parameters.AddWithValue("@usuario", Usuario);
            insert.Parameters.AddWithValue("@correo", Correo);
            insert.Parameters.AddWithValue("@contrasena", PasswordService.Hash(Contrasena));
            await insert.ExecuteNonQueryAsync();

            return RedirectToPage("/Login", new { registro = "ok" });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            ErrorMessage = "El nombre de usuario o el correo ya están en uso.";
            return Page();
        }
        catch (MySqlException ex) when (ex.Number == 1054)
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
