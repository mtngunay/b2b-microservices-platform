# B2B Kubernetes Yönetim Scripti
# Kullanım: .\manage.ps1 [komut]
# Komutlar: deploy, ports, stop, status, clean, urls

param(
    [Parameter(Position=0)]
    [ValidateSet("deploy", "ports", "stop", "status", "clean", "urls", "help")]
    [string]$Command = "help"
)

$kubectl = "C:\Program Files\Docker\Docker\resources\bin\kubectl.exe"

function Show-Help {
    Write-Host ""
    Write-Host "B2B Kubernetes Yönetim Komutları:" -ForegroundColor Cyan
    Write-Host "=================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  .\manage.ps1 deploy  " -NoNewline -ForegroundColor Yellow
    Write-Host "- Tüm Kubernetes kaynaklarını deploy et"
    Write-Host "  .\manage.ps1 ports   " -NoNewline -ForegroundColor Yellow
    Write-Host "- Port-forward'ları başlat"
    Write-Host "  .\manage.ps1 stop    " -NoNewline -ForegroundColor Yellow
    Write-Host "- Port-forward'ları durdur"
    Write-Host "  .\manage.ps1 status  " -NoNewline -ForegroundColor Yellow
    Write-Host "- Pod durumlarını göster"
    Write-Host "  .\manage.ps1 clean   " -NoNewline -ForegroundColor Yellow
    Write-Host "- Tüm kaynakları sil"
    Write-Host "  .\manage.ps1 urls    " -NoNewline -ForegroundColor Yellow
    Write-Host "- Erişim URL'lerini göster"
    Write-Host ""
}

function Deploy-All {
    Write-Host "Deploying..." -ForegroundColor Yellow
    & $kubectl apply -k . 2>&1 | Where-Object { $_ -notmatch "Warning" }
    Write-Host "Deploy tamamlandı!" -ForegroundColor Green
}

function Start-Ports {
    Write-Host "Port-forward'lar başlatılıyor..." -ForegroundColor Yellow
    
    # Eski process'leri temizle
    Get-Process -Name "kubectl" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1

    # Port-forward'ları başlat
    Start-Process -NoNewWindow $kubectl -ArgumentList "port-forward svc/b2b-api-svc -n b2b-system 8080:80"
    Start-Process -NoNewWindow $kubectl -ArgumentList "port-forward svc/rabbitmq-svc -n b2b-system 15672:15672"
    Start-Process -NoNewWindow $kubectl -ArgumentList "port-forward svc/jaeger-svc -n b2b-system 16686:16686"
    Start-Process -NoNewWindow $kubectl -ArgumentList "port-forward svc/kibana-svc -n b2b-system 5601:5601"
    Start-Process -NoNewWindow $kubectl -ArgumentList "port-forward svc/prometheus-svc -n b2b-system 9090:9090"
    Start-Process -NoNewWindow $kubectl -ArgumentList "port-forward svc/grafana-svc -n b2b-system 3000:3000"
    
    Start-Sleep -Seconds 2
    Show-Urls
    Write-Host "Port-forward'lar başlatıldı!" -ForegroundColor Green
}

function Stop-Ports {
    Write-Host "Port-forward'lar durduruluyor..." -ForegroundColor Yellow
    Get-Process -Name "kubectl" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "Port-forward'lar durduruldu!" -ForegroundColor Green
}

function Show-Status {
    Write-Host ""
    Write-Host "=== b2b-system ===" -ForegroundColor Cyan
    & $kubectl get pods -n b2b-system
    Write-Host ""
    Write-Host "=== b2b-data ===" -ForegroundColor Cyan
    & $kubectl get pods -n b2b-data
    Write-Host ""
    Write-Host "=== b2b-messaging ===" -ForegroundColor Cyan
    & $kubectl get pods -n b2b-messaging
    Write-Host ""
    Write-Host "=== b2b-observability ===" -ForegroundColor Cyan
    & $kubectl get pods -n b2b-observability
}

function Clean-All {
    Write-Host "Tüm kaynaklar siliniyor..." -ForegroundColor Red
    Stop-Ports
    & $kubectl delete -k . 2>&1 | Where-Object { $_ -notmatch "Warning" }
    Write-Host "Temizlik tamamlandı!" -ForegroundColor Green
}

function Show-Urls {
    Write-Host ""
    Write-Host "Erişim URL'leri:" -ForegroundColor Cyan
    Write-Host "================" -ForegroundColor Cyan
    Write-Host "  Swagger      : " -NoNewline; Write-Host "http://localhost:8080/swagger" -ForegroundColor Green
    Write-Host "  RabbitMQ     : " -NoNewline; Write-Host "http://localhost:15672" -ForegroundColor Green
    Write-Host "                 (b2b_user / YourRabbitPassword)" -ForegroundColor Gray
    Write-Host "  Jaeger       : " -NoNewline; Write-Host "http://localhost:16686" -ForegroundColor Green
    Write-Host "  Kibana       : " -NoNewline; Write-Host "http://localhost:5601" -ForegroundColor Green
    Write-Host "  Prometheus   : " -NoNewline; Write-Host "http://localhost:9090" -ForegroundColor Green
    Write-Host "  Grafana      : " -NoNewline; Write-Host "http://localhost:3000" -ForegroundColor Green
    Write-Host "                 (admin / admin)" -ForegroundColor Gray
    Write-Host ""
}

# Komut çalıştır
switch ($Command) {
    "deploy" { Deploy-All }
    "ports"  { Start-Ports }
    "stop"   { Stop-Ports }
    "status" { Show-Status }
    "clean"  { Clean-All }
    "urls"   { Show-Urls }
    "help"   { Show-Help }
}
