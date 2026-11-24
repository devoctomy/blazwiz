---
title: Threat protection on the user pool
type: backlog
status: needs a decision
added: 2026-11-24
review: ../security-review.md
finding: SR6
severity: medium
urgency: low
before-1.0: optional
reason: Cognito does not check sign-ins against compromised credentials or score them for risk; MFA (done) covers most of what SR6 raised
areas: [AWS stack]
effort: small
functional-change: no (a stack setting and its documentation)
blocked-by: choosing the mode, and whether apps pay for it by default
---

# Threat protection on the user pool

What is left of SR6 in `../security-review.md`. The MFA half was done and tested on a deployed environment on
2026-11-24, and its plan removed.

## The gap

The user pool in `src/Devoctomy.Blazwiz.Aws/cloudformation/stack.yaml` has no `UserPoolAddOns`, so Cognito's threat
protection is off: no check for compromised credentials and no risk score on sign-ins. Cognito's lockout on repeated
failures still applies, and MFA now stops a phished or reused password on its own for users who turn it on.

## Options

1. **`AUDIT`:** Cognito records risk for each sign-in without acting on it. A cheap first step that shows whether it
   would help.
2. **`ENFORCED`:** Cognito blocks or challenges risky sign-ins. It has a per-user monthly cost and needs the user pool's
   feature plan to allow it.
3. **A stack parameter**, off by default, so each app chooses, with the cost stated in the Aws package README and the
   `docs/blazwiz` guide.

Any of them changes the stack, so it also needs the `-UpgradeFrom latest` test and a deploy.
