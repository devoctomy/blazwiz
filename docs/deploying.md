# Deploying the template app from GitHub

Three workflows deploy the repository's template app (`template/`, as `myapp`) to AWS from GitHub. This tests the
framework as it stands, before it is released:

- The base stack and its scripts come from `src/Devoctomy.Blazwiz.Aws`.
- The portal builds against the packages' source (`template/Directory.Build.targets`), not packages from nuget.org.

Each workflow is run by hand: Actions, then the workflow, then **Run workflow**.

| Workflow | What it does |
| --- | --- |
| **Deploy** (`deploy.yml`) | Creates a new environment: the base stack (`myapp-<environment>`), the app's stack (`myapp-<environment>-app`), the portal, and optionally a test user. Refused if the environment already exists. |
| **Upgrade** (`upgrade.yml`) | Updates an existing environment to the commit it is run on: both stacks, then the portal (or only the portal). Refused if the environment does not exist. |
| **Destroy** (`destroy.yml`) | Deletes an environment, once its name is typed again. |

Deploy and Upgrade only run on a commit the CI workflow (`ci.yml`, on every push) has passed, waiting for it if it is
still running (`ci-passed.yml`). All three run `template/cloudformation/deploy.ps1` or `remove.ps1` with `-Force`,
because a workflow can't answer prompts. That has two consequences:

- **Replaced resources.** Resources a change replaces are replaced without asking. The resources the stack policy
  protects are never replaced, and a change that would replace one fails the run: the user pool, identity pool, data
  and audit log buckets, the CloudFront distribution and the app client.
- **No AWS keys in GitHub.** The workflows sign in to AWS through OIDC, assuming an IAM role for the run.

The template app deploys with user events (`$events = 'enabled'` in `template/cloudformation/deploy.ps1`), so the
stack includes the AppSync Events API.

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
| `TEST_USER_PASSWORD` | Secret | Only for a Deploy that creates a test user: at least 12 characters, with upper case, lower case and a number. |
| `CUSTOM_DOMAIN_NAME` | Variable | Optional: the portal's own address. Set it with `CERTIFICATE_ARN`. |
| `CERTIFICATE_ARN` | Variable | Optional: an ACM certificate in **us-east-1** covering that address. |

With a custom address, Deploy and Upgrade put it on the CloudFront distribution and allow it in the data bucket's
CORS. They pass it on every run, because the stack drops the address when it is not given. They don't create the DNS
record: point the address at the distribution yourself, with a CNAME to its `dxxxxxxxx.cloudfront.net` name or a
Route 53 alias.

Under **Deployment protection rules** on each environment:

- **Required reviewers:** every Deploy, Upgrade or Destroy run waits for one of them to approve it before it starts.
- **Deployment branches and tags:** choose **Selected branches and tags** and add `main`. A run from any other branch
  then gets neither the environment's variables nor the AWS role. Leave this open if you deploy feature branches to a
  test environment.

These rules hold the job back before it starts: the OIDC token is only issued, and AWS only reached, once they pass.
Deploy and Upgrade check CI first, so an approval is only asked for once CI has passed.

## Checking a deployed environment

The workflows don't run `test-auth.ps1`. To check an environment end to end, run it locally with credentials for the
account:

```powershell
./template/cloudformation/test-auth.ps1 -Region eu-west-2 -Environment test -Email <test user>
```

Its last check signs the test user out on every device; `-SkipSignOutEverywhere` leaves it out.
