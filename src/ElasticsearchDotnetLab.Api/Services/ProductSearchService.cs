using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ElasticsearchDotnetLab.Api.Elasticsearch;
using ElasticsearchDotnetLab.Api.Models;

namespace ElasticsearchDotnetLab.Api.Services;

public record ProductSearchRequest(
    string? Query = null,
    string? Category = null,
    string? Brand = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool Fuzzy = false,
    bool Phrase = false,
    int Page = 1,
    int PageSize = 10
);

public record SearchHitResult(
    int Id,
    string Name,
    string Description,
    string Category,
    string Brand,
    decimal Price,
    List<string> Tags,
    bool Available,
    float Rating,
    double? Score
);

public record SearchResponse(
    string? Query,
    long Total,
    int Page,
    int PageSize,
    IReadOnlyCollection<SearchHitResult> Results,
    long TookMs
);

public record AggregationBucket(string Key, long DocCount);

public record ProductAggregationsResponse(
    IReadOnlyCollection<AggregationBucket> Categories,
    IReadOnlyCollection<AggregationBucket> Brands,
    double? MinPrice,
    double? MaxPrice,
    double? AvgPrice
);

public record AnalyzeTokenResult(string Token, long Position, long StartOffset, long EndOffset, string? Type);

public record AnalyzeTextResponse(string Analyzer, string Text, IReadOnlyCollection<AnalyzeTokenResult> Tokens);

public class ProductSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ProductSearchService> _logger;

    public ProductSearchService(ElasticsearchClient client, ILogger<ProductSearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(ProductSearchRequest request, CancellationToken cancellationToken = default)
    {
        var from = (Math.Max(1, request.Page) - 1) * request.PageSize;
        var size = request.PageSize;

        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .From(from)
            .Size(size)
            .Query(q => BuildQuery(q, request)),
            cancellationToken
        );

        if (!response.IsValidResponse)
        {
            _logger.LogError("Search query failed: {DebugInformation}", response.DebugInformation);
            throw new InvalidOperationException($"Search failed: {response.DebugInformation}");
        }

        var results = response.Hits.Select(hit => new SearchHitResult(
            hit.Source?.Id ?? 0,
            hit.Source?.Name ?? string.Empty,
            hit.Source?.Description ?? string.Empty,
            hit.Source?.Category ?? string.Empty,
            hit.Source?.Brand ?? string.Empty,
            hit.Source?.Price ?? 0,
            hit.Source?.Tags ?? [],
            hit.Source?.Available ?? false,
            hit.Source?.Rating ?? 0,
            hit.Score
        )).ToList();

        return new SearchResponse(
            request.Query,
            response.Total,
            request.Page,
            request.PageSize,
            results,
            response.Took
        );
    }

    private static Query BuildQuery(QueryDescriptor<Product> q, ProductSearchRequest request)
    {
        var mustQueries = new List<Action<QueryDescriptor<Product>>>();
        var filterQueries = new List<Action<QueryDescriptor<Product>>>();

        // Textual Query Strategy
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            if (request.Phrase)
            {
                mustQueries.Add(m => m.MatchPhrase(mp => mp.Field(f => f.Name).Query(request.Query)));
            }
            else if (request.Fuzzy)
            {
                mustQueries.Add(m => m.MultiMatch(mm => mm
                    .Fields(new[] { "name^3", "brand^2", "tags^2", "description" })
                    .Query(request.Query)
                    .Fuzziness(new Fuzziness("AUTO"))
                ));
            }
            else
            {
                mustQueries.Add(m => m.MultiMatch(mm => mm
                    .Fields(new[] { "name^3", "brand^2", "tags^2", "description" })
                    .Query(request.Query)
                ));
            }
        }
        else
        {
            mustQueries.Add(m => m.MatchAll(_ => { }));
        }

        // Filters (Category, Brand, Price range)
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            filterQueries.Add(f => f.Term(t => t.Field(p => p.Category).Value(request.Category)));
        }

        if (!string.IsNullOrWhiteSpace(request.Brand))
        {
            filterQueries.Add(f => f.Term(t => t.Field(p => p.Brand).Value(request.Brand)));
        }

        if (request.MinPrice.HasValue || request.MaxPrice.HasValue)
        {
            filterQueries.Add(f => f.Range(r => r.Number(nr =>
            {
                nr.Field(p => p.Price);
                if (request.MinPrice.HasValue) nr.Gte((double)request.MinPrice.Value);
                if (request.MaxPrice.HasValue) nr.Lte((double)request.MaxPrice.Value);
            })));
        }

        return q.Bool(b =>
        {
            if (mustQueries.Count > 0)
            {
                b.Must(mustQueries.ToArray());
            }

            if (filterQueries.Count > 0)
            {
                b.Filter(filterQueries.ToArray());
            }
        });
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetAsync<Product>(id.ToString(), g => g.Index(ProductIndexManager.IndexName), cancellationToken);
        return response.Found ? response.Source : null;
    }

    public async Task<ProductAggregationsResponse> GetAggregationsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _client.SearchAsync<Product>(s => s
            .Indices(ProductIndexManager.IndexName)
            .Size(0)
            .Aggregations(a => a
                .Add("categories", agg => agg.Terms(t => t.Field(f => f.Category).Size(20)))
                .Add("brands", agg => agg.Terms(t => t.Field(f => f.Brand).Size(20)))
                .Add("min_price", agg => agg.Min(m => m.Field(f => f.Price)))
                .Add("max_price", agg => agg.Max(m => m.Field(f => f.Price)))
                .Add("avg_price", agg => agg.Avg(m => m.Field(f => f.Price)))
            ),
            cancellationToken
        );

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Aggregations failed: {response.DebugInformation}");
        }

        var categories = response.Aggregations?.GetStringTerms("categories")?.Buckets?
            .Select(b => new AggregationBucket(b.Key.ToString(), b.DocCount)).ToList() ?? [];

        var brands = response.Aggregations?.GetStringTerms("brands")?.Buckets?
            .Select(b => new AggregationBucket(b.Key.ToString(), b.DocCount)).ToList() ?? [];

        var minPrice = response.Aggregations?.GetMin("min_price")?.Value;
        var maxPrice = response.Aggregations?.GetMax("max_price")?.Value;
        var avgPrice = response.Aggregations?.GetAverage("avg_price")?.Value;

        return new ProductAggregationsResponse(categories, brands, minPrice, maxPrice, avgPrice);
    }

    public async Task<AnalyzeTextResponse> AnalyzeAsync(string text, string? analyzer = null, CancellationToken cancellationToken = default)
    {
        var targetAnalyzer = string.IsNullOrWhiteSpace(analyzer) ? "products_analyzer" : analyzer;

        var response = await _client.Indices.AnalyzeAsync(a => a
            .Index(ProductIndexManager.IndexName)
            .Analyzer(targetAnalyzer)
            .Text(text),
            cancellationToken
        );

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException($"Analyze failed: {response.DebugInformation}");
        }

        var tokens = response.Tokens?.Select(t => new AnalyzeTokenResult(
            t.Token,
            t.Position,
            t.StartOffset,
            t.EndOffset,
            t.Type
        )).ToList() ?? [];

        return new AnalyzeTextResponse(targetAnalyzer, text, tokens);
    }
}
