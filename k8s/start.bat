@echo off
title B2B Kubernetes Stack
cd /d "%~dp0"
powershell -ExecutionPolicy Bypass -File "run.ps1"
pause
