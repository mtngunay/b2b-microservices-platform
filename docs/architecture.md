🇹🇷 Türkçe | 🇬🇧 [English](architecture_en.md)

---

## 🎯 Mimari Amaçlar

Bu mimari aşağıdaki problemleri çözmek için tasarlanmıştır:

* Kurumsal B2B sistemlerde **yüksek trafik ve ölçeklenebilirlik**
* Okuma ve yazma yüklerinin ayrıştırılması (CQRS)
* Servisler arası **gevşek bağlılık** (event-driven)
* Production ortamında **izlenebilirlik (observability)**
* Background job ve async süreçlerin güvenli çalışması

---

## 🧱 Temel Mimari Prensipler

### 1. CQRS (Command Query Responsibility Segregation)

* **Write Model:** MSSQL
* **Read Model:** MongoDB

**Neden?**

* Yazma işlemleri transactional ve güçlü consistency ister
* Okuma işlemleri hızlı, esnek ve query odaklıdır

Bu ayrım sayesinde:

* Performans artar
* Read tarafı bağımsız ölçeklenir
* Domain karmaşıklığı azalır

---

### 2. Event-Driven Architecture

* Servisler **doğrudan birbirini çağırmaz**
* Olaylar RabbitMQ üzerinden publish edilir

Örnek event akışı:

```
UserCreated
   │
   ▼
RabbitMQ
   │
   ▼
B2B Worker → Email / Audit / Cache Invalidation
```

**Kazanımlar:**

* Loose coupling
* Retry & durability
* Eventually consistent süreçler

---

### 3. Redis Kullanımı

Redis şu amaçlarla kullanılır:

* JWT token store
* Session & rate limit
* Cache

Token lifecycle:

```
Login → JWT üret → Redis'e yaz → TTL
Logout → Redis'ten sil
```

---

### 4. Background Processing (Hangfire)

* Uzun süren işler API thread’lerini bloke etmez
* Retry, dashboard ve persistence sağlar

Örnek işler:

* Email gönderimi
* Event handling
* Data senkronizasyonu

---

## 🔍 Observability-First Yaklaşım

### Logging

* Serilog → Logstash → Elasticsearch → Kibana

### Metrics

* Prometheus scrape
* Grafana dashboard

### Tracing

* OpenTelemetry → Jaeger

Her request için:

* CorrelationId
* TraceId
* Span bilgileri

---

## ⚖️ Ne Zaman Bu Mimariyi KULLANMAMALISIN?

❌ Küçük CRUD uygulamalar
❌ Monolitik MVP’ler
❌ Eventual consistency kabul edilemeyen sistemler
