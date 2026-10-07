using SnpEvolution.Tests.Export;
using SnpEvolution.Tests.Search.Proposals;
using SnpEvolution.Tests.Simulation.Metal;
using Xunit.Sdk;

namespace SnpEvolution.Tests.Fixtures
{
    public class SlowAttributeTests
    {
        // CI's slow step filters on Speed=slow, so a discoverer xunit cannot resolve would leave that step running nothing.
        [Fact]
        public void SlowAndToolTestsCarryTheSlowTraitAndOthersDoNot()
        {
            Assert.Equal(new[] { "slow" }, SpeedOf(typeof(RecurrenceProposerTests), nameof(RecurrenceProposerTests.ARandomSequenceAsksForNothing)));
            Assert.Equal(new[] { "slow" }, SpeedOf(typeof(VerilogExporterTests), nameof(VerilogExporterTests.PartsMatchOurEngineStepForStepOnEveryContractCase)));
            Assert.Equal(new[] { "slow" }, SpeedOf(typeof(MetalEngineTests), nameof(MetalEngineTests.MatchesTheCpuOnSmallSingleComputationNetworks)));
            Assert.Empty(SpeedOf(typeof(RecurrenceProposerTests), nameof(RecurrenceProposerTests.PowersOfTwoAskForARegisterAndADouble)));
        }

        private static List<string> SpeedOf(Type testClass, string method)
        {
            var assembly = new TestAssembly(Reflector.Wrap(testClass.Assembly));
            var collection = new TestCollection(assembly, null, "traits");
            var testMethod = new TestMethod(new TestClass(collection, Reflector.Wrap(testClass)), Reflector.Wrap(testClass.GetMethod(method)!));
            var testCase = new XunitTestCase(new NullMessageSink(), TestMethodDisplay.Method, TestMethodDisplayOptions.None, testMethod);
            return testCase.Traits.TryGetValue("Speed", out List<string>? speeds) ? speeds : new List<string>();
        }
    }
}
