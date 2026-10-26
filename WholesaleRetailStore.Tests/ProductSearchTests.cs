using WholesaleRetailStore.Models;
using WholesaleRetailStore.Services;

namespace WholesaleRetailStore.Tests;

public class ProductSearchTests
{
    private static readonly List<Product> Catalogue =
    [
        new() { Id = 1, Name = "Wireless Headphones", Description = "High-quality wireless headphones with noise cancellation" },
        new() { Id = 2, Name = "Bluetooth Speaker", Description = "Portable Bluetooth speaker with 12-hour battery life" },
        new() { Id = 3, Name = "Laptop Stand", Description = "Adjustable aluminum laptop stand for ergonomic working" },
        new() { Id = 4, Name = "Whole Milk", Description = "Fresh full cream milk, 1 litre" },
        new() { Id = 5, Name = "Milk Chocolate", Description = "Creamy chocolate bar" },
    ];

    private readonly ProductSearchService _search = new();

    [Fact]
    public void Missing_letters_are_corrected_mlk_finds_milk()
    {
        var result = _search.Search(Catalogue, "mlk");

        Assert.Equal("milk", result.CorrectedQuery);
        Assert.Equal(new[] { 4, 5 }, result.Products.Select(p => p.Id).Order());
    }

    [Theory]
    [InlineData("headphnes", "headphones", 1)]
    [InlineData("hepadhones", "headphones", 1)]       // two letters swapped
    [InlineData("blutooth speker", "bluetooth speaker", 2)]
    [InlineData("labtop", "laptop", 3)]
    public void Common_typos_are_corrected(string typed, string expected, int expectedProductId)
    {
        var result = _search.Search(Catalogue, typed);

        Assert.Equal(expected, result.CorrectedQuery);
        Assert.Equal(expectedProductId, result.Products.First().Id);
    }

    [Theory]
    [InlineData("milk")]
    [InlineData("MILK")]
    [InlineData("head")]        // a partial word is not a typo
    [InlineData("wireless headphones")]
    public void Correct_queries_are_left_alone(string typed)
    {
        var result = _search.Search(Catalogue, typed);

        Assert.Null(result.CorrectedQuery);
        Assert.NotEmpty(result.Products);
    }

    [Fact]
    public void Nonsense_finds_nothing_and_suggests_nothing()
    {
        var result = _search.Search(Catalogue, "zzxqj");

        Assert.Null(result.CorrectedQuery);
        Assert.Empty(result.Products);
    }

    [Fact]
    public void Autocorrect_can_be_switched_off()
    {
        var result = _search.Search(Catalogue, "mlk", autoCorrect: false);

        Assert.Null(result.CorrectedQuery);
        Assert.Empty(result.Products);
    }

    [Fact]
    public void A_blank_query_returns_everything()
    {
        Assert.Equal(Catalogue.Count, _search.Search(Catalogue, "  ").Products.Count);
        Assert.Equal(Catalogue.Count, _search.Search(Catalogue, null).Products.Count);
    }

    [Fact]
    public void Whole_word_matches_rank_above_partial_word_matches()
    {
        // "cream" is a whole word in Whole Milk's description but only the start of "creamy" in the chocolate's.
        var result = _search.Search(Catalogue, "cream");

        Assert.Equal(new[] { 4, 5 }, result.Products.Select(p => p.Id));
    }

    [Theory]
    [InlineData("milk", "mlik", 1)]      // swap
    [InlineData("milk", "milkk", 1)]     // extra letter
    [InlineData("milk", "mik", 1)]       // dropped letter
    [InlineData("milk", "milk", 0)]
    public void Edit_distance_counts_single_typos_as_one(string a, string b, int expected)
    {
        Assert.Equal(expected, ProductSearchService.EditDistance(a, b));
    }
}
