Feature: Reachability

The connectivity service probes S3 with a one key listing of the user's own prefix.

@aws
Scenario: S3 is reachable with valid credentials
	Given an S3 target with the configured credentials
	When connectivity is checked
	Then the app is online

Scenario: S3 is unreachable at a bad endpoint
	Given an S3 target at an endpoint nothing is listening on
	When connectivity is checked
	Then the app is offline because S3 is unreachable
