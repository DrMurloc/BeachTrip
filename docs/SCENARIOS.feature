# Acceptance scenarios in Gherkin notation.
# Not currently executed — kept as living documentation of intent.
# Could be wired to Reqnroll/SpecFlow for end-to-end tests later.

Feature: Anonymous viewing
  Anyone with the URL can see the beach trip state without signing in.

  Scenario: Anonymous viewer sees the lobby read-only
    Given the beach trip has been seeded
    And I am not signed in
    When I open /lobby
    Then I see the rooms, parking, and carpools sections
    And the nav bar shows a "Sign in" button
    And no Join or Leave or Add my car buttons are visible
    And no admin menus are visible

  Scenario: Sign-in button does not show on the sign-in page
    Given I am not signed in
    When I open /
    Then the nav bar does not show a "Sign in" button

Feature: Sign-in by handle
  Pick a handle to sign in. Existing handle = sign in as that attendee. New handle = register and sign in.

  Scenario: Sign in as an existing attendee
    Given an attendee named "DrMurloc" exists
    And I am on /
    When I type "DrMurloc" into the handle field
    And I click Sign in
    Then I am signed in as DrMurloc
    And I am redirected to /lobby
    And the nav bar shows my handle in the user dropdown

  Scenario: Register a new attendee by typing a fresh handle
    Given no attendee named "Bob" exists
    And I am on /
    When I type "Bob" into the handle field
    And I click Sign in
    Then a new attendee named "Bob" is registered
    And I am signed in as Bob
    And I am redirected to /lobby

  Scenario: Sign in button enables for both existing and new handles
    Given I am on /
    When I type any non-whitespace handle
    Then the Sign in button is enabled

  Scenario: Password field accepts anything and ignores it
    Given I am on /
    When I type "Bob" into the handle field
    And I type "literally-anything" into the password field
    And I click Sign in
    Then the password value is discarded
    And the sign-in succeeds

Feature: Switching identity
  The nav-bar dropdown lets you flip between any registered attendee.

  Scenario: Switch to another attendee from the lobby
    Given I am signed in as Alice
    And Bob is also registered
    When I open the user dropdown in the nav bar
    And I click Bob
    Then the lobby reloads with my identity set to Bob
    And the nav-bar dropdown now shows Bob's handle

  Scenario: Switch User menu item signs out and routes home
    Given I am signed in as Alice
    When I open the user dropdown
    And I click "Switch User"
    Then I am no longer signed in
    And I am on /

Feature: Room assignment
  Each attendee can be in at most one unlocked room.

  Scenario: Join a free room
    Given I am signed in as Alice
    And 3F King has free seats
    When I click Join on the 3F King card
    Then I am an occupant of 3F King
    And the room card lists me among occupants
    And the "Your assignments" pill reads "Sleeping in 3F King"

  Scenario: Joining a new room auto-leaves the previous one
    Given I am signed in as Alice
    And I am an occupant of 1F Queen
    When I click Join on the 3F Twin card
    Then I am removed from 1F Queen
    And I am an occupant of 3F Twin
    And the "Your assignments" pill updates to "Sleeping in 3F Twin"

  Scenario: Locked rooms have no Join button
    Given 2F Right is locked to the family
    When I view the rooms section as any non-family attendee
    Then the 2F Right card shows a lock icon
    And it has no Join or Leave button

  Scenario: Full rooms don't show a Join button
    Given 3F Twin has 3/3 occupants
    When I view the room cards as a non-occupant
    Then the 3F Twin card has no Join button

Feature: Cars and carpools
  Driving up = declaring a car. A carpool is 2+ humans in one car.

  Scenario: Declaring a car unlocks carpool formation
    Given I am signed in and have no car declared
    When I enter 4 in the seats field
    And I click "Add my car"
    Then my car capacity is recorded as 4
    And a "Form" picker appears in the Carpools section

  Scenario: Form a carpool with passengers
    Given I have a 4-seat car declared
    And Bob is registered
    When I select Bob in the Passengers picker
    And I click Form
    Then a new active carpool is created
    And I am the driver
    And Bob is a member
    And the carpool card shows our initials avatars

  Scenario: Joining another carpool auto-leaves the current one
    Given I am a passenger in Alice's carpool
    And Bob's carpool has a free seat
    When I click Join on Bob's carpool
    Then I am no longer in Alice's carpool
    And I am a member of Bob's carpool
    And the "Your assignments" pill reads "In Bob's carpool"

  Scenario: Driver disbands the carpool
    Given I am the driver of an active carpool
    When I click Disband
    Then the carpool becomes inactive
    And it disappears from the lobby's carpool cards
    And my carpool status pill clears

  Scenario: Removing a member drops the carpool below 2 humans
    Given Alice's carpool has only Alice and Bob
    When Bob clicks Leave
    Then the carpool auto-disbands
    And both Alice and Bob have no carpool

Feature: Admin parking assignment (DrMurloc only)
  The host manually assigns parking spots; the saga's auto-allocator is dormant.

  Scenario: Admin sees the assignment menu on each spot
    Given I am signed in as DrMurloc
    When I view the parking section
    Then each spot card shows a kebab menu (⋮)
    And the menu lists all active carpools and all car-having attendees

  Scenario: Non-admin does not see the assignment menu
    Given I am signed in as anyone other than DrMurloc
    When I view the parking section
    Then no spot cards have a kebab menu

  Scenario: Assign a spot to a carpool
    Given I am signed in as DrMurloc
    And Alice's carpool is active
    When I open the kebab menu on Driveway-2
    And I click "Alice's carpool"
    Then Driveway-2 is locked
    And Driveway-2's claim is Alice's carpool
    And the saga's inventory marks Driveway-2 as out-of-pool

  Scenario: Release a manual assignment
    Given Driveway-2 is locked to Alice's carpool
    And I am signed in as DrMurloc
    When I open the kebab menu on Driveway-2
    And I click "Release"
    Then Driveway-2 is unlocked
    And Driveway-2's claim is cleared
    And the saga's inventory marks Driveway-2 as back-in-pool

Feature: Bulk registration (DrMurloc only)
  The host pre-registers handles for everyone before the trip.

  Scenario: Quick-add a handle
    Given I am signed in as DrMurloc
    When I type "Cathy" into the quick-register field
    And I press Enter
    Then a new attendee named Cathy is registered
    And the field clears and refocuses
    And other browsers see Cathy in their user dropdowns within seconds

Feature: Live updates across browsers
  All connected clients see changes in real time.

  Scenario: Room join propagates instantly
    Given Alice and Bob are signed in on different browsers
    When Alice clicks Join on 3F King
    Then Bob's lobby shows Alice as an occupant of 3F King within 1 second
    And the room's free-seat count updates in both browsers

  Scenario: Carpool formation propagates
    Given Alice and Bob are signed in
    When Alice forms a carpool with Bob as passenger
    Then Bob's lobby shows the new carpool with both members
    And Bob's "Your assignments" pill reads "In Alice's carpool"

  Scenario: Bump toast fires for the bumped solo
    Given Alice is a solo driver parked at Driveway-2
    And Bob is signed in as DrMurloc on a separate browser
    When Bob (as admin) assigns Driveway-2 to a different carpool
    Then Alice's browser shows a warning snackbar "A carpool just took your parking spot."
    And Alice's parking pill clears
    And Bob's view does not show the toast
