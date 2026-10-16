
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, MovieDetail } from '../api/client';
import { VibixPlayer } from '../components/VibixPlayer';

export function MoviePage() {
  const { id } = useParams();

  const [movie, setMovie] = useState<MovieDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!id) {
      setMovie(null);
      setError('Не указан идентификатор фильма.');
      setLoading(false);
      return;
    }

    let cancelled = false;

    setLoading(true);
    setError('');
    setMovie(null);

    api
      .movie(Number(id))
      .then((data) => {
        if (!cancelled) {
          setMovie(data);
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) {
          setError(
            e instanceof Error ? e.message : 'Не удалось загрузить фильм.',
          );
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [id]);

  if (loading) {
    return (
      <div className="catalog-section">
        <div className="skeleton big" />
      </div>
    );
  }

  if (error || !movie) {
    return (
      <div className="catalog-section">
        <div className="empty-state">
          <h3>Фильм не найден</h3>
          <p>{error || 'Информация о фильме недоступна.'}</p>
          <Link to="/catalog" className="primary-button">
            В каталог
          </Link>
        </div>
      </div>
    );
  }

  const backgroundStyle = movie.backdropUrl
    ? {
        backgroundImage: `linear-gradient(0deg, #0b0b0a 0%, rgba(11,11,10,0.6) 40%, transparent 100%), url("${movie.backdropUrl}")`,
      }
    : {};

  return (
    <section className="movie-page">
      <div className="movie-hero" style={backgroundStyle}>
        <div className="movie-hero-inner">
          {movie.posterUrl && (
            <img
              className="movie-poster"
              src={movie.posterUrl}
              alt={movie.title}
            />
          )}

          <div className="movie-info">
            <div className="overline muted">
              {movie.category || 'КИНО'}
              {movie.year ? ` · ${movie.year}` : ''}
            </div>

            <h1>{movie.title}</h1>

            {movie.originalTitle && (
              <p className="original-title">{movie.originalTitle}</p>
            )}

            <div className="detail-chips">
              {movie.kpRating != null && (
                <span className="chip kp">
                  КП ★ {movie.kpRating.toFixed(1)}
                </span>
              )}

              {movie.imdbRating != null && (
                <span className="chip imdb">
                  IMDb ★ {movie.imdbRating.toFixed(1)}
                </span>
              )}

              {movie.quality && (
                <span className="chip">{movie.quality}</span>
              )}

              {movie.country && (
                <span className="chip">{movie.country}</span>
              )}

              {movie.duration != null && (
                <span className="chip">{movie.duration} мин</span>
              )}
            </div>

            {movie.genresText && (
              <p className="genres">
                <b>Жанры:</b> {movie.genresText}
              </p>
            )}

            {movie.overview && (
              <p className="overview">{movie.overview}</p>
            )}

            {movie.voicesText && (
              <p className="voices">
                <b>Озвучки:</b> {movie.voicesText}
              </p>
            )}
          </div>
        </div>
      </div>

      <div className="player-section">
        <VibixPlayer
          videoId={movie.iframeVideoId}
          imdbId={movie.imdbId}
          playerUrl={movie.playerUrl}
          type={movie.contentType === 'series' ? 'series' : 'movie'}
          design={1}
        />
      </div>
    </section>
  );
}
