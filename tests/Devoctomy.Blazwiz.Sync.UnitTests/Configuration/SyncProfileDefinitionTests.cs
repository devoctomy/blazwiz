using Devoctomy.Blazwiz.Sync.Configuration;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Configuration;

public class SyncProfileDefinitionTests
{
    private readonly SyncOptions options = new();

    [Fact]
    public void GivenADefinition_WhenAddedByType_ThenItsProfileIsAdded()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act
        var result = sut.AddProfile<BetaSyncProfile>();

        // Assert
        Assert.Same(sut, result);
        var profile = Assert.Single(options.Profiles);
        Assert.Equal("beta", profile.Name);
        Assert.Equal("beta/**/*", profile.RemotePattern);
        Assert.NotNull(profile.KeyScope);
    }

    [Fact]
    public void GivenADefinition_WhenAddedAsAnInstance_ThenItsProfileIsAdded()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act
        sut.AddProfile(new BetaSyncProfile());

        // Assert
        Assert.Equal("beta", Assert.Single(options.Profiles).Name);
    }

    [Fact]
    public void GivenAnAssembly_WhenScanned_ThenEveryConcreteDefinitionIsAddedInNameOrder()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act: the test assembly defines alpha (internal, private constructor), beta and an abstract base.
        var result = sut.AddProfilesFromAssemblyContaining<SyncProfileDefinitionTests>();

        // Assert
        Assert.Same(sut, result);
        Assert.Equal(["alpha", "beta"], options.Profiles.Select(p => p.Name));
        Assert.Equal("alpha/**/*.md", options.Profiles[0].RemotePattern);
        Assert.True(options.Profiles[0].IncludeEmptyFolders);
    }

    [Fact]
    public void GivenAProfileAlreadyAdded_WhenTheSameDefinitionIsAdded_ThenThrows()
    {
        // Arrange
        var sut = new SyncBuilder(options).AddProfile<BetaSyncProfile>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.AddProfile<BetaSyncProfile>());
        Assert.Throws<ArgumentException>(() => sut.AddProfilesFromAssembly(typeof(SyncProfileDefinitionTests).Assembly));
    }

    [Fact]
    public void GivenNullArguments_WhenAdded_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = new SyncBuilder(options);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.AddProfile((SyncProfileDefinition)null!));
        Assert.Throws<ArgumentNullException>(() => sut.AddProfilesFromAssembly(null!));
    }

    internal sealed class AlphaSyncProfile : SyncProfileDefinition
    {
        private AlphaSyncProfile()
        {
        }

        public override string Name => "alpha";

        public override void Configure(SyncProfileBuilder profile) => profile.UseRemote("alpha/**/*.md", includeEmptyFolders: true).UseKeyId("4b9d6e1a-2c7f-4a3e-8d5b-1f0e9c2a7b63");
    }

    public sealed class BetaSyncProfile : SyncProfileDefinition
    {
        public override string Name => "beta";

        public override void Configure(SyncProfileBuilder profile) => profile.UseRemote("beta/**/*").UseKeyId("8e2f4a6c-9b1d-4c5e-a7f3-0d6b2e8c4a19");
    }

    public abstract class SharedSyncProfile : SyncProfileDefinition
    {
        public override string Name => "shared";
    }
}
