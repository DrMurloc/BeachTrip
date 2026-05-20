# 7. Anonymous viewing + sign-in-by-handle, no real auth

Date: 2026-05-17

## Status

Accepted.

## Context

The system serves ~10 people who all know each other, for 4 days, sharing a beach house. The expected access model is "Murky texts everyone the URL and they pick their name from a dropdown." There's no adversary; the worst-case threat is "Bob impersonates Alice and joins Alice's room as a joke."

A real auth system would mean: identity provider, OAuth flow, session management, password reset, email verification, account recovery. That's hours of plumbing for a system that ships and dies in four days.

We considered:

1. **Full ASP.NET Core Identity** — overkill; postpones shipping; demands cookie config, anti-forgery wiring, etc.
2. **Magic-link auth via email/SMS** — closer to right but still needs an SMTP/Twilio dependency and adds latency to onboarding.
3. **Sign-in by handle** — pick a name from a list (or type a new one to register). No password, no email. Identity persists in browser-local session storage.
4. **No identity at all** — everyone is anonymous; actions are credited to the browser session. Loses "who joined which room" semantics.

Option 3 is what social-deduction games and dev playgrounds use. It scales to "everyone in the trip" perfectly because the trust boundary is "you have the URL" — same as a Google Doc share link.

## Decision

**Three identity states**:

1. **Anonymous viewer** — no identity in `ProtectedSessionStorage`. Can browse the lobby read-only. Nav bar shows a "Sign in" button (except on the sign-in page itself). No action buttons appear.
2. **Signed in as an attendee** — an `AttendeeId` and `DisplayName` stored in `ProtectedSessionStorage` for the browser circuit. Can join/leave rooms, declare a car, form/join carpools.
3. **Signed in as DrMurloc** — same as (2), but the literal `DisplayName == "DrMurloc"` unlocks admin UI: parking-spot kebab menus, the bulk-register quick-add field. Hardcoded string check, no role table, no claims.

**Sign-in flow**: the home page has an autocomplete field listing existing attendee handles. Typing a known handle and clicking Sign in stores that identity. Typing a *new* handle and clicking Sign in publishes `RegisterAttendee` then signs in as the new attendee. The password field is present, labeled, and explicitly noted as doing nothing — a small wink at how minimal the auth model is.

**Sign-out / switch identity** — the nav-bar user dropdown lists every registered attendee for one-click switching, plus a "Switch User" item that clears session storage and returns to `/`.

## Consequences

**Good**

- Zero-friction onboarding. The host shares the URL, people pick their name, done.
- The "switch identity" affordance turned into a productivity feature for the host during bulk-registration: he can quickly test what each attendee sees.
- Anonymous viewing means the URL can be shared with anyone (e.g. someone's spouse who wants to see the layout without joining anything) with no privacy concerns — there's nothing private here.
- Zero auth dependencies. No Identity Server, no Azure AD config, no OAuth callbacks, no cookie domains to worry about.

**Bad**

- It's not auth. Anyone on the URL can impersonate anyone else. Acceptable here because the audience is "people the host trusts with the URL." Would be catastrophic for any system with real adversaries.
- "DrMurloc" admin is a string check. Renaming the host requires a code change. Documented in [UBIQUITOUS_LANGUAGE.md § DrMurloc](../UBIQUITOUS_LANGUAGE.md#drmurloc).
- `ProtectedSessionStorage` ties identity to a browser. Switching laptops mid-trip = re-pick your name from the dropdown. Fine.
- If this product ever generalized beyond "trusted friend group," every one of these tradeoffs flips. The replacement would be a real IdP — at which point the sign-in-by-handle screen becomes the sign-up form for a real account, and the "DrMurloc" string check becomes a role.
