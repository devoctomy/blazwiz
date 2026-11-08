# Backlog: create the custom domain's DNS records in the base stack

**Raised:** 2026-11-07, by the maintainer, deploying clypse's test environment at `test.clypse.net`.
**Status:** not started.

## The problem

With `-CustomDomainName` and `-CertificateArn`, the base stack (`src/Devoctomy.Blazwiz.Aws/cloudformation/stack.yaml`):

- puts the address on the CloudFront distribution (`Aliases`, `ViewerCertificate`);
- allows it in the data bucket's CORS.

It creates no DNS record, so the address doesn't reach the app until someone adds one by hand. When the domain's
hosted zone is in Route 53 in the same account, as `clypse.net` is, the stack could do it.

## The change

- **The stack:**
  - **A new optional parameter, `HostedZoneId`:** the Route 53 hosted zone for the custom domain. It's used only with
    `CustomDomainName` and `CertificateArn`.
  - **The records:** when it is set, an `AWS::Route53::RecordSetGroup` with `A` and `AAAA` alias records for the
    address, pointing at the distribution's domain name. CloudFront's alias hosted zone id is `Z2FDTNDATAQYW2`.
  - **Lifecycle:** they are created and updated with the stack, and deleted when it is removed.
- **`deploy.ps1`:** a `-HostedZoneId` parameter, passed on to the stack, and refused without `-CustomDomainName`.
- **The minimum IAM policy** in the README (and apps' `cloudformation/README.md`) gains, for that hosted zone:
  - `route53:ChangeResourceRecordSets` and `route53:GetHostedZone` on `arn:aws:route53:::hostedzone/<id>`;
  - `route53:GetChange` on `arn:aws:route53:::change/*`.
- **Apps' deploy workflows** (e.g. clypse's Deploy and Upgrade): an optional `HOSTED_ZONE_ID` variable on the GitHub
  environment, passed on every run like `CUSTOM_DOMAIN_NAME`.
- **Upgrade notes:** CloudFormation won't create a record that already exists. An environment whose records were added
  by hand must have them deleted before the first deploy with `HostedZoneId`. Say so in the changelog.
- **Tests:**
  - `test-auth.ps1` checks that the address resolves and serves the portal;
  - an upgrade test (`test-template.ps1 -UpgradeFrom latest`) for the new parameter.

## Done when

- **A deploy with a custom domain and `HostedZoneId`** serves the app at the address, with no manual DNS step.
- **Removing the stack** removes the records.
- **A deploy without `HostedZoneId`** behaves as now.
