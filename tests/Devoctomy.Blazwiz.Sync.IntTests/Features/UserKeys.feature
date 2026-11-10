Feature: User keys

The keys document of each protected profile, and the index of them, are kept in the user's prefix of the real data
bucket under keys/, and every write to them is conditional, so two devices changing one at the same time cannot both
succeed. Each scenario uses its own prefix in the bucket, which is emptied afterwards.

Background:
	Given the user's keys kept in the data bucket

@aws
Scenario: A profile's keys are created only once
	When keys "first" are created for profile "P"
	And keys "second" are created for profile "P"
	Then the second creation is refused
	And the keys of profile "P" are "first"

@aws
Scenario: A change made from an older version of the keys is refused
	Given keys "v1" for profile "P", read by devices "A" and "B"
	When device "A" changes the keys of profile "P" to "from A"
	And device "B" changes the keys of profile "P" to "from B"
	Then device "B" is told the keys changed
	And the keys of profile "P" are "from A"

@aws
Scenario: Deleted keys are gone
	Given keys "v1" for profile "P", read by devices "A" and "B"
	When the keys of profile "P" are deleted
	Then profile "P" has no keys
	And device "A" changing the keys of profile "P" to "too late" is told the keys changed

@aws
Scenario: The index is created by one device only
	When device "A" creates the index
	And device "B" creates the index
	Then device "B" is told the keys changed

@aws
Scenario: Two devices adding protected profiles at the same time both end up in the index
	When devices "A" and "B" each create a protected profile at the same time
	Then the index lists both profiles
