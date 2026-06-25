using FigmaToUnity.Core;
using Xunit;

namespace FigmaToUnity.Core.Tests
{
    public class FigmaNameSanitizerTests
    {
        [Theory]
        [InlineData("Hello World", "Hello_World")]
        [InlineData("Button#image", "Button")]
        [InlineData("Home Page#prefab#image", "Home_Page")]
        [InlineData("Card / Item", "Card_Item")]
        [InlineData("  trim  me  ", "trim_me")]
        public void SanitizesToSafeAssetNames(string input, string expected)
        {
            Assert.Equal(expected, FigmaNameSanitizer.Sanitize(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("###")]
        [InlineData("!!!")]
        public void FallsBackToUnnamedWhenNothingSurvives(string input)
        {
            Assert.Equal("Unnamed", FigmaNameSanitizer.Sanitize(input));
        }
    }
}
