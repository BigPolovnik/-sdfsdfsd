import { Link } from 'react-router-dom';
import type { MovieListItem } from '../api/client';

type Props = { movie: MovieListItem; index?: number; kind?: 'movie' | 'series' };

export function MovieCard({ movie, index = 0, kind = 'movie' }: Props) {
  const to = kind === 'series' ? `/series/${movie.id}` : `/movie/${movie.id}`;
  return (
    <Link to={to} className="movie-card">
      <div className={`poster poster-${index % 6}`}>
        {movie.posterUrl
          ? <img src={movie.posterUrl} alt={movie.title} loading="lazy" />
          : (
            <div className="poster-art">
              <span className="poster-index">{String(index + 1).padStart(2, '0')}</span>
              <span className="poster-symbol">✳</span>
              <span className="poster-caption">{(movie.category || 'КИНО').toUpperCase()}</span>
            </div>
          )}
        <span className="card-play">▶</span>
        {movie.quality && <span className="quality-tag">{movie.quality}</span>}
      </div>
      <div className="card-meta">
        <span>{movie.year || '—'}{movie.category ? ` · ${movie.category}` : ''}</span>
        {movie.rating != null && <span className="rating">★ {movie.rating.toFixed(1)}</span>}
      </div>
      <h3>{movie.title}</h3>
      <p>{movie.genresText || movie.originalTitle || '—'}</p>
    </Link>
  );
}