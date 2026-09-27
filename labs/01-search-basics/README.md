# Lab 01 — Basic Search Fundamentals

> **Focus**: Understanding when to use `term`, `match`, `match_phrase`, and `bool` filters.

---

## 1. Problem Scenario

A team migrates product search to Elasticsearch. Soon after launch, stakeholders report two common bugs:
1. **Case A**: Querying `term: { "category": "Computers" }` works, but querying `term: { "name": "Notebook" }` returns **0 hits**, even though multiple products have "Notebook" in the title.
2. **Case B**: A search for `"Dell Notebook"` with standard `match` returns backpacks, cables, and unrelated items before the actual laptop.

---

## 2. Dataset Context

Consider the following products in the index:

| ID | Name | Category | Brand | Description |
|---|---|---|---|---|
| **1** | Notebook Dell Inspiron 15 | Computers | Dell | Notebook Dell Inspiron 15 com Intel Core i7... |
| **4** | Mochila Executiva para Notebook Dell até 15.6 | Accessories | Acme Gear | Mochila para transporte de notebook Dell... |
| **8** | Notebook Lenovo ThinkPad E14 | Computers | Lenovo | Notebook corporativo Lenovo ThinkPad... |

---

## 3. Query Investigation & Theory

### 3.1 `term` Query (Exact Keyword / Token Match)
The `term` query looks for the **exact string** in the inverted index without analyzing the query text.
Because `name` was processed by an analyzer, the token in the index is `"notebook"` (lowercase). The term query searches for uppercase `"Notebook"` and finds nothing.
* **Rule**: Exclusively use `term` on `keyword` fields (e.g. `brand: "Dell"`, `category: "Computers"`).

### 3.2 `match` Query (Full-Text Analyzed Search)
The `match` query analyzes the input `"Notebook Dell"` into tokens: `["notebook", "dell"]` and searches for matches across the analyzed inverted index.

### 3.3 `match_phrase` Query (Sequence & Proximity Match)
Requires all tokens to appear in the exact order and position.

### 3.4 Production Pattern: `bool` with `must` (Scoring) and `filter` (Cached / Non-Scoring)
Isolates business constraints (category, price range, stock availability) into `filter` clauses to leverage node bitset cache and reduce BM25 scoring overhead.

---

## 🧪 How to Test in Swagger (Interactive Reproduction)

Open Swagger UI at `http://localhost:5000/swagger` and navigate to the **`Lab 01: Search Basics`** section:

### Test 1: Compare `term` vs `match` (`GET /api/labs/01/term-vs-match`)
1. Expand `GET /api/labs/01/term-vs-match`.
2. Fill the parameters:
   - **`field`**: `name`
   - **`value`**: `Notebook` (with capital `N`)
3. Click **Execute**.
4. **What to Observe in Response**:
   - `TermQuery.TotalHits`: `0` *(Proves that searching with uppercase on an analyzed text field fails).*
   - `MatchQuery.TotalHits`: `> 0` *(Proves that `match` analyzes the query and matches lowercased tokens).*
5. **Next test (Keyword field)**: Change `field` to `brand` and `value` to `Dell`.
   - Both `TermQuery` and `MatchQuery` return hits because `brand` is a keyword field.

---

### Test 2: Match Exact Phrase (`GET /api/labs/01/match-phrase`)
1. Expand `GET /api/labs/01/match-phrase`.
2. Fill the parameters:
   - **`phrase`**: `Notebook Dell`
3. Click **Execute**.
4. **What to Observe in Response**:
   - Returns products where "Notebook" is immediately followed by "Dell" (e.g., *Notebook Dell Inspiron 15*, *Notebook Dell XPS*).
   - Excludes *Notebook Lenovo ThinkPad* because "Lenovo" breaks the phrase sequence.

---

### Test 3: Scored Search with Cached Filters (`GET /api/labs/01/bool-filter`)
1. Expand `GET /api/labs/01/bool-filter`.
2. Fill the parameters:
   - **`q`**: `notebook`
   - **`category`**: `Computers`
   - **`minPrice`**: `3000`
   - **`maxPrice`**: `7000`
3. Click **Execute**.
4. **What to Observe in Response**:
   - Returns only products within the price range and category, ranked by the relevance score of the word `notebook`.
