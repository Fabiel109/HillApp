using MySqlConnector;

namespace HillApp.Services;

public sealed class InactiveAccountCleanupService : BackgroundService
{
    private readonly UserAccountService _accounts;
    private readonly ILogger<InactiveAccountCleanupService> _logger;
    private readonly int _inactiveDays;

    public InactiveAccountCleanupService(
        UserAccountService accounts,
        IConfiguration configuration,
        ILogger<InactiveAccountCleanupService> logger)
    {
        _accounts = accounts;
        _logger = logger;
        _inactiveDays = Math.Max(1, configuration.GetValue<int?>("Accounts:InactiveDays") ?? 500);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = await _accounts.DeleteInactiveUsersAsync(_inactiveDays, stoppingToken);
                if (removed > 0)
                    _logger.LogInformation("Se eliminaron {Count} cuentas inactivas.", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (MySqlException ex)
            {
                _logger.LogWarning(ex, "No se pudo ejecutar la limpieza de cuentas inactivas.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado durante la limpieza de cuentas inactivas.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
