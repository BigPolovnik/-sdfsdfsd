using System.ComponentModel.DataAnnotations;

namespace MoviePlatform.Domain;

public class Movie
{
    public int Id { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public string? OriginalTitle { get; set; }
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public decimal? Rating { get; set; }
    public decimal? KpRating { get; set; }
    public decimal? ImdbRating { get; set; }
    public int? KpVotes { get; set; }
    public int? ImdbVotes { get; set; }
    public int? TmdbId { get; set; }
    public int? KinopoiskId { get; set; }
    public int? ImdbId { get; set; }
    [MaxLength(120)] public string? VibixId { get; set; }
    public int? IframeVideoId { get; set; }
    public string? PlayerUrl { get; set; }
    public int? Year { get; set; }
    [MaxLength(80)] public string Category { get; set; } = "Фильмы";
    public string? GenresText { get; set; }
    public string? VoicesText { get; set; }
    public string? Country { get; set; }
    public string? Quality { get; set; }
    public int? Duration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public List<MediaSource> MediaSources { get; set; } = [];
    public List<MovieGenre> Genres { get; set; } = [];
}

public class Series
{
    public int Id { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public string? OriginalTitle { get; set; }
    public string? Overview { get; set; }
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public decimal? Rating { get; set; }
    public decimal? KpRating { get; set; }
    public decimal? ImdbRating { get; set; }
    public int? KpVotes { get; set; }
    public int? ImdbVotes { get; set; }
    public int? TmdbId { get; set; }
    public int? KinopoiskId { get; set; }
    [MaxLength(120)] public string? VibixId { get; set; }
    public int? IframeVideoId { get; set; }
    public string? PlayerUrl { get; set; }
    public int? Year { get; set; }
    [MaxLength(80)] public string Category { get; set; } = "Сериалы";
    public string? GenresText { get; set; }
    public string? VoicesText { get; set; }
    public string? Country { get; set; }
    public string? Quality { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<Season> Seasons { get; set; } = [];
}

public class Season
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;
    public int Number { get; set; }
    public List<Episode> Episodes { get; set; } = [];
}

public class Episode
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public Season Season { get; set; } = null!;
    public int Number { get; set; }
    [MaxLength(300)] public string Title { get; set; } = "";
    public string? Overview { get; set; }

    /// <summary>
    /// Словарь озвучек: {"1": 8636, "12": 206687} — voiceover_id -> video_id.
    /// Именно video_id вставляется в data-id плеера.
    /// </summary>
    public string? SourcesJson { get; set; }

    public List<MediaSource> MediaSources { get; set; } = [];
}

public class Genre
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    public List<MovieGenre> Movies { get; set; } = [];
}

public class MovieGenre
{
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public int GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}

public class Voice
{
    public int Id { get; set; }
    [MaxLength(150)] public string Name { get; set; } = "";
}

public class Balancer
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public int Priority { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class MediaSource
{
    public int Id { get; set; }
    public int? MovieId { get; set; }
    public Movie? Movie { get; set; }
    public int? EpisodeId { get; set; }
    public Episode? Episode { get; set; }
    public int BalancerId { get; set; }
    public Balancer Balancer { get; set; } = null!;
    public int? VoiceId { get; set; }
    public Voice? Voice { get; set; }
    [MaxLength(30)] public string Quality { get; set; } = "1080p";
    [MaxLength(20)] public string Type { get; set; } = "hls";
    public string? StreamUrl { get; set; }
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class User
{
    public int Id { get; set; }
    [MaxLength(320)] public string Email { get; set; } = "";
    [MaxLength(200)] public string PasswordHash { get; set; } = "";
    [MaxLength(100)] public string DisplayName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<WatchHistory> History { get; set; } = [];
    public List<Favorite> Favorites { get; set; } = [];
}

public class WatchHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? MovieId { get; set; }
    public int? EpisodeId { get; set; }
    public int PositionSeconds { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Favorite
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int? MovieId { get; set; }
    public int? SeriesId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}