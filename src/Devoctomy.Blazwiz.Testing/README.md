# Devoctomy.Blazwiz.Testing

Test helpers for blazwiz apps. `FrameworkText` gives tests the framework's real text, as the app gets it before its
own overrides, so tests can check the actual wording. It also checks that the app's text overrides still match the
framework, so a test fails after an upgrade that renamed or removed a key, or changed its placeholders.

## Usage

Reference it from the app's test projects.

```csharp
private readonly FrameworkText framework = new(typeof(SyncUiText));

[Fact]
public void GivenTheAppsOverrides_WhenComparedWithTheFramework_ThenEveryKeyIsFrameworkText()
{
    // Act
    var unknown = framework.UnknownOverrideKeys(typeof(MyAppText));

    // Assert
    Assert.Empty(unknown);
}
```

Pass the text resources of the modules the app uses (such as `SyncUiText`) to the constructor. Use
`MismatchedOverridePlaceholders` in the same way to check placeholders.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
