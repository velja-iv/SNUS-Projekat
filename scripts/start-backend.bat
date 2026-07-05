@echo off
setlocal
cd /d "%~dp0\.."

powershell -ExecutionPolicy Bypass -File ".\scripts\generate-keys.ps1"
docker compose -f .\docker\docker-compose.backend.yml up --build
