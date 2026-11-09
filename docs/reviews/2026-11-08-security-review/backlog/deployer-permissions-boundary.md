---
title: Cap the roles the deployer can create with a permissions boundary
type: backlog
status: not applicable for now
added: 2026-11-09
review: ../security-review.md
finding: SR17
severity: low
urgency: low
before-1.0: no
reason: only matters once the deployer is a limited identity (an IAM user or a CI role); the maintainer deploys by hand as an account administrator
trigger: the deployer becomes a limited identity, or deployment moves to CI
areas: [AWS stack, deploy scripts, template/cloudformation]
effort: small
functional-change: no
blocked-by: an account administrator creating the boundary policy outside the stack
---

# Cap the roles the deployer can create with a permissions boundary

From SR17 of `../security-review.md`, done in part: the Cognito admin actions were scoped by the pool's `app` tag; the
permissions boundary was not added because it needs a policy that only an account administrator may create.

## The gap

The deployer is the IAM identity that runs `template/cloudformation/deploy.ps1`, with the policy in
`template/cloudformation/deploy-policy.json`. Its IAM statement allows `iam:CreateRole`, `iam:PutRolePolicy` and
`iam:UpdateAssumeRolePolicy` on any role named `myapp-*`. That scopes which roles it may touch, but not what those
roles may do: a leaked deployer credential can create `myapp-escalate` with administrator permissions, trusting the
deployer, and assume it. `template/cloudformation/README.md` discloses this under "IAM is the sensitive part".

This only matters when the deployer is a limited identity (an IAM user or a CI role). While the maintainer deploys by
hand as an account administrator, the boundary adds nothing, which is why this is in the backlog rather than active.

## Design

1. **Boundary policy, once per account, by an administrator.** A customer managed policy, `myapp-role-boundary`,
   allowing only what the app's roles legitimately need: the S3 actions on the data bucket, the Secrets Manager
   actions on the users' secrets, and `cognito-identity:GetCredentialsForIdentity`. Whatever a role is granted beyond
   this is capped. The deployer must not be able to create or edit this policy, so it is created outside the stack,
   as a documented set-up step, like the deployer policy itself.
2. **The stack attaches it.** `AuthenticatedRole` in `src/Devoctomy.Blazwiz.Aws/cloudformation/stack.yaml` gets a
   `PermissionsBoundary` property with the boundary's ARN, built from the account id and `AppName` so no new parameter
   is needed. Any role the stack adds later needs the same property.
3. **The deployer policy requires it.** Split the IAM statement in `deploy-policy.json`: `iam:CreateRole` and
   `iam:PutRolePolicy` get the condition
   `"StringEquals": { "iam:PermissionsBoundary": "arn:aws:iam::ACCOUNT_ID:policy/myapp-role-boundary" }`, so the
   deployer can only create roles that carry the cap. Add a `Deny` on `iam:DeleteRolePermissionsBoundary` for
   `role/myapp-*`, otherwise the deployer could strip the cap after creation. The read and tag actions stay as they
   are.
4. **Keep the deployer away from the boundary itself.** Confirm the deployer policy grants no `iam:CreatePolicy`,
   `iam:CreatePolicyVersion`, `iam:SetDefaultPolicyVersion` or `iam:DeletePolicy` on `policy/myapp-*`. It does not
   today; this is a check, not a change.
5. **Fail early.** `deploy.ps1` checks the boundary policy exists in the account before creating the stack, and
   points at the README step if it does not, rather than letting CloudFormation fail part way through role creation.
6. **Documentation.** The README's "IAM is the sensitive part" paragraph becomes the set-up step for the boundary;
   `docs/diagrams` where the AWS stack is drawn; the review's SR17 row and the changelog record it as complete.
7. **Test on the live stack.** Deploy again with the updated deployer policy and run `test-auth.ps1`. As a negative
   check, run `aws iam create-role` as the deployer without the boundary and confirm it is denied. Upgrade test as
   AGENTS.md requires for a change to the AWS base stack: `./templatepack/test-template.ps1 -UpgradeFrom latest`.

## Trade-off

The boundary duplicates the list of actions the authenticated role already has. A future change to the role's
permissions must be made in both places, or the role silently loses the new permission. Put a comment in both files
pointing at the other.
