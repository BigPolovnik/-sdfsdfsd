@echo off
chcp 65001 >nul
cd /d "%~dp0"
docker compose down
echo Контейнеры остановлены. Данные PostgreSQL сохранены.
pause
