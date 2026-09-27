# Lab 04 — Query Optimization & Troubleshooting

> **Focus**: Diagnosing slow queries, eliminating unneeded scoring calculations, and leveraging Elasticsearch caching.

---

## 1. Problem Scenario

An API endpoint `/api/products/search` experiences latency spikes during traffic peaks.

Upon profiling the Elasticsearch queries, we discovered the following problematic query structure:

### The Problematic Query (Before)

```json
POST /products/_search
{
  "profile": true,
  "query": {
    "bool": {
      "must": [
        { "wildcard": { "name": "*notebook*" } },
        { "match": { "available": true } },
        { "range": { "price": { "gte": 2000, "lte": 8000 } } }
      ]
    }
  }
}
```

### Why This Is Inefficient (Root Cause Analysis):
1. **Leading Wildcard (`*notebook*`)**: Forces Elasticsearch to perform a full scan of all terms in the inverted index dictionary, bypassing Lucene's finite state transducer (FST) optimization.
2. **Scoring on Exact Business Filters**: `available` and `price` are placed inside `must` clauses. Elasticsearch calculates Lucene BM25 scores for boolean and range checks, burning CPU unnecessarily.
3. **No Bitset Caching**: Queries inside `must` are never cached by the Node Query Cache.

---

## 🧪 How to Test in Swagger (Interactive Reproduction)

Open Swagger UI at `http://localhost:5000/swagger` and navigate to the **`Lab 04: Query Optimization`** section:

### Test 1: Run the Unoptimized Query (`GET /api/labs/04/unoptimized-query`)
1. Expand `GET /api/labs/04/unoptimized-query`.
2. Fill the parameters:
   - **`q`**: `notebook`
3. Click **Execute**.
4. **What to Observe in Response**:
   - `Approach`: Indicates that a leading wildcard `*notebook*` was executed and all boolean/range filters were placed in `must` context.
   - Every document has a score computed from boolean/range conditions.

---

### Test 2: Run the Optimized Query (`GET /api/labs/04/optimized-query`)
1. Expand `GET /api/labs/04/optimized-query`.
2. Fill the parameters:
   - **`q`**: `notebook`
3. Click **Execute**.
4. **What to Observe in Response**:
   - `Approach`: Analyzed `match` query with business constraints isolated into `filter` clauses.
   - Scoring is computed **only** for textual relevance of the word `notebook`.
   - The filter bitsets are automatically cached by Elasticsearch for high-throughput repeat queries.

---

## 2. Comparison & Profiling Metrics

| Metric | Before (Unoptimized) | After (Optimized) | Improvement |
|---|---|---|---|
| **Query Type** | `WildcardQuery` + `TermScorer` | `MatchQuery` + `TermFilter` | Reduced term dictionary scan |
| **Scoring Workload** | 3 clauses computed `_score` | Only 1 clause computed `_score` | ~66% reduction in scoring computation |
| **Filter Cacheability** | 0% (in `must` context) | 100% (in `filter` context) | Bitsets cached in node memory |
| **Response Latency** | Higher latency under load | Sub-millisecond on repeat filter queries | Significantly more scalable |
