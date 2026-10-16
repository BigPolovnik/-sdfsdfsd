import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { api, SeriesDetail } from '../api/client';
import { VibixPlayer } from '../components/VibixPlayer';

export function SeriesPage() {
  const { id } = useParams();
  const [data, setData] = useState<{ series: SeriesDetail; seasons: SeriesDetail['seasons'] } | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!id) return;
    setLoading(true); setError('');
    api.seriesById(Number(id))
      .then(setData)
      .catch(e => setError(e instanceof Error ? e.message : 'Не найдено'))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) return <div className="catalog-section"><div className="skeleton big" /></div>;
  if (error || !data) return <div className="catalog-section"><div className="empty-state"><h3>Сериал не найден</h3><p>{error}</p><Link to="/catalog" className="primary-button">В каталог</Link></div></div>;

  const { series } = data;

  return (
    <section className="movie-page">
      <div className="movie-hero" style={series.backdropUrl
        ? { backgroundImage: `linear-gradient(0deg, #0b0b0a 0%, rgba(11,11,10,0.6) 40%, transparent 100%), url("${series.backdropUrl}")` }
        : {}}>
        <div className="movie-hero-inner">
          {series.posterUrl && <img className="movie-poster" src={series.posterUrl} alt={series.title} />}
          <div className="movie-info">
            <div className="overline muted">{series.category || 'СЕРИАЛ'}{series.year ? ` · ${series.year}` : ''}</div>
            <h1>{series.title}</h1>
            {series.originalTitle && <p className="original-title">{series.originalTitle}</p>}
            <div className="detail-chips">
              {series.kpRating != null && <span className="chip kp">КП ★ {series.kpRating.toFixed(1)}</span>}
              {series.imdbRating != null && <span className="chip imdb">IMDb ★ {series.imdbRating.toFixed(1)}</span>}
              {series.quality && <span className="chip">{series.quality}</span>}
              {series.country && <span className="chip">{series.country}</span>}
            </div>
            {series.genresText && <p className="genres"><b>Жанры:</b> {series.genresText}</p>}
            {series.overview && <p className="overview">{series.overview}</p>}
          </div>
        </div>
      </div>

      <div className="player-section">
        <VibixPlayer videoId={series.iframeVideoId} playerUrl={series.playerUrl} type="series" />
        <p className="episode-caption">Выбор сезона, серии и озвучки — внутри плеера.</p>
      </div>
    </section>
  );
}