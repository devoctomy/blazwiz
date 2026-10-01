Feature: Sync

Two devices, signed in as the same user, keep their files in step through the real S3 bucket.
Each scenario uses its own prefix in the bucket, which is emptied afterwards.

Background:
	Given two devices signed in as the same user

@aws
Scenario: A new file reaches the other device
	Given device "A" has a file "notes/Ideas.md" containing "first thoughts"
	When device "A" syncs
	And device "B" syncs
	Then device "B" has a file "notes/Ideas.md" containing "first thoughts"

@aws
Scenario: An edit reaches the other device
	Given device "A" has a file "notes/Ideas.md" containing "v1"
	And both devices have synced
	When device "B" changes "notes/Ideas.md" to "v2"
	And device "B" syncs
	And device "A" syncs
	Then device "A" has a file "notes/Ideas.md" containing "v2"

@aws
Scenario: A delete reaches the other device
	Given device "A" has a file "notes/Old.md" containing "remove me"
	And both devices have synced
	When device "A" deletes "notes/Old.md"
	And device "A" syncs
	And device "B" syncs
	Then device "B" has no file "notes/Old.md"
	And the bucket has no object "notes/Old.md"

@aws
Scenario: A rename reaches the other device
	Given device "A" has a file "notes/Draft.md" containing "content"
	And both devices have synced
	When device "A" renames "notes/Draft.md" to "notes/Final.md"
	And device "A" syncs
	And device "B" syncs
	Then device "B" has a file "notes/Final.md" containing "content"
	And device "B" has no file "notes/Draft.md"

@aws
Scenario: Nested folders and empty folders reach the other device
	Given device "A" has a file "notes/Work/Meetings/2026-10-01.md" containing "agenda"
	And device "A" has an empty folder "notes/Empty Folder/"
	When device "A" syncs
	And device "B" syncs
	Then device "B" has a file "notes/Work/Meetings/2026-10-01.md" containing "agenda"
	And device "B" has an empty folder "notes/Empty Folder/"

@aws
Scenario: A binary file is synced byte for byte
	Given device "A" has a binary file "notes/image.png" of 4096 bytes
	When device "A" syncs
	And device "B" syncs
	Then device "B" has the same bytes for "notes/image.png" as device "A"

@aws
Scenario: Edited on both devices, the newest edit wins
	Given device "A" has a file "notes/Ideas.md" containing "v1"
	And both devices have synced
	When device "A" changes "notes/Ideas.md" to "from A" at 10:00
	And device "B" changes "notes/Ideas.md" to "from B" at 11:00
	And device "B" syncs
	And device "A" syncs
	And device "B" syncs
	Then device "A" has a file "notes/Ideas.md" containing "from B"
	And device "B" has a file "notes/Ideas.md" containing "from B"
	And the last sync of device "A" resolved 1 conflict

@aws
Scenario: Deleted on one device and edited later on the other, the edit wins
	Given device "A" has a file "notes/Ideas.md" containing "v1"
	And both devices have synced
	When device "A" deletes "notes/Ideas.md" at 10:00
	And device "B" changes "notes/Ideas.md" to "still needed" at 11:00
	And device "B" syncs
	And device "A" syncs
	Then device "A" has a file "notes/Ideas.md" containing "still needed"

@aws
Scenario: An interrupted pass recovers without uploading again
	Given device "A" has a file "notes/Ideas.md" containing "uploaded before the crash"
	And device "A" has synced
	When device "A" loses its sync index
	And device "A" syncs
	Then the last sync of device "A" uploaded 0 files and adopted 1
	And device "A" has a file "notes/Ideas.md" containing "uploaded before the crash"
