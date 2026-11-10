# myapp AWS deployment

The infrastructure is in two stacks:

- **The blazwiz base stack**, `<AppName>-<Environment>`: CloudFront and S3 for the portal, Cognito, the per-user data
  bucket. It is released with the framework (`Devoctomy.Blazwiz.Aws`, at the app's `BlazwizVersion`), not copied
  here, so upgrading the framework upgrades it: deploy after upgrading. `base-stack.ps1` prints where it is.
- **The app's own stack**, `<AppName>-<Environment>-app`, from `app.yaml`: add the app's own resources there. It can
  import every output of the base stack (`!ImportValue !Sub '${BaseStackName}-DataBucketName'`).

| Script | What it does |
| --- | --- |
| `deploy.ps1` | Creates or updates the base stack, then the app's stack, publishes the portal to S3, invalidates CloudFront and sets up a test user in Cognito. |
| `remove.ps1` | Deletes the app's stack, then empties the buckets (every version, including the users' keys) and deletes the base stack, turning the user pool's deletion protection off first. |
| `app.yaml` | The app's own CloudFormation template. |
| `base-stack.ps1` | Finds the base stack and its scripts. |

The scripts here pass the app's name, folder and portal project to the base stack's scripts, and every other
parameter through. `deploy.ps1` also passes `-Events enabled` or `-Events disabled`, depending on whether the app was
created with user events. `-Aot` publishes the portal compiled ahead of time to WebAssembly, with SIMD (the optimal
build: needs `dotnet workload install wasm-tools`; see the app's README). With user events, the base stack has an AWS AppSync Events API, charged per use (connection minutes
and event operations, with no fixed charge). Its deployer permissions are the `AppSyncEvents` statement of
`deploy-policy.json`. `remove.ps1` needs nothing extra: the API is part of the stack.

All three scripts use the AWS CLI v2 with your normal credential chain. Pass `-AwsProfile` to use a named profile.

## Minimum IAM policy for running the scripts

`deploy.ps1` deploys the base stack through a change set (and the app's stack with `aws cloudformation deploy`) without a CloudFormation service role, so CloudFormation creates the resources using **your** credentials. The deploying identity needs permission for every resource type in the template, as well as for the S3 upload, the CloudFront invalidation, and the Cognito user admin calls.

[`deploy-policy.json`](deploy-policy.json) covers `deploy.ps1` and `remove.ps1`, and nothing more.
Before you use it:

- Replace `ACCOUNT_ID` with your 12-digit AWS account id.
- If you deploy with an `-AppName` other than `myapp`, change the `myapp-*` prefixes to match. Stack, bucket and IAM role names all start with `<AppName>-`.

To create it as a managed policy, with your account id filled in:

```powershell
$account = aws sts get-caller-identity --query Account --output text
(Get-Content ./cloudformation/deploy-policy.json -Raw) -replace 'ACCOUNT_ID', $account | Set-Content "$env:TEMP/myapp-deploy-policy.json" -Encoding ascii
aws iam create-policy --policy-name myapp-deploy --policy-document "file://$env:TEMP/myapp-deploy-policy.json"
```

To change an existing policy after updating the file, use `aws iam create-policy-version` with `--set-as-default`, or
paste the file into the policy's JSON editor in the IAM console.

Notes:

- **You don't need permissions for some calls.** `sts:GetCallerIdentity` is always allowed.
- **`IamPassRoleToIdentityPool` has no `iam:PassedToService` condition.** A condition of `cognito-identity.amazonaws.com` doesn't match when the role is attached to the identity pool, and the deploy fails at `IdentityPoolRoleAttachment`. It is limited by the `myapp-*` role name instead.
- **`CustomDomainCertificate`** is only needed when you pass `-CustomDomainName` / `-CertificateArn`. You can drop it otherwise.
- **IAM is the sensitive part.** The template creates an IAM role, so this identity can create and edit any role whose name starts with `myapp-`. In principle that lets it create a more powerful role for itself. For throwaway testing that is usually fine. For stronger isolation, use a dedicated sandbox account, or add a permissions boundary condition (`iam:PermissionsBoundary`) to `CreateRole` / `PutRolePolicy`.
- **If a deploy fails with `AccessDenied`**, the stack events name the exact missing action. Run `aws cloudformation describe-stack-events --stack-name myapp-dev`, add that action to the policy and deploy again. A failed first deploy leaves the stack in `ROLLBACK_COMPLETE`. Delete it with `remove.ps1` before you retry. A failed update whose rollback also failed (`UPDATE_ROLLBACK_FAILED`, e.g. because the same permission was missing both ways) is recovered with `aws cloudformation continue-update-rollback --stack-name <stack>` once the policy has the action, then deployed again.

### Setting up the credentials

1. Create a customer managed policy (e.g. `myapp-deploy`) from `deploy-policy.json`, as above.
2. Create an IAM user (e.g. `myapp-deployer`) with no console access and attach only that policy.
3. Create an access key for the user and configure a named CLI profile:

   ```powershell
   aws configure --profile myapp-deploy
   ```

4. Use that profile with the scripts:

   ```powershell
   ./cloudformation/deploy.ps1   -AwsProfile myapp-deploy -Region eu-west-2 -TestUserEmail me@example.com
   ./cloudformation/remove.ps1   -AwsProfile myapp-deploy -Region eu-west-2
   ```

When you've finished testing, delete the access key, or the whole user.
