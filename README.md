# MAPS — Medical AI Prediction System

> A comprehensive, role-based medical platform integrating ML.NET disease prediction,
> ONNX-powered image classification, NLP-driven clinical note processing, personalized
> AI chatbot with multi-modal inputs, Docker containerization, and Kubernetes orchestration.

**BSCS-6-B · KICSIT Computer Science Department · Sir Uzair Janjua**

---

## Team

| Name | GitHub Handle | Responsibility |
|------|--------------|----------------|
| Muhammad Ameer Hamza | @ameer-hamza | Backend + AI/ML Lead |
| Muhammad Jazib Asad Kayani | @jazib-kayani | Full-Stack + Modules |
| Muhammad Abdullah Khan | @abdullah-khan | DevOps + Frontend |

---

## Tech Stack (All Free & Open-Source)

| Layer | Technologies |
|-------|-------------|
| Backend | C# 12, .NET 8, ASP.NET Core Web API, SignalR, Hangfire |
| Frontend | ASP.NET Core MVC, Blazor WebAssembly, Bootstrap 5, Chart.js |
| Desktop | WinForms (.NET 8) |
| AI / ML | ML.NET, ONNX Runtime, Ollama LLaMA 3, Whisper.cpp, pgvector RAG |
| Database | PostgreSQL 16, EF Core 8, Redis, MinIO |
| Security | ASP.NET Core Identity, JWT, TLS 1.3, AES-256 |
| DevOps | Docker, Docker Compose, Kubernetes, Helm, GitHub Actions |
| Monitoring | Grafana, Grafana Loki, Prometheus, Serilog |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Git](https://git-scm.com/)
- (Optional for K8s) [kubectl](https://kubernetes.io/docs/tasks/tools/) + [Helm](https://helm.sh/)

---

## Quick Start — Docker Compose (Recommended)

```bash
# 1. Clone the repository
git clone https://github.com/YOUR_ORG/MAPS.git
cd MAPS

# 2. Copy and configure environment variables
cp .env.example .env
# Edit .env with your values (DB password, JWT secret, etc.)

# 3. Build .NET Docker images
docker compose -f infra/docker/docker-compose.yml build

# 4. Start all 11 containers
docker compose -f infra/docker/docker-compose.yml up -d

# 5. Verify all containers are healthy
docker compose ps

# 6. Access the system
# MVC Web:   https://localhost
# Swagger:   https://localhost/swagger
# Grafana:   http://localhost:3000
```

---

## Development Setup (Without Docker)

```bash
# 1. Install dependencies
dotnet restore MAPS.sln

# 2. Set up PostgreSQL locally and update appsettings.Development.json

# 3. Run EF Core migrations
cd src/MAPS.API
dotnet ef database update

# 4. Run the API
dotnet run --project src/MAPS.API

# 5. Run the MVC Web (separate terminal)
dotnet run --project src/MAPS.Web
```

---

## Project Structure

```
MAPS/
├── MAPS.sln
├── Directory.Build.props       ← Centralised NuGet versions
├── src/
│   ├── MAPS.API/               ← ASP.NET Core Web API (backend)
│   ├── MAPS.Web/               ← ASP.NET Core MVC (web frontend)
│   ├── MAPS.Desktop/           ← WinForms (clinic desktop client)
│   ├── MAPS.ML/                ← ML.NET + ONNX pipelines
│   ├── MAPS.Shared/            ← Shared DTOs, Enums, Constants
│   └── MAPS.Tests/             ← xUnit integration tests
├── infra/
│   ├── docker/                 ← Docker Compose + Nginx + Prometheus
│   └── kubernetes/             ← Helm chart for production K8s
├── models/
│   ├── onnx/                   ← Pre-trained ONNX CNN models
│   └── mlnet/                  ← ML.NET trained model files
├── data/
│   ├── datasets/               ← Training datasets (CSV)
│   └── seed/                   ← SQL seed scripts for demo data
└── docs/                       ← Architecture, API reference, guides
```

---

## Role Hierarchy

| Role | Access Level |
|------|-------------|
| **Admin** | Full system control — users, assignments, analytics, Docker/K8s |
| **Doctor** | AI prediction, chatbot (4 modalities), patients, prescriptions, chat |
| **Patient** | Appointments, health records, doctor-shared predictions, feedback |

---

## Supported Disease Predictions

| Disease | Input Type |
|---------|-----------|
| Diabetes | Structured (Glucose, BMI, Age) + Text |
| Heart Disease | Structured (ECG, Cholesterol) + Text |
| Pneumonia | Chest X-Ray (ONNX) + Text |
| Brain Tumour | MRI Scan (ONNX) + Text |
| Skin Cancer | Skin Lesion Photo (ONNX) + Text |

---

## Kubernetes Deployment

```bash
# Deploy to production K8s cluster
helm upgrade --install maps-prod infra/kubernetes/charts/maps \
  -f infra/kubernetes/charts/maps/values.prod.yaml \
  -n maps-production

# Verify rollout
kubectl rollout status deployment/maps-api -n maps-production
kubectl get pods --all-namespaces
```

---

## Contributing

1. Pull latest main: `git pull origin main`
2. Create feature branch: `git checkout -b chunk/NN-description-INITIALS`
3. Commit in small logical units
4. Push and open a PR — requires 2 approvals before merge
5. Never push directly to `main`

---

## License

Academic project — KICSIT Computer Science Department, 2026.
All technologies used are free and open-source.
