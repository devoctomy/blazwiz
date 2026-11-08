# Edge cases: two devices writing the same secret at once

**Related:** `docs/plans/2026-11-05-profile-passphrases-plan.md` (removed) (R10, D3)

Secrets Manager has no conditional write: every `PutSecretValue` creates a new version and makes it current, whatever
the current version is (see the
[PutSecretValue reference](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_PutSecretValue.html)).
When two devices write the same secret within the moment between reading it and writing it back, the later write
wins. The cases below are considered unlikely, and nothing is done about them for now.

## Changing a protected profile's passphrase on two devices at once

Both devices write the profile's keys secret. The second replaces the first, so only the passphrase set by the second
device works afterwards.

## Creating or deleting protected profiles on two devices at once

Both devices read the index secret, add or remove their entry, and write it back. The second write replaces the first,
so the first device's change is missing from the index. For a new profile, its keys secret exists but other devices
do not find it.

## Changing a protected profile's idle timeout while another device writes the index

A profile's own idle timeout (`ISyncProtectedProfileService.SetIdleTimeoutAsync`) is kept in the index secret too
(`E2eProfileIndexEntry.IdleTimeoutSeconds`), so changing it is another read and write of the index. It races with
creating or deleting a profile, or changing another timeout, on another device, as above. Either the timeout change
is lost, or the other device's change is: a new profile's entry, which leaves that profile's keys secret unreachable
from every device, its files with it.

A possible fix, if this is taken on: after writing the index, read it back, and if this device's change is not there,
apply it again and write again, a few times at most. That narrows the window a lot, but does not close it.
