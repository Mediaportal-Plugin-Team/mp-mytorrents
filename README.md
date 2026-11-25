[![Downloads](https://img.shields.io/github/downloads/hounddog59/mp-mytorrents/total?label=downloads)](https://github.com/hounddog59/mp-mytorrents/releases)
[![Latest release](https://img.shields.io/github/v/release/hounddog59/mp-mytorrents?label=latest)](https://github.com/hounddog59/mp-mytorrents/releases/latest)

# MyTorrents for MediaPortal 1

MyTorrents finds and downloads TV episodes and movies for your MediaPortal library, and files them where
MediaPortal expects them. It is a 2026 revival of the old MP-MyTorrents plugin (originally by Sohvaperuna /
edalex86, last release 0.80 in 2018), updated for **MediaPortal 1.39 x64**.

The aim is to be easy and friendly for the average person: plain words, sensible defaults, and help where
it's needed - every setting has a tooltip in Configuration and a **?** on the web page.

**[Download the latest version](https://github.com/hounddog59/mp-mytorrents/releases/latest)**

## What it does

- **Setup assistant.** The first time you run it, it installs and sets up qBittorrent (recommended) or BiglyBT,
  and Jackett, picks some reliable search sites, and starts everything with Windows.
- **Your torrent program.** qBittorrent, BiglyBT, Deluge, Transmission, uTorrent or Vuze, with step-by-step
  help for turning on each one's remote control. You can switch program at any time; it takes effect straight
  away.
- **Search.** Jackett (all your indexers in one go) plus your own sites. Results are sorted, and fakes,
  samples and foreign-language dubs are filtered out (or, per show, English-dubbed copies allowed - handy for
  foreign-language shows).
- **TV shows.** It follows your shows in MP-TVSeries and gets new or missing episodes - whole seasons first when
  a lot is missing. The episode must really belong to that show (a same-named show from another year is turned
  down), in the quality, size and sound you want.
- **Wanted movies.** Movies you add, from your Trakt list or from Moving Pictures, are found and fetched.
- **Movie upgrades.** Stereo-only movies can get a 5.1 or better copy by themselves (its own on/off switch).
- **Automatic or Manual.** *Automatic* keeps your shows up to date by itself; *Manual* only looks when you ask
  (Check now, Grab the missing ones, Look now, Search) - your show list is kept. It's at the top of the
  Downloading tab.
- **No doubles.** Nothing is downloaded that's already in your torrent program - also when another program
  (Sonarr, Radarr...) or you put it there.
- **When a download finishes.** It names the files your way (several naming styles, or your own), copies them
  into your TV or Movies folders - subtitles too - and tells MP-TVSeries / Moving Pictures to add them. It
  cleans up after seeding, but never deletes anything inside your library.
- **Stuck downloads.** Downloads with no seeds are replaced by another copy. Ones that are nearly finished
  are protected.
- **Drop folder.** Put a movie or episode in it and MyTorrents files it for you. A movie copy at least a
  quarter bigger than yours replaces it (the old one is put aside, not deleted).
- **Works with MediaPortal closed.** A small background helper keeps going, and there's a web page for your
  phone, tablet or laptop at `http://<your PC>:9118` - shows, wanted movies, downloads, search and settings.
- **Settings in three places, always the same.** MediaPortal, *MyTorrents Configuration* (desktop icon) and
  the web page all use one settings file, so they always agree.
- **A daily check-up** that tells you in plain words if anything needs your attention, with **Find
  duplicates** and **Start fresh** (forget what it has tried and look for everything again - can be undone).
- **Any skin.** MyTorrents adds its own screens to skins that don't have them.

See [CHANGELOG.md](CHANGELOG.md) for everything that changed, version by version.

## Install

1. Download `MyTorrents.x.x.x.mpe1` from the [Releases](https://github.com/hounddog59/mp-mytorrents/releases)
   page. If Windows blocked it: right-click it > Properties > tick **Unblock** > OK.
2. Close MediaPortal, then double-click the file (MediaPortal Extension Installer).
3. On the last page, leave **Open MyTorrents Configuration** ticked. The setup assistant walks you through
   the rest.

You can change settings at any time, either in **MyTorrents Configuration** (desktop icon, no need to start
MediaPortal), on the web page, or in MediaPortal under *My Torrents*.

The tabs in MyTorrents Configuration: **TV shows**, **Show list**, **Movies**, **Downloading**,
**File management**, **Check-up**, **Background**, **Search sites**, **Torrent program** and **About**.

**Updating:** MediaPortal's extension manager tells you when there's a new version (from 1.3.81). Or just
install the new `.mpe1` over the old one. Your settings are kept, and the installer stops and restarts the
background helper by itself.

## Requirements

- MediaPortal 1.39 x64 (1.34 or newer should work)
- MP-TVSeries for TV shows (the community 4.8 version is recommended) and Moving Pictures for movies
- A torrent program. Setup installs qBittorrent if you don't have one.

## Torrent programs

| Program | Notes |
|---|---|
| **qBittorrent** (recommended) | Fully tested. Setup installs it, turns on its Web UI and starts it minimised with Windows. |
| BiglyBT | Through its *Web Remote* plugin. The setup assistant can set it up for you (also when BiglyBT is already installed). The replacement for Vuze/Azureus. |
| Deluge | Web UI, default password `deluge`. |
| Transmission | Remote access turned on in its settings. |
| uTorrent | Works, but not recommended (adverts, and old versions have known security problems). |
| Vuze | Works, but BiglyBT is its maintained replacement. |

## Adding torrent sites yourself

In MyTorrents Configuration → **Search sites**:

- **Jackett:** add sites in Jackett (*Open Jackett* → *Add indexer*) and tick the ones to use.
  The recommended ones are already ticked.
- **My sites:** type a name and the site's search address, with `%search%` where the words go. (Search the
  site in your browser, copy the address, and replace your search words with `%search%`.) Then click
  *Test site*. Sites with a "checking your browser" page only work through Jackett.

## Skins

- **Any skin works.** At start-up MyTorrents adds its universal screens to every skin that has no MyTorrents
  screens of its own, at 1920×1080 or 1280×720 to match the skin.
- Skins with their own MyTorrents screens (TITANIUS, Frames, Titan…) are left alone.
- The universal files carry a marker comment. Delete them from a skin to get fresh copies on the next start.
  Files without the marker (a skin author's own) are never changed.
- The designs live in `Skin\tools\make_universal.py`. `Skin\tools\preview.py` renders a preview PNG without
  MediaPortal.

## Building

1. Open `MyTorrents.sln` in Visual Studio 2019 or newer, or run `dotnet build MyTorrents.sln -c Release`.
   Output goes to `bin\Release`.
2. Run `python Installer\build_mpe1.py` to make the `.mpe1` package.

MediaPortal reference DLLs are in `lib\MediaPortal`. `lib\MediaPortal\MP-TVSeries.dll` is **not** included,
because a built MP-TVSeries has API keys inside. Copy it from
`C:\Program Files\Team MediaPortal\MediaPortal\plugins\Windows\` before building.

## Layout

- `src\MyTorrents` – the MediaPortal plugin (screens, auto grabber, watch list, finished downloads,
  web page, check-up)
- `src\MyTorrentsConfig` – the stand-alone *MyTorrents Configuration.exe*, the setup assistant and the
  background helper
- `src\TorrentEngine` – shared torrent-program interface and helpers
- `src\QBittorrentEngine`, `src\TransmissionEngine` (BiglyBT / Transmission), `src\DelugeEngine`,
  `src\UTorrentEngine`, `src\AzureusEngine` – one per torrent program
- `src\TorrentSearch` – Jackett and your own search sites
- `src\RSSReader` – RSS/Atom feeds
- `Skin` – the universal screens and the tools that make them
- `Installer` – the `.mpe1` package builder

## The rule for every feature

Easy and friendly for the average person: plain words, no jargon, sensible defaults, and help where it's
needed. Anything you can set or do in MediaPortal can also be done in MyTorrents Configuration, and the other
way round. Both use the same setting or the same file, so they always agree.

## Help and bug reports

- **[Issues](https://github.com/hounddog59/mp-mytorrents/issues)** on GitHub, or the
  [MediaPortal forum](https://forum.team-mediaportal.com/).
- Please attach `MyTorrents.log` from `C:\ProgramData\Team MediaPortal\MediaPortal\log`.
- It contains your Jackett address, so have a quick look before posting it publicly.

## Credits and licence

- The original MP-MyTorrents was written by Sohvaperuna and edalex86.
- The 2026 revival is by Mark Willmer (hounddog59), developed with Claude (Anthropic).
- Licensed under the GNU General Public License v3. See [LICENSE](LICENSE).
