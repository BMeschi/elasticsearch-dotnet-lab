# Lab 02 — Analyzers & Tokenization

> **Core Concept**: Most search bugs are not query bugs — they are analysis and mapping bugs.

---

## 1. Problem Scenario

Customers in international or multilingual markets report missing results:
- Searching for `"cafeteira eletrica"` (without accent) fails to match `"Cafeteira Elétrica Oster"` when both terms are required.
- Searching for `"teclado mecanico"` or `"robo"` returns 0 results on standard-mapped text fields.
- Searching for `"José"` vs `"Jose"` fails to match documents.

Developers often attempt to fix this by writing complex wildcard or regex queries. **This is an antipattern that severely degrades performance.** The true fix lies in the analysis pipeline.

---

## 2. The Analysis Pipeline

An analyzer processes text during **indexing** and during **search** through three stages:

```text
Input Text: "Cafeteira Elétrica!"
      │
      ▼
1. Character Filters  ──> (HTML strip, mapping chars, regex replacement)
      │
      ▼
2. Tokenizer          ──> Splits text into terms: ["Cafeteira", "Elétrica"]
      │
      ▼
3. Token Filters      ──> Lowercase:    ["cafeteira", "elétrica"]
                          Asciifolding: ["cafeteira", "eletrica"]
      │
      ▼
Inverted Index        ──> Tokens stored: ["cafeteira", "eletrica"]
```

---

## 🧪 How to Test in Swagger (Interactive Reproduction)

Open Swagger UI at `http://localhost:5000/swagger` and navigate to the **`Lab 02: Analyzers & Tokenization`** section:

### Test 1: Diagnose Token Generation (`GET /api/labs/02/analyze`)

#### Step 1.1: Test Custom Analyzer (`products_analyzer`)
1. Expand `GET /api/labs/02/analyze`.
2. Fill the parameters:
   - **`text`**: `Cafeteira Elétrica`
   - **`analyzer`**: `products_analyzer`
3. Click **Execute**.
4. **What to Observe in Response**:
   - The generated tokens are `["cafeteira", "eletrica"]`.
   - The token filter `asciifolding` converted `é` to `e`.

#### Step 1.2: Compare with Elasticsearch `standard` Analyzer
1. In the same endpoint, change:
   - **`analyzer`**: `standard`
2. Click **Execute**.
3. **What to Observe in Response**:
   - The generated tokens are `["cafeteira", "elétrica"]`.
   - The accent `é` was preserved, proving why unaccented queries fail to match on standard analyzers.

---

### Test 2: Compare Search With vs Without Asciifolding (`GET /api/labs/02/compare-accent-search`)

1. Expand `GET /api/labs/02/compare-accent-search`.
2. Fill the parameters:
   - **`q`**: `cafeteira eletrica` (typed completely without accents)
3. Click **Execute**.
4. **What to Observe in Response**:
   - **`WithAsciifolding` (`name`)**: `TotalHits: 1` ➡️ Returns *Cafeteira Elétrica Programável Oster Flavor* (matches both tokens `"cafeteira"` and `"eletrica"`).
   - **`WithoutAsciifolding` (`name.standard`)**: `TotalHits: 0` ➡️ Returns `0 hits`!
   
> [!NOTE]
> **Why `Operator: AND` matters here:**
> When a user queries two words like `"cafeteira eletrica"`, the word `"cafeteira"` has no accents, so under default `OR` behavior it matches other coffee makers. By enforcing `AND`, we prove that the unaccented word `"eletrica"` completely fails to match `"elétrica"` on standard fields without `asciifolding`.

5. **Additional Single-Word Accent Tests:**
   Try typing single unaccented words:
   - **`q`**: `eletrica` ➡️ `WithAsciifolding: 2 hits` vs `WithoutAsciifolding: 0 hits`.
   - **`q`**: `mecanico` ➡️ `WithAsciifolding: 2 hits` vs `WithoutAsciifolding: 0 hits`.
   - **`q`**: `robo` ➡️ `WithAsciifolding: 1 hit` vs `WithoutAsciifolding: 0 hits`.

---

## 3. Key Takeaways

| Technique | Pros | Cons / Considerations |
|---|---|---|
| **`lowercase` token filter** | Case-insensitive search | Must be applied to both index and query analyzer |
| **`asciifolding` token filter** | Handles accented languages (Portuguese, Spanish, French) | Collapses distinctions (e.g. German `ä` vs `a`) — choose language-specific folding if required |
| **Multi-fields (`name` vs `name.keyword` vs `name.standard`)** | Allows both normalized search and exact/accent-sensitive matching | Increases index storage slightly |
