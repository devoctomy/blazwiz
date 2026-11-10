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
`deploy.ps1` keeps the stack's current value of any optional parameter it is not given (the custom domain and
certificate, local development origins, deletion protection, audit logging, events), so an upgrade run need not
repeat them; pass a value to change one (`-CustomDomainName ''` removes the custom domain).

With `-Events enabled` (the default; apps created without user events pass `disabled`) the stack has a user events
API: an AWS AppSync Events API (`<AppName>-<Environment>-events`) with one channel namespace, `users`. A user's open
devices use it to tell each other at once when they sign out everywhere. It is charged per use (connection minutes and
event operations), with no fixed charge. `deploy.ps1` writes its hosts (`EventsHttpEndpoint`,
`EventsRealtimeEndpoint`) to the `Events` section of `appsettings.Production.json`, and adds them to the
Content-Security-Policy's `connect-src` (`https:` and `wss:`).

## What protects the users' data

- **Each user reaches only their own data.** The identity pool's role allows S3 only under `<identity id>/` in the data
  bucket (listing included) and Secrets Manager only for `<user pool sub>/keys/*`, where the sub comes from the ID
  token as a principal tag. `test-auth.ps1` checks both, including that another user's prefix and secrets are refused.
- **The password never leaves the browser.** The portal's app client allows SRP and token refresh only. The admin
  password flow is on for `test-auth.ps1`, which runs as the deployer, not as the browser.
- **Nothing a user's credentials can do is final.** The data bucket is versioned, so a delete leaves a marker and an
  overwrite keeps the old version for 30 days, and only an operator (not the role) can remove them. `remove.ps1`
  deletes every version when the environment goes.
- **A deployment never replaces what cannot be rebuilt without asking.** `stack-policy.json` denies replacing or
  removing the user pool, identity pool, data bucket and audit log bucket (their users, data and logs), the
  CloudFront distribution (the portal's address) and the app client (every session). `deploy.ps1` refuses a change
  set that would, interactive or not, unless `-AllowStatefulReplacement` is given. Other replacements are listed with a
  warning and need confirming.
- **The user pool is protected in production.** With `-UserPoolDeletionProtection auto` (the default) Cognito refuses
  to delete the pool when `-Environment` is `prod`, even through the stack; `remove.ps1` turns it off first. Pass
  `ACTIVE` or `INACTIVE` to choose for other environments.
- **Data access is logged in production.** With `-AuditLogging auto` (the default) a CloudTrail trail records every
  read, write and delete of an object in the data bucket when `-Environment` is `prod`, in an audit log bucket kept a
  year (`AuditLogBucketName`). CloudTrail charges for data events; pass `false` to turn it off, or `true` for other
  environments. Management events (Cognito, Secrets Manager, CloudFormation) are in the account's own trail, if it has
  one.
- **A user's events reach only their own devices.** The events API takes only the user pool's tokens (no IAM, so the
  identity pool's AWS credentials cannot reach it), and the `users` namespace's handlers refuse any channel but the
  caller's own `/users/<sub>`. Events carry no secrets. `test-auth.ps1` checks that another user's channel is
  refused.
- **Email changes need verification.** A new email address only takes over once the user has proved they can read it,
  and the app client can read `email`, `email_verified` and `name` and write nothing but `name`.
- **Local development origins are only allowed where asked.** `-LocalDevOrigins` defaults to the portal's local URLs
  for every environment but `prod`, which gets none.
- **The users' secrets go with the environment.** The portal tags every keys secret `app=<AppName>`; `remove.ps1`
  finds and deletes them (the deployer's policy allows deleting tagged secrets only).
- **A deleted profile's keys can be restored for 7 days.** The portal deletes a keys secret with a recovery window,
  and the role refuses deletion without one, so neither a bug nor a hijacked session can destroy a profile's keys at
  once. An operator can restore the secret (`aws secretsmanager restore-secret`) within the window; the app treats
  the profile as gone.
- **Secrets stay off the command line.** The scripts pass passwords and tokens to the AWS CLI through files only the
  current user can read, overwritten and deleted afterwards.
- **TLS.** With the CloudFront certificate (no custom domain) the minimum TLS version is 1.0, which CloudFront does not
  let a default certificate raise; a custom domain gets `TLSv1.2_2021`. The managed security headers policy sends HSTS
  for a year without `includeSubDomains`.
