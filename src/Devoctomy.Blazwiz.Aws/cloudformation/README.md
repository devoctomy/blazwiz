# blazwiz base stack

The AWS infrastructure every blazwiz app runs on, released with the framework at the same version
(`Devoctomy.Blazwiz.Aws`): CloudFront and S3 for the portal, the Cognito user pool and identity pool, the per-user data
bucket and the IAM role for signed-in users (`stack.yaml`), and the scripts that deploy, test and remove it.

Apps do not copy these files. Each app has its own `cloudformation/deploy.ps1`, `test-auth.ps1` and `remove.ps1`,
which find this package at the app's `BlazwizVersion` and run its scripts with the app's name, folder and portal
project. Upgrading the framework upgrades the stack; deploy after upgrading.

An app's own AWS resources go in its `cloudformation/app.yaml`, deployed after this stack as
`<app>-<environment>-app`. It can import every output of this stack, exported as `<stack name>-<output>`, e.g.
`!ImportValue !Sub '${BaseStackName}-DataBucketName'`.

Every script takes `-AppName`, `-Environment`, `-Region` and `-AwsProfile`; see each script's help for the rest.
