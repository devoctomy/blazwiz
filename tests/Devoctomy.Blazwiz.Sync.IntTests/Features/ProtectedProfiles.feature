Feature: Protected profiles

Two devices, signed in as the same user, with protected profiles: each has its own passphrase and keys, and its files
are stored and synced encrypted. The devices share the user's keys, the index of their profiles and their prefix in
the bucket, kept in memory; the S3 scenarios in Sync.feature cover the bucket itself.

Background:
	Given two devices with protected profiles, signed in as the same user

Scenario: Two profiles with their own passphrases, on two devices
	Given device "A" creates a protected profile "Work" with the passphrase "work passphrase"
	And device "A" creates a protected profile "Home" with the passphrase "home passphrase"
	And device "A" writes "plan.md" containing "work plan" in "Work"
	And device "A" writes "list.md" containing "groceries" in "Home"
	And device "A" syncs
	When device "B" loads its protected profiles
	And device "B" syncs
	And device "B" unlocks "Work" with the passphrase "work passphrase"
	Then device "B" reads "plan.md" in "Work" as "work plan"
	And device "B" cannot read "list.md" in "Home" while it is locked
	And device "B" cannot unlock "Home" with the passphrase "work passphrase"

Scenario: A locked profile still sends and receives changes
	Given device "A" creates a protected profile "Work" with the passphrase "work passphrase"
	And device "A" writes "plan.md" containing "v1" in "Work"
	And device "A" syncs
	And device "B" loads its protected profiles
	And device "B" syncs
	And device "B" unlocks "Work" with the passphrase "work passphrase"
	When device "B" writes "plan.md" containing "v2" in "Work"
	And device "B" locks "Work"
	And device "B" syncs
	And device "A" locks "Work"
	And device "A" syncs
	Then device "A" cannot read "plan.md" in "Work" while it is locked
	And device "A" stores "plan.md" in "Work" encrypted
	When device "A" unlocks "Work" with the passphrase "work passphrase"
	Then device "A" reads "plan.md" in "Work" as "v2"

Scenario: A file moved to another path or profile cannot be decrypted
	Given device "A" creates a protected profile "Work" with the passphrase "work passphrase"
	And device "A" creates a protected profile "Home" with the passphrase "home passphrase"
	And device "A" writes "plan.md" containing "work plan" in "Work"
	When the stored "plan.md" in "Work" is copied on device "A" to "copy.md" in "Work"
	And the stored "plan.md" in "Work" is copied on device "A" to "plan.md" in "Home"
	Then device "A" cannot decrypt "copy.md" in "Work"
	And device "A" cannot decrypt "plan.md" in "Home"

Scenario: Deleting a profile leaves nothing that can be decrypted
	Given device "A" creates a protected profile "Work" with the passphrase "work passphrase"
	And device "A" writes "plan.md" containing "work plan" in "Work"
	And device "A" syncs
	And device "B" loads its protected profiles
	And device "B" syncs
	And device "B" unlocks "Work" with the passphrase "work passphrase"
	When device "A" deletes "Work"
	And device "B" loads its protected profiles
	Then the bucket has no files of "Work"
	And the user's keys of "Work" are gone
	And device "A" has no keys or files of "Work"
	And device "B" has no keys or files of "Work"
	And device "B" cannot unlock "Work" with the passphrase "work passphrase" because it does not exist

Scenario: The passphrase is changed on one device while another has the profile unlocked
	Given device "A" creates a protected profile "Work" with the passphrase "old passphrase"
	And device "A" writes "plan.md" containing "work plan" in "Work"
	And device "A" syncs
	And device "B" loads its protected profiles
	And device "B" syncs
	And device "B" unlocks "Work" with the passphrase "old passphrase"
	When device "A" changes the passphrase of "Work" from "old passphrase" to "new passphrase"
	Then device "B" reads "plan.md" in "Work" as "work plan"
	When device "B" writes "notes.md" containing "still unlocked" in "Work"
	And device "B" locks "Work"
	Then device "B" cannot unlock "Work" with the passphrase "old passphrase"
	When device "B" unlocks "Work" with the passphrase "new passphrase"
	Then device "B" reads "notes.md" in "Work" as "still unlocked"
