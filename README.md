🇹🇷 Türkçe | 🇬🇧 [English](README_EN.md)
- 📘 [Mimari Dokümantasyon](docs/architecture.md)


# 🚀 B2B Microservices Platform 2026

Kurumsal B2B sistemleri için tasarlanmış, **event‑driven**, **ölçeklenebilir** ve **observability‑first** bir Kubernetes tabanlı mikroservis platformu.

Bu repo özellikle:

* Gerçek hayata yakın **production‑grade** K8s manifestleri
* CQRS + Event‑Driven mimari
* ELK, Prometheus, Grafana, Jaeger ile **tam gözlemlenebilirlik**
* .NET tabanlı API & Worker servisleri

sunmayı amaçlar.

---

## 📌 İçindekiler

* [Hızlı Başlangıç](#hızlı-başlangıç)
* [Mimari Genel Bakış](#mimari-genel-bakış)
* [Çalışan Servisler](#çalışan-servisler)
* [Erişim URL’leri](#erişim-urlleri)
* [API & Kimlik Doğrulama](#api--kimlik-doğrulama)
* [Dizin Yapısı](#dizin-yapısı)
* [Observability](#observability)
* [Scaling & Health Checks](#scaling--health-checks)
* [Troubleshooting](#troubleshooting)
* [Prerequisites](#prerequisites)

---

<a name="hızlı-başlangıç"></a>

## 🚀 Hızlı Başlangıç

### Tek Komutla Tüm Sistemi Başlat

```powershell
cd k8s
.\run.ps1
```

veya:

```powershell
start.bat
```

Bu script otomatik olarak:

1. Kubernetes namespace’lerini oluşturur
2. Tüm servisleri deploy eder
3. MSSQL, MongoDB, Redis, RabbitMQ başlatır
4. Veritabanlarını oluşturur (B2BWriteDb, HangfireDb)
5. API & Worker servislerini ayağa kaldırır
6. ELK, Prometheus, Grafana, Jaeger stack’ini kurar
7. Gerekli port‑forward işlemlerini başlatır

### Sistemi Durdurmak

```powershell
Get-Process kubectl | Stop-Process
```

---

<a name="mimari-genel-bakış"></a>

## 🏗️ Mimari Genel Bakış

```
CLIENT (Swagger / API Consumer)
        │
        ▼
┌─────────────────────────────┐
│        B2B API (3x)         │
│     CQRS + JWT + Redis      │
└───────────┬─────────────────┘
            │
   ┌────────┼────────┐
   ▼        ▼        ▼
 MSSQL   MongoDB   Redis
 Write     Read     Cache
   │
   ▼
RabbitMQ (Event Bus)
   │
   ▼
B2B Worker (2x)
Hangfire + Consumers
```

**Temel prensipler**:

* **Write:** MSSQL
* **Read:** MongoDB
* **Cache / Session:** Redis
* **Async Communication:** RabbitMQ
* **Background Jobs:** Hangfire

---

<a name="çalışan-servisler"></a>

## 📦 Çalışan Servisler

| Servis        | Replika | Amaç                      |
| ------------- | ------- | ------------------------- |
| B2B API       | 3       | REST API + Swagger        |
| B2B Worker    | 2       | Hangfire & Event Consumer |
| MSSQL         | 1       | Write DB                  |
| MongoDB       | 1       | Read DB                   |
| Redis         | 3       | Cache / Token Store       |
| RabbitMQ      | 3       | Event Bus                 |
| Elasticsearch | 3       | Log Storage               |
| Logstash      | 2       | Log Processing            |
| Kibana        | 1       | Log UI                    |
| Prometheus    | 1       | Metrics                   |
| Grafana       | 1       | Dashboards                |
| Jaeger        | 1       | Distributed Tracing       |

---

<a name="erişim-urlleri"></a>

## 🌐 Erişim URL’leri

| Servis     | URL                                                            | Kullanıcı / Şifre |
| ---------- | -------------------------------------------------------------- | ----------------- |
| Swagger    | [http://localhost:8080/swagger](http://localhost:8080/swagger) | -                 |
| RabbitMQ   | [http://localhost:15672](http://localhost:15672)               | b2b_user / ****   |
| Jaeger     | [http://localhost:16686](http://localhost:16686)               | -                 |
| Kibana     | [http://localhost:5601](http://localhost:5601)                 | -                 |
| Prometheus | [http://localhost:9090](http://localhost:9090)                 | -                 |
| Grafana    | [http://localhost:3000](http://localhost:3000)                 | admin / admin     |

---

<a name="api--kimlik-doğrulama"></a>

## 🔐 API & Kimlik Doğrulama

### Test Kullanıcıları

| Email                                   | Şifre     | Rol   |
| --------------------------------------- | --------- | ----- |
| [admin@demo.com](mailto:admin@demo.com) | Admin123! | Admin |
| [user@demo.com](mailto:user@demo.com)   | Admin123! | User  |

### Login Örneği

```bash
curl -X POST http://localhost:8080/api/v1/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.com","password":"Admin123!"}'
```

Swagger’da **Authorize → Bearer <token>** kullanılır.

---

<a name="dizin-yapısı"></a>

## 📂 Dizin Yapısı

```
k8s/
├── namespaces/
├── config/
├── api/
├── worker/
├── data/
├── messaging/
├── ingress/
├── observability/
├── run.ps1
├── start.bat
└── kustomization.yaml
```

---

<a name="observability"></a>

## 📊 Observability

### Logging – ELK

* Elasticsearch (3 replica, StatefulSet)
* Logstash (B2B pipeline)
* Kibana UI

### Metrics – Prometheus & Grafana

* HPA uyumlu metrikler
* Hazır dashboard’lar

### Tracing – Jaeger

* OpenTelemetry (OTLP)
* API → Worker → DB izleme

---

<a name="scaling--health-checks"></a>

## 📈 Scaling & Health Checks

* **HPA:** CPU %70 / Memory %80
* **Liveness:** `/health/live`
* **Readiness:** `/health/ready`

---

<a name="troubleshooting"></a>

## 🐛 Troubleshooting

### Pod Çalışmıyor

```powershell
kubectl describe pod <pod> -n <ns>
kubectl logs <pod> -n <ns>
```

### 401 Unauthorized

* Token süresi dolmuş olabilir
* Redis temizlenmiş olabilir
* JWT secret uyumsuz olabilir

---

<a name="prerequisites"></a>

## ✅ Prerequisites

* Docker Desktop (Kubernetes enabled)
* kubectl
* PowerShell 5.1+


