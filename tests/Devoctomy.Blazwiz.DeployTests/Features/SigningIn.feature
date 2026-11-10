Feature: Signing in

The test user signs in as the portal signs users in: SRP against the user pool, then AWS credentials from the
identity pool for the ID token.

Scenario: The test user signs in and gets AWS credentials
	When the test user signs in
	Then they have an ID token and an access token
	When they get AWS credentials from the identity pool
	Then they have an identity id and temporary AWS credentials

@signouteverywhere
Scenario: Signing out everywhere revokes the session
	Given the test user is signed in
	When they sign out everywhere
	Then their access token is refused
