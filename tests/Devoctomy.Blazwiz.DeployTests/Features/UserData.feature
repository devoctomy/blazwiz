Feature: The user's data

With the credentials the identity pool gives them, a user reaches their own prefix of the data bucket and nothing
else.

Background:
	Given the test user is signed in with AWS credentials

Scenario: The user can write, read, list and delete in their own prefix
	When they write a test file in their own prefix
	Then they can read it back
	And they can list it
	And they can delete it

Scenario: The user can list and read their own files' past versions, and cannot delete them
	When they write a test file in their own prefix
	And they write it again with new content
	Then they can list both versions of it
	And they can read an earlier version of it
	And they cannot delete a version of it

Scenario: The user is refused another user's prefix and the whole bucket
	Then they cannot write in another user's prefix
	And they cannot read from another user's prefix
	And they cannot list another user's prefix
	And they cannot list the whole bucket
	And they cannot list the versions in another user's prefix
	And they cannot read a version from another user's prefix
	And they cannot list the versions in the whole bucket
