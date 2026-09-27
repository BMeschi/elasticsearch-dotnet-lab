# Lab 05 — Aggregations & Analytics

> **Focus**: Building high-performance faceted search, price metrics, and category distributions using Elasticsearch Aggregations.

---

## 1. Aggregations vs Document Search

In Elasticsearch:
- **Search Query**: Retrieves individual matching documents and ranks them by `_score`.
- **Aggregations**: Computes statistical summaries, distributions, and faceted buckets across documents matching the query (using columnar `doc_values`).

---

## 🧪 How to Test in Swagger (Interactive Reproduction)

Open Swagger UI at `http://localhost:5000/swagger` and navigate to the **`Lab 05: Aggregations & Facets`** section:

### Test 1: Nested Sub-Aggregations — Average Price per Category (`GET /api/labs/05/average-price-per-category`)

1. Expand `GET /api/labs/05/average-price-per-category`.
2. Click **Execute** (no parameters required).
3. **What to Observe in Response**:
   - The `Categories` array returns each category bucket along with its `ProductCount` and the computed `AveragePrice`.
   - Example:
     ```json
     {
       "category": "Computers",
       "productCount": 6,
       "averagePrice": 8415.66
     }
     ```
   - *Technical Note*: The query runs with `size: 0` so no document bodies are returned, maximizing performance and minimizing serialization overhead.

---

### Test 2: Matrix Numeric Price Statistics (`GET /api/labs/05/price-statistics`)

1. Expand `GET /api/labs/05/price-statistics`.
2. Click **Execute** (no parameters required).
3. **What to Observe in Response**:
   - Returns full descriptive statistics across all 40 products in a single pass:
     - `Count`: Total number of priced items (40).
     - `Min`: Cheapest product price.
     - `Max`: Most expensive product price.
     - `Avg`: Average product catalog price.
     - `Sum`: Total inventory catalog value.

---

## 2. Best Practices & Performance

1. **Use `keyword` or numeric fields for aggregations**: Elasticsearch computes aggregations on disk-based columnar `doc_values`. Trying to aggregate on an analyzed `text` field requires loading expensive in-memory `fielddata`, which can trigger out-of-memory errors on large clusters.
2. **Set `"size": 0`** when rendering standalone dashboard widgets or facet menus.
3. **Combine with `bool.filter`**: Filter documents before aggregating to restrict calculations to the active user scope (e.g., active catalog, tenant).
