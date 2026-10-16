const API = (import.meta.env.VITE_API_URL || '').replace(/\/$/, '');
export const PUBLISHER_ID = '679372446';

export type MovieListItem = {
  id: number;
  title: string;
  originalTitle?: string;
  year?: number;
  posterUrl?: string;
  backdropUrl?: string;
  rating?: number;
  kpRating?: number;
  imdbRating?: number;
  category?: string;
  genresText?: string;
  country?: string;
  quality?: string;
  hasPlayer?: boolean;
};

export type MovieDetail = MovieListItem & {
  overview?: string;
  kpVotes?: number;
  imdbVotes?: number;
  voicesText?: string;
  iframeVideoId?: number;
  playerUrl?: string;
  duration?: number;
  kinopoiskId?: number;
  imdbId?: number;
  vibixId?: string;
  contentType: 'movie' | 'series';
};

export type EpisodeSources = Record<string, number>;

export type SeriesDetail = MovieDetail & {
  seasons: {
    number: number;
    episodes: {
      number: number;
      title?: string;
      sources: EpisodeSources;
    }[];
  }[];
};

export type Paged<T> = { total: number; page: number; pageSize: number; pages: number; items: T[] };
export type CategoryCount = { category: string; count: number };

async function j<T>(r: Response): Promise<T> {
  if (!r.ok) throw new Error(await r.text().catch(() => r.statusText));
  return r.json() as Promise<T>;
}

export const api = {
  health: () => fetch(`${API}/health`).then(r => r.ok),

  top: (limit = 10, category?: string) => {
    const p = new URLSearchParams({ limit: String(limit) });
    if (category && category !== 'Все') p.set('category', category);
    return fetch(`${API}/api/catalog/top?${p}`).then(j<MovieListItem[]>);
  },

  categories: () => fetch(`${API}/api/catalog/categories`).then(j<CategoryCount[]>),

  movies: (params: {
    q?: string; category?: string; genre?: string;
    year?: number; sort?: string; page?: number; pageSize?: number;
  } = {}) => {
    const p = new URLSearchParams();
    if (params.q) p.set('q', params.q);
    if (params.category && params.category !== 'Все') p.set('category', params.category);
    if (params.genre) p.set('genre', params.genre);
    if (params.year) p.set('year', String(params.year));
    if (params.sort) p.set('sort', params.sort);
    p.set('page', String(params.page ?? 1));
    p.set('pageSize', String(params.pageSize ?? 24));
    return fetch(`${API}/api/catalog/movies?${p}`).then(j<Paged<MovieListItem>>);
  },

  series: (params: { q?: string; category?: string; page?: number; pageSize?: number } = {}) => {
    const p = new URLSearchParams();
    if (params.q) p.set('q', params.q);
    if (params.category && params.category !== 'Все') p.set('category', params.category);
    p.set('page', String(params.page ?? 1));
    p.set('pageSize', String(params.pageSize ?? 24));
    return fetch(`${API}/api/catalog/series?${p}`).then(j<Paged<MovieListItem>>);
  },

  movie: (id: number) => fetch(`${API}/api/catalog/movies/${id}`).then(j<MovieDetail>),
  seriesById: (id: number) => fetch(`${API}/api/catalog/series/${id}`).then(j<{ series: SeriesDetail; seasons: SeriesDetail['seasons'] }>),

  adminStatus: (key: string) =>
    fetch(`${API}/api/admin/status`, { headers: { 'X-Admin-Key': key } })
      .then(j<{
        movies: number; series: number; message: string; vibixTokenConfigured: boolean;
        syncInProgress?: boolean; enrichInProgress?: boolean;
        moviesWithoutIframe?: number; seriesWithoutIframe?: number;
      }>),

  adminSync: (key: string) =>
    fetch(`${API}/api/admin/sync-vibix`, { method: 'POST', headers: { 'X-Admin-Key': key } })
      .then(j<{ started: boolean; message: string }>),
};