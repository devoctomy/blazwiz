# blazwiz AWS deployment

| Script | What it does |
| --- | --- |
| `deploy.ps1` | Creates or updates the CloudFormation stack `<AppName>-<Environment>`, publishes the portal to S3, invalidates CloudFront and sets up a test user in Cognito. |
| `test-auth.ps1` | Checks a deployed environment end to end: the portal, Cognito sign-in, identity pool credentials and per-user S3 access. |
| `remove.ps1` | Empties both buckets and deletes the stack. |
| `blazwiz-aws.yaml` | The CloudFormation template. |

All three scripts use the AWS CLI v2 with your normal credential chain. Pass `-AwsProfile` to use a named profile.

## Minimum IAM policy for running the scripts

`deploy.ps1` runs `aws cloudformation deploy` without a CloudFormation service role, so CloudFormation creates the resources using **your** credentials. The deploying identity needs permission for every resource type in the template, as well as for the S3 upload, the CloudFront invalidation and the Cognito user admin calls.

The policy below covers `deploy.ps1`, `test-auth.ps1` and `remove.ps1`, and nothing more. Before you use it:

- Replace `ACCOUNT_ID` with your 12-digit AWS account id.
- If you deploy with an `-AppName` other than `blazwiz`, change the `blazwiz-*` prefixes to match. Stack, bucket and IAM role names all start with `<AppName>-`.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "CloudFormationStack",
      "Effect": "Allow",
      "Action": [
        "cloudformation:CreateChangeSet",
        "cloudformation:DescribeChangeSet",
        "cloudformation:ExecuteChangeSet",
        "cloudformation:DeleteChangeSet",
        "cloudformation:SetStackPolicy",
        "cloudformation:DescribeStacks",
        "cloudformation:DescribeStackEvents",
        "cloudformation:DescribeStackResources",
        "cloudformation:GetTemplate",
        "cloudformation:DeleteStack"
      ],
      "Resource": [
        "arn:aws:cloudformation:*:ACCOUNT_ID:stack/blazwiz-*/*",
        "arn:aws:cloudformation:*:ACCOUNT_ID:changeSet/*/*"
      ]
    },
    {
      "Sid": "S3Buckets",
      "Effect": "Allow",
      "Action": [
        "s3:CreateBucket",
        "s3:DeleteBucket",
        "s3:PutBucketOwnershipControls",
        "s3:PutBucketPublicAccessBlock",
        "s3:PutEncryptionConfiguration",
        "s3:PutBucketCORS",
        "s3:PutBucketTagging",
        "s3:PutBucketPolicy",
        "s3:DeleteBucketPolicy",
        "s3:Get*",
        "s3:List*"
      ],
      "Resource": "arn:aws:s3:::blazwiz-*"
    },
    {
      "Sid": "S3Objects",
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
        "s3:PutObject",
        "s3:DeleteObject"
      ],
      "Resource": "arn:aws:s3:::blazwiz-*/*"
    },
    {
      "Sid": "CloudFront",
      "Effect": "Allow",
      "Action": [
        "cloudfront:CreateDistribution",
        "cloudfront:UpdateDistribution",
        "cloudfront:DeleteDistribution",
        "cloudfront:GetDistribution",
        "cloudfront:GetDistributionConfig",
        "cloudfront:CreateOriginAccessControl",
        "cloudfront:UpdateOriginAccessControl",
        "cloudfront:DeleteOriginAccessControl",
        "cloudfront:GetOriginAccessControl",
        "cloudfront:GetOriginAccessControlConfig",
        "cloudfront:TagResource",
        "cloudfront:UntagResource",
        "cloudfront:ListTagsForResource",
        "cloudfront:CreateInvalidation",
        "cloudfront:GetInvalidation"
      ],
      "Resource": "arn:aws:cloudfront::ACCOUNT_ID:*"
    },
    {
      "Sid": "CognitoUserPoolCreate",
      "Effect": "Allow",
      "Action": "cognito-idp:CreateUserPool",
      "Resource": "*"
    },
    {
      "Sid": "CognitoUserPool",
      "Effect": "Allow",
      "Action": [
        "cognito-idp:DescribeUserPool",
        "cognito-idp:UpdateUserPool",
        "cognito-idp:DeleteUserPool",
        "cognito-idp:GetUserPoolMfaConfig",
        "cognito-idp:SetUserPoolMfaConfig",
        "cognito-idp:CreateUserPoolClient",
        "cognito-idp:DescribeUserPoolClient",
        "cognito-idp:UpdateUserPoolClient",
        "cognito-idp:DeleteUserPoolClient",
        "cognito-idp:TagResource",
        "cognito-idp:UntagResource",
        "cognito-idp:ListTagsForResource",
        "cognito-idp:AdminGetUser",
        "cognito-idp:AdminCreateUser",
        "cognito-idp:AdminSetUserPassword"
      ],
      "Resource": "arn:aws:cognito-idp:*:ACCOUNT_ID:userpool/*"
    },
    {
      "Sid": "CognitoIdentityPool",
      "Effect": "Allow",
      "Action": [
        "cognito-identity:CreateIdentityPool",
        "cognito-identity:DescribeIdentityPool",
        "cognito-identity:UpdateIdentityPool",
        "cognito-identity:DeleteIdentityPool",
        "cognito-identity:SetIdentityPoolRoles",
        "cognito-identity:GetIdentityPoolRoles",
        "cognito-identity:TagResource",
        "cognito-identity:UntagResource",
        "cognito-identity:ListTagsForResource"
      ],
      "Resource": "*"
    },
    {
      "Sid": "IamAuthenticatedRole",
      "Effect": "Allow",
      "Action": [
        "iam:CreateRole",
        "iam:DeleteRole",
        "iam:GetRole",
        "iam:UpdateRole",
        "iam:UpdateRoleDescription",
        "iam:UpdateAssumeRolePolicy",
        "iam:PutRolePolicy",
        "iam:GetRolePolicy",
        "iam:DeleteRolePolicy",
        "iam:ListRolePolicies",
        "iam:ListAttachedRolePolicies",
        "iam:TagRole",
        "iam:UntagRole"
      ],
      "Resource": "arn:aws:iam::ACCOUNT_ID:role/blazwiz-*"
    },
    {
      "Sid": "IamPassRoleToIdentityPool",
      "Effect": "Allow",
      "Action": "iam:PassRole",
      "Resource": "arn:aws:iam::ACCOUNT_ID:role/blazwiz-*"
    },
    {
      "Sid": "CustomDomainCertificate",
      "Effect": "Allow",
      "Action": "acm:DescribeCertificate",
      "Resource": "arn:aws:acm:us-east-1:ACCOUNT_ID:certificate/*"
    }
  ]
}
```

Notes:

- **You don't need permissions for some calls.** `sts:GetCallerIdentity` is always allowed. The Cognito sign-in calls in `test-auth.ps1` (`initiate-auth`, `get-id`, `get-credentials-for-identity`) are public APIs. The S3 checks in that script run as the signed-in test user, not as you.
- **`IamPassRoleToIdentityPool` has no `iam:PassedToService` condition.** A condition of `cognito-identity.amazonaws.com` doesn't match when the role is attached to the identity pool, and the deploy fails at `IdentityPoolRoleAttachment`. It is limited by the `blazwiz-*` role name instead.
- **`CustomDomainCertificate`** is only needed when you pass `-CustomDomainName` / `-CertificateArn`. You can drop it otherwise.
- **IAM is the sensitive part.** The template creates an IAM role, so this identity can create and edit any role whose name starts with `blazwiz-`. In principle that lets it create a more powerful role for itself. For throwaway testing that is usually fine. For stronger isolation, use a dedicated sandbox account, or add a permissions boundary condition (`iam:PermissionsBoundary`) to `CreateRole` / `PutRolePolicy`.
- **If a deploy fails with `AccessDenied`**, the stack events name the exact missing action. Run `aws cloudformation describe-stack-events --stack-name blazwiz-dev`, add that action to the policy and deploy again. A failed first deploy leaves the stack in `ROLLBACK_COMPLETE`. Delete it with `remove.ps1` before you retry.

### Setting up the credentials

1. In the IAM console, create a customer managed policy (e.g. `blazwiz-deploy`) from the JSON above.
2. Create an IAM user (e.g. `blazwiz-deployer`) with no console access and attach only that policy.
3. Create an access key for the user and configure a named CLI profile:

   ```powershell
   aws configure --profile blazwiz-deploy
   ```

4. Use that profile with the scripts:

   ```powershell
   ./cloudformation/deploy.ps1   -AwsProfile blazwiz-deploy -Region eu-west-2 -TestUserEmail me@example.com
   ./cloudformation/test-auth.ps1 -AwsProfile blazwiz-deploy -Region eu-west-2 -Email me@example.com
   ./cloudformation/remove.ps1   -AwsProfile blazwiz-deploy -Region eu-west-2
   ```

When you've finished testing, delete the access key, or the whole user.
