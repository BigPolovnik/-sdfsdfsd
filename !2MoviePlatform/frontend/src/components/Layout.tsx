import { Link, NavLink, useNavigate } from 'react-router-dom';
import { ReactNode, useState } from 'react';

const NAV = [
  { to: '/', label: 'Главная', end: true },
  { to: '/catalog', label: 'Каталог' },
  { to: '/catalog?category=Фильмы', label: 'Фильмы' },
  { to: '/catalog?category=Сериалы', label: 'Сериалы' },
  { to: '/catalog?category=Мультфильмы', label: 'Мультфильмы' },
  { to: '/catalog?category=Аниме', label: 'Аниме' },
];

export function Layout({ children, onAdmin }: { children: ReactNode; onAdmin: () => void }) {
  const [q, setQ] = useState('');
  const nav = useNavigate();

  function submit(e: React.FormEvent) {
    e.preventDefault();
    nav(`/catalog?q=${encodeURIComponent(q)}`);
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <Link to="/" className="brand">
          <span className="brand-mark">К</span>
          <span className="brand-name">КИНО<span>ТЕКА</span><small>ВАШЕ ПРОСТРАНСТВО КИНО</small></span>
        </Link>
        <nav className="main-nav">
          {NAV.map(({ to, label, end }) => (
            <NavLink key={to} to={to} end={end} className={({ isActive }) => isActive ? 'active' : ''}>
              {label}
            </NavLink>
          ))}
        </nav>
        <div className="top-actions">
          <form className="search" onSubmit={submit}>
            <span>⌕</span>
            <input value={q} onChange={e => setQ(e.target.value)} placeholder="Название фильма" />
          </form>
          <button className="icon-button" title="Настройки" onClick={onAdmin}>⚙</button>
        </div>
      </header>
      <main>{children}</main>
      <footer>
        <span className="brand small-brand">
          <span className="brand-mark">К</span>
          <span className="brand-name">КИНО<span>ТЕКА</span></span>
        </span>
        <span>КАТАЛОГ ФИЛЬМОВ И СЕРИАЛОВ</span>
        <span>ТЕСТОВАЯ ВЕРСИЯ · 2026</span>
      </footer>
    </div>
  );
}