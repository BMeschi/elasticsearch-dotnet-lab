# Acme Store — Synthetic Products Dataset

This dataset contains **40 realistic e-commerce products** from a fictitious company named **Acme Store**.

> [!IMPORTANT]
> **Data Privacy & Synthetic Origin**:  
> All data in this directory is 100% synthetic and created purely for technical demonstration, educational research, and search relevance benchmarks. No proprietary, corporate, or customer data is used.

## Schema

```json
{
  "id": 1,
  "name": "Notebook Dell Inspiron 15",
  "description": "Notebook Dell Inspiron 15 com processador Intel Core i7, 16GB RAM, SSD 512GB...",
  "category": "Computers",
  "brand": "Dell",
  "price": 4899.00,
  "tags": ["notebook", "laptop", "dell", "intel", "computador"],
  "available": true,
  "rating": 4.7
}
```

## Field Mappings & Analysis Rationale

| Field | Elasticsearch Type | Analyzer / Subfield | Purpose |
|---|---|---|---|
| `id` | `integer` | - | Unique product identifier |
| `name` | `text` | `products_analyzer` (asciifolding + lowercase), `keyword`, `standard` | Primary search target; supports accent-insensitive search, exact sorting, and analyzer comparisons |
| `description` | `text` | `products_analyzer`, `standard` | Full-text contextual matching |
| `category` | `keyword` | `.text` (analyzed) | Fast exact filtering and term aggregations |
| `brand` | `keyword` | `.text` (analyzed) | Brand filtering and aggregations |
| `price` | `double` | - | Numeric range filtering and metric aggregations (min, max, avg) |
| `tags` | `keyword` | - | Exact tag matching and term filtering |
| `available` | `boolean` | - | Inventory state filtering |
| `rating` | `float` | - | Sorting and relevance scoring |

## Scenarios Covered in Dataset

- **Accents & Diacritics**: `Cafeteira Elétrica`, `Teclado Mecânico`, `Câmera de Segurança`, `Aspirador de Pó Robô`.
- **Cross-term Ambiguity**: Products that contain a brand in their accessory title (e.g., `"Mochila Executiva para Notebook Dell"`) vs. the actual laptop (`"Notebook Dell Inspiron 15"`).
- **Compound & Synonym Terms**: `notebook`, `laptop`, `ultrabook`, `computador`.
- **Fuzzy / Misspellings Candidates**: `iPhone`, `Xiaomi`, `Alienware`, `AirPods`, `QuietComfort`.
