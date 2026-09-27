# Lab 03 — Search Relevance & Ranking

> **Focus**: Tuning `_score` with field boosting, `bool.should`, phrase matching with slop, and balanced fuzzy search.

---

## 1. Problem Scenario

When a customer searches for `"dell notebook"`, they expect laptops by Dell to appear first.

### The Naive Query Flaw:
In many search engines, developers configure multi-field searches with `type: most_fields` without boosting:
- An accessory titled *"Mochila Executiva para Notebook Dell"* contains the words "Notebook" and "Dell" in its title, repeats them in its description, and lists them in tags.
- Under unweighted `most_fields`, the term frequencies across description and tags accumulate, allowing accessories to score on par with or even outrank core products!

---

## 2. Relevance Tuning Strategies

### Strategy 1: Unweighted `most_fields` (Naive Baseline)
Accumulates raw term frequency across `name`, `description`, and `tags`.

```json
POST /products/_search
{
  "query": {
    "multi_match": {
      "query": "dell notebook",
      "type": "most_fields",
      "fields": ["name", "description", "tags"]
    }
  }
}
```

---

### Strategy 2: Field Boosting (`name^4`, `brand^3`, `description^1`)
Switches to `type: best_fields` with heavy weights on primary identifying fields:

```json
POST /products/_search
{
  "query": {
    "multi_match": {
      "query": "dell notebook",
      "type": "best_fields",
      "fields": ["name^4", "brand^3", "tags^2", "description^1"]
    }
  }
}
```
* **Effect**: Matches in the product title and brand provide a 4x multiplier, lifting laptops to scores of ~16.8+.

---

### Strategy 3: Compound Scoring with `bool.should` & Phrase Boost (+10)
Combines broad recall with exact word proximity bonus (using `slop: 2` to tolerate natural Portuguese word order variations like "Dell Notebook" vs "Notebook Dell"):

```json
POST /products/_search
{
  "query": {
    "bool": {
      "must": [
        {
          "multi_match": {
            "query": "dell notebook",
            "fields": ["name^4", "brand^3", "description^1"]
          }
        }
      ],
      "should": [
        {
          "match_phrase": {
            "name": {
              "query": "dell notebook",
              "slop": 2,
              "boost": 10.0
            }
          }
        }
      ]
    }
  }
}
```
* **Effect**: Products with the adjacent phrase in the title receive an additional +10.0 boost (reaching scores of ~26.8+), establishing an unbeatable relevance lead.

---

## 🧪 How to Test in Swagger (Interactive Reproduction)

Open Swagger UI at `http://localhost:5000/swagger` and navigate to the **`Lab 03: Search Relevance`** section:

### Test 1: Compare 3 Relevance Ranking Strategies (`GET /api/labs/03/compare-relevance`)

1. Expand `GET /api/labs/03/compare-relevance`.
2. Fill the parameters:
   - **`q`**: `dell notebook`
3. Click **Execute**.
4. **What to Observe in Response**:
   - **`Strategy1_FlatMatch`**: Raw Lucene score (~4.2) where accessories (e.g. *Mochila Executiva*) compete closely with other laptops.
   - **`Strategy2_FieldBoosting`**: Scores on core Dell laptops surge to **~16.86** because `name^4` and `brand^3` multiply the title match relevance.
   - **`Strategy3_PhraseBoost`**: Scores on *Notebook Dell Inspiron 15* and *Notebook Dell XPS 13 Plus* jump to > **30** due to the `match_phrase` +10 boost, cementing clear ranking dominance.

---

### Test 2: Typo-Tolerance with Fuzzy Search (`GET /api/labs/03/fuzzy-search`)

1. Expand `GET /api/labs/03/fuzzy-search`.
2. Fill the parameters:
   - **`q`**: `del notebok` (contains typos in both words)
   - **`prefixLength`**: `2`
3. Click **Execute**.
4. **What to Observe in Response**:
   - Returns the Dell laptops correctly despite the typos.
   - The `prefixLength: 2` parameter prevents Elasticsearch from mutating short initial characters.

---

## 3. Key Takeaways

| Approach                                    | Score on *"Notebook Dell Inspiron"* | Role in Search Architecture                                     |
| ------------------------------------------- | ----------------------------------- | --------------------------------------------------------------- |
| **1. Unweighted Most Fields**               | ~4.2                                | Naive baseline; prone to description noise and keyword stuffing |
| **2. Field Boosting (`name^4`, `brand^3`)** | ~16.8                               | Production standard for e-commerce catalogs                     |
| **3. Boosting + Phrase Boost (Slop: 2)**    | ~30                                 | Premium intent ranking for multi-word branded queries           |
| **4. Controlled Fuzzy Search**              | Dynamic                             | Fallback for typos without sacrificing speed or precision       |
