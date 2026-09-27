# Elasticsearch .NET Lab 🔍⚡

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Elasticsearch 9.x](https://img.shields.io/badge/Elasticsearch-9.x-005571.svg?logo=elasticsearch)](https://www.elastic.co/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED.svg?logo=docker)](https://www.docker.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

> **A practical portfolio and technical laboratory demonstrating how to investigate, troubleshoot, optimize, and build robust search solutions with Elasticsearch and .NET 10.**

---

## 🎯 Why This Project Exists

In production environments, search systems rarely fail because of simple syntax errors. They fail due to subtle architectural and domain-specific challenges:

- **Analyzer mismatches**: Accents, casing, or tokenizers causing 0 hits on obvious queries.
- **Relevance collapse**: Generic or accessory products outranking high-intent core products.
- **Suboptimal Query DSL**: Burning Lucene CPU on non-scoring criteria and leading wildcards.
- **Mapping oversights**: Aggregating on analyzed text instead of keyword doc values.

This repository demonstrates the **Problem → Investigation → Solution → Result** engineering approach across real-world search scenarios.

---

## 🏗️ Architecture & Stack

```text
                     ┌────────────────────────────────┐
                     │   ASP.NET Core (.NET 10 API)   │
                     │    Minimal APIs & Services     │
                     └───────────────┬────────────────┘
                                     │
                     ┌───────────────▼────────────────┐
                     │  Elastic.Clients.Elasticsearch │
                     │      (Official v9.x Client)    │
                     └───────────────┬────────────────┘
                                     │
                     ┌───────────────▼────────────────┐
                     │      Elasticsearch 9.x         │
                     │   Docker Single-Node Cluster   │
                     └────────────────────────────────┘
```

- **Runtime & Language**: .NET 10, C# 13, ASP.NET Core Minimal APIs
- **Elasticsearch Client**: `Elastic.Clients.Elasticsearch` (v9.x official client)
- **Search Engine**: Elasticsearch 9.x (Docker containerized)
- **Dataset**: 100% Synthetic e-commerce brazilian (pt-br) catalog (*Acme Store*) designed for search diagnostics

---

## 🧪 Search Labs & Troubleshooting Guides

Each lab provides a dedicated investigation guide, query comparisons, and takeaways:

| Lab | Topic | Focus & Scenarios | Guide Link |
|---|---|---|:---:|
| **Lab 01** | **Search Basics** | `term` vs `match` vs `match_phrase`, `bool` must/filter contexts | [Explore Lab 01](labs/01-search-basics/README.md) |
| **Lab 02** | **Analyzers & Tokenization** | Accent folding (`asciifolding`), case sensitivity, `_analyze` API diagnostics | [Explore Lab 02](labs/02-analyzers-and-tokenization/README.md) |
| **Lab 03** | **Search Relevance** | Field boosting (`^`), `bool.should` phrase bonuses, controlled fuzzy matching | [Explore Lab 03](labs/03-search-relevance/README.md) |
| **Lab 04** | **Query Optimization** | Eliminating slow wildcards, moving business filters to cached `filter` clauses, query profiling | [Explore Lab 04](labs/04-query-optimization/README.md) |
| **Lab 05** | **Aggregations & Analytics** | Faceted navigation (terms), price metrics (`min`, `max`, `avg`), `doc_values` | [Explore Lab 05](labs/05-aggregations/README.md) |

---

## 🚀 Quick Start

### 1. Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/)

### 2. Start Elasticsearch
```bash
docker compose up -d
```

Verify Elasticsearch is running:
```bash
curl http://localhost:9200
```

### 3. Seed the Dataset & Run the API
Run the initial seed task to create index mappings, custom analyzers, and ingest the synthetic dataset:

```bash
# Seed index & products
dotnet run --project src/ElasticsearchDotnetLab.Api -- --seed

# Start the API server
dotnet run --project src/ElasticsearchDotnetLab.Api
```

The API will be available at `http://localhost:5000` (or `https://localhost:5001`).

---

## 📡 API Endpoints & Usage

| Method | Endpoint | Description | Example Query |
|---|---|---|---|
| `GET` | `/api/health` | Elasticsearch connection & cluster health | `/api/health` |
| `POST` | `/api/admin/seed` | Recreates index with analyzers & seeds dataset | `/api/admin/seed` |
| `GET` | `/api/products/search` | Full-text search with boosting, filters & fuzzy | `/api/products/search?q=dell%20notebook` |
| `GET` | `/api/products/{id}` | Retrieve single product by ID | `/api/products/1` |
| `GET` | `/api/products/aggregations`| Faceted categories, brands and price statistics | `/api/products/aggregations` |
| `GET` | `/api/search/analyze` | Tokenizer & analyzer diagnostic endpoint | `/api/search/analyze?text=Cafeteira%20Elétrica` |

### Example Search Requests:

```bash
# Search for Dell laptops with relevance boosting
curl "http://localhost:5000/api/products/search?q=dell%20notebook"

# Search with typo-tolerance (fuzzy matching)
curl "http://localhost:5000/api/products/search?q=del%20notebok&fuzzy=true"

# Search with exact phrase matching
curl "http://localhost:5000/api/products/search?q=smart%20tv&phrase=true"

# Search with category and price range filters
curl "http://localhost:5000/api/products/search?category=Computers&minPrice=3000&maxPrice=8000"
```

---

## 📂 Repository Structure

```text
elasticsearch-dotnet-lab/
├── docker-compose.yml                       # Elasticsearch 9.x local environment
├── README.md                                # Portfolio documentation & overview
│
├── src/
│   └── ElasticsearchDotnetLab.Api/          # ASP.NET Core API (.NET 10)
│       ├── Elasticsearch/                   # Index definitions, mappings & seed logic
│       ├── Models/                          # Product domain entities & DTOs
│       ├── Services/                        # Search, Aggregation & Analysis services
│       └── Program.cs                       # Minimal API endpoints & DI setup
│
├── datasets/
│   └── products/                            # Synthetic e-commerce dataset (Acme Store)
│       ├── products.json
│       └── README.md
│
├── labs/                                    # Technical search labs & diagnostic guides
│   ├── 01-search-basics/
│   ├── 02-analyzers-and-tokenization/
│   ├── 03-search-relevance/
│   ├── 04-query-optimization/
│   └── 05-aggregations/
│
├── docs/                                    # Architecture & design notes
│   └── architecture.md
│
└── tests/
    └── ElasticsearchDotnetLab.Tests/        # Automated tests
```

---

## 🔒 Confidentiality & Safe Data Policy

All datasets, product titles, descriptions, and mock scenarios in this repository are **100% synthetic** and created exclusively for portfolio demonstration and technical training.

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
