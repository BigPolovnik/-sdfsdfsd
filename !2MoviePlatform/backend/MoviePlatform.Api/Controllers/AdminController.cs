using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoviePlatform.Application;
using MoviePlatform.Infrastructure;

namespace MoviePlatform.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(
    AppDbContext db,
    VibixCatalogService vibix,
    IOptions<VibixOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<AdminController> logger) : ControllerBase
{
    private static int _syncInProgress;
    private static int _enrichInProgress;

    private bool Authorized() =>
        !string.IsNullOrWhiteSpace(options.Value.AdminKey) &&
        string.Equals(
            Request.Headers["X-Admin-Key"].FirstOrDefault(),
            options.Value.AdminKey,
            StringComparison.Ordinal);

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        if (!Authorized())
            return Unauthorized(new
            {
                message = "Укажите ключ администратора в X-Admin-Key."
            });

        return Ok(new
        {
            vibixTokenConfigured =
                !string.IsNullOrWhiteSpace(options.Value.ApiToken),
            apiBaseUrl = options.Value.BaseUrl,
            movies = await db.Movies.CountAsync(ct),
            series = await db.Series.CountAsync(ct),
            moviesWithoutIframe =
                await db.Movies.CountAsync(m => m.IframeVideoId == null, ct),
            seriesWithoutIframe =
                await db.Series.CountAsync(s => s.IframeVideoId == null, ct),
            syncInProgress = _syncInProgress == 1,
            enrichInProgress = _enrichInProgress == 1,
            message = "Готово к работе."
        });
    }

    [HttpPost("sync-vibix")]
    public IActionResult Sync()
    {
        if (!Authorized())
            return Unauthorized(new
            {
                message = "Укажите ключ администратора в X-Admin-Key."
            });

        if (Interlocked.CompareExchange(ref _syncInProgress, 1, 0) == 1)
            return Ok(new
            {
                started = false,
                message = "Синхронизация уже выполняется."
            });

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var syncService =
                scope.ServiceProvider.GetRequiredService<VibixSyncService>();

            try
            {
                logger.LogInformation(
                    "Ручная синхронизация Vibix запущена в фоне");

                var result =
                    await syncService.SyncAsync(CancellationToken.None);

                logger.LogInformation(
                    "Ручная синхронизация завершена: read={Read}, " +
                    "created={Created}, updated={Updated}, skipped={Skipped}",
                    result.Read,
                    result.Created,
                    result.Updated,
                    result.Skipped);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ручная синхронизация упала");
            }
            finally
            {
                Interlocked.Exchange(ref _syncInProgress, 0);
            }
        });

        return Ok(new
        {
            started = true,
            message = "Синхронизация запущена в фоне."
        });
    }

    [HttpPost("enrich")]
    public IActionResult Enrich()
    {
        if (!Authorized())
            return Unauthorized(new
            {
                message = "Укажите ключ администратора в X-Admin-Key."
            });

        if (Interlocked.CompareExchange(ref _enrichInProgress, 1, 0) == 1)
            return Ok(new
            {
                started = false,
                message = "Докачка уже выполняется."
            });

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var syncService =
                scope.ServiceProvider.GetRequiredService<VibixSyncService>();

            try
            {
                logger.LogInformation(
                    "Ручная докачка IframeVideoId запущена в фоне");

                var result =
                    await syncService.EnrichIframeIdsAsync(
                        CancellationToken.None);

                logger.LogInformation(
                    "Ручная докачка завершена: processed={Processed}, " +
                    "updated={Updated}, notFound={NotFound}, error={Error}",
                    result.Read,
                    result.Updated,
                    result.Skipped,
                    result.Error);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ручная докачка упала");
            }
            finally
            {
                Interlocked.Exchange(ref _enrichInProgress, 0);
            }
        });

        return Ok(new
        {
            started = true,
            message = "Докачка IframeVideoId запущена в фоне."
        });
    }

    /// <summary>
    /// Точечное обновление одной записи по Vibix ID.
    /// По умолчанию проверяется страница 3 каталога.
    /// POST /api/admin/enrich-vibix?vibixId=1395397&page=3
    /// </summary>
    [HttpPost("enrich-vibix")]
    public async Task<IActionResult> EnrichByVibixId(
        [FromQuery] string vibixId,
        [FromQuery] int page = 3,
        CancellationToken ct = default)
    {
        if (!Authorized())
            return Unauthorized(new
            {
                message = "Укажите ключ администратора в X-Admin-Key."
            });

        vibixId = vibixId?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(vibixId))
            return BadRequest(new
            {
                message = "Укажите ?vibixId=1395397."
            });

        if (!int.TryParse(vibixId, out var parsedId) || parsedId <= 0)
            return BadRequest(new
            {
                message = "Vibix ID должен быть положительным числом."
            });

        if (page < 1 || page > 5000)
            return BadRequest(new
            {
                message = "Номер страницы должен быть от 1 до 5000."
            });

        try
        {
            // Загружаем только одну страницу каталога.
            // Полная синхронизация не запускается.
            var batch = await vibix.GetVideosPageAsync(page, ct);

            if (batch.RateLimited)
                return StatusCode(429, new
                {
                    message = "Vibix ограничил частоту запросов. " +
                              "Повторите запрос позже."
                });

            var video = batch.Items.FirstOrDefault(
                v => v.Id == parsedId);

            if (video is null)
            {
                return NotFound(new
                {
                    found = false,
                    vibixId,
                    page,
                    message = "Запись с таким Vibix ID не найдена " +
                              "на указанной странице. Проверьте номер страницы."
                });
            }

            var iframeId = video.IframeVideoId ?? video.EmbedVideoId;
            var playerUrl = NormalizeHttpsUrl(video.IframeUrl);

            if (iframeId is null or <= 0)
            {
                return Ok(new
                {
                    found = true,
                    vibixId,
                    page,
                    title = video.NameRus ?? video.Name,
                    type = video.Type,
                    iframeVideoId = (int?)null,
                    playerUrl,
                    embedCode = video.EmbedCode,
                    updated = false,
                    message = "Запись найдена, но ID плеера отсутствует " +
                              "и в iframe_video_id, и в embed_code."
                });
            }

            var isSeries = string.Equals(
                video.Type,
                "serial",
                StringComparison.OrdinalIgnoreCase);

            if (isSeries)
            {
                var series = await db.Series.FirstOrDefaultAsync(
                    s => s.VibixId == vibixId,
                    ct);

                if (series is null)
                    return NotFound(new
                    {
                        found = true,
                        vibixId,
                        iframeVideoId = iframeId,
                        message = "Запись найдена в Vibix, но соответствующий " +
                                  "сериал не найден в локальной базе данных."
                    });

                series.IframeVideoId = iframeId;

                if (playerUrl is not null)
                    series.PlayerUrl = playerUrl;

                series.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                return Ok(new
                {
                    found = true,
                    updated = true,
                    type = "series",
                    id = series.Id,
                    title = series.Title,
                    vibixId = series.VibixId,
                    iframeVideoId = series.IframeVideoId,
                    playerUrl = series.PlayerUrl,
                    message = $"Сериал обновлён. IframeVideoId={iframeId}."
                });
            }
            else
            {
                var movie = await db.Movies.FirstOrDefaultAsync(
                    m => m.VibixId == vibixId,
                    ct);

                if (movie is null)
                    return NotFound(new
                    {
                        found = true,
                        vibixId,
                        iframeVideoId = iframeId,
                        message = "Запись найдена в Vibix, но соответствующий " +
                                  "фильм не найден в локальной базе данных."
                    });

                movie.IframeVideoId = iframeId;

                if (playerUrl is not null)
                    movie.PlayerUrl = playerUrl;

                movie.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                return Ok(new
                {
                    found = true,
                    updated = true,
                    type = "movie",
                    id = movie.Id,
                    title = movie.Title,
                    vibixId = movie.VibixId,
                    iframeVideoId = movie.IframeVideoId,
                    playerUrl = movie.PlayerUrl,
                    message = $"Фильм обновлён. IframeVideoId={iframeId}."
                });
            }
        }
        catch (RateLimitedException ex)
        {
            return StatusCode(429, new
            {
                message = $"Vibix вернул ограничение частоты запросов: {ex.Message}"
            });
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "Ошибка запроса Vibix для ID {VibixId}, страница {Page}",
                vibixId,
                page);

            return StatusCode(502, new
            {
                message = "Не удалось получить данные из Vibix. " +
                          "Подробности доступны в логах API."
            });
        }
    }

    /// <summary>
    /// Старая точечная докачка по Kinopoisk ID.
    /// POST /api/admin/enrich-one?kinopoiskId=1142153
    /// </summary>
    [HttpPost("enrich-one")]
    public async Task<IActionResult> EnrichOne(
        [FromQuery] int kinopoiskId,
        CancellationToken ct)
    {
        if (!Authorized())
            return Unauthorized(new
            {
                message = "Укажите ключ администратора в X-Admin-Key."
            });

        if (kinopoiskId <= 0)
            return BadRequest(new
            {
                message = "Укажите ?kinopoiskId=..."
            });

        var movie = await db.Movies.FirstOrDefaultAsync(
            m => m.KinopoiskId == kinopoiskId,
            ct);

        if (movie != null)
        {
            try
            {
                var iframeId =
                    await vibix.GetIframeVideoIdByKpAsync(kinopoiskId, ct);

                if (iframeId is null or 0)
                    return Ok(new
                    {
                        found = true,
                        type = "movie",
                        title = movie.Title,
                        iframeVideoId = (int?)null,
                        message = "Vibix не вернул iframe_video_id."
                    });

                movie.IframeVideoId = iframeId;
                movie.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                return Ok(new
                {
                    found = true,
                    type = "movie",
                    title = movie.Title,
                    iframeVideoId = iframeId,
                    message = $"Обновлено. Id={movie.Id}, IframeVideoId={iframeId}."
                });
            }
            catch (RateLimitedException ex)
            {
                return StatusCode(429, new
                {
                    message = $"Vibix вернул 429: {ex.Message}"
                });
            }
        }

        var series = await db.Series.FirstOrDefaultAsync(
            s => s.KinopoiskId == kinopoiskId,
            ct);

        if (series != null)
        {
            try
            {
                var iframeId =
                    await vibix.GetIframeVideoIdByKpAsync(kinopoiskId, ct);

                if (iframeId is null or 0)
                    return Ok(new
                    {
                        found = true,
                        type = "series",
                        title = series.Title,
                        iframeVideoId = (int?)null,
                        message = "Vibix не вернул iframe_video_id."
                    });

                series.IframeVideoId = iframeId;
                series.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                return Ok(new
                {
                    found = true,
                    type = "series",
                    title = series.Title,
                    iframeVideoId = iframeId,
                    message = $"Обновлено. Id={series.Id}, IframeVideoId={iframeId}."
                });
            }
            catch (RateLimitedException ex)
            {
                return StatusCode(429, new
                {
                    message = $"Vibix вернул 429: {ex.Message}"
                });
            }
        }

        return NotFound(new
        {
            message = $"Ни фильм, ни сериал с KinopoiskId={kinopoiskId} не найден в БД."
        });
    }

    private static string? NormalizeHttpsUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri) &&
               uri.Scheme.Equals(
                   Uri.UriSchemeHttps,
                   StringComparison.OrdinalIgnoreCase)
            ? uri.ToString()
            : null;
    }
}