using Microsoft.Extensions.Options;
using MoviePlatform.Application;

namespace MoviePlatform.Api;

/// <summary>
/// Периодический фоновый синк. Работает, только если Vibix:AutoSync = true.
/// </summary>
public sealed class VibixBackgroundSync : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<VibixOptions> _options;
    private readonly ILogger<VibixBackgroundSync> _logger;

    public VibixBackgroundSync(
        IServiceScopeFactory scopeFactory,
        IOptions<VibixOptions> options,
        ILogger<VibixBackgroundSync> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value;
        if (!opts.AutoSync)
        {
            _logger.LogInformation("VibixBackgroundSync выключен (AutoSync = false).");
            return;
        }

        var interval = TimeSpan.FromMinutes(opts.SyncIntervalMinutes > 0
            ? opts.SyncIntervalMinutes
            : 360);

        _logger.LogInformation("VibixBackgroundSync запущен. Интервал: {Interval}", interval);

        // Первая синхронизация — через 20 секунд после старта, чтобы API успел поднять БД.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            await RunOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Первая фоновая синхронизация упала");
        }

        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { _logger.LogError(ex, "Фоновая синхронизация упала"); }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<VibixSyncService>();

        _logger.LogInformation("Фоновая синхронизация Vibix: старт");
        var result = await sync.SyncAsync(ct);

        if (result.Error is null)
            _logger.LogInformation(
                "Фоновая синхронизация Vibix: read={Read}, created={Created}, updated={Updated}, skipped={Skipped}",
                result.Read, result.Created, result.Updated, result.Skipped);
        else
            _logger.LogWarning("Фоновая синхронизация Vibix завершилась ошибкой: {Error}", result.Error);
    }
}