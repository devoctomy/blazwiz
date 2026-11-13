using System.Diagnostics;
using System.Reflection;
using Devoctomy.Blazwiz.UI.Services;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class AppBuildInfoTests
{
    [Fact]
    public void GivenNullAssembly_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("assembly", Assert.Throws<ArgumentNullException>(() => new AppBuildInfo(null!)).ParamName);
    }

    [Fact]
    public void GivenDebugAssemblyWithBuildTime_WhenConstructed_ThenReadsVersionDebugAndBuildTime()
    {
        // Arrange
        var assembly = new FakeAssembly(
            new Version(1, 2, 3, 4),
            new DebuggableAttribute(isJITTrackingEnabled: true, isJITOptimizerDisabled: true),
            new AssemblyMetadataAttribute("Other", "x"),
            new AssemblyMetadataAttribute("BuildTimeUtc", "2026-11-12T10:00:00Z"));

        // Act
        var sut = new AppBuildInfo(assembly);

        // Assert
        Assert.Equal("1.2.3.4", sut.Version);
        Assert.True(sut.IsDebugBuild);
        Assert.Equal("2026-11-12T10:00:00Z", sut.BuildTime);
    }

    [Fact]
    public void GivenOptimizedAssembly_WhenConstructed_ThenNotDebugBuild()
    {
        // Arrange
        var assembly = new FakeAssembly(new Version(2, 0), new DebuggableAttribute(isJITTrackingEnabled: false, isJITOptimizerDisabled: false));

        // Act
        var sut = new AppBuildInfo(assembly);

        // Assert
        Assert.False(sut.IsDebugBuild);
    }

    [Fact]
    public void GivenAssemblyWithoutVersionOrAttributes_WhenConstructed_ThenDefaults()
    {
        // Arrange
        var assembly = new FakeAssembly(null);

        // Act
        var sut = new AppBuildInfo(assembly);

        // Assert
        Assert.Equal(string.Empty, sut.Version);
        Assert.False(sut.IsDebugBuild);
        Assert.Null(sut.BuildTime);
        Assert.DoesNotContain("+", sut.FrameworkVersion, StringComparison.Ordinal);
        Assert.NotEmpty(sut.FrameworkVersion);
    }

    private sealed class FakeAssembly : Assembly
    {
        private readonly Version? version;
        private readonly Attribute[] attributes;

        public FakeAssembly(Version? version, params Attribute[] attributes)
        {
            this.version = version;
            this.attributes = attributes;
        }

        public override AssemblyName GetName() => GetName(false);

        public override AssemblyName GetName(bool copiedName) => new("Fake") { Version = version };

        public override object[] GetCustomAttributes(bool inherit) => attributes;

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            // Typed, as Attribute.GetCustomAttributes casts the array to the attribute's type.
            var matching = attributes.Where(attributeType.IsInstanceOfType).ToArray();
            var result = Array.CreateInstance(attributeType, matching.Length);
            Array.Copy(matching, result, matching.Length);
            return (object[])result;
        }
    }
}
