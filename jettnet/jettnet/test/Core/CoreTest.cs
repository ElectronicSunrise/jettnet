using Xunit;

namespace jettnet.test.Core
{
    public class CoreTest
    {
        [Fact]
        public void ToID_generatesId()
        {
            string instance = "I'm getting bricked up writing these unit tests";
            var expected = 1895833957;
            Assert.Equal(expected, instance.ToID());
        }
    }
}