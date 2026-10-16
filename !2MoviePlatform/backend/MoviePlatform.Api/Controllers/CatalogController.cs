using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoviePlatform.Infrastructure;

namespace MoviePlatform.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet("movies")]
    public async Task<IActionResult> Movies(
        [FromQuery] string? q = null,
        [FromQuery] string? category = null,
        [FromQuery] string? genre = null,
        [FromQuery] int? year = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 24;

        var query = db.Movies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(m =>
                EF.Functions.ILike(m.Title, pattern) ||
                (m.OriginalTitle != null && EF.Functions.ILike(m.OriginalTitle, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "Все")
            query = query.Where(m => m.Category == category);

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(m => m.GenresText != null && EF.Functions.ILike(m.GenresText, $"%{genre}%"));

        if (year.HasValue)
            query = query.Where(m => m.Year == year.Value);

        query = sort switch
        {
            "year_desc" => query.OrderByDescending(m => m.Year ?? 0),
            "year_asc"  => query.OrderBy(m => m.Year ?? 0),
            "rating"    => query.OrderByDescending(m => m.Rating ?? 0),
            "title"     => query.OrderBy(m => m.Title),
            _           => query.OrderByDescending(m => m.Rating ?? 0).ThenByDescending(m => m.Year ?? 0)
        };

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id, m.Title, m.OriginalTitle, m.Year,
                m.PosterUrl, m.BackdropUrl, m.Rating, m.KpRating, m.ImdbRating,
                m.Category, m.GenresText, m.Country, m.Quality,
                hasPlayer = m.IframeVideoId != null || m.PlayerUrl != null
            })
            .ToListAsync(ct);

        return Ok(new
        {
            total, page, pageSize,
            pages = (int)Math.Ceiling(total / (double)pageSize),
            items
        });
    }

    [HttpGet("movies/{id:int}")]
    public async Task<IActionResult> MovieById(int id, CancellationToken ct)
    {
        var movie = await db.Movies.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new
            {
                m.Id, m.Title, m.OriginalTitle, m.Overview, m.Year,
                m.PosterUrl, m.BackdropUrl, m.Rating, m.KpRating, m.KpVotes,
                m.ImdbRating, m.ImdbVotes, m.Category, m.GenresText,
                m.Country, m.Quality, m.VoicesText, m.Duration,
                m.IframeVideoId, m.PlayerUrl, m.KinopoiskId, m.ImdbId, m.VibixId,
                contentType = "movie"
            })
            .FirstOrDefaultAsync(ct);

        if (movie is null) return NotFound(new { message = "Фильм не найден" });
        return Ok(movie);
    }

    [HttpGet("top")]
    public async Task<IActionResult> Top(
        [FromQuery] int limit = 10,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        if (limit < 1 || limit > 50) limit = 10;

        var query = db.Movies.AsNoTracking()
            .Where(m => m.Rating != null && m.Rating > 0 && m.PosterUrl != null);

        if (!string.IsNullOrWhiteSpace(category) && category != "Все")
            query = query.Where(m => m.Category == category);

        var items = await query
            .OrderByDescending(m => m.Rating).ThenByDescending(m => m.Year ?? 0)
            .Take(limit)
            .Select(m => new
            {
                m.Id, m.Title, m.OriginalTitle, m.Year,
                m.PosterUrl, m.Rating, m.KpRating, m.ImdbRating,
                m.Category, m.GenresText
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        var counts = await db.Movies.AsNoTracking()
            .GroupBy(m => m.Category)
            .Select(g => new { category = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync(ct);

        return Ok(counts);
    }

    [HttpGet("series")]
    public async Task<IActionResult> Series(
        [FromQuery] string? q = null,
        [FromQuery] string? category = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 24;

        var query = db.Series.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Title, pattern) ||
                (s.OriginalTitle != null && EF.Functions.ILike(s.OriginalTitle, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(category) && category != "Все")
            query = query.Where(s => s.Category == category);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.Rating ?? 0).ThenByDescending(s => s.Year ?? 0)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.Title, s.OriginalTitle, s.Year,
                s.PosterUrl, s.BackdropUrl, s.Rating, s.KpRating, s.ImdbRating,
                s.Category, s.GenresText, s.Country,
                hasPlayer = s.IframeVideoId != null || s.PlayerUrl != null
            })
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("series/{id:int}")]
    public async Task<IActionResult> SeriesById(int id, CancellationToken ct)
    {
        var series = await db.Series.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id, s.Title, s.OriginalTitle, s.Overview, s.Year,
                s.PosterUrl, s.BackdropUrl, s.Rating, s.KpRating, s.KpVotes,
                s.ImdbRating, s.ImdbVotes, s.Category, s.GenresText,
                s.Country, s.Quality, s.VoicesText, s.IframeVideoId, s.PlayerUrl,
                s.KinopoiskId, s.VibixId,
                contentType = "series"
            })
            .FirstOrDefaultAsync(ct);

        if (series is null) return NotFound(new { message = "Сериал не найден" });

        var seasons = await db.Seasons.AsNoTracking()
            .Where(s => s.SeriesId == id)
            .OrderBy(s => s.Number)
            .Select(s => new
            {
                s.Number,
                episodes = s.Episodes
                    .OrderBy(e => e.Number)
                    .Select(e => new { e.Number, e.Title, e.SourcesJson })
                    .ToList()
            })
            .ToListAsync(ct);

        // Преобразуем sourcesJson в словарь.
        var seasonsOut = seasons.Select(s => new
        {
            s.Number,
            episodes = s.episodes.Select(e => new
            {
                e.Number,
                e.Title,
                sources = string.IsNullOrEmpty(e.SourcesJson)
                    ? new Dictionary<string, int>()
                    : JsonSerializer.Deserialize<Dictionary<string, int>>(e.SourcesJson) ?? new()
            }).ToList()
        }).ToList();

        return Ok(new { series, seasons = seasonsOut });
    }
}