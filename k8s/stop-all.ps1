# B2B Kubernetes Stack - Stop All Script
# Bu script tüm servisleri ve port-forward'ları durdurur

$ErrorActionPreference = "Continue"
$kubectl = "C:\Program Files\Docker\Docker\resources\bin\kubectl.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  B2B Kubernetes Stack Durduruluyor" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Port-forward process'lerini durdur
Write-Host "[1/2] Port-forward'lar durduruluyor..." -ForegroundColor Yellow
Get-Process -Name "kubectl" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Job | Stop-Job -PassThru | Remove-Job -Force -ErrorAction SilentlyContinue
Write-Host "  Port-forward'lar durduruldu" -ForegroundColor Gray

# 2. Kubernetes kaynaklarını sil (opsiyonel)
$deleteResources = Read-Host "Kubernetes kaynaklarını da silmek ister misiniz? (E/H)"
if ($deleteResources -eq "E" -or $deleteResources -eq "e") {
    Write-Host "[2/2] Kubernetes kaynakları siliniyor..." -ForegroundColor Yellow
    
    # Deployments
    & $kubectl delete deployment b2b-api b2b-worker -n b2b-system --ignore-not-found 2>$null
    & $kubectl delete deployment grafana alertmanager jaeger kibana logstash -n b2b-observability --ignore-not-found 2>$null
    
    # StatefulSets
    & $kubectl delete statefulset mongodb mssql redis -n b2b-data --ignore-not-found 2>$null
    & $kubectl delete statefulset rabbitmq -n b2b-messaging --ignore-not-found 2>$null
    & $kubectl delete statefulset elasticsearch prometheus -n b2b-observability --ignore-not-found 2>$null
    
    # PVCs (verileri de siler!)
    $deletePVC = Read-Host "  PVC'leri (veritabanı verilerini) de silmek ister misiniz? (E/H)"
    if ($deletePVC -eq "E" -or $deletePVC -eq "e") {
        & $kubectl delete pvc --all -n b2b-data --ignore-not-found 2>$null
        & $kubectl delete pvc --all -n b2b-messaging --ignore-not-found 2>$null
        & $kubectl delete pvc --all -n b2b-observability --ignore-not-found 2>$null
        Write-Host "  PVC'ler silindi (veriler kayboldu)" -ForegroundColor Red
    }
    
    Write-Host "  Kaynaklar silindi" -ForegroundColor Gray
} else {
    Write-Host "[2/2] Kubernetes kaynakları korunuyor" -ForegroundColor Gray
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  B2B Stack Durduruldu!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Yeniden başlatmak için: start.bat" -ForegroundColor Gray
Write-Host ""
