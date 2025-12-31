🇹🇷 [Türkçe](README.md) | 🇬🇧 English
- 📘 [Architecture Guide](docs/architecture_en.md)

# 🚀 B2B Microservices Platform 2026

An **enterprise-grade, event-driven, observability-first Kubernetes microservices platform** designed for modern B2B systems.

This repository provides:

* **Production-ready Kubernetes manifests** (not demo-level)
* CQRS + Event-Driven architecture
* Full observability with **ELK, Prometheus, Grafana, Jaeger**
* .NET-based API & Worker services

---

## 📌 Table of Contents

* [Quick Start](#hızlı-başlangıç)
* [Architecture Overview](#mimari-genel-bakış)
* [Running Services](#çalışan-servisler)
* [Access URLs](#erişim-urlleri)
* [API & Authentication](#api--kimlik-doğrulama)
* [Directory Structure](#dizin-yapısı)
* [Observability](#observability)
* [Scaling & Health Checks](#scaling--health-checks)
* [Troubleshooting](#troubleshooting)
* [Prerequisites](#prerequisites)

> 🔗 **Note:** Anchor names are intentionally identical to the Turkish README to guarantee 1:1 navigation compatibility.

---

<a name="hızlı-başlangıç"></a>

## 🚀 Quick Start

### Start the Entire System with One Command

```powershell
cd k8s
.\run.ps1
```

or:

```powershell
start.bat
```

This script will automatically:

1. Create Kubernetes namespaces
2. Deploy all services
3. Start MSSQL, MongoDB, Redis, RabbitMQ
4. Initialize databases (B2BWriteDb, HangfireDb)
5. Start API & Worker services
6. Deploy ELK, Prometheus, Grafana, Jaeger
7. Start required port-forwards

### Stop the System

```powershell
Get-Process kubectl | Stop-Process
```

---

<a name="mimari-genel-bakış"></a>

## 🏗️ Architecture Overview

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

**Core principles**:

* **Write model:** MSSQL
* **Read model:** MongoDB
* **Cache / Session:** Redis
* **Async messaging:** RabbitMQ
* **Background processing:** Hangfire

---

<a name="çalışan-servisler"></a>

## 📦 Running Services

| Service       | Replicas | Purpose                    |
| ------------- | -------- | -------------------------- |
| B2B API       | 3        | REST API + Swagger         |
| B2B Worker    | 2        | Hangfire & Event Consumers |
| MSSQL         | 1        | Write Database             |
| MongoDB       | 1        | Read Database              |
| Redis         | 3        | Cache / Token Store        |
| RabbitMQ      | 3        | Event Bus                  |
| Elasticsearch | 3        | Log Storage                |
| Logstash      | 2        | Log Processing             |
| Kibana        | 1        | Log UI                     |
| Prometheus    | 1        | Metrics                    |
| Grafana       | 1        | Dashboards                 |
| Jaeger        | 1        | Distributed Tracing        |

---

<a name="erişim-urlleri"></a>

## 🌐 Access URLs

| Service    | URL                                                            | Credentials     |
| ---------- | -------------------------------------------------------------- | --------------- |
| Swagger    | [http://localhost:8080/swagger](http://localhost:8080/swagger) | -               |
| RabbitMQ   | [http://localhost:15672](http://localhost:15672)               | b2b_user / **** |
| Jaeger     | [http://localhost:16686](http://localhost:16686)               | -               |
| Kibana     | [http://localhost:5601](http://localhost:5601)                 | -               |
| Prometheus | [http://localhost:9090](http://localhost:9090)                 | -               |
| Grafana    | [http://localhost:3000](http://localhost:3000)                 | admin / admin   |

---

<a name="api--kimlik-doğrulama"></a>

## 🔐 API & Authentication

### Test Users

| Email                                   | Password  | Role  |
| --------------------------------------- | --------- | ----- |
| [admin@demo.com](mailto:admin@demo.com) | Admin123! | Admin |
| [user@demo.com](mailto:user@demo.com)   | Admin123! | User  |

### Login Example

```bash
curl -X POST http://localhost:8080/api/v1/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.com","password":"Admin123!"}'
```

Use **Authorize → Bearer <token>** in Swagger UI.

---

<a name="dizin-yapısı"></a>

## 📂 Directory Structure

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

* Elasticsearch (3 replicas, StatefulSet)
* Logstash (B2B pipelines)
* Kibana UI

### Metrics – Prometheus & Grafana

* HPA-ready metrics
* Preconfigured dashboards

### Tracing – Jaeger

* OpenTelemetry (OTLP)
* API → Worker → DB tracing

---

<a name="scaling--health-checks"></a>

## 📈 Scaling & Health Checks

* **HPA:** CPU 70% / Memory 80%
* **Liveness:** `/health/live`
* **Readiness:** `/health/ready`

---

<a name="troubleshooting"></a>

## 🐛 Troubleshooting

### Pod Not Running

```powershell
kubectl describe pod <pod> -n <namespace>
kubectl logs <pod> -n <namespace>
```

### 401 Unauthorized

* Token expired
* Redis flushed
* JWT secret mismatch

---

<a name="prerequisites"></a>

## ✅ Prerequisites

* Docker Desktop (Kubernetes enabled)
* kubectl
* PowerShell 5.1+

