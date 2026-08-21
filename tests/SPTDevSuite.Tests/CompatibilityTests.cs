using SPTDevSuite.Contracts;
using SPTDevSuite.Server;

namespace SPTDevSuite.Tests;

public sealed class CompatibilityTests
{
    [Fact]
    public void ExactSptVersionIsAccepted()
    {
        var result = CompatibilityPolicy.Evaluate(DevSuiteConstants.RequiredSptVersion);
        var metadata = new ModMetadata();

        Assert.True(result.IsCompatible);
        Assert.Contains("OK", result.Message, StringComparison.Ordinal);
        Assert.True(metadata.SptVersion.IsSatisfied(new SemanticVersioning.Version("4.1.2")));
        Assert.False(metadata.SptVersion.IsSatisfied(new SemanticVersioning.Version("4.1.3")));
    }

    [Theory]
    [InlineData("4.1.1")]
    [InlineData("4.1.3")]
    [InlineData("4.2.0")]
    [InlineData(null)]
    public void EveryOtherSptVersionIsRejected(string? actual)
    {
        var result = CompatibilityPolicy.Evaluate(actual);

        Assert.False(result.IsCompatible);
        Assert.Contains("REJECTED", result.Message, StringComparison.Ordinal);
    }
}
