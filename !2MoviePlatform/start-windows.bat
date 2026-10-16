@echo off
chcp 65001 >nul
cd /d "%~dp0"
where docker >nul 2>nul
if errorlevel 1 (
  echo Docker не найден. Установите и запустите Docker Desktop, затем повторите.
  pause
  exit /b 1
)
if not exist .env (
  copy .env.example .env >nul
  echo Создан файл .env из шаблона.
  echo Откройте .env и задайте VIBIX_API_TOKEN и ADMIN_KEY перед запуском.
  notepad .env
  echo После сохранения файла нажмите любую клавишу для запуска.
  pause >nul
)
docker compose up -d --build
if errorlevel 1 (
  echo Ошибка запуска. Проверьте сообщения выше или выполните: docker compose logs -f api
  pause
  exit /b 1
)
echo.
echo Сайт запущен: http://localhost:3000
echo Настройки Vibix: нажмите значок шестерёнки в правом верхнем углу.
echo Логи API: docker compose logs -f api
start http://localhost:3000
pause
