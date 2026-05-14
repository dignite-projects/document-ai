# Dignite Paperbase

A modular, extensible ABP-based application for paperless workflows — built **AI-native** for the LLM era.

## Design Principle: Markdown-first

Paperbase is an AI-driven enterprise document platform. Every text-bearing document — whether a digital PDF, a Word file, or a scanned image — flows through the pipeline as **Markdown**, never plain text. **For structured documents** (contracts, reports, CSV, DOCX with headings, PP-StructureV3 / Azure DI layout output), headings, tables and lists carry real semantic structure that downstream consumers (vector chunking, LLM classification / Q&A / rerank, business-module field extraction) rely on. **For unstructured content** (OCR loose paragraphs, plain `.txt`, PP-OCRv4 line dumps), the Markdown wrapper is a container name — it keeps the downstream pipeline on one shape, not a signal in itself. Plain-text fallback paths are a design violation; nullable-text projections happen on the consumer side via `MarkdownStripper.Strip(...)` only when truly needed (e.g. keyword fallback classifiers).

**Markdown-first is an engineering default, not a creed.** Out-of-band signals (coordinates, confidence, page metadata, key-value structure) are orthogonal to Markdown. When future needs arise (citation highlighting, stamp localization, form key-value extraction), they belong on `TextExtractionResult` as **named, optional, strongly-typed** fields — not stuffed back into the Markdown string and not hidden behind a `Dictionary<string, object>` extension slot. See `CLAUDE.md` → "Markdown-first 数据流" for the full contract.

## Solution Structure

```
dignite-paperbase/
├── core/       # Core ABP module — domain models, repositories, application services, HTTP API
├── modules/    # Reusable business modules
├── host/       # Host application for development and deployment
│   ├── src/    # ASP.NET Core API backend
│   └── angular/# Angular SPA frontend
└── docs/       # Developer documentation and design documents
```

## Pre-requirements

* [.NET 10.0+ SDK](https://dotnet.microsoft.com/download/dotnet)
* [Node.js v18 or later](https://nodejs.org/en)
* PostgreSQL 16+ with the **pgvector** extension

### Installing pgvector

**Ubuntu / Debian (including WSL):**

```bash
sudo apt install -y postgresql-16-pgvector
```

If the package is not found, add the official PGDG repository first:

```bash
sudo sh -c 'echo "deb https://apt.postgresql.org/pub/repos/apt $(lsb_release -cs)-pgdg main" > /etc/apt/sources.list.d/pgdg.list'
wget -qO- https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo tee /etc/apt/trusted.gpg.d/postgresql.asc > /dev/null
sudo apt update
sudo apt install -y postgresql-16-pgvector
```

**Docker:** use the `pgvector/pgvector:pg17` image instead of `postgres:17` — pgvector is pre-installed.

**Other platforms:** see the [pgvector installation guide](https://github.com/pgvector/pgvector#installation).

## Getting Started (Local Development)

### 1. Start infrastructure services

Qdrant (vector search) and PaddleOCR (OCR sidecar) run as Docker containers. Start them before the backend:

```bash
cd host
docker compose up -d
```


### 2. Configure the database

Create `host/src/appsettings.Development.json` with your local database connection:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  },
  "ConnectionStrings": {
    "Default": "Server=YOUR_DB_SERVER;Database=Paperbase-Dev;User ID=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=true"
  },
  "StringEncryption": {
    "DefaultPassPhrase": "YOUR_ENCRYPTION_KEY"
  }
}
```

> This file is git-ignored. In Development mode, the application automatically generates temporary OpenIddict certificates — no `.pfx` file is needed.

### 3. Install client-side libraries

The host project includes a login UI that requires client-side libraries. Run once after cloning or when dependencies change:

```bash
cd host/src
abp install-libs
```

### 4. Run the backend

```bash
cd host/src
dotnet run
```

### 5. Install frontend dependencies and run Angular

```bash
cd host/angular
npm install
npm start
```

## Choosing an OCR Provider

Paperbase ships two OCR providers — local **PaddleOCR** (default, CPU, no network) and cloud **Azure Document Intelligence**. PaddleOCR is the zero-config default for development; Azure DI is the recommended production option when data is allowed to leave the network.

Full selection guidance, configuration, and resource footprint: see [docs/text-extraction.md](./docs/text-extraction.md).

## Deploying to Production

For database connection strings, OpenIddict signing certificate, string-encryption key, Docker layout and the migration boundary between PostgreSQL and Qdrant, see [docs/deployment.md](./docs/deployment.md). For per-release smoke tests, see [docs/deployment-checklist.md](./docs/deployment-checklist.md).

## Documentation

Feature docs (start here for any specific topic):

* [Local development setup](./docs/local-development.md) — prerequisites, Docker services, configuration, troubleshooting
* [Text extraction](./docs/text-extraction.md) — Markdown-first contract, PaddleOCR / Azure DI configuration
* [Classification](./docs/classification.md) — document-type pipeline and prompt tuning
* [Embedding](./docs/embedding.md) — Markdown-aware chunking, switching the embedding model
* [Vector store](./docs/vectors.md) — Qdrant + Microsoft.Extensions.VectorData configuration, dense-only retrieval, swapping the backing store
* [Document chat](./docs/chat.md) — feature overview, rerank, tool contributors → [HTTP client guide](./docs/chat-client.md)
* [AI provider](./docs/ai-provider.md) — wiring `IChatClient` and `IEmbeddingGenerator`
* [Structured extraction](./docs/structured-extraction.md) — `IExtractionValidator<T>` + MAF Agent Middleware for LLM field extraction with retry
* [Observability](./docs/observability.md) — OpenTelemetry pipeline, aspire-dashboard for local dev, switching OTLP backends
* [Pipeline runs](./docs/pipeline-runs.md) — run history and review-UI payloads
* [Deployment](./docs/deployment.md) — DB, Qdrant, certificate, Docker

External references:

* [Angular Application](./host/angular/README.md)
* [ABP Framework Documentation](https://abp.io/docs/latest)
* [Application (Single Layer) Startup Template](https://abp.io/docs/latest/solution-templates/application-single-layer)
* [Configuring OpenIddict for Production](https://abp.io/docs/latest/Deployment/Configuring-OpenIddict#production-environment)
