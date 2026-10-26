# Lidarr/ALAC

An unofficial Lidarr fork focused on accurate ALAC quality classification during upgrade decisions. It is based on the upstream Lidarr code in this repository. The patched default branch is [`alac`](https://github.com/rtm135/Lidarr-ALAC/tree/alac).

## What changes

Lidarr caches media quality information for track files. This patch refreshes and persists stale ALAC classifications before upgrade decisions, so ALAC files are compared in the appropriate quality tier. Focused tests cover stale cached entries, 16-bit ALAC, unrelated formats, corrupt ALAC data, and upgrade decisions.

This patch corrects classification; it does not convert audio files or guarantee that an ALAC release is available from your sources.

## Build and test

This repository provides source code only. It does not publish a fork-specific installer or container image. To try the `alac` branch, build from source using the upstream [Lidarr contribution and development guidance](https://github.com/Lidarr/Lidarr/blob/develop/CONTRIBUTING.md). Back up your configuration and database first, and prefer a separate test instance.

## Help test

Report reproducible issues at [Lidarr-ALAC Issues](https://github.com/rtm135/Lidarr-ALAC/issues). Include the Lidarr version, operating system and architecture, reproduction steps, and sanitized logs. Do not include credentials, private tracker details, or copyrighted audio.

## Upstream project

Lidarr/ALAC is an independent, unofficial fork of [Lidarr](https://github.com/Lidarr/Lidarr), not an official release. Upstream license, documentation, and project credits remain applicable; see [LICENSE.md](LICENSE.md) and the [Lidarr Wiki](https://wiki.servarr.com/lidarr).
