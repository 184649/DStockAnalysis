using DStockAnalysis.Common;
using Xunit;

namespace DStockAnalysis.Tests.Unit;

public class GradesTests
{
    [Theory] // UT-GR-01: スコア→レターグレード境界
    [InlineData(90, "S")]
    [InlineData(85, "S")]
    [InlineData(84.9, "A")]
    [InlineData(75, "A")]
    [InlineData(60, "B")]
    [InlineData(45, "C")]
    [InlineData(44.9, "D")]
    [InlineData(0, "D")]
    public void Letter_Boundaries(double score, string expected)
        => Assert.Equal(expected, Grades.Letter(score));
}
