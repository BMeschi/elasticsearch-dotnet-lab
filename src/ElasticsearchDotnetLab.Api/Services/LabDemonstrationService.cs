using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ElasticsearchDotnetLab.Api.Elasticsearch;
using ElasticsearchDotnetLab.Api.Models;

namespace ElasticsearchDotnetLab.Api.Services;

public class LabDemonstrationService
{
    private readonly ElasticsearchClient _client;

    public LabDemonstrationService(ElasticsearchClient client)
    {
        _client = client;
    }

    #region Lab 01 — Search Basics

    public async Task<object> Lab01TermVsMatchAsync(string field, string value, CancellationToken ct = default)
    {
        // 1. Term query (no analysis on query term)
        var termResponse = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.Term(t => t.Field(field).Value(value))),
            ct
        );

        // 2. Match query (analyzed query)
        var matchResponse = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.Match(m => m.Field(field).Query(value))),
            ct
        );

        return new
        {
            Explanation = $"Comparing term query vs match query on field '{field}' for value '{value}'. Term query searches exact unanalyzed token, while match runs the query through the analyzer.",
            TermQuery = new
            {
                TotalHits = termResponse.Total,
                Hits = termResponse.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Category, h.Source?.Brand, h.Score })
            },
            MatchQuery = new
            {
                TotalHits = matchResponse.Total,
                Hits = matchResponse.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Category, h.Source?.Brand, h.Score })
            }
        };
    }

    public async Task<object> Lab01MatchPhraseAsync(string phrase, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.MatchPhrase(mp => mp.Field(f => f.Name).Query(phrase))),
            ct
        );

        return new
        {
            QueryPhrase = phrase,
            TotalHits = response.Total,
            Explanation = "match_phrase requires all tokens to be present in the exact order and position.",
            Hits = response.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Score })
        };
    }

    public async Task<object> Lab01BoolFilterAsync(string text, string? category, decimal? minPrice, decimal? maxPrice, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.Bool(b =>
            {
                b.Must(m => m.Match(mt => mt.Field(f => f.Name).Query(text)));

                var filters = new List<Action<QueryDescriptor<Product>>>();
                if (!string.IsNullOrWhiteSpace(category))
                {
                    filters.Add(f => f.Term(t => t.Field(p => p.Category).Value(category)));
                }

                if (minPrice.HasValue || maxPrice.HasValue)
                {
                    filters.Add(f => f.Range(r => r.Number(nr =>
                    {
                        nr.Field(p => p.Price);
                        if (minPrice.HasValue) nr.Gte((double)minPrice.Value);
                        if (maxPrice.HasValue) nr.Lte((double)maxPrice.Value);
                    })));
                }

                if (filters.Count > 0)
                {
                    b.Filter(filters.ToArray());
                }
            })),
            ct
        );

        return new
        {
            Explanation = "Bool query combining 'must' (calculates BM25 relevance score for search term) and 'filter' (cached, non-scoring binary filters for category and price).",
            TotalHits = response.Total,
            Hits = response.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Category, h.Source?.Price, h.Score })
        };
    }

    #endregion

    #region Lab 02 — Analyzers & Tokenization

    public async Task<object> Lab02CompareAccentSearchAsync(string query, CancellationToken ct = default)
    {
        // 1. Search on 'name' which uses custom products_analyzer (with asciifolding)
        var customResponse = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.Match(m => m
                .Field(f => f.Name)
                .Query(query)
                .Operator(Operator.And)
            )),
            ct
        );

        // 2. Search on 'name.standard' which uses standard analyzer (without asciifolding)
        var standardResponse = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.Match(m => m
                .Field("name.standard")
                .Query(query)
                .Operator(Operator.And)
            )),
            ct
        );

        return new
        {
            Query = query,
            Explanation = "Demonstrating how asciifolding solves accent mismatches. With Operator: AND, searching 'cafeteira eletrica' requires both tokens. On 'name' (with asciifolding), 'eletrica' matches 'Elétrica' -> 1 hit. On 'name.standard' (standard analyzer), 'eletrica' fails to match 'elétrica' -> 0 hits.",
            WithAsciifolding = new
            {
                Field = "name (products_analyzer with asciifolding)",
                TotalHits = customResponse.Total,
                Hits = customResponse.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Score })
            },
            WithoutAsciifolding = new
            {
                Field = "name.standard (standard analyzer without asciifolding)",
                TotalHits = standardResponse.Total,
                Hits = standardResponse.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Score })
            }
        };
    }

    #endregion

    #region Lab 03 — Search Relevance

    public async Task<object> Lab03CompareRelevanceAsync(string query, CancellationToken ct = default)
    {
        // Strategy 1: Unweighted multi_match with most_fields (accumulates term frequency from descriptions and tags)
        var flatResponse = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.MultiMatch(mm => mm
                .Query(query)
                .Type(TextQueryType.MostFields)
                .Fields(new[] { "name", "description", "tags" })
            )),
            ct
        );

        // Strategy 2: Field boosting (name^4, brand^3, tags^2, description^1)
        var boostedResponse = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.MultiMatch(mm => mm
                .Query(query)
                .Type(TextQueryType.BestFields)
                .Fields(new[] { "name^4", "brand^3", "tags^2", "description^1" })
            )),
            ct
        );

        // Strategy 3: Compound scoring (Base Match + Phrase Boost with Slop for word order variations)
        var phraseBoostResponse = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.Bool(b => b
                .Must(m => m.MultiMatch(mm => mm
                    .Query(query)
                    .Fields(new[] { "name^4", "brand^3", "description^1" })
                ))
                .Should(
                    sh => sh.MatchPhrase(mp => mp
                        .Field(f => f.Name)
                        .Query(query)
                        .Slop(2)
                        .Boost(10.0f)
                    )
                )
            )),
            ct
        );

        return new
        {
            Query = query,
            Strategy1_FlatMatch = new
            {
                Description = "Unweighted most_fields: accumulates term frequencies across title, description, and tags without prioritizing product name.",
                TopHits = flatResponse.Hits.Take(5).Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Category, h.Source?.Brand, h.Score })
            },
            Strategy2_FieldBoosting = new
            {
                Description = "Field boosting (name^4, brand^3): gives strong priority to matches in the product title and brand.",
                TopHits = boostedResponse.Hits.Take(5).Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Category, h.Source?.Brand, h.Score })
            },
            Strategy3_PhraseBoost = new
            {
                Description = "Compound scoring: adds a +10.0 boost when query terms appear adjacent in the title (with slop for word order).",
                TopHits = phraseBoostResponse.Hits.Take(5).Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Category, h.Source?.Brand, h.Score })
            }
        };
    }

    public async Task<object> Lab03FuzzySearchAsync(string query, int prefixLength = 2, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Query(q => q.MultiMatch(mm => mm
                .Query(query)
                .Fields(new[] { "name^3", "brand^2", "description" })
                .Fuzziness(new Fuzziness("AUTO"))
                .PrefixLength(prefixLength)
            )),
            ct
        );

        return new
        {
            Query = query,
            PrefixLength = prefixLength,
            Explanation = "Fuzzy search with Levenshtein automaton (AUTO) and prefix_length protection against mutation of short tokens.",
            TotalHits = response.Total,
            Hits = response.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Brand, h.Score })
        };
    }

    #endregion

    #region Lab 04 — Query Optimization & Profiling

    public async Task<object> Lab04UnoptimizedQueryAsync(string query, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Profile(true)
            .Query(q => q.Bool(b => b.Must(
                m => m.Wildcard(w => w.Field(f => f.Name).Value($"*{query.ToLower()}*")),
                m => m.Match(mt => mt.Field(p => p.Available).Query(true.ToString().ToLower())),
                m => m.Range(r => r.Number(nr => nr.Field(p => p.Price).Gte(1000).Lte(10000)))
            ))),
            ct
        );

        return new
        {
            Approach = "Unoptimized (Leading Wildcard + Business Filters in 'must' context calculating unneeded BM25 scores)",
            TotalHits = response.Total,
            TookMs = response.Took,
            Hits = response.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Price, h.Score }),
            ProfileSummary = "Check profile diagnostics in Elasticsearch logs: Wildcard full dictionary scan + scoring all boolean filters."
        };
    }

    public async Task<object> Lab04OptimizedQueryAsync(string query, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Profile(true)
            .Query(q => q.Bool(b => b
                .Must(m => m.Match(mt => mt.Field(f => f.Name).Query(query)))
                .Filter(
                    f => f.Term(t => t.Field(p => p.Available).Value(true)),
                    f => f.Range(r => r.Number(nr => nr.Field(p => p.Price).Gte(1000).Lte(10000)))
                )
            )),
            ct
        );

        return new
        {
            Approach = "Optimized (Analyzed Match Query + Filters in cached 'filter' context without scoring overhead)",
            TotalHits = response.Total,
            TookMs = response.Took,
            Hits = response.Hits.Select(h => new { h.Source?.Id, h.Source?.Name, h.Source?.Price, h.Score }),
            ProfileSummary = "Scoring computed ONLY for text match. Filters cached as reusable bitsets."
        };
    }

    #endregion

    #region Lab 05 — Aggregations & Analytics

    public async Task<object> Lab05AveragePricePerCategoryAsync(CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Size(0)
            .Aggregations(a => a
                .Add("categories", agg => agg.Terms(t => t.Field(f => f.Category).Size(10))
                    .Aggregations(sub => sub
                        .Add("avg_price", subAgg => subAgg.Avg(m => m.Field(f => f.Price)))
                    )
                )
            ),
            ct
        );

        var buckets = response.Aggregations?.GetStringTerms("categories")?.Buckets?.Select(b => new
        {
            Category = b.Key.ToString(),
            ProductCount = b.DocCount,
            AveragePrice = b.Aggregations?.GetAverage("avg_price")?.Value
        }).ToList();

        return new
        {
            Explanation = "Nested sub-aggregation: computing average price partitioned by product category directly on doc_values with size: 0.",
            Categories = buckets
        };
    }

    public async Task<object> Lab05PriceStatsAsync(CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Size(0)
            .Aggregations(a => a
                .Add("price_stats", agg => agg.Stats(st => st.Field(f => f.Price)))
            ),
            ct
        );

        var stats = response.Aggregations?.GetStats("price_stats");

        return new
        {
            Explanation = "Matrix stats aggregation computing count, min, max, avg, and sum over numeric field 'price'.",
            Count = stats?.Count,
            Min = stats?.Min,
            Max = stats?.Max,
            Avg = stats?.Avg,
            Sum = stats?.Sum
        };
    }

    #endregion
}
