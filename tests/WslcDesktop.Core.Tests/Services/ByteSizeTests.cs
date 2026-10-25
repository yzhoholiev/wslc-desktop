using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class ByteSizeTests
{
    [Theory]
    [InlineData("0B", 0L)]
    [InlineData("512B", 512L)]
    [InlineData("120MB", 120_000_000L)]
    [InlineData("4.4MB", 4_400_000L)]
    [InlineData("1.5GB", 1_500_000_000L)]
    [InlineData("2kB", 2_000L)]
    [InlineData("15.1MiB", 15_833_497L)]
    [InlineData("1KiB", 1_024L)]
    [InlineData("1GiB", 1_073_741_824L)]
    [InlineData("5 MiB", 5_242_880L)]
    [InlineData(" 1TB ", 1_000_000_000_000L)]
    public void Parse_ReadsDecimalAndBinaryUnits(string text, long expected) => Assert.Equal(expected, ByteSize.Parse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("—")]
    [InlineData("N/A")]
    [InlineData("12PB")]
    public void Parse_ReturnsZeroForUnparseableText(string? text) => Assert.Equal(0L, ByteSize.Parse(text));

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1_023L, "1023 B")]
    [InlineData(1_024L, "1 KiB")]
    [InlineData(5_242_880L, "5 MiB")]
    [InlineData(33_285_996_544L, "31 GiB")]
    [InlineData(1_209_462_790_554L, "1.1 TiB")]
    public void Format_UsesBinaryUnits(long bytes, string expected) => Assert.Equal(expected, ByteSize.Format(bytes));

    [Theory]
    [InlineData("120MB", "114.4 MiB")]
    [InlineData("15.1MiB", "15.1 MiB")]
    [InlineData("0B", "0 B")]
    [InlineData("", "")]
    public void Normalize_RewritesCliSizesInBinaryUnits(string text, string expected) => Assert.Equal(expected, ByteSize.Normalize(text));

    [Theory]
    [InlineData("49.90%", 49.9)]
    [InlineData("0.00%", 0)]
    [InlineData("150.5%", 150.5)]
    [InlineData("—", 0)]
    [InlineData(null, 0)]
    public void ParsePercent_ReadsCliPercentages(string? text, double expected) => Assert.Equal(expected, ByteSize.ParsePercent(text), Tolerance.Default);
}
