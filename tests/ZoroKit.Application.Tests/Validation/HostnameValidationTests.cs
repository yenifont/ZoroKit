using System.Text.RegularExpressions;

namespace ZoroKit.Application.Tests.Validation;

public class HostnameValidationTests
{
    // Same regex used in AutoVirtualHostService and HostsFileViewModel
    private static readonly Regex ValidHostnameRegex = new(
        @"^[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?(\.[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled);

    [Theory]
    [InlineData("localhost")]
    [InlineData("example.com")]
    [InlineData("my-site.test")]
    [InlineData("sub.domain.test")]
    [InlineData("a")]
    [InlineData("a-b")]
    [InlineData("my-app.local")]
    [InlineData("zorokit.app")]
    [InlineData("test123.dev")]
    [InlineData("a.b.c.d")]
    [InlineData("ABC.COM")]
    [InlineData("My-Site.Test")]
    public void ValidHostnames_ShouldMatch(string hostname)
    {
        Assert.True(ValidHostnameRegex.IsMatch(hostname), $"'{hostname}' should be a valid hostname");
    }

    [Theory]
    [InlineData("-start")]
    [InlineData("end-")]
    [InlineData("spa ce")]
    [InlineData("special@char")]
    [InlineData(".leading-dot")]
    [InlineData("trailing.")]
    [InlineData("")]
    [InlineData("double..dot")]
    [InlineData("-")]
    [InlineData("hello world.test")]
    [InlineData("tab\there")]
    [InlineData("semi;colon")]
    [InlineData("slash/path")]
    [InlineData("back\\slash")]
    [InlineData("under_score.test")]
    public void InvalidHostnames_ShouldNotMatch(string hostname)
    {
        Assert.False(ValidHostnameRegex.IsMatch(hostname), $"'{hostname}' should NOT be a valid hostname");
    }
}
