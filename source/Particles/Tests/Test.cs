using System;
using Xunit;
namespace Tests
{
    public class Test
    {
        [Fact()]
        public void TestMethod()
        {
            var m = new Particles.MyClass();
            Assert.Equal(42, m.Test());
        }
    }
}
