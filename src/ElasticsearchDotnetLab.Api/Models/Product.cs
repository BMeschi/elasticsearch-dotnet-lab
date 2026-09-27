namespace ElasticsearchDotnetLab.Api.Models;

public record Product
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public List<string> Tags { get; init; } = [];
    public bool Available { get; init; }
    public float Rating { get; init; }
}
