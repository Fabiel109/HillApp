using HillApp.Data;
using HillApp.Models;
using HillApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace HillApp.Pages;

public class RecordsModel : PageModel
{
    private readonly Database _database;
    private readonly UserSessionService _sessions;
    private readonly AdminAccessService _adminAccess;

    public RecordsModel(Database database, UserSessionService sessions, AdminAccessService adminAccess)
    {
        _database = database;
        _sessions = sessions;
        _adminAccess = adminAccess;
    }

    public static readonly IReadOnlyList<GameItem> Mapas = new List<GameItem>
    {
        new("campo", "Campo", "campo.jpg"),
        new("cascadadelosmanantiales", "Cascada de los Manantiales", "cascadadelosmanantiales.jpg"),
        new("bosque", "Bosque", "bosque.jpg"),
        new("ciudad", "Ciudad", "ciudad.jpg"),
        new("montana", "Montaña", "montana.jpg"),
        new("arrecifedeloscacharros", "Arrecife de los Cacharros", "arrecifedeloscacharros.jpg"),
        new("invierno", "Invierno", "invierno.jpg"),
        new("minas", "Minas", "minas.jpg"),
        new("valledesertico", "Valle Desértico", "valledesertico.jpg"),
        new("playa", "Playa", "playa.jpg"),
        new("pantano", "Pantano", "pantano.jpg"),
        new("circuitodelglaciar", "Circuito del Glaciar", "circuitodelglaciar.jpg"),
        new("planta", "Planta", "planta.jpg"),
        new("savannazigzaguiante", "Savanna Zigzaguante", "savannazigzaguiante.jpg"),
        new("gloomvale", "Gloomvale", "gloomvale.jpg"),
        new("desvordeydiversion", "Desborde y Diversión", "desvordeydiversion.jpg"),
        new("arenadeldesfiladero", "Arena del Desfiladero", "arenadeldesfiladero.jpg"),
        new("ciudadcopa", "Ciudad Copa", "ciudadcopa.jpg"),
        new("puestoavanzadoenlaluna", "Puesto Avanzado Luna", "puestoavanzadoenlaluna.jpg"),
        new("pruebasdelbosque", "Pruebas del Bosque", "pruebasdelbosque.jpg"),
        new("ciudadintensa", "Ciudad Intensa", "ciudadintensa.jpg"),
        new("crudoinvierno", "Crudo Invierno", "crudoinvierno.jpg")
    };

    public static readonly IReadOnlyList<GameItem> Vehiculos = new List<GameItem>
    {
        new("jeep", "Hill Climber", "jeep.jpg"),
        new("mk2", "Hill Climber Mk2", "mk2.jpg"),
        new("autodeportivo", "Auto Deportivo", "autodeportivo.jpg"),
        new("buggyparaarena", "Buggy para Arena", "buggyparaarena.jpg"),
        new("superdiesel", "Superdiesel", "superdiesel.jpg"),
        new("supermoto", "Supermoto", "supermoto.jpg"),
        new("superauto", "Superauto", "superauto.jpg"),
        new("autoderally", "Auto De Rally", "autoderally.jpg"),
        new("lowrider", "Lowrider", "lowrider.jpg"),
        new("beast", "Beast", "beast.jpg"),
        new("hotrod", "Hotrod", "hotrod.jpg"),
        new("camiondecarreras", "Camión de Carreras", "camiondecarreras.jpg"),
        new("formula", "Formula", "formula.jpg"),
        new("musclecar", "Musclecar", "musclecar.jpg"),
        new("camionmonstruo", "Camión Monstruo", "camionmonstruo.jpg"),
        new("rotador", "Rotador", "rotador.jpg"),
        new("motochopper", "Motochopper", "motochopper.jpg"),
        new("tanque", "Tanque", "tanque.jpg"),
        new("ccev", "CC-EV", "CCEV.jpg"),
        new("offroader", "Offroader", "offroader.jpg"),
        new("stocker", "Stocker", "stocker.jpg"),
        new("autobus", "Autobus", "autobus.jpg"),
        new("modulolunar", "Modulo Lunar", "modulolunar.jpg"),
        new("motovoladora", "Moto Voladora", "motovoladora.jpg"),
        new("rockbouncer", "Rockbouncer", "rockbouncer.jpg"),
        new("raider", "Raider", "raider.jpg"),
        new("bolt", "Bolt", "bolt.jpg"),
        new("atv", "Atv", "atv.jpg"),
        new("escuter", "Escuter", "escuter.jpg"),
        new("enduro", "Enduro", "enduro.jpg"),
        new("tractor", "Tractor", "tractor.jpg"),
        new("vehiculodenieve", "Vehículo de Nieve", "vehiculodenieve.jpg"),
        new("monociclo", "Monociclo", "monociclo.jpg"),
        new("planeador", "Planeador", "planeador.jpg")
    };

    private readonly Dictionary<(string Map, string Vehicle), int> _records = new();

    [BindProperty]
    public string MapaSeleccionado { get; set; } = Mapas[0].Key;

    [BindProperty]
    public string VehiculoSeleccionado { get; set; } = Vehiculos[0].Key;

    [BindProperty]
    public int? Distancia { get; set; }

    public CurrentUser? CurrentUser { get; private set; }
    public string NombreUsuario => CurrentUser?.Username ?? "Usuario";
    public bool EsAdministrador => _adminAccess.IsAdmin(CurrentUser);
    public string? Mensaje => TempData.Peek("Mensaje") as string;
    public string? ErrorMessage { get; set; }

    public int TotalCeldas10k => Mapas.Count(m => !IsSpecialMap(m.Key)) * Vehiculos.Count;
    public int TotalCeldas5k => Mapas.Count(m => IsSpecialMap(m.Key)) * Vehiculos.Count;
    public int Completadas10k => CountCompleted(false);
    public int Completadas5k => CountCompleted(true);

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var currentUser = await _sessions.ValidateAndTouchAsync(HttpContext);
            if (currentUser is null)
                return RedirectToPage("/Login");

            CurrentUser = currentUser;
            await LoadRecordsAsync(currentUser.UserId);
        }
        catch (MySqlException ex) when (ex.Number is 1054 or 1146)
        {
            ErrorMessage = "Falta actualizar la base de datos. Ejecuta SQL/ACTUALIZAR_BASE_V3.sql en TiDB Cloud.";
        }
        catch (MySqlException)
        {
            ErrorMessage = "No fue posible conectar con TiDB Cloud. Revisa la conexión y que el clúster esté disponible.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostGuardarAsync()
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
            ErrorMessage = "No fue posible validar la sesión con TiDB Cloud.";
            return Page();
        }

        if (!IsValidMap(MapaSeleccionado) || !IsValidVehicle(VehiculoSeleccionado))
        {
            ErrorMessage = "Selecciona un mapa y un vehículo válidos.";
            await LoadRecordsAsync(userId);
            return Page();
        }

        if (Distancia is null || Distancia < 0)
        {
            ErrorMessage = "Por favor, ingresa una distancia válida.";
            await LoadRecordsAsync(userId);
            return Page();
        }

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO Records (Mapa, Vehiculo, Distancia, UsuarioId)
                VALUES (@mapa, @vehiculo, @distancia, @usuarioId)
                ON DUPLICATE KEY UPDATE
                    Distancia = @distanciaUpdate,
                    FechaActualizacion = CURRENT_TIMESTAMP;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@mapa", MapaSeleccionado);
            command.Parameters.AddWithValue("@vehiculo", VehiculoSeleccionado);
            command.Parameters.AddWithValue("@distancia", Distancia.Value);
            command.Parameters.AddWithValue("@usuarioId", userId);
            command.Parameters.AddWithValue("@distanciaUpdate", Distancia.Value);
            await command.ExecuteNonQueryAsync();

            TempData["Mensaje"] = "¡Récord guardado / actualizado correctamente!";
            return RedirectToPage("/Records");
        }
        catch (MySqlException)
        {
            ErrorMessage = "No se pudo guardar el récord. Revisa la conexión con TiDB Cloud.";
            await LoadRecordsAsync(userId);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostEliminarAsync()
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
            ErrorMessage = "No fue posible validar la sesión con TiDB Cloud.";
            return Page();
        }

        if (!IsValidMap(MapaSeleccionado) || !IsValidVehicle(VehiculoSeleccionado))
        {
            ErrorMessage = "Selecciona un mapa y un vehículo válidos.";
            await LoadRecordsAsync(userId);
            return Page();
        }

        try
        {
            await using var connection = _database.CreateConnection();
            await connection.OpenAsync();

            const string sql = @"
                DELETE FROM Records
                WHERE Mapa = @mapa AND Vehiculo = @vehiculo AND UsuarioId = @usuarioId;";

            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@mapa", MapaSeleccionado);
            command.Parameters.AddWithValue("@vehiculo", VehiculoSeleccionado);
            command.Parameters.AddWithValue("@usuarioId", userId);
            var affected = await command.ExecuteNonQueryAsync();

            TempData["Mensaje"] = affected > 0
                ? "Récord eliminado de los registros."
                : "No se encontró ningún récord para borrar.";

            return RedirectToPage("/Records");
        }
        catch (MySqlException)
        {
            ErrorMessage = "No se pudo eliminar el récord. Revisa la conexión con TiDB Cloud.";
            await LoadRecordsAsync(userId);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _sessions.EndSessionAsync(HttpContext);
        return RedirectToPage("/Login");
    }

    public int GetDistance(string map, string vehicle) =>
        _records.TryGetValue((map, vehicle), out var distance) ? distance : 0;

    public long GetPoints(string map, string vehicle)
    {
        var distance = GetDistance(map, vehicle);
        var calculated = IsSpecialMap(map) ? (long)distance * 3L : distance;
        return Math.Min(calculated, MaxPointsForMap(map));
    }

    public bool IsCompleted(string map, string vehicle)
    {
        var distance = GetDistance(map, vehicle);
        return distance >= (IsSpecialMap(map) ? 5_000 : 10_000);
    }

    public long GetMapTotal(string map) => Vehiculos.Sum(v => GetPoints(map, v.Key));

    public double GetMapPercentage(string map)
    {
        var max = (long)MaxPointsForMap(map) * Vehiculos.Count;
        return max == 0 ? 0 : Math.Min(100.0, GetMapTotal(map) * 100.0 / max);
    }

    public long GetVehicleTotal(string vehicle) => Mapas.Sum(m => GetPoints(m.Key, vehicle));

    public double GetVehiclePercentage(string vehicle)
    {
        var max = Mapas.Sum(m => (long)MaxPointsForMap(m.Key));
        return max == 0 ? 0 : Math.Min(100.0, GetVehicleTotal(vehicle) * 100.0 / max);
    }

    public long GrandTotal => Mapas.Sum(m => GetMapTotal(m.Key));

    public double GlobalPercentage
    {
        get
        {
            var max = Mapas.Sum(m => (long)MaxPointsForMap(m.Key) * Vehiculos.Count);
            return max == 0 ? 0 : Math.Min(100.0, GrandTotal * 100.0 / max);
        }
    }

    public static bool IsSpecialMap(string map) =>
        map is "pruebasdelbosque" or "ciudadintensa" or "crudoinvierno";

    private static int MaxPointsForMap(string map) => IsSpecialMap(map) ? 15_000 : 10_000;

    private int CountCompleted(bool special)
    {
        var total = 0;
        foreach (var map in Mapas.Where(m => IsSpecialMap(m.Key) == special))
        {
            foreach (var vehicle in Vehiculos)
            {
                if (IsCompleted(map.Key, vehicle.Key))
                    total++;
            }
        }
        return total;
    }

    private static bool IsValidMap(string value) => Mapas.Any(m => m.Key == value);
    private static bool IsValidVehicle(string value) => Vehiculos.Any(v => v.Key == value);

    private async Task LoadRecordsAsync(int userId)
    {
        _records.Clear();

        await using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = "SELECT Mapa, Vehiculo, Distancia FROM Records WHERE UsuarioId = @usuarioId";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@usuarioId", userId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var map = reader.GetString(reader.GetOrdinal("Mapa")).Trim().ToLowerInvariant().Replace("ñ", "n");
            var vehicle = reader.GetString(reader.GetOrdinal("Vehiculo")).Trim().ToLowerInvariant();
            var distance = reader.GetInt32(reader.GetOrdinal("Distancia"));
            _records[(map, vehicle)] = distance;
        }
    }
}
