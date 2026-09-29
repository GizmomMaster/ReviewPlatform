using ReviewPlatform.Domain.Common;
using ReviewPlatform.Domain.Matrix;

namespace ReviewPlatform.Domain.Tests.Matrix;

public sealed class GradeRoleRuleTests
{
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(3, 2)]
    public void Create_InvalidLimits_Throws(int min, int max) =>
        Assert.Throws<DomainException>(() => new GradeRoleRule(Guid.NewGuid(), EvaluatorRole.Peer, min, max));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    public void Create_SelfNotExactlyOne_Throws(int min, int max) =>
        Assert.Throws<DomainException>(() => new GradeRoleRule(Guid.NewGuid(), EvaluatorRole.Self, min, max));

    [Fact]
    public void SetLimits_Valid_Updates()
    {
        var rule = new GradeRoleRule(Guid.NewGuid(), EvaluatorRole.Peer, 1, 5);

        rule.SetLimits(2, 3);

        Assert.Equal((2, 3), (rule.MinCount, rule.MaxCount));
    }
}
