# B2B Platform Kubernetes Manifests

Bu dizin, B2B Platform'un tüm Kubernetes manifest dosyalarını içerir.

## 🚀 Hızlı Başlangıç

### Tek Komutla Tüm Sistemi Başlat

```powershell
cd k8s
.\run.ps1
```

veya çift tıkla: `start.bat`

Bu komut otomatik olarak:
1. ✅ Tüm Kubernetes kaynaklarını deploy eder
2. ✅ MSSQL, MongoDB, Redis, RabbitMQ başlatır
3. ✅ Veritabanlarını oluşturur (B2BWriteDb, HangfireDb)
4. ✅ API ve Worker servislerini başlatır
5. ✅ Observability stack'i başlatır (ELK, Prometheus, Grafana, Jaeger)
6. ✅ Port-forward'ları başlatır
7. ✅ Tüm URL'leri test eder

### Durdurmak İçin

```powershell
Get-Process kubectl | Stop-Process
```

---

## 🌐 Erişim URL'leri

| Servis | URL | Kullanıcı / Şifre |
|--------|-----|-------------------|
| **Swagger (API)** | http://localhost:8080/swagger | - |
| **RabbitMQ** | http://localhost:15672 | `b2b_user` / `YourRabbitPassword` |
| **Jaeger** | http://localhost:16686 | - |
| **Kibana** | http://localhost:5601 | - |
| **Prometheus** | http://localhost:9090 | - |
| **Grafana** | http://localhost:3000 | `admin` / `admin` |
| **MSSQL** | localhost,31433 | `sa` / `YourStrong@Passw0rd` |

---

## 👤 Test Kullanıcıları

API'yi test etmek için aşağıdaki kullanıcıları kullanabilirsiniz:

| Email | Şifre | Rol | Yetkiler |
|-------|-------|-----|----------|
| `admin@demo.com` | `Admin123!` | Admin | Tüm yetkiler (users.read, users.write, users.delete, roles.read, roles.write) |
| `user@demo.com` | `Admin123!` | User | Sadece okuma (users.read, roles.read) |

### Login Örneği

```bash
# PowerShell
$body = '{"email":"admin@demo.com","password":"Admin123!"}'
Invoke-RestMethod -Uri "http://localhost:8080/api/v1/Auth/login" -Method POST -Body $body -ContentType "application/json"

# cURL
curl -X POST "http://localhost:8080/api/v1/Auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.com","password":"Admin123!"}'
```

### Swagger'da Token Kullanımı

1. http://localhost:8080/swagger adresine git
2. `/api/v1/Auth/login` endpoint'ini kullan
3. Dönen `accessToken` değerini kopyala
4. Sağ üstteki **Authorize** butonuna tıkla
5. `Bearer <token>` formatında yapıştır (örn: `Bearer eyJhbGciOiJIUzI1...`)
6. Artık korumalı endpoint'leri kullanabilirsin

---

## 🔗 API Endpoint'leri

| Endpoint | Method | Auth | Açıklama |
|----------|--------|------|----------|
| `/api/v1/Auth/login` | POST | ❌ | Kullanıcı girişi, JWT token döner |
| `/api/v1/Auth/logout` | POST | ✅ | Çıkış, token'ı iptal eder |
| `/api/v1/Auth/refresh` | POST | ❌ | Token yenileme |
| `/api/v1/Users` | GET | ✅ | Kullanıcı listesi (MongoDB'den okur) |
| `/api/v1/Users/{id}` | GET | ✅ | Kullanıcı detayı |
| `/api/v1/Users` | POST | ✅ | Yeni kullanıcı oluştur (MSSQL'e yazar) |
| `/api/v1/Users/{id}` | PUT | ✅ | Kullanıcı güncelle |
| `/api/v1/Users/{id}` | DELETE | ✅ | Kullanıcı sil (soft delete) |
| `/health/live` | GET | ❌ | Liveness probe |
| `/health/ready` | GET | ❌ | Readiness probe |
| `/metrics` | GET | ❌ | Prometheus metrics |

---

## 📦 Çalışan Servisler (23 Pod)

| Servis | Replika | Açıklama |
|--------|---------|----------|
| B2B API | 3 | REST API + Swagger |
| B2B Worker | 2 | Hangfire Background Jobs |
| MSSQL | 1 | Write Database (Create/Update/Delete) |
| MongoDB | 1 | Read Database (Query) |
| Redis | 3 | Cache + Session (LFU Eviction) |
| RabbitMQ | 3 | Event Bus (MassTransit) |
| Elasticsearch | 3 | Log Storage |
| Logstash | 2 | Log Processing |
| Kibana | 1 | Log Visualization |
| Prometheus | 1 | Metrics Collection |
| Grafana | 1 | Metrics Dashboard |
| Jaeger | 1 | Distributed Tracing |
| Alertmanager | 1 | Alert Management |

---

## 🏗️ Mimari

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENT                                   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      B2B API (3 replicas)                        │
│                    http://localhost:8080                         │
└─────────────────────────────────────────────────────────────────┘
          │                    │                    │
          ▼                    ▼                    ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│     MSSQL       │  │    MongoDB      │  │     Redis       │
│   (Write DB)    │  │   (Read DB)     │  │ (Cache/Session) │
│ Create/Update/  │  │     Query       │  │   LFU Eviction  │
│    Delete       │  │                 │  │                 │
└─────────────────┘  └─────────────────┘  └─────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────┐
│                    RabbitMQ (Event Bus)                          │
│                  http://localhost:15672                          │
└─────────────────────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────────────────────┐
│                  B2B Worker (2 replicas)                         │
│              Event Consumers + Hangfire Jobs                     │
└─────────────────────────────────────────────────────────────────┘
```

---

## Directory Structure

```
k8s/
├── namespaces/          # Namespace definitions
│   └── namespaces.yaml  # b2b-system, b2b-data, b2b-messaging, b2b-observability
├── config/              # ConfigMaps and Secrets
│   ├── configmap.yaml   # Application configuration
│   └── secrets.yaml     # Sensitive data (connection strings, JWT keys)
├── api/                 # API Service manifests
│   ├── deployment.yaml  # API Deployment with 3 replicas
│   ├── service.yaml     # ClusterIP Service
│   └── hpa.yaml         # Horizontal Pod Autoscaler
├── worker/              # Hangfire Worker manifests
│   └── deployment.yaml  # Worker Deployment with 2 replicas
├── data/                # Database StatefulSets
│   ├── mssql.yaml       # MSSQL Server (Write DB)
│   ├── mongodb.yaml     # MongoDB ReplicaSet (Read DB)
│   └── redis.yaml       # Redis Cluster (Cache)
├── messaging/           # Message Broker manifests
│   └── rabbitmq.yaml    # RabbitMQ Cluster
├── ingress/             # Ingress configuration
│   └── ingress.yaml     # NGINX Ingress with TLS
├── observability/       # Observability Stack manifests
│   ├── elasticsearch.yaml   # Elasticsearch StatefulSet (3 replicas)
│   ├── logstash.yaml        # Logstash Deployment with pipeline config
│   ├── kibana.yaml          # Kibana Deployment
│   ├── prometheus.yaml      # Prometheus StatefulSet with scrape configs
│   ├── grafana.yaml         # Grafana Deployment with dashboards
│   ├── alertmanager.yaml    # Alertmanager Deployment
│   ├── servicemonitor.yaml  # ServiceMonitor CRDs for Prometheus Operator
│   └── jaeger.yaml          # Jaeger All-in-One Deployment
├── run.ps1              # Tek komutla başlat script'i
├── start.bat            # Windows batch file
└── kustomization.yaml   # Kustomize configuration
```

## Prerequisites

- Docker Desktop with Kubernetes enabled
- kubectl configured
- PowerShell 5.1+

## Configuration

### Secrets

**IMPORTANT**: The secrets in `config/secrets.yaml` contain placeholder values. Before deploying to production:

1. Generate strong passwords for all services
2. Update the base64-encoded values in the secrets file
3. Consider using external secret management (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)

To encode a value to base64:
```bash
echo -n "your-secret-value" | base64
```

### TLS Certificates

The ingress uses a placeholder TLS certificate. For production:

1. Use cert-manager for automatic certificate management
2. Or manually create a TLS secret with your certificate:
```bash
kubectl create secret tls b2b-tls-secret \
  --cert=path/to/tls.crt \
  --key=path/to/tls.key \
  -n b2b-system
```

## Namespaces

| Namespace | Purpose |
|-----------|---------|
| b2b-system | Core application (API, Worker) |
| b2b-data | Databases (MSSQL, MongoDB, Redis) |
| b2b-messaging | Message broker (RabbitMQ) |
| b2b-observability | Monitoring stack (ELK, Prometheus, Grafana, Jaeger) |

## Services

| Service | Namespace | Port | Description |
|---------|-----------|------|-------------|
| b2b-api-svc | b2b-system | 80 | API Service |
| b2b-worker-svc | b2b-system | 80 | Hangfire Dashboard |
| mssql-svc | b2b-data | 1433 | MSSQL Server |
| mongodb-svc | b2b-data | 27017 | MongoDB |
| redis-svc | b2b-data | 6379 | Redis |
| rabbitmq-svc | b2b-messaging | 5672 | RabbitMQ AMQP |
| rabbitmq-management | b2b-messaging | 15672 | RabbitMQ Management UI |
| elasticsearch-client | b2b-observability | 9200 | Elasticsearch HTTP |
| logstash | b2b-observability | 5044, 5000, 8080 | Logstash (Beats, TCP, HTTP) |
| kibana | b2b-observability | 5601 | Kibana UI |
| prometheus | b2b-observability | 9090 | Prometheus |
| grafana | b2b-observability | 3000 | Grafana UI |
| alertmanager | b2b-observability | 9093 | Alertmanager |
| jaeger-query | b2b-observability | 16686 | Jaeger UI |
| jaeger-collector | b2b-observability | 14250, 14268 | Jaeger Collector (gRPC, HTTP) |
| jaeger-otlp | b2b-observability | 4317, 4318 | Jaeger OTLP (gRPC, HTTP) |

## Scaling

The API deployment is configured with HPA (Horizontal Pod Autoscaler):
- Min replicas: 3
- Max replicas: 10
- Scale up at 70% CPU or 80% memory utilization

## Health Checks

All deployments include:
- **Liveness Probe**: Restarts unhealthy containers
- **Readiness Probe**: Controls traffic routing

Endpoints:
- `/health/live` - Liveness check
- `/health/ready` - Readiness check (includes dependency checks)

## Monitoring

Prometheus annotations are included on all deployments:
```yaml
prometheus.io/scrape: "true"
prometheus.io/port: "8080"
prometheus.io/path: "/metrics"
```

## Observability Stack

The observability stack provides comprehensive monitoring, logging, and tracing capabilities.

### ELK Stack (Logging)

- **Elasticsearch**: 3-replica StatefulSet for log storage with 100Gi PVC per node
- **Logstash**: 2-replica Deployment for log processing with B2B-specific pipeline
- **Kibana**: Single-replica Deployment for log visualization

**Accessing Kibana:**
```bash
kubectl port-forward svc/kibana 5601:5601 -n b2b-observability
# Open http://localhost:5601
```

### Prometheus/Grafana (Metrics)

- **Prometheus**: StatefulSet with 50Gi storage, configured to scrape B2B API, Worker, Redis, and RabbitMQ
- **Grafana**: Deployment with pre-configured datasources (Prometheus, Elasticsearch, Jaeger) and B2B API dashboard
- **Alertmanager**: Deployment for alert routing and notification

**Accessing Grafana:**
```bash
kubectl port-forward svc/grafana 3000:3000 -n b2b-observability
# Open http://localhost:3000 (default: admin/admin)
```

**Accessing Prometheus:**
```bash
kubectl port-forward svc/prometheus 9090:9090 -n b2b-observability
# Open http://localhost:9090
```

### Jaeger (Tracing)

- **Jaeger All-in-One**: Single deployment with collector, query, and agent components
- Supports OTLP (OpenTelemetry), Zipkin, and native Jaeger protocols

**Accessing Jaeger:**
```bash
kubectl port-forward svc/jaeger-query 16686:16686 -n b2b-observability
# Open http://localhost:16686
```

### Sending Logs to Logstash

Configure your .NET application to send logs to Logstash:

```csharp
// In Program.cs
Log.Logger = new LoggerConfiguration()
    .WriteTo.Http("http://logstash.b2b-observability:8080")
    .CreateLogger();
```

### Sending Traces to Jaeger

Configure OpenTelemetry in your .NET application:

```csharp
// In Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger-otlp.b2b-observability:4317");
        }));
```

### Alert Configuration

Alertmanager is configured with default alert rules for:
- High error rate (>5% for 5 minutes)
- High latency (p95 > 1 second)
- Pod not ready
- High memory usage (>90%)

To customize alerts, update the `alertmanager-config` ConfigMap.
