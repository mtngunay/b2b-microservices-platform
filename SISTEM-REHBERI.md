# 🎯 B2B Platform - Uçtan Uca Sistem Rehberi

## 📚 Temel Kavramlar

### Kubernetes Nedir?
Kubernetes (K8s), container'ları (Docker) yöneten bir orkestrasyon platformudur. Uygulamalarınızı otomatik olarak dağıtır, ölçeklendirir ve yönetir.

### Pod Nedir?
Pod, Kubernetes'in en küçük dağıtım birimidir. Bir veya daha fazla container içerir. Örneğin `b2b-api-6fdf76d664-2tpdd` bir pod'dur.

### Service Nedir?
Service, pod'lara sabit bir IP ve DNS adı sağlar. Pod'lar ölse bile service adresi değişmez.

### Namespace Nedir?
Namespace, kaynakları mantıksal olarak gruplar. Bu projede:
- `b2b-system` → API ve Worker
- `b2b-data` → Veritabanları (MSSQL, MongoDB, Redis)
- `b2b-messaging` → RabbitMQ
- `b2b-observability` → Monitoring (Prometheus, Grafana, Jaeger, Kibana)

### Port-Forward Nedir?
Kubernetes cluster'ı dışarıdan erişime kapalıdır. `port-forward` komutu, cluster içindeki bir servisi senin bilgisayarına "tünel" açarak bağlar.

```
┌─────────────────────┐         ┌─────────────────────────────────┐
│  Senin Bilgisayarın │  tünel  │     Kubernetes Cluster          │
│  localhost:8080     │ ◄─────► │  b2b-api-svc:80 (cluster içi)   │
└─────────────────────┘         └─────────────────────────────────┘
```

---

## 🏗️ Sistem Mimarisi

```
                              ┌─────────────────┐
                              │    KULLANICI    │
                              │   (Swagger UI)  │
                              └────────┬────────┘
                                       │
                              ┌────────▼────────┐
                              │   B2B API (3x)  │
                              │ localhost:8080  │
                              └────────┬────────┘
                                       │
        ┌──────────────────────────────┼──────────────────────────────┐
        │                              │                              │
        ▼                              ▼                              ▼
┌───────────────┐            ┌───────────────┐            ┌───────────────┐
│    MSSQL      │            │   MongoDB     │            │    Redis      │
│  (Write DB)   │            │  (Read DB)    │            │   (Cache)     │
│ Create/Update │            │    Query      │            │ Token/Session │
│    Delete     │            │               │            │               │
└───────────────┘            └───────────────┘            └───────────────┘
        │
        │ Events
        ▼
┌───────────────┐            ┌───────────────┐
│   RabbitMQ    │ ─────────► │  B2B Worker   │
│  (Event Bus)  │            │  (2 replicas) │
└───────────────┘            └───────────────┘
```

---

## 🔄 İstek Akışı (Login Örneği)

### 1. Kullanıcı Login İsteği Gönderir
```
POST http://localhost:8080/api/v1/Auth/login
Body: {"email":"admin@demo.com","password":"Admin123!"}
```

### 2. API İsteği Alır
- CorrelationId oluşturulur (izleme için)
- Rate limiting kontrol edilir (Redis)
- İstek loglanır

### 3. MSSQL'de Kullanıcı Doğrulanır
```sql
SELECT * FROM Users WHERE Email = 'admin@demo.com' AND IsDeleted = 0
```
- Şifre hash'i kontrol edilir
- Kullanıcı aktif mi kontrol edilir

### 4. JWT Token Oluşturulur
- Access Token (1 saat geçerli)
- Refresh Token (7 gün geçerli)

### 5. Token Redis'e Kaydedilir
```
Key: token:{jti}
Value: {userId, tenantId, email, roles, expiresAt}
TTL: 3600 saniye
```

### 6. Response Döner
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "rT8ZhX3LNKhgOl5x...",
  "tokenType": "Bearer",
  "expiresIn": 3600
}
```

---

## 🔍 Servisleri Kontrol Etme

### Pod'ları Listele
```powershell
kubectl get pods -A | Where-Object { $_ -match "b2b-" }
```

### Servisleri Listele
```powershell
kubectl get svc -n b2b-system
kubectl get svc -n b2b-data
kubectl get svc -n b2b-messaging
kubectl get svc -n b2b-observability
```

### API Loglarını Gör
```powershell
kubectl logs -l app.kubernetes.io/name=b2b-api -n b2b-system --tail=50
```

### Redis'teki Key'leri Gör
```powershell
kubectl exec -it redis-0 -n b2b-data -- redis-cli -a "YourRedisPassword" KEYS "*"
```

### MSSQL'de Sorgu Çalıştır
```powershell
kubectl exec -it mssql-0 -n b2b-data -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd" -d B2BWriteDb -Q "SELECT * FROM Users" -C
```

---

## 🌐 Erişim URL'leri

| Servis | URL | Kullanıcı / Şifre |
|--------|-----|-------------------|
| Swagger (API) | http://localhost:8080/swagger | - |
| RabbitMQ | http://localhost:15672 | b2b_user / YourRabbitPassword |
| Jaeger (Tracing) | http://localhost:16686 | - |
| Kibana (Logs) | http://localhost:5601 | - |
| Prometheus | http://localhost:9090 | - |
| Grafana | http://localhost:3000 | admin / admin |

---

## 👤 Test Kullanıcıları

| Email | Şifre | Rol | Yetkiler |
|-------|-------|-----|----------|
| admin@demo.com | Admin123! | Admin | Tüm yetkiler |
| user@demo.com | Admin123! | User | Sadece okuma |

---

## 🔧 Port-Forward Komutları

Her servis için ayrı port-forward gerekir:

```powershell
# API
kubectl port-forward svc/b2b-api-svc 8080:80 -n b2b-system

# RabbitMQ
kubectl port-forward svc/rabbitmq-svc 15672:15672 -n b2b-messaging

# Jaeger
kubectl port-forward svc/jaeger-query 16686:16686 -n b2b-observability

# Kibana
kubectl port-forward svc/kibana 5601:5601 -n b2b-observability

# Prometheus
kubectl port-forward svc/prometheus 9090:9090 -n b2b-observability

# Grafana
kubectl port-forward svc/grafana 3000:3000 -n b2b-observability

# Redis (CLI erişimi için)
kubectl port-forward svc/redis-svc 6379:6379 -n b2b-data
```

---

## 📊 Monitoring Araçları

### Jaeger (Distributed Tracing)
- URL: http://localhost:16686
- Ne yapar: İsteklerin hangi servislerden geçtiğini gösterir
- Kullanım: Service dropdown'dan "B2B.API" seç, "Find Traces" tıkla

### Kibana (Log Analizi)
- URL: http://localhost:5601
- Ne yapar: Tüm logları merkezi olarak gösterir
- Kullanım: Discover → Index pattern oluştur → Logları ara

### Prometheus (Metrikler)
- URL: http://localhost:9090
- Ne yapar: CPU, memory, request sayısı gibi metrikleri toplar
- Kullanım: Query kutusuna `http_requests_total` yaz

### Grafana (Dashboard)
- URL: http://localhost:3000
- Ne yapar: Prometheus metriklerini görselleştirir
- Kullanım: Dashboards → Browse → B2B API Dashboard

### RabbitMQ Management
- URL: http://localhost:15672
- Ne yapar: Mesaj kuyruklarını gösterir
- Kullanım: Queues sekmesinde bekleyen mesajları gör

---

## 🐛 Sorun Giderme

### Port-Forward Koptu
```powershell
# Mevcut kubectl process'lerini kapat
Get-Process kubectl | Stop-Process

# Yeniden başlat
kubectl port-forward svc/b2b-api-svc 8080:80 -n b2b-system
```

### Pod Çalışmıyor
```powershell
# Pod durumunu kontrol et
kubectl describe pod <pod-adı> -n <namespace>

# Pod loglarını gör
kubectl logs <pod-adı> -n <namespace>
```

### 401 Unauthorized Hatası
1. Token süresi dolmuş olabilir → Yeni token al
2. Token Redis'te yok → Login yap
3. JWT secret key uyuşmuyor → API'yi yeniden deploy et

---

## 📝 Sık Kullanılan Komutlar

```powershell
# Tüm pod'ları gör
kubectl get pods -A

# Belirli namespace'deki pod'ları gör
kubectl get pods -n b2b-system

# Pod'u yeniden başlat
kubectl rollout restart deployment/b2b-api -n b2b-system

# Pod'a shell aç
kubectl exec -it <pod-adı> -n <namespace> -- /bin/bash

# Secret'ları gör
kubectl get secrets -n b2b-system

# ConfigMap'leri gör
kubectl get configmaps -n b2b-system
```
