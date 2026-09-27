using Elastic.Clients.Elasticsearch;
using ElasticsearchDotnetLab.Api.Elasticsearch;
using ElasticsearchDotnetLab.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Elasticsearch Client Configuration
var esUrl = builder.Configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
var defaultIndex = builder.Configuration["Elasticsearch:DefaultIndex"] ?? "products";

var clientSettings = new ElasticsearchClientSettings(new Uri(esUrl))
    .DefaultIndex(defaultIndex)
    .DisableDirectStreaming();

builder.Services.AddSingleton(new ElasticsearchClient(clientSettings));
builder.Services.AddScoped<ProductIndexManager>();
builder.Services.AddScoped<ProductSearchService>();
builder.Services.AddScoped<LabDemonstrationService>();

// Swagger & OpenAPI Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Elasticsearch .NET Lab API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
    });
}

// Support command-line argument: dotnet run -- --seed
if (args.Contains("--seed") || args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();
    var indexManager = scope.ServiceProvider.GetRequiredService<ProductIndexManager>();
    Console.WriteLine("Running seed task...");
    await indexManager.RecreateIndexAndSeedAsync();
    Console.WriteLine("Seed task completed successfully.");
    return;
}

// Redirect root to Swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

#region System & Admin Endpoints

app.MapGet("/api/health", async (ElasticsearchClient client) =>
{
    var pingResponse = await client.PingAsync();
    var clusterHealth = await client.Cluster.HealthAsync();

    return Results.Ok(new
    {
        Status = pingResponse.IsValidResponse ? "Healthy" : "Unhealthy",
        ElasticsearchConnected = pingResponse.IsValidResponse,
        ClusterStatus = clusterHealth.Status.ToString(),
        ClusterName = clusterHealth.ClusterName,
        NumberOfNodes = clusterHealth.NumberOfNodes
    });
})
.WithName("HealthCheck")
.WithTags("0. System & Health")
.WithSummary("Check Elasticsearch cluster connection health");

app.MapPost("/api/admin/seed", async (ProductIndexManager indexManager) =>
{
    await indexManager.RecreateIndexAndSeedAsync();
    return Results.Ok(new { message = "Index 'products' recreated and dataset ingested successfully." });
})
.WithName("SeedDatabase")
.WithTags("0. System & Health")
.WithSummary("Recreate index with custom analyzer mappings and re-seed 40 synthetic products");

#endregion

#region Core Product API

app.MapGet("/api/products/search", async (
    string? q,
    string? category,
    string? brand,
    decimal? minPrice,
    decimal? maxPrice,
    bool? fuzzy,
    bool? phrase,
    int? page,
    int? pageSize,
    ProductSearchService searchService) =>
{
    var request = new ProductSearchRequest(
        Query: q,
        Category: category,
        Brand: brand,
        MinPrice: minPrice,
        MaxPrice: maxPrice,
        Fuzzy: fuzzy ?? false,
        Phrase: phrase ?? false,
        Page: page ?? 1,
        PageSize: pageSize ?? 10
    );

    var result = await searchService.SearchAsync(request);
    return Results.Ok(result);
})
.WithName("SearchProducts")
.WithTags("Core API")
.WithSummary("Full-text search with field boosting, filters, fuzzy matching & pagination");

app.MapGet("/api/products/{id:int}", async (int id, ProductSearchService searchService) =>
{
    var product = await searchService.GetByIdAsync(id);
    return product is not null ? Results.Ok(product) : Results.NotFound(new { message = $"Product with ID {id} not found." });
})
.WithName("GetProductById")
.WithTags("Core API")
.WithSummary("Get single product details by ID");

app.MapGet("/api/products/aggregations", async (ProductSearchService searchService) =>
{
    var aggs = await searchService.GetAggregationsAsync();
    return Results.Ok(aggs);
})
.WithName("GetAggregations")
.WithTags("Core API")
.WithSummary("Faceted category/brand counts and price summary (min/max/avg)");

#endregion

#region Lab 01 — Search Basics Endpoints

app.MapGet("/api/labs/01/term-vs-match", async (string? field, string? value, LabDemonstrationService labService) =>
{
    var targetField = string.IsNullOrWhiteSpace(field) ? "name" : field;
    var targetValue = string.IsNullOrWhiteSpace(value) ? "Notebook" : value;
    var result = await labService.Lab01TermVsMatchAsync(targetField, targetValue);
    return Results.Ok(result);
})
.WithName("Lab01_TermVsMatch")
.WithTags("Lab 01: Search Basics")
.WithSummary("Compare 'term' (exact unanalyzed token) vs 'match' (analyzed text) on the same field");

app.MapGet("/api/labs/01/match-phrase", async (string? phrase, LabDemonstrationService labService) =>
{
    var targetPhrase = string.IsNullOrWhiteSpace(phrase) ? "Notebook Dell" : phrase;
    var result = await labService.Lab01MatchPhraseAsync(targetPhrase);
    return Results.Ok(result);
})
.WithName("Lab01_MatchPhrase")
.WithTags("Lab 01: Search Basics")
.WithSummary("Match exact word sequence and token proximity using match_phrase");

app.MapGet("/api/labs/01/bool-filter", async (string? q, string? category, decimal? minPrice, decimal? maxPrice, LabDemonstrationService labService) =>
{
    var queryText = string.IsNullOrWhiteSpace(q) ? "notebook" : q;
    var result = await labService.Lab01BoolFilterAsync(queryText, category ?? "Computers", minPrice ?? 3000, maxPrice ?? 7000);
    return Results.Ok(result);
})
.WithName("Lab01_BoolFilter")
.WithTags("Lab 01: Search Basics")
.WithSummary("Bool query combining scored text search ('must') with cached business constraints ('filter')");

#endregion

#region Lab 02 — Analyzers & Tokenization Endpoints

app.MapGet("/api/labs/02/analyze", async (string? text, string? analyzer, ProductSearchService searchService) =>
{
    var targetText = string.IsNullOrWhiteSpace(text) ? "Cafeteira Elétrica" : text;
    var targetAnalyzer = string.IsNullOrWhiteSpace(analyzer) ? "products_analyzer" : analyzer;
    var result = await searchService.AnalyzeAsync(targetText, targetAnalyzer);
    return Results.Ok(result);
})
.WithName("Lab02_AnalyzeText")
.WithTags("Lab 02: Analyzers & Tokenization")
.WithSummary("Diagnose how tokenizers and filters transform text ('products_analyzer' vs 'standard')");

app.MapGet("/api/labs/02/compare-accent-search", async (string? q, LabDemonstrationService labService) =>
{
    var queryText = string.IsNullOrWhiteSpace(q) ? "cafeteira eletrica" : q;
    var result = await labService.Lab02CompareAccentSearchAsync(queryText);
    return Results.Ok(result);
})
.WithName("Lab02_CompareAccentSearch")
.WithTags("Lab 02: Analyzers & Tokenization")
.WithSummary("Compare searching on 'name' (with asciifolding) vs 'name.standard' (without asciifolding)");

#endregion

#region Lab 03 — Search Relevance Endpoints

app.MapGet("/api/labs/03/compare-relevance", async (string? q, LabDemonstrationService labService) =>
{
    var queryText = string.IsNullOrWhiteSpace(q) ? "dell notebook" : q;
    var result = await labService.Lab03CompareRelevanceAsync(queryText);
    return Results.Ok(result);
})
.WithName("Lab03_CompareRelevance")
.WithTags("Lab 03: Search Relevance")
.WithSummary("Compare 3 ranking strategies: Flat Match vs Field Boosting (name^4) vs Phrase Boost (bool.should)");

app.MapGet("/api/labs/03/fuzzy-search", async (string? q, int? prefixLength, LabDemonstrationService labService) =>
{
    var queryText = string.IsNullOrWhiteSpace(q) ? "del notebok" : q;
    var result = await labService.Lab03FuzzySearchAsync(queryText, prefixLength ?? 2);
    return Results.Ok(result);
})
.WithName("Lab03_FuzzySearch")
.WithTags("Lab 03: Search Relevance")
.WithSummary("Fuzzy matching with Levenshtein automaton (AUTO) protected by prefix_length");

#endregion

#region Lab 04 — Query Optimization Endpoints

app.MapGet("/api/labs/04/unoptimized-query", async (string? q, LabDemonstrationService labService) =>
{
    var queryText = string.IsNullOrWhiteSpace(q) ? "notebook" : q;
    var result = await labService.Lab04UnoptimizedQueryAsync(queryText);
    return Results.Ok(result);
})
.WithName("Lab04_UnoptimizedQuery")
.WithTags("Lab 04: Query Optimization")
.WithSummary("Slow unoptimized query with leading wildcard (*term*) and scoring on filters");

app.MapGet("/api/labs/04/optimized-query", async (string? q, LabDemonstrationService labService) =>
{
    var queryText = string.IsNullOrWhiteSpace(q) ? "notebook" : q;
    var result = await labService.Lab04OptimizedQueryAsync(queryText);
    return Results.Ok(result);
})
.WithName("Lab04_OptimizedQuery")
.WithTags("Lab 04: Query Optimization")
.WithSummary("Fast optimized query with analyzed match and non-scoring cached filter clauses");

#endregion

#region Lab 05 — Aggregations Endpoints

app.MapGet("/api/labs/05/average-price-per-category", async (LabDemonstrationService labService) =>
{
    var result = await labService.Lab05AveragePricePerCategoryAsync();
    return Results.Ok(result);
})
.WithName("Lab05_AveragePricePerCategory")
.WithTags("Lab 05: Aggregations & Facets")
.WithSummary("Nested sub-aggregations: average price per category computed on columnar doc_values");

app.MapGet("/api/labs/05/price-statistics", async (LabDemonstrationService labService) =>
{
    var result = await labService.Lab05PriceStatsAsync();
    return Results.Ok(result);
})
.WithName("Lab05_PriceStatistics")
.WithTags("Lab 05: Aggregations & Facets")
.WithSummary("Matrix stats metrics (min, max, avg, sum, count) for numeric field price");

#endregion

app.Run();
