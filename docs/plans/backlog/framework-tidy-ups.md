---
title: "Tidy-ups found while writing the developer guide"
type: backlog
status: needs-decision
added: 2026-11-22
reason: found while writing docs/blazwiz; items 1 and 2 need the maintainer's decision first
areas: [docs, Auth, UI, UI.Sync, template]
effort: small
functional-change: yes (item 2, depending on the decision)
blocked-by: none
---

# Tidy-ups found while writing the developer guide (not implemented yet)

Four things the developer guide (`docs/blazwiz`) turned up that were left as they were. Each is small.

## 1. Another project named in older docs

**What.** The rule is that blazwiz never names the other project built on it. Three dated documents still do:

- `docs/plans/backlog/custom-domain-dns-records.md`: the `origin` line and two examples (its test environment, its
  domain, its deploy workflows).
- `docs/reviews/2026-11-08-security-review/security-review.md`: two lines (the template check and SR34).
- `docs/reviews/2026-11-13-codacy/codacy-review.md`: the "Compared with ..." section and several lines that compare
  Codacy settings with it, including a path outside this repository.

**Decision needed.** Reword them to "an app built on blazwiz" (and drop the outside path), or leave the reviews as
dated records and reword only the backlog plan.

## 2. "Sign out everywhere" without user events

**What.** The template says an app created with `--events false` does not offer "Sign out everywhere":
`template/README.md` (lines 237 and 247), the Events option's description in `template/.template.config/template.json`
and its `manualInstructions` message. The code decides differently: `ProfileViewModel.CanSignOutEverywhere` and
`DeviceSettingsViewModel.IsVisible` check only `ISignOutEverywhereNotifier.HasHandlers`, and the sync module always
registers `KeyRotationSignOutHandler` (`SyncModuleServiceCollectionExtensions`). So an app with sync on and events off
most likely still offers it. Found by reading the code; not yet confirmed by running such an app.

**Decision needed.** Which is right?

- **The code.** Signing out everywhere still ends the user's Cognito sessions (`GlobalSignOut`) without events; the
  other devices find out when they next refresh their session. Then the template's text changes to say what events add
  (other devices told at once), not that the button is missing.
- **The docs.** The button is hidden without events. Then the check needs to know whether events are configured, not
  only whether a handler exists, with unit tests for both view models.

**Then.** Confirm the behaviour in an app created with `--events false`, make the change, and update `docs/blazwiz`
(`sign-in-and-sessions.md`) to match.

## 3. Components that break the view model rule

AGENTS.md: views inherit `MvvmComponentBase<TViewModel>`, code-behind holds parameters only, and logic is in a unit
tested view model.

- **`ForgotPasswordLink`** (`src/Devoctomy.Blazwiz.UI/Components`) inherits `ComponentBase`; its code-behind injects
  `IPageRegistry` and has a computed `IsEnabled`. Move both into a `ForgotPasswordLinkViewModel` with unit tests (page
  turned on and off).
- **`StartSyncOffOption`** (`src/Devoctomy.Blazwiz.UI.Sync/Components`) inherits `ComponentBase` and its markup binds
  straight to the injected `StartSyncOffSignInOption`. Give it a view model that exposes `StartWithSyncOff`, with unit
  tests.

Public API changes for both (PublicAPI.Unshipped.txt) and a `CHANGELOG.md` entry. Markup lines change; report how many
are uncovered.

## 4. `E2eChangePassphraseDialog` takes its profile only once

`E2eChangePassphraseDialog.razor.cs` hands `Profile` to the view model in `OnInitializedAsync`, so a change to `Profile`
while the dialog is open is ignored. `E2eUnlockPanel` does it in `OnParametersSetAsync`. Today every caller renders the
dialog only while it is open for one profile, so nothing is broken. Either move the handoff to `OnParametersSetAsync`
(only when the profile changes, so the dialog is not reset on every render), or document on the component that
`Profile` is read once. Small; can wait until the dialog is next changed.

## Tasks

- [ ] 1: maintainer decides; reword the chosen documents.
- [ ] 2: maintainer decides; confirm in a generated app; change the code or the template's text; update `docs/blazwiz`.
- [ ] 3: view models for `ForgotPasswordLink` and `StartSyncOffOption`, with unit tests; public API, changelog.
- [ ] 4: decide handoff or documentation; change it.
- [ ] Build and every test suite as in AGENTS.md; coverage of changed lines measured and reported.
