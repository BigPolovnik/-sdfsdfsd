using Microsoft.EntityFrameworkCore;
using MoviePlatform.Domain;

namespace MoviePlatform.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Episode> Episodes => Set<Episode>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<Voice> Voices => Set<Voice>();
    public DbSet<Balancer> Balancers => Set<Balancer>();
    public DbSet<MediaSource> MediaSources => Set<MediaSource>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WatchHistory> WatchHistory => Set<WatchHistory>();
    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<MovieGenre>().HasKey(x => new { x.MovieId, x.GenreId });
        b.Entity<MovieGenre>()
            .HasOne(x => x.Movie).WithMany(x => x.Genres)
            .HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MovieGenre>()
            .HasOne(x => x.Genre).WithMany(x => x.Movies)
            .HasForeignKey(x => x.GenreId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Season>().HasIndex(x => new { x.SeriesId, x.Number }).IsUnique();
        b.Entity<Episode>().HasIndex(x => new { x.SeasonId, x.Number }).IsUnique();
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();

        b.Entity<MediaSource>()
            .HasOne(x => x.Movie).WithMany(x => x.MediaSources)
            .HasForeignKey(x => x.MovieId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<MediaSource>()
            .HasOne(x => x.Episode).WithMany(x => x.MediaSources)
            .HasForeignKey(x => x.EpisodeId).OnDelete(DeleteBehavior.Cascade);
    }
}
