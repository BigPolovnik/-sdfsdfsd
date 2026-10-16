import { useEffect, useRef, useState } from 'react';
import { PUBLISHER_ID } from '../api/client';

type Props = {
  videoId: number | null | undefined;
  imdbId?: number | string | null;
  playerUrl?: string | null;
  type?: 'movie' | 'series';
  design?: number;
};

const SDK_URLS = [
  'https://graphicslab.io/sdk/v2/rendex-sdk.min.js',
  'https://alt.graphicslab.io/sdk/v2/rendex-sdk.min.js',
];

function loadSdk(url: string, id: string): Promise<void> {
  return new Promise((resolve, reject) => {
    const existing = document.getElementById(id) as HTMLScriptElement | null;

    if (existing) {
      if (existing.dataset.loaded === 'true') {
        resolve();
      } else {
        existing.addEventListener('load', () => resolve(), { once: true });
        existing.addEventListener(
          'error',
          () => reject(new Error(`Ошибка загрузки ${url}`)),
          { once: true },
        );
      }
      return;
    }

    const script = document.createElement('script');
    script.id = id;
    script.src = url;
    script.async = true;

    script.onload = () => {
      script.dataset.loaded = 'true';
      resolve();
    };

    script.onerror = () => reject(new Error(`Ошибка загрузки ${url}`));

    document.head.appendChild(script);
  });
}

export function VibixPlayer({
  videoId,
  playerUrl,
  type = 'movie',
  design = 1,
}: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [sdkReady, setSdkReady] = useState(false);
  const [sdkError, setSdkError] = useState('');

  useEffect(() => {
    let cancelled = false;

    async function initialize() {
      // Загружаем оба SDK, как указано в документации Vibix.
      const results = await Promise.allSettled(
        SDK_URLS.map((url, index) => loadSdk(url, `vibix-sdk-${index + 1}`)),
      );

      if (cancelled) return;

      if (results.some((result) => result.status === 'fulfilled')) {
        setSdkReady(true);
        setSdkError('');
      } else {
        setSdkError('Не удалось загрузить SDK Vibix.');
      }
    }

    void initialize();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const container = containerRef.current;

    if (!container || !sdkReady) return;

    container.replaceChildren();

    if (!videoId) return;

    const embed = document.createElement('ins');

    embed.setAttribute('data-publisher-id', String(PUBLISHER_ID));
    embed.setAttribute('data-type', type);
    embed.setAttribute('data-id', String(videoId));
    embed.setAttribute('data-design', String(design));

    // Диагностический тест: явно указываем доступную озвучку.
    embed.setAttribute('data-voiceover', '214');

    container.appendChild(embed);
  }, [sdkReady, videoId, type, design]);

  if (!videoId && playerUrl) {
    try {
      const url = new URL(playerUrl);

      if (url.protocol === 'https:') {
        return (
          <div className="vibix-player-wrap">
            <iframe
              src={url.toString()}
              title="Плеер Vibix"
              allow="autoplay; fullscreen; picture-in-picture"
              allowFullScreen
              referrerPolicy="strict-origin-when-cross-origin"
            />
          </div>
        );
      }
    } catch {
      // Некорректный URL — покажем сообщение ниже.
    }
  }

  if (!videoId) {
    return (
      <div className="empty-state">
        <h3>Плеер недоступен</h3>
        <p>Vibix не вернул идентификатор видео.</p>
      </div>
    );
  }

  return (
    <div className="vibix-player-wrap">
      {!sdkReady && !sdkError && <p>Загрузка плеера Vibix…</p>}

      {sdkError && !sdkReady && (
        <div className="empty-state">
          <h3>Ошибка загрузки плеера</h3>
          <p>{sdkError}</p>
        </div>
      )}

      <div ref={containerRef} />
    </div>
  );
}