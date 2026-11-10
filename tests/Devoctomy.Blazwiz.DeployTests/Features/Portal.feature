Feature: Portal

The deployed portal is served from CloudFront, falls back to index.html for the app's own routes, and serves the
settings deploy.ps1 wrote for this stack.

Scenario: The home page is served
	When the page "" is requested
	Then the portal's index.html is returned

Scenario: Deep links fall back to index.html
	When the page "some/deep/link" is requested
	Then the portal's index.html is returned

Scenario: The settings name this environment
	Then the portal's settings name a user pool, an app client, an identity pool and a data bucket
