using Procument.Module.Sales.Services;
using Xunit;

namespace Procument.OurInventory.Tests;

/// <summary>Wallet transaction Base tags: one or more of "B1".."B7", stored comma-separated and sorted.</summary>
public sealed class TransactionBasesTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("b3", "B3")]
    [InlineData("B3,B1,B3", "B1,B3")]
    [InlineData(" b2 ; B9, X, B7 ", "B2,B7")]
    [InlineData("B8", null)]
    public void Normalize_keeps_valid_tags_deduplicated_and_sorted(string? input, string? expected)
        => Assert.Equal(expected, TransactionBases.Normalize(input));

    [Fact]
    public void Has_matches_whole_tags_only()
    {
        Assert.True(TransactionBases.Has("B1,B3", "B1"));
        Assert.True(TransactionBases.Has("B1,B3", "B3"));
        Assert.False(TransactionBases.Has("B1,B3", "B2"));
        Assert.False(TransactionBases.Has(null, "B1"));
    }
}
