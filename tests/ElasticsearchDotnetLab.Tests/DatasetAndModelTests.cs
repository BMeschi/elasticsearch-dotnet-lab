using System.Text.Json;
using ElasticsearchDotnetLab.Api.Models;
using ElasticsearchDotnetLab.Api.Services;

namespace ElasticsearchDotnetLab.Tests;

public class DatasetAndModelTests
{
    [Fact]
    public void Dataset_ShouldLoadAllProductsValidly()
    {
        // Arrange
        var datasetPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "datasets", "products", "products.json");
        var fullPath = Path.GetFullPath(datasetPath);

        Assert.True(File.Exists(fullPath), $"Dataset file not found at: {fullPath}");

        var json = File.ReadAllText(fullPath);

        // Act
        var products = JsonSerializer.Deserialize<List<Product>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        Assert.NotNull(products);
        Assert.NotEmpty(products);
        Assert.True(products.Count >= 30, $"Expected at least 30 products, got {products.Count}");

        foreach (var product in products)
        {
            Assert.True(product.Id > 0, "Product ID must be greater than 0");
            Assert.False(string.IsNullOrWhiteSpace(product.Name), "Product Name must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(product.Category), "Product Category must not be empty");
            Assert.False(string.IsNullOrWhiteSpace(product.Brand), "Product Brand must not be empty");
            Assert.True(product.Price > 0, $"Product {product.Name} has invalid price: {product.Price}");
            Assert.NotEmpty(product.Tags);
        }
    }

    [Fact]
    public void ProductSearchRequest_ShouldSetDefaultsCorrectly()
    {
        // Act
        var request = new ProductSearchRequest();

        // Assert
        Assert.Null(request.Query);
        Assert.Null(request.Category);
        Assert.Null(request.Brand);
        Assert.False(request.Fuzzy);
        Assert.False(request.Phrase);
        Assert.Equal(1, request.Page);
        Assert.Equal(10, request.PageSize);
    }
}
