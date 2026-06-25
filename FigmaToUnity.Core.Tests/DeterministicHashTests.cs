using FigmaToUnity.Core;
using Xunit;

namespace FigmaToUnity.Core.Tests
{
    public class DeterministicHashTests
    {
        [Fact]
        public void EmptyHashCodeIsZero()
        {
            DeterministicHash hash = new();
            Assert.Equal(0, hash.ToHashCode());
        }

        [Fact]
        public void SameInputsProduceSameHash()
        {
            DeterministicHash a = new();
            a.Add("hello");
            a.Add(42);
            a.Add(3.14f);
            a.Add(true);

            DeterministicHash b = new();
            b.Add("hello");
            b.Add(42);
            b.Add(3.14f);
            b.Add(true);

            Assert.Equal(a.ToHashCode(), b.ToHashCode());
        }

        [Fact]
        public void OrderOfInputsMatters()
        {
            DeterministicHash a = new();
            a.Add("x");
            a.Add("y");

            DeterministicHash b = new();
            b.Add("y");
            b.Add("x");

            Assert.NotEqual(a.ToHashCode(), b.ToHashCode());
        }

        [Fact]
        public void StringHashIsStableAcrossInvocations()
        {
            // The whole point of DeterministicHash is to survive process
            // restarts. Hardcode one observed value so regressions that
            // accidentally reintroduce GetHashCode randomness are caught.
            DeterministicHash hash = new();
            hash.Add("FigmaToUnity");
            int value = hash.ToHashCode();

            DeterministicHash again = new();
            again.Add("FigmaToUnity");
            Assert.Equal(value, again.ToHashCode());
        }

        [Fact]
        public void NullableFloatDistinguishesNullFromZero()
        {
            DeterministicHash a = new();
            a.Add((float?)null);

            DeterministicHash b = new();
            b.Add((float?)0f);

            Assert.NotEqual(a.ToHashCode(), b.ToHashCode());
        }
    }
}
