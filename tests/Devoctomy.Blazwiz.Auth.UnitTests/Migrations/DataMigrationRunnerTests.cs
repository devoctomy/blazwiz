using Devoctomy.Blazwiz.Auth.Migrations;
using Devoctomy.Blazwiz.Auth.Migrations.Interfaces;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Migrations;

public class DataMigrationRunnerTests
{
    private readonly Mock<ILocalStorageService> mockStorage = new();
    private readonly List<string> ran = [];
    private readonly UserSession session = new() { Username = "Bob@Example.com" };
    private Dictionary<string, int>? stored;

    public DataMigrationRunnerTests()
    {
        mockStorage.Setup(x => x.GetObjectAsync<Dictionary<string, int>>(DataMigrationRunner.StorageKey)).ReturnsAsync(() => stored);
        mockStorage
            .Setup(x => x.SetObjectAsync(DataMigrationRunner.StorageKey, It.IsAny<Dictionary<string, int>>()))
            .Callback<string, Dictionary<string, int>>((_, v) => stored = new Dictionary<string, int>(v))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("migrations", Assert.Throws<ArgumentNullException>(() => new DataMigrationRunner(null!, mockStorage.Object, NullLogger<DataMigrationRunner>.Instance)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new DataMigrationRunner([], null!, NullLogger<DataMigrationRunner>.Instance)).ParamName);
        Assert.Equal("logger", Assert.Throws<ArgumentNullException>(() => new DataMigrationRunner([], mockStorage.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenTwoMigrationsToTheSameVersion_WhenConstructing_ThenThrowsInvalidOperationException()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => CreateSut(Migration("Sync", 1), Migration("Sync", 1)));

        // Assert
        Assert.Contains("version 1 of 'Sync'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheRunner_WhenOrdered_ThenItRunsBeforeEveryOtherHandler()
    {
        // Act & Assert
        Assert.Equal(int.MinValue, CreateSut().Order);
    }

    [Fact]
    public async Task GivenNoMigrations_WhenShellLoading_ThenNothingIsReadOrRecorded()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.OnShellLoadingAsync(session);

        // Assert
        mockStorage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenNoRecordedVersion_WhenShellLoading_ThenEveryMigrationRunsInOrderAndIsRecorded()
    {
        // Arrange
        var sut = CreateSut(Migration("Sync", 2), Migration("Sync", 1), Migration("Sync", 3));

        // Act
        await sut.OnShellLoadingAsync(session);

        // Assert
        Assert.Equal(["Sync|App|1", "Sync|App|2", "Sync|App|3"], ran);
        Assert.Equal(3, stored!["Sync|app"]);
    }

    [Fact]
    public async Task GivenARecordedVersion_WhenShellLoading_ThenOnlyNewerMigrationsRun()
    {
        // Arrange
        stored = new Dictionary<string, int> { ["Sync|app"] = 2 };
        var sut = CreateSut(Migration("Sync", 1), Migration("Sync", 2), Migration("Sync", 3));

        // Act
        await sut.OnShellLoadingAsync(session);

        // Assert
        Assert.Equal(["Sync|App|3"], ran);
        Assert.Equal(3, stored["Sync|app"]);
    }

    [Fact]
    public async Task GivenUserMigrations_WhenTwoUsersSignIn_ThenEachUsersDataIsMigratedOnce()
    {
        // Arrange
        var sut = CreateSut(Migration("Notes", 1, DataMigrationScope.User));

        // Act
        await sut.OnShellLoadingAsync(session);
        await sut.OnShellLoadingAsync(new UserSession { Username = "ann@example.com" });
        await sut.OnShellLoadingAsync(new UserSession { Username = "bob@example.com" });

        // Assert
        Assert.Equal(["Notes|User|1", "Notes|User|1"], ran);
        Assert.Equal(1, stored!["Notes|user|bob@example.com"]);
        Assert.Equal(1, stored["Notes|user|ann@example.com"]);
    }

    [Fact]
    public async Task GivenAFailingMigration_WhenShellLoading_ThenItsAreaStopsThereAndOtherAreasCarryOn()
    {
        // Arrange
        var sut = CreateSut(
            Migration("Sync", 1),
            Migration("Sync", 2, fail: true),
            Migration("Sync", 3),
            Migration("Text", 1));

        // Act
        await sut.OnShellLoadingAsync(session);

        // Assert
        Assert.Equal(["Sync|App|1", "Text|App|1"], ran);
        Assert.Equal(1, stored!["Sync|app"]);
        Assert.Equal(1, stored["Text|app"]);
    }

    [Fact]
    public async Task GivenAFailedMigrationLastTime_WhenShellLoadingAgain_ThenItIsTriedAgain()
    {
        // Arrange
        stored = new Dictionary<string, int> { ["Sync|app"] = 1 };
        var sut = CreateSut(Migration("Sync", 1), Migration("Sync", 2));

        // Act
        await sut.OnShellLoadingAsync(session);

        // Assert
        Assert.Equal(["Sync|App|2"], ran);
        Assert.Equal(2, stored["Sync|app"]);
    }

    [Fact]
    public async Task GivenNullSession_WhenShellLoading_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut(Migration("Sync", 1));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.OnShellLoadingAsync(null!));
    }

    private DataMigrationRunner CreateSut(params IDataMigration[] migrations) =>
        new(migrations, mockStorage.Object, NullLogger<DataMigrationRunner>.Instance);

    private IDataMigration Migration(string area, int version, DataMigrationScope scope = DataMigrationScope.App, bool fail = false)
    {
        var mock = new Mock<IDataMigration>();
        mock.SetupGet(x => x.Area).Returns(area);
        mock.SetupGet(x => x.Version).Returns(version);
        mock.SetupGet(x => x.Scope).Returns(scope);
        mock.Setup(x => x.MigrateAsync(It.IsAny<DataMigrationContext>()))
            .Returns<DataMigrationContext>(context =>
            {
                Assert.NotNull(context.Session);
                if (fail)
                {
                    throw new InvalidOperationException("broken");
                }

                ran.Add($"{area}|{scope}|{version}");
                return Task.CompletedTask;
            });
        return mock.Object;
    }
}
