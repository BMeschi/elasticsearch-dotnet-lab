using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.Analysis;
using ElasticsearchDotnetLab.Api.Models;

namespace ElasticsearchDotnetLab.Api.Elasticsearch;

public class ProductIndexManager
{
    public const string IndexName = "products";
    private readonly ElasticsearchClient _client;
    private readonly ILogger<ProductIndexManager> _logger;
    private readonly IHostEnvironment _env;

    public ProductIndexManager(ElasticsearchClient client, ILogger<ProductIndexManager> logger, IHostEnvironment env)
    {
        _client = client;
        _logger = logger;
        _env = env;
    }

    public async Task RecreateIndexAndSeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking if index '{IndexName}' exists...", IndexName);

        var existsResponse = await _client.Indices.ExistsAsync(IndexName, cancellationToken);
        if (existsResponse.Exists)
        {
            _logger.LogInformation("Deleting existing index '{IndexName}'...", IndexName);
            await _client.Indices.DeleteAsync(IndexName, cancellationToken);
        }

        _logger.LogInformation("Creating index '{IndexName}' with custom analyzers and mappings...", IndexName);

        var createResponse = await _client.Indices.CreateAsync(IndexName, c => c
            .Settings(s => s
                .Analysis(a => a
                    .Analyzers(an => an
                        .Custom("products_analyzer", ca => ca
                            .Tokenizer("standard")
                            .Filter(["lowercase", "asciifolding"])
                        )
                    )
                )
            )
            .Mappings(m => m
                .Properties<Product>(p => p
                    .IntegerNumber(x => x.Id)
                    .Text(x => x.Name, t => t
                        .Analyzer("products_analyzer")
                        .Fields(f => f
                            .Keyword("keyword")
                            .Text("standard", st => st.Analyzer("standard"))
                        )
                    )
                    .Text(x => x.Description, t => t
                        .Analyzer("products_analyzer")
                        .Fields(f => f
                            .Text("standard", st => st.Analyzer("standard"))
                        )
                    )
                    .Keyword(x => x.Category, k => k
                        .Fields(f => f
                            .Text("text", t => t.Analyzer("products_analyzer"))
                        )
                    )
                    .Keyword(x => x.Brand, k => k
                        .Fields(f => f
                            .Text("text", t => t.Analyzer("products_analyzer"))
                        )
                    )
                    .DoubleNumber(x => x.Price)
                    .Keyword(x => x.Tags)
                    .Boolean(x => x.Available)
                    .FloatNumber(x => x.Rating)
                )
            ),
            cancellationToken
        );

        if (!createResponse.IsValidResponse)
        {
            _logger.LogError("Failed to create index '{IndexName}': {DebugInformation}", IndexName, createResponse.DebugInformation);
            throw new InvalidOperationException($"Could not create index: {createResponse.DebugInformation}");
        }

        _logger.LogInformation("Index '{IndexName}' created successfully.", IndexName);

        await SeedProductsAsync(cancellationToken);
    }

    private async Task SeedProductsAsync(CancellationToken cancellationToken)
    {
        var possiblePaths = new[]
        {
            Path.Combine(_env.ContentRootPath, "..", "..", "datasets", "products", "products.json"),
            Path.Combine(_env.ContentRootPath, "datasets", "products", "products.json"),
            Path.Combine(AppContext.BaseDirectory, "datasets", "products", "products.json")
        };

        var filePath = possiblePaths.FirstOrDefault(File.Exists);

        if (filePath == null)
        {
            _logger.LogWarning("Products dataset JSON file not found in tested paths. Skipping seed ingestion.");
            return;
        }

        _logger.LogInformation("Loading products dataset from '{FilePath}'...", filePath);
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var products = JsonSerializer.Deserialize<List<Product>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        if (products.Count == 0)
        {
            _logger.LogWarning("No products found in dataset JSON.");
            return;
        }

        _logger.LogInformation("Ingesting {Count} products into index '{IndexName}'...", products.Count, IndexName);

        var bulkResponse = await _client.BulkAsync(b => b
            .Index(IndexName)
            .IndexMany(products, (descriptor, product) => descriptor.Id(product.Id.ToString())),
            cancellationToken
        );

        if (!bulkResponse.IsValidResponse || bulkResponse.Errors)
        {
            _logger.LogError("Bulk indexing completed with errors: {DebugInformation}", bulkResponse.DebugInformation);
            throw new InvalidOperationException($"Bulk indexing failed: {bulkResponse.DebugInformation}");
        }

        // Refresh index to make documents immediately searchable
        await _client.Indices.RefreshAsync(IndexName, cancellationToken);
        _logger.LogInformation("Successfully indexed and refreshed {Count} products.", products.Count);
    }
}
