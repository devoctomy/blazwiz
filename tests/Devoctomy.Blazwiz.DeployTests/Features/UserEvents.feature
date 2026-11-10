@events
Feature: User events

The user events API lets a user publish only to their own channel, /users/<sub>. Skipped when the stack has no
events API.

Background:
	Given the test user is signed in

Scenario: The user can publish to their own channel
	When they publish a test event to their own channel
	Then the event is accepted

Scenario: The user is refused another user's channel
	When they publish a test event to another user's channel
	Then the event is refused
