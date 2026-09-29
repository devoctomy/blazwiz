Feature: Greetings

Users are greeted appropriately for the time of day.

@greetings
Scenario Outline: User is greeted based on the time of day
	Given the current local time is <Time>
	And the user is called <Name>
	When the user is greeted
	Then the greeting should be "<Greeting>"

Examples:
	| Time  | Name  | Greeting             |
	| 03:00 | Alice | Good evening, Alice  |
	| 09:15 | Bob   | Good morning, Bob    |
	| 13:45 | Carol | Good afternoon, Carol |
	| 20:30 | Dave  | Good evening, Dave   |

@greetings
Scenario: Anonymous user receives a generic greeting
	Given the current local time is 10:00
	When the user is greeted
	Then the greeting should be "Good morning"
