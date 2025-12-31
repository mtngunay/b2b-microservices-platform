# B2B Kubernetes - Complete Stack Startup Script
# Tüm servisleri başlatır, migration ve seed işlemlerini yapar

$ErrorActionPreference = "Continue"
$kubectl = "C:\Program Files\Docker\Docker\resources\bin\kubectl.exe"
$scriptRoot = $PSScriptRoot

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  B2B Platform - Kubernetes Stack" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Eski kaynakları temizle
Write-Host "[1/10] Eski kaynaklar temizleniyor..." -ForegroundColor Yellow
Get-Process -Name "kubectl" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Get-Job | Stop-Job -PassThru | Remove-Job -Force -ErrorAction SilentlyContinue
& $kubectl delete deployment b2b-api b2b-worker -n b2b-system --ignore-not-found 2>$null
& $kubectl delete deployment grafana alertmanager jaeger kibana logstash -n b2b-observability --ignore-not-found 2>$null
& $kubectl delete statefulset mongodb mssql redis -n b2b-data --ignore-not-found 2>$null
& $kubectl delete statefulset rabbitmq -n b2b-messaging --ignore-not-found 2>$null
& $kubectl delete statefulset elasticsearch prometheus -n b2b-observability --ignore-not-found 2>$null
Start-Sleep -Seconds 5
Write-Host "  Temizlendi" -ForegroundColor Gray

# 2. Tüm kaynakları deploy et
Write-Host "[2/10] Kubernetes kaynakları deploy ediliyor..." -ForegroundColor Yellow
Set-Location -Path $scriptRoot
& $kubectl apply -k . 2>&1 | Out-Null
Write-Host "  Deploy tamamlandı" -ForegroundColor Gray

# 3. MSSQL hazır olana kadar bekle
Write-Host "[3/10] MSSQL bekleniyor..." -ForegroundColor Yellow
$mssqlReady = $false
$attempts = 0
while (-not $mssqlReady -and $attempts -lt 60) {
    $attempts++
    Start-Sleep -Seconds 3
    $mssqlPod = & $kubectl get pods -n b2b-data --no-headers 2>$null | Where-Object { $_ -match "mssql.*1/1.*Running" }
    if ($mssqlPod) { $mssqlReady = $true }
    Write-Host "  Bekleniyor... ($($attempts * 3) saniye)" -ForegroundColor Gray
}
if ($mssqlReady) {
    Write-Host "  MSSQL hazır!" -ForegroundColor Green
} else {
    Write-Host "  MSSQL timeout - devam ediliyor..." -ForegroundColor Yellow
}

# 4. Veritabanlarını oluştur
Write-Host "[4/10] MSSQL veritabanları oluşturuluyor..." -ForegroundColor Yellow
if ($mssqlReady) {
    Start-Sleep -Seconds 10  # MSSQL'in tam başlaması için ekstra bekleme
    & $kubectl exec -it mssql-0 -n b2b-data -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C -Q "IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'B2BWriteDb') CREATE DATABASE B2BWriteDb; IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'HangfireDb') CREATE DATABASE HangfireDb;" 2>&1 | Out-Null
    Write-Host "  B2BWriteDb ve HangfireDb oluşturuldu" -ForegroundColor Gray
}

# 5. EF Core Migration çalıştır
Write-Host "[5/10] EF Core Migration çalıştırılıyor..." -ForegroundColor Yellow
if ($mssqlReady) {
    # Port-forward başlat (migration için)
    $migrationPF = Start-Job -ScriptBlock {
        param($kubectl)
        & $kubectl port-forward svc/mssql-svc -n b2b-data 1433:1433 2>&1
    } -ArgumentList $kubectl
    Start-Sleep -Seconds 3
    
    # Migration SQL dosyasını oluştur ve çalıştır
    $projectRoot = Split-Path -Parent $scriptRoot
    $migrationSql = Join-Path $scriptRoot "migration.sql"
    
    # Migration SQL'i oluştur
    Push-Location $projectRoot
    $env:ConnectionStrings__WriteDb = "Server=localhost;Database=B2BWriteDb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
    dotnet ef migrations script --project src/B2B.Infrastructure --startup-project src/B2B.API -o $migrationSql --idempotent 2>&1 | Out-Null
    Pop-Location
    
    # Migration'ı pod'a kopyala ve çalıştır
    if (Test-Path $migrationSql) {
        & $kubectl cp $migrationSql b2b-data/mssql-0:/tmp/migration.sql 2>&1 | Out-Null
        & $kubectl exec -it mssql-0 -n b2b-data -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -C -d B2BWriteDb -i /tmp/migration.sql 2>&1 | Out-Null
        Write-Host "  EF Core Migration tamamlandı" -ForegroundColor Gray
        Remove-Item $migrationSql -Force -ErrorAction SilentlyContinue
    }
    
    # Migration port-forward'ı durdur
    $migrationPF | Stop-Job -PassThru | Remove-Job -Force -ErrorAction SilentlyContinue
}

# 6. MongoDB hazır olana kadar bekle ve seed yap
Write-Host "[6/10] MongoDB seed yapılıyor..." -ForegroundColor Yellow
$mongoReady = $false
$attempts = 0
while (-not $mongoReady -and $attempts -lt 30) {
    $attempts++
    Start-Sleep -Seconds 2
    $mongoPod = & $kubectl get pods -n b2b-data --no-headers 2>$null | Where-Object { $_ -match "mongodb-0.*1/1.*Running" }
    if ($mongoPod) { $mongoReady = $true }
}

if ($mongoReady) {
    # Kullanıcı sayısını kontrol et
    $userCount = & $kubectl exec -it mongodb-0 -n b2b-data -- mongosh -u admin -p YourMongoPassword --authenticationDatabase admin B2BReadDb --quiet --eval "db.users.countDocuments()" 2>$null
    
    if ($userCount -match "^0$" -or $userCount -match "^\s*0\s*$") {
        # Seed data ekle
        $seedScript = @"
db.users.insertMany([
    {
        _id: '22222222-2222-2222-2222-222222222222',
        email: 'admin@demo.com',
        firstName: 'Admin',
        lastName: 'User',
        fullName: 'Admin User',
        isActive: true,
        isDeleted: false,
        tenantId: 'demo',
        roles: ['Admin'],
        permissions: ['users.read', 'users.write', 'users.delete', 'roles.read', 'roles.write'],
        createdAt: new Date(),
        lastLoginAt: new Date()
    },
    {
        _id: '33333333-3333-3333-3333-333333333333',
        email: 'user@demo.com',
        firstName: 'Test',
        lastName: 'User',
        fullName: 'Test User',
        isActive: true,
        isDeleted: false,
        tenantId: 'demo',
        roles: ['User'],
        permissions: ['users.read', 'roles.read'],
        createdAt: new Date(),
        lastLoginAt: null
    }
])
"@
        & $kubectl exec -it mongodb-0 -n b2b-data -- mongosh -u admin -p YourMongoPassword --authenticationDatabase admin B2BReadDb --eval $seedScript 2>&1 | Out-Null
        Write-Host "  MongoDB seed tamamlandı (2 kullanıcı eklendi)" -ForegroundColor Gray
    } else {
        Write-Host "  MongoDB zaten seed edilmiş" -ForegroundColor Gray
    }
}

# 7. Diğer servislerin hazır olmasını bekle
Write-Host "[7/10] Tüm servisler bekleniyor..." -ForegroundColor Yellow
$timeout = 180
$elapsed = 0
$minReady = 15

do {
    Start-Sleep -Seconds 5
    $elapsed += 5
    $pods = & $kubectl get pods -A --no-headers 2>$null | Where-Object { $_ -match "b2b" }
    $running = ($pods | Where-Object { $_ -match "1/1.*Running" }).Count
    Write-Host "  $running pod hazır ($elapsed saniye)" -ForegroundColor Gray
} while ($running -lt $minReady -and $elapsed -lt $timeout)

# Worker'ı yeniden başlat (tüm bağımlılıklar hazır olduktan sonra)
Write-Host "  Worker yeniden başlatılıyor..." -ForegroundColor Gray
& $kubectl rollout restart deployment/b2b-worker -n b2b-system 2>&1 | Out-Null
Start-Sleep -Seconds 10

# 8. Port-forward'ları başlat
Write-Host "[8/10] Port-forward'lar başlatılıyor..." -ForegroundColor Yellow

$ports = @(
    @{Name="API/Swagger"; Svc="b2b-api-svc"; NS="b2b-system"; Local=8080; Remote=80},
    @{Name="MSSQL"; Svc="mssql-svc"; NS="b2b-data"; Local=1433; Remote=1433},
    @{Name="MongoDB"; Svc="mongodb-svc"; NS="b2b-data"; Local=27017; Remote=27017},
    @{Name="Redis"; Svc="redis-svc"; NS="b2b-data"; Local=6379; Remote=6379},
    @{Name="RabbitMQ"; Svc="rabbitmq-management"; NS="b2b-messaging"; Local=15672; Remote=15672},
    @{Name="RabbitMQ AMQP"; Svc="rabbitmq-svc"; NS="b2b-messaging"; Local=5672; Remote=5672},
    @{Name="Jaeger"; Svc="jaeger-query"; NS="b2b-observability"; Local=16686; Remote=16686},
    @{Name="Kibana"; Svc="kibana"; NS="b2b-observability"; Local=5601; Remote=5601},
    @{Name="Prometheus"; Svc="prometheus"; NS="b2b-observability"; Local=9090; Remote=9090},
    @{Name="Grafana"; Svc="grafana"; NS="b2b-observability"; Local=3000; Remote=3000},
    @{Name="Elasticsearch"; Svc="elasticsearch-client"; NS="b2b-observability"; Local=9200; Remote=9200}
)

$global:portForwardJobs = @()
foreach ($p in $ports) {
    $job = Start-Job -ScriptBlock {
        param($kubectl, $svc, $ns, $local, $remote)
        & $kubectl port-forward "svc/$svc" -n $ns "${local}:${remote}" 2>&1
    } -ArgumentList $kubectl, $p.Svc, $p.NS, $p.Local, $p.Remote
    $global:portForwardJobs += $job
    Write-Host "  $($p.Name): localhost:$($p.Local)" -ForegroundColor Gray
    Start-Sleep -Milliseconds 300
}

# Port-forward'ların başlaması için bekle
Start-Sleep -Seconds 5

# 9. Servisleri test et
Write-Host "[9/10] Servisler test ediliyor..." -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Erişim Bilgileri" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Web UI'lar
$webUrls = @(
    @{Name="Swagger API"; Url="http://localhost:8080/swagger"; Cred=""},
    @{Name="RabbitMQ"; Url="http://localhost:15672"; Cred="b2b_user / YourRabbitPassword"},
    @{Name="Jaeger"; Url="http://localhost:16686"; Cred=""},
    @{Name="Kibana"; Url="http://localhost:5601"; Cred=""},
    @{Name="Prometheus"; Url="http://localhost:9090"; Cred=""},
    @{Name="Grafana"; Url="http://localhost:3000"; Cred="admin / admin"}
)

Write-Host "  Web Arayüzleri:" -ForegroundColor Cyan
foreach ($u in $webUrls) {
    try {
        $r = Invoke-WebRequest -Uri $u.Url -UseBasicParsing -TimeoutSec 3 -ErrorAction Stop
        $status = "OK"
        $color = "Green"
    } catch {
        $status = "Bekleniyor..."
        $color = "Yellow"
    }
    
    $line = "    $($u.Name.PadRight(14)) : $($u.Url)"
    Write-Host $line -NoNewline
    Write-Host " [$status]" -ForegroundColor $color
    if ($u.Cred) { Write-Host "                     ($($u.Cred))" -ForegroundColor Gray }
}

Write-Host ""
Write-Host "  Veritabanları:" -ForegroundColor Cyan
Write-Host "    MSSQL          : localhost,1433" -ForegroundColor White
Write-Host "                     (sa / YourStrong@Passw0rd)" -ForegroundColor Gray
Write-Host "                     Database: B2BWriteDb, HangfireDb" -ForegroundColor Gray
Write-Host "    MongoDB        : localhost:27017" -ForegroundColor White
Write-Host "                     (admin / YourMongoPassword)" -ForegroundColor Gray
Write-Host "                     Database: B2BReadDb" -ForegroundColor Gray
Write-Host "    Redis          : localhost:6379" -ForegroundColor White
Write-Host "                     (password: YourRedisPassword)" -ForegroundColor Gray
Write-Host ""
Write-Host "  Mesajlaşma:" -ForegroundColor Cyan
Write-Host "    RabbitMQ AMQP  : localhost:5672" -ForegroundColor White
Write-Host "                     (b2b_user / YourRabbitPassword)" -ForegroundColor Gray

# Pod durumu
Write-Host ""
$total = (& $kubectl get pods -A --no-headers 2>$null | Where-Object { $_ -match "b2b" }).Count
$running = (& $kubectl get pods -A --no-headers 2>$null | Where-Object { $_ -match "b2b" -and $_ -match "1/1.*Running" }).Count
Write-Host "  Pod Durumu: $running / $total çalışıyor" -ForegroundColor $(if ($running -eq $total) { "Green" } else { "Yellow" })

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 10. URL'leri aç
$open = Read-Host "Tüm URL'leri tarayıcıda açmak ister misiniz? (E/H)"
if ($open -eq "E" -or $open -eq "e") {
    Write-Host ""
    Write-Host "  Tarayıcıda açılıyor..." -ForegroundColor Gray
    Start-Sleep -Seconds 2
    
    Start-Process "http://localhost:8080/swagger"
    Start-Sleep -Milliseconds 500
    Start-Process "http://localhost:15672"
    Start-Sleep -Milliseconds 500
    Start-Process "http://localhost:16686"
    Start-Sleep -Milliseconds 500
    Start-Process "http://localhost:5601"
    Start-Sleep -Milliseconds 500
    Start-Process "http://localhost:9090"
    Start-Sleep -Milliseconds 500
    Start-Process "http://localhost:3000"
    
    Write-Host "  Tüm URL'ler açıldı!" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "  Port-forward'lar arka planda çalışıyor" -ForegroundColor Yellow
Write-Host "  Bu pencereyi KAPATMAYIN!" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "Durdurmak için: stop.bat veya Ctrl+C" -ForegroundColor Gray
Write-Host ""

# Port-forward'ların çalışmaya devam etmesi için bekle
try {
    Write-Host "Çıkmak için Ctrl+C basın..." -ForegroundColor Gray
    while ($true) { 
        Start-Sleep -Seconds 30
        # Job'ların durumunu kontrol et
        $runningJobs = $global:portForwardJobs | Where-Object { $_.State -eq "Running" }
        $failedCount = $global:portForwardJobs.Count - $runningJobs.Count
        
        if ($failedCount -gt 0) {
            Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] $failedCount port-forward yeniden başlatılıyor..." -ForegroundColor Yellow
            
            # Durmuş job'ları yeniden başlat
            foreach ($p in $ports) {
                $existingJob = $global:portForwardJobs | Where-Object { 
                    $_.Command -match $p.Svc -and $_.State -eq "Running" 
                }
                if (-not $existingJob) {
                    $job = Start-Job -ScriptBlock {
                        param($kubectl, $svc, $ns, $local, $remote)
                        & $kubectl port-forward "svc/$svc" -n $ns "${local}:${remote}" 2>&1
                    } -ArgumentList $kubectl, $p.Svc, $p.NS, $p.Local, $p.Remote
                    $global:portForwardJobs += $job
                }
            }
        }
    }
} finally {
    Write-Host ""
    Write-Host "Port-forward'lar durduruluyor..." -ForegroundColor Yellow
    $global:portForwardJobs | Stop-Job -PassThru | Remove-Job -Force -ErrorAction SilentlyContinue
    Get-Process -Name "kubectl" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "Temizlendi." -ForegroundColor Green
}
