# Architecture & Design

## System Architecture

```text
                     ┌────────────────────────────────┐
                     │          Client / API          │
                     │  (HTTP / Minimal API .NET 10)  │
                     └───────────────┬────────────────┘
                                     │
                 ┌───────────────────┼───────────────────┐
                 │                   │                   │
                 ▼                   ▼                   ▼
        ┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
        │ /api/products/  │ │ /api/products/  │ │ /api/search/    │
        │ search          │ │ aggregations    │ │ analyze         │
        └────────┬────────┘ └────────┬────────┘ └────────┬────────┘
                 │                   │                   │
                 └───────────────────┼───────────────────┘
                                     │
                     ┌───────────────▼────────────────┐
                     │   Elastic.Clients.Elasticsearch │
                     │       (v9.x Official Client)   │
                     └───────────────┬────────────────┘
                                     │
                     ┌───────────────▼────────────────┐
                     │         Elasticsearch          │
                     │          (Docker / 9.x)        │
                     │                                │
                     │  Index: `products`             │
                     │  Analyzers: `products_analyzer`│
                     └────────────────────────────────┘
```

## Component Overview

1. **`ElasticsearchDotnetLab.Api`**:
   - Built on **.NET 10** Minimal APIs.
   - Leverages the official **`Elastic.Clients.Elasticsearch`** client.
   - Clean separation between index definition/management (`ProductIndexManager`), query handling (`ProductSearchService`), and HTTP endpoints (`Program.cs`).

2. **Index Configuration & Mappings**:
   - `products_analyzer`: Custom analyzer composed of `standard` tokenizer + `lowercase` + `asciifolding` token filters.
   - Multi-field strategy on `name` and `description` to enable both normalized search and exact/term comparisons.

3. **Seeding Strategy**:
   - Automatic or on-demand seed ingestion via CLI (`dotnet run -- --seed`) or API endpoint (`POST /api/admin/seed`).
   - Reads from `datasets/products/products.json` using bulk operations.
