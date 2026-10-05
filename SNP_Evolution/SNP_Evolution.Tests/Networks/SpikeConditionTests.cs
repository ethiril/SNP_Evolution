using System.Text.RegularExpressions;
using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Networks
{
    public class SpikeConditionTests
    {
        // Every template the generator produces, every expression in the saved data, and some general regex syntax.
        public static TheoryData<string> Expressions => new TheoryData<string>
        {
            "a", "aa", "aaa", "a+", "aa+", "a*", "aa*", "a?", "aa?", "a(a)+", "a(aa)+", "aa(aaa)+", "aaa(aa)*",
            "aa(a)?", "a(aaa)?", "aaa(aaa)*", "", "()", "a|aa", "(a|aaa)*", "(aa|aaa)+", "a{3}", "a{2,}", "a{2,4}",
            "(aa){1,3}a", "(?:aaa)+", "a+?", "(a{2})*?", ".", "a.a", "b", "ab|aa", "a{x}", "(aaaa|aaaaaa)+a",
        };

        [Theory]
        [MemberData(nameof(Expressions))]
        public void MatchesTheSameCountsAsTheAnchoredRegex(string expression)
        {
            var regex = new Regex("^(?:" + expression + ")$", RegexOptions.NonBacktracking);
            SpikeCondition condition = SpikeCondition.Parse(expression);

            for (int spikes = 0; spikes <= 200; spikes++)
            {
                Assert.True(regex.IsMatch(new string('a', spikes)) == condition.Matches(spikes), $"'{expression}' disagrees at {spikes} spikes");
            }
        }

        [Theory]
        [InlineData("a(aa)+", 1_000_001, true)]
        [InlineData("a(aa)+", 1_000_000, false)]
        [InlineData("aaa(aaaaa)*", 5_000_000_003, true)]
        [InlineData("aa", 4_000_000_000, false)]
        [InlineData("a+", long.MaxValue, true)]
        public void MatchesHugeCountsWithoutBuildingThem(string expression, long spikes, bool matches)
        {
            Assert.Equal(matches, SpikeCondition.Parse(expression).Matches(spikes));
        }

        [Fact]
        public void StoresTheSetAsATailAndARepeatingCycle()
        {
            SpikeCondition condition = SpikeCondition.Parse("a(aaa)+");

            // Accepts 4, 7, 10, ...: counts 0 and 1 are the tail, then false, false, true repeats from 2.
            Assert.Equal(2, condition.TailLength);
            Assert.Equal(3, condition.Period);
            Assert.Equal(new[] { false, false, false, false, true }, condition.Accepts.ToArray());
        }

        [Theory]
        [InlineData("(")]
        [InlineData("a)")]
        [InlineData("*a")]
        [InlineData("a**")]
        [InlineData("a{3}{2}")]
        [InlineData("a{3,1}")]
        [InlineData("[a]")]
        [InlineData("\\d")]
        [InlineData("(?=a)")]
        [InlineData("a{99999999999}")]
        public void RejectsInvalidOrUnsupportedSyntax(string expression)
        {
            Assert.Throws<ArgumentException>(() => SpikeCondition.Parse(expression));
        }
    }
}
