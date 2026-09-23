using HillApp.Data;
using HillApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Credenciales locales opcionales. Este archivo queda fuera de Git por seguridad.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
// Las variables del hosting tienen la última palabra sobre los archivos JSON.
builder.Configuration.AddEnvironmentVariables();

// Render, Koyeb y otros hosts entregan el puerto mediante la variable PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddRazorPages();
builder.Services.AddResponseCompression();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Database>();
builder.Services.AddScoped<UserSessionService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<AdminAccessService>();
builder.Services.AddSingleton<UserAccountService>();
builder.Services.AddHostedService<InactiveAccountCleanupService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseResponseCompression();

// Cabeceras básicas de seguridad sin alterar el funcionamiento de la aplicación.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

app.UseStaticFiles();
app.UseRouting();

// Endpoint barato para health checks del hosting.
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

// Mantiene como activa la sesión mientras el usuario realmente tiene la app abierta.
app.MapGet("/session/ping", async (HttpContext context, UserSessionService sessions) =>
{
    try
    {
        return await sessions.ValidateAndTouchAsync(context) is null
            ? Results.Unauthorized()
            : Results.NoContent();
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapRazorPages();

app.Run();
