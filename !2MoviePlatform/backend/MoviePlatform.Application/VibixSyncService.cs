using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoviePlatform.Domain;
using MoviePlatform.Infrastructure;

namespace MoviePlatform.Application;

public sealed class VibixSyncService
{
    private readonly AppDbContext _db;
    private readonly VibixCatalogService _vibix;
    private readonly VibixOptions _options;
    private readonly ILogger<VibixSyncService> _logger;

    public VibixSyncService(
        AppDbContext db,
        VibixCatalogService vibix,
        IOptions<VibixOptions> options,
        ILogger<VibixSyncService> logger)
    {
        _db = db;
        _vibix = vibix;
        _options = options.Value;
        _logger = logger;
    }

    // ============================================================
    // ПРОХОД 1: загрузка каталога и сохранение метаданных
    // ============================================================
    public async Task<VibixSyncResult> SyncAsync(
        CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var read = 0;
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var page = 1;

        var requestedPageSize = _options.PageSize > 0
            ? _options.PageSize
            : 50;

        // Vibix API поддерживает limit от 20 до 100.
        var pageSize = Math.Clamp(requestedPageSize, 20, 100);

        try
        {
            if (string.IsNullOrWhiteSpace(_options.ApiToken))
            {
                return new VibixSyncResult(
                    0, 0, 0, 0,
                    "VIBIX_API_TOKEN не задан.",
                    DateTime.UtcNow);
            }

            _logger.LogInformation(
                "Постраничная синхронизация Vibix началась. Размер страницы: {PageSize}",
                pageSize);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var batch = await _vibix.GetVideosPageAsync(page, ct);

                if (batch.RateLimited)
                {
                    var error =
                        $"Vibix ограничил частоту запросов на странице {page}. " +
                        "Уже сохранённые страницы оставлены в базе; " +
                        "повторите синхронизацию позже.";

                    _logger.LogWarning(error);

                    return new VibixSyncResult(
                        read, created, updated, skipped,
                        error,
                        DateTime.UtcNow);
                }

                var videos = batch.Items;

                if (videos.Count == 0)
                {
                    if (page == 1)
                    {
                        _logger.LogWarning(
                            "Vibix вернул пустую первую страницу каталога.");
                    }

                    break;
                }

                var movieIds = videos
                    .Where(v => !string.Equals(
                        v.Type, "serial",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(v => v.Id.ToString())
                    .Distinct()
                    .ToList();

                var seriesIds = videos
                    .Where(v => string.Equals(
                        v.Type, "serial",
                        StringComparison.OrdinalIgnoreCase))
                    .Select(v => v.Id.ToString())
                    .Distinct()
                    .ToList();

                var movies = await _db.Movies
                    .Where(m =>
                        m.VibixId != null &&
                        movieIds.Contains(m.VibixId))
                    .ToDictionaryAsync(m => m.VibixId!, ct);

                var series = await _db.Series
                    .Where(s =>
                        s.VibixId != null &&
                        seriesIds.Contains(s.VibixId))
                    .Include(s => s.Seasons)
                    .ThenInclude(season => season.Episodes)
                    .ToDictionaryAsync(s => s.VibixId!, ct);

                foreach (var video in videos)
                {
                    ct.ThrowIfCancellationRequested();
                    read++;

                    var id = video.Id.ToString();

                    if (string.Equals(
                        video.Type,
                        "serial",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        var incoming = MapSeries(video);

                        if (series.TryGetValue(id, out var existing))
                        {
                            CopySeriesFields(incoming, existing);

                            // Не удаляем сохранённые серии, если API
                            // не прислал данные об эпизодах.
                            if (incoming.Seasons.Count > 0)
                            {
                                UpdateSeasons(
                                    existing,
                                    incoming.Seasons);
                            }

                            updated++;
                        }
                        else
                        {
                            await _db.Series.AddAsync(incoming, ct);
                            series[id] = incoming;
                            created++;
                        }
                    }
                    else
                    {
                        var incoming = MapMovie(video);

                        if (movies.TryGetValue(id, out var existing))
                        {
                            CopyMovieFields(incoming, existing);
                            updated++;
                        }
                        else
                        {
                            await _db.Movies.AddAsync(incoming, ct);
                            movies[id] = incoming;
                            created++;
                        }
                    }
                }

                // Сохраняем каждую страницу отдельно.
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();

                _logger.LogInformation(
                    "Импорт Vibix: страница {Page} сохранена; " +
                    "прочитано={Read}, добавлено={Created}, обновлено={Updated}",
                    page,
                    read,
                    created,
                    updated);

                if (videos.Count < pageSize)
                    break;

                page++;

                if (page > 5000)
                {
                    _logger.LogWarning(
                        "Импорт остановлен на защитном лимите 5000 страниц.");
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }

            _logger.LogInformation(
                "Синхронизация Vibix завершена: прочитано={Read}, " +
                "создано={Created}, обновлено={Updated}, " +
                "пропущено={Skipped}, время={Elapsed:F1} сек",
                read,
                created,
                updated,
                skipped,
                (DateTime.UtcNow - started).TotalSeconds);

            return new VibixSyncResult(
                read, created, updated, skipped, null, DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Синхронизация Vibix остановлена. " +
                "Уже сохранённые страницы остаются в базе.");

            return new VibixSyncResult(
                read, created, updated, skipped, null, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Синхронизация Vibix завершилась с ошибкой после страницы {Page}; " +
                "уже сохранённые страницы остаются в базе",
                page);

            return new VibixSyncResult(
                read, created, updated, skipped, ex.Message, DateTime.UtcNow);
        }
    }

    // ============================================================
    // ПРОХОД 2: докачка ID плеера по Kinopoisk ID
    // ============================================================
    public async Task<VibixSyncResult> EnrichIframeIdsAsync(
        CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var processed = 0;
        var updated = 0;
        var notFound = 0;
        var rateLimited = 0;

        try
        {
            var moviesWithoutId = await _db.Movies
                .Where(m =>
                    m.IframeVideoId == null &&
                    m.KinopoiskId != null)
                .OrderBy(m => m.Id)
                .ToListAsync(ct);

            _logger.LogInformation(
                "Докачка IframeVideoId: {Count} фильмов без плеера",
                moviesWithoutId.Count);

            foreach (var movie in moviesWithoutId)
            {
                ct.ThrowIfCancellationRequested();
                processed++;

                try
                {
                    var iframeId = await _vibix.GetIframeVideoIdByKpAsync(
                        movie.KinopoiskId!.Value,
                        ct);

                    if (iframeId is null or <= 0)
                    {
                        notFound++;
                    }
                    else
                    {
                        movie.IframeVideoId = iframeId;
                        movie.UpdatedAt = DateTime.UtcNow;
                        updated++;
                    }
                }
                catch (RateLimitedException ex)
                {
                    _logger.LogWarning(
                        "Докачка фильмов остановлена: HTTP 429. {Reason}",
                        ex.Message);

                    rateLimited = 1;
                    break;
                }

                if (updated > 0 && updated % 100 == 0)
                {
                    await _db.SaveChangesAsync(ct);

                    _logger.LogInformation(
                        "Докачка фильмов: обработано {Processed}/{Total}, " +
                        "обновлено {Updated}, не найдено {NotFound}",
                        processed,
                        moviesWithoutId.Count,
                        updated,
                        notFound);
                }
            }

            await _db.SaveChangesAsync(ct);

            // Сериалы обрабатываем только в случае,
            // если запросы для фильмов не остановлены из-за HTTP 429.
            if (rateLimited == 0)
            {
                var seriesWithoutId = await _db.Series
                    .Where(s =>
                        s.IframeVideoId == null &&
                        s.KinopoiskId != null)
                    .OrderBy(s => s.Id)
                    .ToListAsync(ct);

                if (seriesWithoutId.Count > 0)
                {
                    _logger.LogInformation(
                        "Докачка IframeVideoId: {Count} сериалов без плеера",
                        seriesWithoutId.Count);

                    var seriesProcessed = 0;
                    var seriesUpdated = 0;
                    var seriesNotFound = 0;

                    foreach (var item in seriesWithoutId)
                    {
                        ct.ThrowIfCancellationRequested();
                        seriesProcessed++;
                        processed++;

                        try
                        {
                            var iframeId =
                                await _vibix.GetIframeVideoIdByKpAsync(
                                    item.KinopoiskId!.Value,
                                    ct);

                            if (iframeId is null or <= 0)
                            {
                                seriesNotFound++;
                                notFound++;
                            }
                            else
                            {
                                item.IframeVideoId = iframeId;
                                item.UpdatedAt = DateTime.UtcNow;
                                seriesUpdated++;
                                updated++;
                            }
                        }
                        catch (RateLimitedException ex)
                        {
                            _logger.LogWarning(
                                "Докачка сериалов остановлена: HTTP 429. {Reason}",
                                ex.Message);

                            rateLimited = 1;
                            break;
                        }

                        if (seriesUpdated > 0 && seriesUpdated % 100 == 0)
                        {
                            await _db.SaveChangesAsync(ct);

                            _logger.LogInformation(
                                "Докачка сериалов: обработано {Processed}/{Total}, " +
                                "обновлено {Updated}, не найдено {NotFound}",
                                seriesProcessed,
                                seriesWithoutId.Count,
                                seriesUpdated,
                                seriesNotFound);
                        }
                    }

                    await _db.SaveChangesAsync(ct);

                    _logger.LogInformation(
                        "Докачка сериалов завершена: обработано {Processed}, " +
                        "обновлено {Updated}, не найдено {NotFound}",
                        seriesProcessed,
                        seriesUpdated,
                        seriesNotFound);
                }
            }

            _logger.LogInformation(
                "Докачка завершена: обработано {Processed}, " +
                "обновлено {Updated}, не найдено {NotFound}, " +
                "429={RateLimited}, за {Elapsed:F1} с",
                processed,
                updated,
                notFound,
                rateLimited,
                (DateTime.UtcNow - started).TotalSeconds);

            return new VibixSyncResult(
                processed,
                0,
                updated,
                notFound,
                rateLimited == 1
                    ? "Vibix вернул HTTP 429. Повторите докачку позже."
                    : null,
                DateTime.UtcNow);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Докачка отменена.");

            return new VibixSyncResult(
                processed, 0, updated, notFound, null, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Докачка ID плееров завершилась с ошибкой.");

            return new VibixSyncResult(
                processed, 0, updated, notFound, ex.Message, DateTime.UtcNow);
        }
    }

    // ============================================================
    // МАППИНГ КАТАЛОГА
    // ============================================================

    private static Movie MapMovie(VibixVideoDto v) => new()
    {
        VibixId = v.Id.ToString(),
        KinopoiskId = v.KpId ?? v.KinopoiskIdLegacy,
        ImdbId = ParseImdbId(v.ImdbId),
        Title = FirstNonEmpty(
            v.NameRus, v.Name, v.NameEng, v.NameOriginal) ?? "Без названия",
        OriginalTitle = FirstNonEmpty(
            v.NameOriginal, v.NameEng, v.Name),
        Year = v.Year,
        Category = DetermineCategory(v, isSerial: false),

        // Сначала используем специальное поле API,
        // затем извлекаем data-id из embed_code.
        IframeVideoId = GetPlayerId(v),
        PlayerUrl = NormalizeHttpsUrl(v.IframeUrl),

        PosterUrl = v.PosterUrl,
        BackdropUrl = v.BackdropUrl,
        Overview = FirstNonEmpty(v.Description, v.DescriptionShort),
        KpRating = v.KpRating,
        KpVotes = v.KpVotes,
        ImdbRating = v.ImdbRating,
        ImdbVotes = v.ImdbVotes,
        Rating = v.KpRating ?? v.ImdbRating,
        GenresText = v.GenreList.Count > 0
            ? string.Join(", ", v.GenreList)
            : null,
        Country = v.CountryList.Count > 0
            ? string.Join(", ", v.CountryList)
            : null,
        Quality = v.Quality,
        Duration = v.Duration,
        VoicesText = v.Voiceovers is { Count: > 0 }
            ? string.Join(
                ", ",
                v.Voiceovers
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .Select(x => x.Name))
            : null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Series MapSeries(VibixVideoDto v)
    {
        var s = new Series
        {
            VibixId = v.Id.ToString(),
            KinopoiskId = v.KpId ?? v.KinopoiskIdLegacy,
            Title = FirstNonEmpty(
                v.NameRus, v.Name, v.NameEng, v.NameOriginal) ?? "Без названия",
            OriginalTitle = FirstNonEmpty(
                v.NameOriginal, v.NameEng, v.Name),
            Year = v.Year,
            Category = DetermineCategory(v, isSerial: true),

            IframeVideoId = GetPlayerId(v),
            PlayerUrl = NormalizeHttpsUrl(v.IframeUrl),

            PosterUrl = v.PosterUrl,
            BackdropUrl = v.BackdropUrl,
            Overview = FirstNonEmpty(v.Description, v.DescriptionShort),
            KpRating = v.KpRating,
            KpVotes = v.KpVotes,
            ImdbRating = v.ImdbRating,
            ImdbVotes = v.ImdbVotes,
            Rating = v.KpRating ?? v.ImdbRating,
            GenresText = v.GenreList.Count > 0
                ? string.Join(", ", v.GenreList)
                : null,
            Country = v.CountryList.Count > 0
                ? string.Join(", ", v.CountryList)
                : null,
            Quality = v.Quality,
            VoicesText = v.Voiceovers is { Count: > 0 }
                ? string.Join(
                    ", ",
                    v.Voiceovers
                        .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                        .Select(x => x.Name))
                : null,
            UpdatedAt = DateTime.UtcNow
        };

        if (v.Episodes is { Count: > 0 })
        {
            foreach (var (seasonKey, episodesDict) in v.Episodes)
            {
                var seasonNumber =
                    ParseNumberAfterPrefix(seasonKey, 's') ?? 0;

                var season = new Season
                {
                    Number = seasonNumber
                };

                foreach (var (episodeKey, voicesDict) in episodesDict)
                {
                    var episodeNumber =
                        ParseNumberAfterPrefix(episodeKey, 'e') ?? 0;

                    var sources = voicesDict
                        .Where(kv => kv.Key.StartsWith(
                            "v",
                            StringComparison.OrdinalIgnoreCase))
                        .ToDictionary(
                            kv => kv.Key.Substring(1),
                            kv => kv.Value);

                    season.Episodes.Add(new Episode
                    {
                        Number = episodeNumber,
                        Title = $"Серия {episodeNumber}",
                        SourcesJson = JsonSerializer.Serialize(sources)
                    });
                }

                s.Seasons.Add(season);
            }
        }

        return s;
    }

    private static int? GetPlayerId(VibixVideoDto video)
    {
        if (video.IframeVideoId is > 0)
            return video.IframeVideoId;

        if (video.EmbedVideoId is > 0)
            return video.EmbedVideoId;

        return null;
    }

    private static void UpdateSeasons(
        Series existing,
        List<Season> incoming)
    {
        if (incoming.Count == 0)
            return;

        var incomingNumbers = incoming
            .Select(s => s.Number)
            .ToHashSet();

        foreach (var season in existing.Seasons.ToList())
        {
            if (!incomingNumbers.Contains(season.Number))
                existing.Seasons.Remove(season);
        }

        foreach (var incomingSeason in incoming)
        {
            var existingSeason = existing.Seasons
                .FirstOrDefault(s => s.Number == incomingSeason.Number);

            if (existingSeason is null)
            {
                existing.Seasons.Add(incomingSeason);
                continue;
            }

            var incomingEpisodeNumbers = incomingSeason.Episodes
                .Select(e => e.Number)
                .ToHashSet();

            foreach (var episode in existingSeason.Episodes.ToList())
            {
                if (!incomingEpisodeNumbers.Contains(episode.Number))
                    existingSeason.Episodes.Remove(episode);
            }

            foreach (var incomingEpisode in incomingSeason.Episodes)
            {
                var existingEpisode = existingSeason.Episodes
                    .FirstOrDefault(e => e.Number == incomingEpisode.Number);

                if (existingEpisode is null)
                {
                    existingSeason.Episodes.Add(incomingEpisode);
                }
                else
                {
                    existingEpisode.Title = incomingEpisode.Title;
                    existingEpisode.SourcesJson = incomingEpisode.SourcesJson;
                }
            }
        }
    }

    private static void CopyMovieFields(Movie from, Movie to)
    {
        to.KinopoiskId = from.KinopoiskId ?? to.KinopoiskId;
        to.ImdbId = from.ImdbId ?? to.ImdbId;
        to.Title = from.Title;
        to.OriginalTitle = from.OriginalTitle ?? to.OriginalTitle;
        to.Year = from.Year ?? to.Year;
        to.Category = from.Category;

        // Не стираем рабочий ID плеера пустым значением API.
        to.IframeVideoId = from.IframeVideoId ?? to.IframeVideoId;
        to.PlayerUrl = from.PlayerUrl ?? to.PlayerUrl;

        to.PosterUrl = from.PosterUrl ?? to.PosterUrl;
        to.BackdropUrl = from.BackdropUrl ?? to.BackdropUrl;
        to.Overview = from.Overview ?? to.Overview;
        to.KpRating = from.KpRating ?? to.KpRating;
        to.KpVotes = from.KpVotes ?? to.KpVotes;
        to.ImdbRating = from.ImdbRating ?? to.ImdbRating;
        to.ImdbVotes = from.ImdbVotes ?? to.ImdbVotes;
        to.Rating = from.Rating ?? to.Rating;
        to.GenresText = from.GenresText ?? to.GenresText;
        to.Country = from.Country ?? to.Country;
        to.Quality = from.Quality ?? to.Quality;
        to.Duration = from.Duration ?? to.Duration;
        to.VoicesText = from.VoicesText ?? to.VoicesText;
        to.UpdatedAt = DateTime.UtcNow;
    }

    private static void CopySeriesFields(Series from, Series to)
    {
        to.KinopoiskId = from.KinopoiskId ?? to.KinopoiskId;
        to.Title = from.Title;
        to.OriginalTitle = from.OriginalTitle ?? to.OriginalTitle;
        to.Year = from.Year ?? to.Year;
        to.Category = from.Category;

        // Не стираем рабочий ID плеера пустым значением API.
        to.IframeVideoId = from.IframeVideoId ?? to.IframeVideoId;
        to.PlayerUrl = from.PlayerUrl ?? to.PlayerUrl;

        to.PosterUrl = from.PosterUrl ?? to.PosterUrl;
        to.BackdropUrl = from.BackdropUrl ?? to.BackdropUrl;
        to.Overview = from.Overview ?? to.Overview;
        to.KpRating = from.KpRating ?? to.KpRating;
        to.KpVotes = from.KpVotes ?? to.KpVotes;
        to.ImdbRating = from.ImdbRating ?? to.ImdbRating;
        to.ImdbVotes = from.ImdbVotes ?? to.ImdbVotes;
        to.Rating = from.Rating ?? to.Rating;
        to.GenresText = from.GenresText ?? to.GenresText;
        to.Country = from.Country ?? to.Country;
        to.Quality = from.Quality ?? to.Quality;
        to.VoicesText = from.VoicesText ?? to.VoicesText;
        to.UpdatedAt = DateTime.UtcNow;
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

    private static string DetermineCategory(
        VibixVideoDto v,
        bool isSerial)
    {
        var genres = string.Join(" ", v.GenreList).ToLowerInvariant();

        if (genres.Contains("аниме") || genres.Contains("anime"))
            return "Аниме";

        if (genres.Contains("мультфильм") ||
            genres.Contains("анимац") ||
            genres.Contains("animation") ||
            genres.Contains("детск"))
        {
            return "Мультфильмы";
        }

        return isSerial ? "Сериалы" : "Фильмы";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(
            s => !string.IsNullOrWhiteSpace(s));
    }

    private static int? ParseImdbId(string? imdb)
    {
        if (string.IsNullOrWhiteSpace(imdb))
            return null;

        var digits = new string(imdb.Where(char.IsDigit).ToArray());

        return int.TryParse(digits, out var value)
            ? value
            : null;
    }

    private static int? ParseNumberAfterPrefix(
        string key,
        char prefix)
    {
        if (string.IsNullOrEmpty(key) || key[0] != prefix)
            return null;

        return int.TryParse(key.AsSpan(1), out var number)
            ? number
            : null;
    }
}
