# Self sign-up (the app developer's choice)

Added 2026-11-03, from Phase 3 of `docs/reviews/2026-11-03-upgradeability-review.md` (optional there, not done).

The user pool is invite-only today. Whether people can create their own account is the app developer's choice,
never something users switch.

- **User pool** (`stack.yaml`): a parameter for `AllowAdminCreateUserOnly`, plus email verification
  (`AutoVerifiedAttributes: email`). Infrastructure is left to the end of the plan (D4): ship with the AWS base stack
  (U15).
- **App**: a setting (e.g. `AwsCognito:AllowSelfSignUp`) that shows a "Create an account" link on the sign-in page
  and a framework page (`BlazwizPages.SignUp`, replaceable or turned off like the others).
- **Library**: `CognitoUserPool.SignUpAsync`, `CognitoUser.ConfirmSignUpAsync` and resending the confirmation code,
  from `Amazon.Extensions.CognitoAuthentication` / the Cognito API. `Auth.UserNotConfirmed` at sign in should lead to
  confirming the account rather than just an error.
- **Abuse**: open sign-up invites bots; consider Cognito's advanced security features or a pre sign-up Lambda before
  turning it on for a real app, and say so in the docs.
- **Placeholder**: a no-op implementation for local development, like the password flows.
- **Text and tests**: as for MFA.
