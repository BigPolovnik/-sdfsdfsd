import { useEffect, useState } from 'react';
import { api } from '../api/client';

export function AdminModal({ onClose }: { onClose: () => void }) {
  const [key, setKey] = useState(localStorage.getItem('adminKey') ?? '');
  const [msg, setMsg] = useState('');
  const [busy, setBusy] = useState(false);
  const [watching, setWatching] = useState(false);

  useEffect(() => {
    if (!watching || !key) return;
    let cancelled = false;
    const timer = window.setInterval(async () => {
      try {
        const s = await api.adminStatus(key);
        if (cancelled) return;
        setMsg(`В базе: ${s.movies} фильмов и ${s.series} сериалов${s.syncInProgress ? ' · импорт продолжается' : ' · импорт не выполняется'}.`);
        if (!s.syncInProgress) setWatching(false);
      } catch { /* следующая проверка повторит запрос */ }
    }, 3000);
    return () => { cancelled = true; window.clearInterval(timer); };
  }, [watching, key]);

  async function check() {
    setBusy(true); setMsg('Проверяю…');
    try {
      localStorage.setItem('adminKey', key);
      const s = await api.adminStatus(key);
      setMsg(`Каталог: ${s.movies} фильмов, ${s.series} сериалов. ${s.message}`);
    } catch { setMsg('Ключ не принят или API недоступен.'); }
    finally { setBusy(false); }
  }

  async function sync() {
    setBusy(true); setMsg('Запускаю синхронизацию в фоне…');
    try {
      localStorage.setItem('adminKey', key);
      const r = await api.adminSync(key);
      setMsg(r.message);
      setWatching(true);
    } catch { setMsg('Не удалось запустить синхронизацию.'); }
    finally { setBusy(false); }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="admin-modal" onClick={e => e.stopPropagation()}>
        <button className="modal-close" onClick={onClose}>×</button>
        <div className="overline muted">СИСТЕМА · ИНТЕГРАЦИИ</div>
        <h2>Подключение Vibix</h2>
        <p className="admin-intro">Токен Vibix хранится на сервере. Здесь — ключ администратора (ADMIN_KEY из .env).</p>
        <label className="field-label">Ключ администратора
          <input type="password" value={key} onChange={e => setKey(e.target.value)} placeholder="ADMIN_KEY" autoComplete="off" />
        </label>
        <div className="admin-status">{msg || 'Введите ключ и проверьте подключение.'}</div>
        <div className="admin-actions">
          <button className="secondary-button" disabled={!key || busy} onClick={check}>Проверить</button>
          <button className="primary-button" disabled={!key || busy} onClick={sync}>Синхронизировать каталог <span>↗</span></button>
        </div>
        <div className="admin-note">Импорт идёт в фоне небольшими страницами. Уже сохранённые записи сразу доступны на сайте; статус и количество материалов обновляются автоматически. Подробные события: docker compose logs -f api.</div>
      </div>
    </div>
  );
}