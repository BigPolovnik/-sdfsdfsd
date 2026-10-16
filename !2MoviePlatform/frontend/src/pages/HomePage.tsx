import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api, CategoryCount, MovieListItem } from '../api/client';
import { MovieCard } from '../components/MovieCard';

export function HomePage() {
  const [top, setTop] = useState<MovieListItem[]>([]);
  const [fresh, setFresh] = useState<MovieListItem[]>([]);
  const [series, setSeries] = useState<MovieListItem[]>([]);
  const [cats, setCats] = useState<CategoryCount[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        setLoading(true);
        const [t, m, s, c] = await Promise.all([
          api.top(12),
          api.movies({ sort: 'year_desc', pageSize: 12 }),
          api.series({ pageSize: 12 }),
          api.categories(),
        ]);
        if (cancelled) return;
        setTop(t); setFresh(m.items); setSeries(s.items); setCats(c);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    void load();
    const id = window.setInterval(load, 60_000);
    return () => { cancelled = true; window.clearInterval(id); };
  }, []);

  return (
    <>
      <section className="hero">
        <div className="hero-copy">
          <div className="overline"><span className="live-dot" /> ОНЛАЙН-КИНОТЕАТР <span className="line" /></div>
          <h1>Истории,<br /><em>которые</em> остаются.</h1>
          <p>Большое кино, сериалы и анимация — в одной коллекции.</p>
          <div className="hero-buttons">
            <Link to="/catalog" className="primary-button">Смотреть каталог <span>↗</span></Link>
          </div>
        </div>
      </section>

      <section className="stats-strip">
        <div><strong>{cats.reduce((s, c) => s + c.count, 0) || '—'}</strong><span>МАТЕРИАЛОВ</span></div>
        {cats.slice(0, 3).map(c => (
          <div key={c.category}><strong>{c.count}</strong><span>{c.category.toUpperCase()}</span></div>
        ))}
      </section>

      <section className="catalog-section">
        <div className="section-heading">
          <div><div className="overline muted">ТОП</div><h2>Лучшие по рейтингу<span className="count">{String(top.length).padStart(2, '0')}</span></h2></div>
          <Link to="/catalog?sort=rating" className="secondary-button">Весь топ ↗</Link>
        </div>
        {loading
          ? <div className="movie-grid">{Array.from({ length: 6 }).map((_, i) => <div key={i} className="movie-card skeleton" />)}</div>
          : <div className="movie-grid">{top.map((m, i) => <MovieCard key={m.id} movie={m} index={i} />)}</div>}
      </section>

      <section className="catalog-section">
        <div className="section-heading">
          <div><div className="overline muted">НОВОЕ</div><h2>Свежие поступления<span className="count">{String(fresh.length).padStart(2, '0')}</span></h2></div>
          <Link to="/catalog?sort=year_desc" className="secondary-button">Все новинки ↗</Link>
        </div>
        <div className="movie-grid">{fresh.map((m, i) => <MovieCard key={m.id} movie={m} index={i} />)}</div>
      </section>

      {series.length > 0 && (
        <section className="catalog-section">
          <div className="section-heading">
            <div><div className="overline muted">СЕРИАЛЫ</div><h2>Сериалы<span className="count">{String(series.length).padStart(2, '0')}</span></h2></div>
            <Link to="/catalog?category=Сериалы" className="secondary-button">Все сериалы ↗</Link>
          </div>
          <div className="movie-grid">{series.map((m, i) => <MovieCard key={m.id} movie={m} index={i} kind="series" />)}</div>
        </section>
      )}

      <section className="catalog-section">
        <div className="section-heading">
          <div><div className="overline muted">КАТЕГОРИИ</div><h2>Что смотреть?</h2></div>
        </div>
        <div className="cat-grid">
          {cats.map(c => (
            <Link key={c.category} to={`/catalog?category=${encodeURIComponent(c.category)}`} className="cat-tile">
              <strong>{c.category}</strong><span>{c.count} материалов</span>
            </Link>
          ))}
        </div>
      </section>
    </>
  );
}