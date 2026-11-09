---
title: Produce the Content-Security-Policy in the build, not only in deploy.ps1
type: backlog
status: needs a decision
added: 2026-11-08
review: ../security-review.md
finding: SR31
severity: low
urgency: medium
before-1.0: recommended
reason: an app published any way other than deploy.ps1 ships without the one control that limits what an XSS can do with the tokens and keys
areas: [template/MyApp.Portal, Devoctomy.Blazwiz.Aws cloudformation scripts]
effort: medium
functional-change: no (a build change)
blocked-by: choosing one of the three options; tightening style-src needs a browser check against a deployed app
---

# Produce the Content-Security-Policy in the build, not only in deploy.ps1

From SR31 of `../security-review.md`: a design decision, so not done with the review's fixes.

## The gap

`Add-ContentSecurityPolicy` in `src/Devoctomy.Blazwiz.Aws/cloudformation/_common.ps1` injects a meta CSP into the
published `index.html` after `dotnet publish`, with the inline import map allowed by hash and `connect-src` limited to
this region's Cognito, Secrets Manager and data bucket endpoints. The template's `index.html` has no policy of its own,
so an app published any other way (another CDN, a container, `dotnet run` behind a proxy) ships without the one control
that limits what an XSS can do with the tokens in localStorage and the keys in memory. `style-src 'unsafe-inline'` is
also wider than it may need to be.

## Options

1. **An MSBuild target in the template's portal project** that runs after publish, reads the region and bucket from
   `appsettings.Production.json` (which `deploy.ps1` writes before publishing) and injects the same policy. The deploy
   script then only checks it is there. Apps published another way get the policy as long as they write that settings
   file. The hash of the import map is computed from the published `index.html`, as the script does now.
2. **A static policy in the template's `index.html`** with the AWS endpoints as placeholders that the template or the
   build fills. Simpler, but the inline import map's hash changes every build, so the build must still rewrite the tag.
3. **A header, not a meta tag,** from a CloudFront response headers policy, which also allows `frame-ancestors`. Only
   for the CloudFront path; the meta tag would stay for the others.

Whichever is chosen: check whether Blazor still needs `style-src 'unsafe-inline'` (components set `style` attributes),
and test in a browser against a deployed app, with the sync, notes and passkey flows, for violations in the console.
