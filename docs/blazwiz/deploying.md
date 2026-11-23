# Deploying

A blazwiz app is a set of static files (HTML, JavaScript, WebAssembly), so it needs no web server of its own. It is
hosted on AWS: S3 holds the files and CloudFront serves them. Cognito signs users in, and a second S3 bucket holds
their synced files.

> The deploy scripts create, change and delete **real AWS resources**, which can cost money, in the account whose
> credentials you run them with. Read the scripts' documentation before running them.

## The two stacks

An app's AWS resources are in two CloudFormation stacks (a stack is a group of AWS resources created and updated
together from a template):

- **The base stack**, `<AppName>-<Environment>`, comes with the framework in the `Devoctomy.Blazwiz.Aws` package:
  CloudFront and S3 for the app, the Cognito user pool and identity pool, the per-user data bucket, the IAM role for
  signed-in users, and, with user events, the AppSync Events API. Your app does not copy it: it uses the one at its
  `BlazwizVersion`, so **upgrading the framework upgrades the stack**. Deploy again after upgrading.
- **Your app's stack**, `<AppName>-<Environment>-app`, from `cloudformation/app.yaml`. Add your own AWS resources
  here. It can import every output of the base stack, for example
  `!ImportValue !Sub '${BaseStackName}-DataBucketName'`.

## The scripts

They are in your app's `cloudformation/` folder:

| Script | What it does |
|---|---|
| `deploy.ps1` | Creates or updates the base stack, then your app's stack; publishes the app to S3; clears CloudFront's cache; sets up a test user. It writes your stack's ids to `wwwroot/appsettings.Production.json`. |
| `remove.ps1` | Deletes everything: your app's stack, then the buckets' contents (including every user's files and keys) and the base stack. |
| `base-stack.ps1` | Finds the base stack and its scripts in the restored `Devoctomy.Blazwiz.Aws` package. |
| `app.yaml` | Your app's own CloudFormation template. |
| `deploy-policy.json` | The least IAM permissions needed to run the scripts. |

You need the AWS CLI v2, the .NET 10 SDK, and credentials for the target account. The scripts take `-AppName`
(default: your app's name), `-Environment` (default `dev`), `-Region` and `-AwsProfile`, so several environments can
share one account.

The commands are in your app's README (`Deploying to AWS`) and in `cloudformation/README.md`, which also explains how
to create a least-privilege IAM user to deploy with. The base stack's scripts document every parameter: run
`./cloudformation/base-stack.ps1` to find them, then `Get-Help <path>/deploy.ps1 -Full`.

## What deploying sets up in your app

`deploy.ps1` writes `wwwroot/appsettings.Production.json` (git ignored) with:

- `AwsCognito`: the region, user pool, app client and identity pool, so the app signs in with Cognito instead of the
  placeholder;
- `AwsS3`: the region and data bucket, so sync reaches S3;
- `Events`: the AppSync Events endpoints, when user events are on.

With user events, the base stack's `Events` parameter is `enabled`. Your app's `cloudformation/deploy.ps1` sets it
from `$events`, which the template set from `--events`. To turn events off later, change it to `'disabled'` and deploy
(and remove `AddBlazwizAppSyncEvents` and its package).

## Two-step verification

The base stack's `Mfa` parameter says whether users sign in with a code from an authenticator app as well as their
password (see [Sign in and sessions](sign-in-and-sessions.md#two-step-verification)):

| `-Mfa` | What users get |
|---|---|
| `optional` (the default) | Each user turns it on, or not, in their Profile. |
| `required` | Every user sets up an authenticator app the next time they sign in. |
| `off` | No second step for anyone. |

```powershell
./cloudformation/deploy.ps1 -Mfa required
```

Like the other optional parameters, it keeps the stack's current value when you leave it out. The user pool also
remembers devices whatever you choose, so a user who signs in with a passkey is asked for a code only once on each
device.

## The optimal build

By default the app is published as .NET code run by WebAssembly's interpreter. For production, compile it ahead of
time to WebAssembly, with SIMD: it runs several times faster (deriving keys from passphrases most of all), at the
cost of a bigger download and a publish that takes several minutes.

```powershell
dotnet workload install wasm-tools
./cloudformation/deploy.ps1 -Region eu-west-2 -Aot
```

To always build that way, uncomment the `RunAOTCompilation` / `WasmEnableSIMD` block in `MyApp.Portal.csproj`.

## Deploying from GitHub

The blazwiz repository deploys its own template app from GitHub Actions, through OIDC (so no AWS keys are kept in
GitHub). [docs/deploying.md](../deploying.md) describes its workflows and the one-off AWS and GitHub setup. Use it as
a model if you want to deploy your app the same way.
