# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.1] - 2021-03-12

## Added

- Support for Signal UUIDs. This is an underlying change in libsignal-service-dotnet.
- Option to toggle sending messages with enter. If disabled on desktop press Shift-Enter to send messages. Thanks to @ShelbyBoss for contributing this feature in https://github.com/signal-csharp/Signal-Windows/pull/224!
- Support for saving and loading drafts including attachment drafts. Thanks to @ShelbyBoss for contributing this feature in https://github.com/signal-csharp/Signal-Windows/pull/225!

## Fixed

- Attachment uploads and downloads are now fixed.
- Adding an unknown number will first check if that number has registered with Signal.
- Contact color will not be reset back to its original color if you change it. Thanks to @ShelbyBoss for contributing this feature in https://github.com/signal-csharp/Signal-Windows/pull/225!

## [0.3.0] - 2021-02-18

## Added

- Support for disappearing messages
- Support for CAPTCHAs when registering

## Changed

- Use [Signal Contact Discovery Service](https://signal.org/blog/private-contact-discovery/) for discovering contacts.

## Fixed

- 429 error when trying to add new contacts https://github.com/signal-csharp/Signal-Windows/issues/212
- Incoming messages failing to be received https://github.com/signal-csharp/Signal-Windows/issues/220

## [0.2.26] - 2019-12-05

## [0.2.25] - 2019-04-03

## [0.2.24] - 2019-03-16

## Added
- Fullscreen imageview

## Fixes
- use new cert for pinning

## [0.2.23] - 2018-12-04

## Fixes
- fix "database is locked errors"
- fix crash in privacy settings menu

## [0.2.22] - 2018-10-02

## [0.2.21] - 2018-07-09

## Fixes
- fix invalid urls causing a message to be displayed incorrectly

## [0.2.20] - 2018-07-06

## Features
- clickable hyperlinks
- send attachments
- block contacts
## Fixes
- use composed timestamps for the conversation list ordering
- mark bottommost message read when window gets focus
- stop creating multiple identity key change messages
- limit message maximum size to match Signal-Android

## [0.2.19] - 2018-05-28

## Fixes
- Properly suspend on suspend
- Improve textbox reselection speed
- Disable debug log export on crash, the app did not always cleanly shutdown
- Request group/contact sync after linking

## [0.2.18] - 2018-05-23

## Fixes
- rebuild on a different machine

## [0.2.17] - 2018-05-23

## [0.2.16] - 2018-05-21

## Features
- offer ui debug log export on crash
- add fancy layout of our settings pages

## Fixes
- fix conversation re-selection issues

## [0.2.15] - 2018-05-18

## Fixes
- fix messages failing to send if the selected conversation was moved in the conversation list

## [0.2.14] - 2018-05-17

## Features
- import contacts and groups from master device

## Fixes
- fix not sending the correct disappearing timer
- remove notification when receiving synced sent message
- honor synced read messages from sibling devices

## [0.2.13] - 2018-05-09

## Fixes
- fix app window not being properly activated in all cases

## [0.2.12] - 2018-05-07

## Fixes
- fix crashes on mobile when scrolling

## [0.2.11] - 2018-05-07

## [0.2.10] - 2018-05-04

## [0.2.9] - 2018-05-04

## Features
- remove notification if the frontend considers a message read

## Fixes
- mitigate thread-pool blocking by the scroll handler

## [0.2.8] - 2018-05-03

## Features
- support exporting debug logs

## Fixes
- fix title bar colors for secondary windows
- properly switch to formerly suspended windows

## [0.2.7] - 2018-04-28

## Features
- hide notification when the corresponding message is read

## Fixes
- fix multiple notification click handling issues

## [0.2.6] - 2018-04-27

## Features
- save read messages as read

## [0.2.5] - 2018-04-26

## Bugfixes
- fix handle acquisition and release when registering/linking

## [0.2.4] - 2018-04-26

## Bugfixes
- fix outgoing newlines

## Features
- add a background task to poll messages
- initial support for incoming attachments
- support multiple windows on different virtual desktops

## [0.2.3] - 2017-12-05

## Bugfixes
- fix crash on disconnect

## [0.2.2] - 2017-11-02

## Bugfixes
- fix messages being lost on shutdown

## General
- timestamps in the conversation list
- added logfiles

## [0.2.1] - 2017-10-22

## Bugfixes:
- unrecoverable disconnect after being in the background on W10M

## General:
- changed the package name
- use shorter timestamps for recent messages
- proper input scopes for mobile keyboards
- signed by a new key

## Remarks
This release will be installed in a different folder. Backup your databases if you want to keep your old data!

## [0.2.0] - 2017-10-21

Initial alpha release

## [0.1.9] - 2017-08-29

## [0.1.8] - 2017-08-22
