Feature: The user's keys

Each protected profile's keys document is kept in the user's prefix under keys/, and written only conditionally, so
two devices cannot both change it. Deleting one only adds a delete marker: the user cannot delete a version, so a
deleted document stays restorable by an operator. A made-up profile id is used, so the user's real keys are never
touched; its old versions are left to the bucket's lifecycle rule.

Background:
	Given the test user is signed in with AWS credentials

Scenario: A keys document is created only once
	When they create a keys document
	And they create the same keys document again
	Then the second creation is refused

Scenario: A keys document is replaced only at the version read
	Given they have created a keys document
	When they replace it at the version they read
	And they replace it again at that same, older version
	Then the second replacement is refused as changed

Scenario: Deleting a keys document only adds a delete marker, and no version can be deleted
	Given they have created a keys document
	When they delete it
	Then the bucket kept it behind a delete marker
	And they cannot delete the version they created

Scenario: Another user's keys are refused
	Then they cannot create another user's keys document
	And they cannot read another user's keys document
