using System.Reflection;
using System.Text;

namespace SnpEvolution.Tests.Layering
{
    public class MethodBodyTypesTests
    {
        private static int NamesAStringBuilderOnlyInItsBody() => new StringBuilder("ab").Length;

        // A type a method only names inside its body is invisible to its signature, so the namespace graph needs the IL.
        [Fact]
        public void FindsATypeNamedOnlyInsideAMethodBody()
        {
            MethodInfo method = typeof(MethodBodyTypesTests).GetMethod(nameof(NamesAStringBuilderOnlyInItsBody), BindingFlags.NonPublic | BindingFlags.Static)!;

            Assert.Contains(typeof(StringBuilder), MethodBodyTypes.Of(method));
        }
    }
}
