@echo off
echo Proje Baslatiliyor...

:: 1. Backend'i yeni pencerede başlat
start cmd /k " dotnet run"

:: 2. Frontend'i yeni pencerede başlat
start cmd /k "cd FrontEnd && npm run dev"

echo Iki taraf da ayaga kaldirildi! Iyi calismalar.