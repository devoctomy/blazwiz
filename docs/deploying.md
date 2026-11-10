# Deploying the template app from GitHub

Four workflows deploy and check the repository's template app (`template/`, as `myapp`) to AWS from GitHub. This tests the
framework as it stands, before it is released:

- The base stack and its scripts come from `src/Devoctomy.Blazwiz.Aws`.
- The portal builds against the packages' source (`template/Directory.Build.targets`), not packages from nuget.org.

Each workflow is run by hand: Actions, then the workflow, then **Run workflow**.

| Workflow | What it does |
| --- | --- |
| **Deploy** (`deploy.yml`) | Creates a new environment: the base stack (`myapp-<environment>`), the app's stack (`myapp-<environment>-app`), the portal, and optionally the deploy tests' user and the deploy tests. Refused if the environment already exists. |
| **Upgrade** (`upgrade.yml`) | Updates an existing environment to the commit it is run on: both stacks, then the portal (or only the portal), and optionally the deploy tests' user and the deploy tests. Refused if the environment does not exist. |
| **Deploy tests** (`deploy-tests.yml`) | Runs the deploy tests against an existing environment (below). Also run by Deploy and Upgrade when "Run the deploy tests afterwards" is ticked. |
| **Destroy** (`destroy.yml`) | Deletes an environment, once its name is typed again. |

Deploy and Upgrade only run on a commit the CI workflow (`ci.yml`, on every push) has passed, waiting for it if it is
still running (`ci-passed.yml`). Deploy, Upgrade and Destroy run `template/cloudformation/deploy.ps1` or `remove.ps1` with `-Force`,
because a workflow can't answer prompts. That has two consequences:

- **Replaced resources.** Resources a change replaces are replaced without asking. The resources the stack policy
  protects are never replaced, and a change that would replace one fails the run: the user pool, identity pool, data
  and audit log buckets, the CloudFront distribution and the app client.
- **No AWS keys in GitHub.** The workflows sign in to AWS through OIDC, assuming an IAM role for the run.

The template app deploys with user events (`$events = 'enabled'` in `template/cloudformation/deploy.ps1`), so the
stack includes the AppSync Events API.

Deploy and Upgrade publish the portal as apps should ship it: compiled ahead of time to WebAssembly, with SIMD
(`deploy.ps1 -Aot`, after installing the `wasm-tools` workload). This makes publishing take several minutes longer.

## Setting up AWS (once per account)

The workflows need an IAM role that GitHub can assume. None of this can be done from the repository.

1. **The OIDC identity provider.** If the account already has one for GitHub (another repository's workflows use it),
   reuse it: there is one per account. Otherwise, in IAM go to Identity providers and add one:
   - type **OpenID Connect**;
   - provider URL `https://token.actions.githubusercontent.com`;
   - audience `sts.amazonaws.com`.
2. **The deploy policy.** Create a customer managed policy, e.g. `myapp-deploy`, from
   `template/cloudformation/deploy-policy.json`, with `ACCOUNT_ID` replaced (`template/cloudformation/README.md` has
   the commands). It allows only resources named `myapp-*`, so it cannot touch another app's stacks.
3. **The role,** e.g. `blazwiz-github-deploy`. Attach only `myapp-deploy`, and give it this trust policy (with
   `ACCOUNT_ID` replaced). It lets only this repository's workflows assume the role, and only for its GitHub
   environments:

   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Principal": { "Federated": "arn:aws:iam::ACCOUNT_ID:oidc-provider/token.actions.githubusercontent.com" },
         "Action": "sts:AssumeRoleWithWebIdentity",
         "Condition": {
           "StringEquals": { "token.actions.githubusercontent.com:aud": "sts.amazonaws.com" },
           "StringLike": { "token.actions.githubusercontent.com:sub": "repo:devoctomy@14939242/blazwiz@1394831664:environment:*" }
         }
       }
     ]
   }
   ```

   - **Use a new role, not clypse's.** A role per repository keeps each repository's workflows to its own policy.
     Clypse's role trusts only clypse's repository.
   - **The subject contains numeric ids.** GitHub's token names the repository with its owner's and its own numeric
     ids (`devoctomy@14939242/blazwiz@1394831664`), so a repository later renamed or recreated with the same name
     doesn't match.
   - **The IAM wizard writes it wrong.** It writes `repo:devoctomy/blazwiz:*`, without the ids, which never matches.
     AWS then refuses with "Not authorized to perform sts:AssumeRoleWithWebIdentity".
   - **To see what GitHub sends,** read the workflows' "Show the OIDC token's claims" step. If the `sub` there differs
     from the one above, use the one the step shows.
   - **To allow only some environments,** list them instead of `*`, e.g.
     `repo:devoctomy@14939242/blazwiz@1394831664:environment:test`.

## Setting up GitHub (once per environment)

In the repository's **Settings, Environments**, add one GitHub environment for each AWS environment, named the same
(e.g. `test`; lowercase letters and numbers, starting with a letter, up to 10 characters). Give it:

| Name | Kind | Value |
| --- | --- | --- |
| `AWS_DEPLOY_ROLE_ARN` | Variable | The role's ARN, e.g. `arn:aws:iam::ACCOUNT_ID:role/blazwiz-github-deploy`. |
| `AWS_REGION` | Variable | The environment's region, e.g. `eu-west-2`. |
| `TEST_USER_EMAIL` | Variable | For the deploy tests: the test user's email. |
| `TEST_USER_PASSWORD` | Secret | For the deploy tests: the test user's password, at least 12 characters, with upper case, lower case and a number. |
| `CUSTOM_DOMAIN_NAME` | Variable | Optional: the portal's own address. Set it with `CERTIFICATE_ARN`. |
| `CERTIFICATE_ARN` | Variable | Optional: an ACM certificate in **us-east-1** covering that address. |

With a custom address, Deploy and Upgrade put it on the CloudFront distribution and allow it in the data bucket's
CORS. They pass it on every run, because the stack drops the address when it is not given. They don't create the DNS
record: point the address at the distribution yourself, with a CNAME to its `dxxxxxxxx.cloudfront.net` name or a
Route 53 alias.

Under **Deployment protection rules** on each environment:

- **Required reviewers:** every Deploy, Upgrade, Deploy tests or Destroy run waits for one of them to approve it
  before it starts. Deploy tests run by Deploy or Upgrade are a job of their own, so they are approved separately.
- **Deployment branches and tags:** choose **Selected branches and tags** and add `main`. A run from any other branch
  then gets neither the environment's variables nor the AWS role. Leave this open if you deploy feature branches to a
  test environment.

These rules hold the job back before it starts: the OIDC token is only issued, and AWS only reached, once they pass.
Deploy and Upgrade check CI first, so an approval is only asked for once CI has passed.

## Checking a deployed environment: the deploy tests

The unit, integration and UI tests run before anything is deployed. The deploy tests
(`tests/Devoctomy.Blazwiz.DeployTests`, Reqnroll) run after, against the deployed environment: what only AWS can show.

| Feature | What it checks |
| --- | --- |
| Portal | The home page and a deep link are served (index.html); the portal serves its settings. |
| Signing in | The test user signs in with SRP, as the portal does, and gets AWS credentials from the identity pool. |
| The user's data | Read, write, list and delete in their own prefix; refused another user's prefix and listing the whole bucket. |
| The user's keys | A keys document is created only once and replaced only at the version read; a delete only adds a delete marker; no version can be deleted; another user's keys are refused. |
| User events | Publishing to the user's own channel is accepted, and to another user's refused. Skipped when the stack has no events API. |
| Signing out everywhere | Revokes the access token. Only when asked: it signs the test user out on every device. |

Run them with the **Deploy tests** workflow, or tick "Run the deploy tests afterwards" on Deploy or Upgrade. They need
the environment deployed, and the test user: set `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` on the GitHub
environment, then tick "Create or update the deploy tests' user" on a Deploy or Upgrade run once (again whenever the
password changes).

The workflow uses the deploy role only to read the portal's address from the stack. The tests run with no AWS
credentials: they read everything else from the portal's public `appsettings.Production.json` and act only as the
test user, so they need nothing more on AWS. They leave behind only old versions of their test keys document (the
user cannot delete versions, by design), which the bucket's lifecycle rule removes after 30 days.

To run them from your own machine, set `BLAZWIZ_DEPLOYTEST_PORTAL_URL`, `BLAZWIZ_DEPLOYTEST_EMAIL` and
`BLAZWIZ_DEPLOYTEST_PASSWORD` (and `BLAZWIZ_DEPLOYTEST_SIGN_OUT_EVERYWHERE=true` to include signing out everywhere),
then `dotnet test tests/Devoctomy.Blazwiz.DeployTests`. Without them every scenario is skipped.
