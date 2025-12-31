🇹🇷 [Türkçe](architecture.md) | 🇬🇧 English


## 🎯 Architectural Goals

This architecture is designed to address:

* High traffic and scalability in enterprise B2B systems
* Separation of read/write workloads (CQRS)
* Loose coupling between services (event-driven)
* Production-grade observability
* Reliable background and async processing

---

## 🧱 Core Architectural Principles

### 1. CQRS (Command Query Responsibility Segregation)

* **Write Model:** MSSQL
* **Read Model:** MongoDB

**Why?**

* Writes require strong consistency
* Reads require speed and flexibility

Benefits:

* Better performance
* Independent scaling
* Reduced domain complexity

---

### 2. Event-Driven Architecture

* Services do not call each other synchronously
* Events are published via RabbitMQ

Example flow:

```
UserCreated → RabbitMQ → Worker → Side Effects
```

Benefits:

* Loose coupling
* Durability & retries
* Eventual consistency

---

### 3. Redis Usage

Redis is used for:

* JWT token storage
* Sessions & rate limiting
* Caching

---

### 4. Background Processing (Hangfire)

* Prevents blocking API threads
* Provides retries and monitoring

---

## 🔍 Observability-First Design

* **Logs:** ELK Stack
* **Metrics:** Prometheus & Grafana
* **Tracing:** OpenTelemetry & Jaeger

Every request includes:

* CorrelationId
* TraceId

---

## ⚖️ When NOT to Use This Architecture

❌ Simple CRUD apps
❌ Early-stage MVPs
❌ Systems requiring strict immediate consistency

