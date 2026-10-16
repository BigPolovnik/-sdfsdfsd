import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api, CategoryCount, MovieListItem } from '../api/client';
import { MovieCard } from '../components/MovieCard';

const SORTS = [
  { value: '', label: 'По рейтингу' },
  { value: 'year_desc', label: 'Новые' },
  { value: 'year_asc', label: 'Старые' },
  { value: 'title', label: 'По алфавиту' },
];

export function CatalogPage() {
  const [params, setParams] = useSearchParams();
  const q = params.get('q') ?? '';
  const category = params.get('category') ?? 'Все';
  const sort = params.get('sort') ?? '';
  const page = Number(params.get('page') ?? '1');

  const [items, setItems] = useState<MovieListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [pages, setPages] = useState(1);
  const [cats, setCats] = useState<CategoryCount[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => { api.categories().then(setCats).catch(() => {}); }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    const isSeries = category === 'Сериалы';
    const request = isSeries
      ? api.series({ q, category, page, pageSize: 24 }).then(r => ({
          total: r.total, pages: Math.ceil(r.total / 24), items: r.items,
        }))
      : api.movies({ q, category, sort, page, pageSize: 24 });

    request
      .then(r => { if (!cancelled) { setItems(r.items); setTotal(r.total); setPages(r.pages); } })
      .catch(() => {})
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [q, category, sort, page]);

  function update(patch: Record<string, string | undefined>) {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(patch)) {
      if (v === undefined || v === '' || v === 'Все') next.delete(k);
      else next.set(k, v);
    }
    if (!('page' in patch)) next.delete('page');
    setParams(next);
  }

  return (
    <section className="catalog-section">
      <div className="section-heading">
        <div>
          <div className="overline muted">КАТАЛОГ</div>
          <h2>{q ? `Поиск: ${q}` : category !== 'Все' ? category : 'Все материалы'}
            <span className="count">{String(total).padStart(2, '0')}</span>
          </h2>
        </div>
      </div>

      <div className="filters wide">
        <button className={category === 'Все' ? 'selected' : ''} onClick={() => update({ category: undefined })}>Все</button>
        {cats.map(c => (
          <button key={c.category} className={category === c.category ? 'selected' : ''}
            onClick={() => update({ category: c.category })}>
            {c.category} · {c.count}
          </button>
        ))}
      </div>

      <div className="filters">
        {SORTS.map(s => (
          <button key={s.value} className={sort === s.value ? 'selected' : ''}
            onClick={() => update({ sort: s.value || undefined })}>
            {s.label}
          </button>
        ))}
      </div>

      {loading
        ? <div className="movie-grid">{Array.from({ length: 8 }).map((_, i) => <div key={i} className="movie-card skeleton" />)}</div>
        : items.length
          ? <div className="movie-grid">{items.map((m, i) => (
              <MovieCard key={m.id} movie={m} index={i} kind={category === 'Сериалы' ? 'series' : 'movie'} />
            ))}</div>
          : <div className="empty-state"><h3>Ничего не найдено</h3><p>Попробуйте изменить фильтры.</p></div>}

      {pages > 1 && (
        <div className="pagination">
          <button disabled={page <= 1} onClick={() => update({ page: String(page - 1) })}>← Назад</button>
          <span>Страница {page} из {pages}</span>
          <button disabled={page >= pages} onClick={() => update({ page: String(page + 1) })}>Далее →</button>
        </div>
      )}
    </section>
  );
}