using FigmaToUnity.Core;
using Xunit;

namespace FigmaToUnity.Core.Tests
{
    public class FigmaUrlParserTests
    {
        [Theory]
        [InlineData("https://www.figma.com/design/dPLaWbEkiBd1ScMjzNJNfk/1", "dPLaWbEkiBd1ScMjzNJNfk")]
        [InlineData("https://www.figma.com/file/ABC123/hello", "ABC123")]
        [InlineData("https://figma.com/design/xYz9/title?node-id=1:2", "xYz9")]
        [InlineData("https://FIGMA.COM/DESIGN/MixedCase/x", "MixedCase")]
        public void ParsesKnownUrlShapes(string url, string expectedKey)
        {
            Assert.True(FigmaUrlParser.TryParseFileKey(url, out string key));
            Assert.Equal(expectedKey, key);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("https://example.com/design/ABC")]
        [InlineData("not a url")]
        public void RejectsInvalidUrls(string url)
        {
            Assert.False(FigmaUrlParser.TryParseFileKey(url, out string key));
            Assert.Equal(string.Empty, key);
        }
    }
}
