using OpsMind.Core.Rag;

namespace OpsMind.Tests;

public class Bm25EncoderTests {
    [Fact]
    public void Tokenize_KeepsCompoundTokensAndTheirParts() {
        var tokens = Bm25Encoder.Tokenize("Адрес 169.254.x.x и pg_stat_activity");

        Assert.Contains("169.254.x.x", tokens);
        Assert.Contains("169", tokens);
        Assert.Contains("254", tokens);
        Assert.Contains("pg_stat_activity", tokens);
        Assert.Contains("activity", tokens);
    }

    [Fact]
    public void Tokenize_StemsRussianWordForms_ToSameToken() {
        var a = Bm25Encoder.Tokenize("блокировка");
        var b = Bm25Encoder.Tokenize("блокируется");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Tokenize_DropsStopWords() {
        var tokens = Bm25Encoder.Tokenize("Что делать, если не работает VPN");

        Assert.DoesNotContain("что", tokens);
        Assert.DoesNotContain("не", tokens);
        Assert.Contains("vpn", tokens);
    }

    [Fact]
    public void EncodeDocument_IndicesAreSortedAndUnique() {
        var (values, indices) = Bm25Encoder.EncodeDocument("VPN VPN VPN не подключается, ошибка -12");

        Assert.Equal(values.Length, indices.Length);
        Assert.Equal(indices.Order(), indices);
        Assert.Equal(indices.Distinct().Count(), indices.Length);
    }

    [Fact]
    public void EncodeDocument_RepeatedWordWeighsMoreButSaturates() {
        var once = Bm25Encoder.EncodeDocument("vpn ошибка").Values.Max();
        var thrice = Bm25Encoder.EncodeDocument("vpn vpn vpn ошибка").Values.Max();

        Assert.True(thrice > once);
        Assert.True(thrice < once * 3);
    }
}