# Devoctomy.Blazwiz.Aws

The AWS base stack every blazwiz app runs on, and the scripts that deploy, test and remove it: CloudFront and S3 for
the portal, the Cognito user pool and identity pool, the per-user data bucket and the IAM role for signed-in users. It
contains no code.

## Usage

Apps reference it so it is restored; they do not copy its files. An app made from the template has its own
`cloudformation/deploy.ps1` and `remove.ps1`, which run this package's scripts (from its
`tools/cloudformation` folder) at the app's `BlazwizVersion`. Upgrading the framework upgrades the stack: deploy
again after upgrading.

An app's own AWS resources go in its `cloudformation/app.yaml`, which can import every output of this stack. See
`tools/cloudformation/README.md` in the package.

The scripts create, change and delete real AWS resources in the account you run them with.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
