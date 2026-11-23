# MyTorrents - what changed, version by version

Newest first. The plugin was revived in 2026 from the last 2018 build (0.80);
1.1 was the first build that loads in 64-bit MediaPortal again.

## 1.3.81

- MediaPortal's extension manager can now tell you when a newer version of MyTorrents is out (the installer
  points at the update list on GitHub).
- The README is up to date, with download and release badges.

## 1.3.80

- Help on every setting: hold the mouse over a setting in MyTorrents Configuration and a tooltip says what it does
  and what changing it means (it stays up for 30 seconds). The web page has the same help as a small "?" next to
  each setting - hover it, or tap it on a phone or tablet. Both come from one list, so they always say the same.

## 1.3.79

HTPC Library Manager's updated naming rules (19 Nov 2026, 3:30 PM):
- Movies: Title, The (Year) [Score][Genre][Quality] - article last, genre before quality. New style "like HTPC
  Library Manager".
- Quality is read from the video file itself, never trusted from the name: 2160p / 1080p / 720p / 576p / 480p only
  (width 1700+ or height 950+ is 1080p - no "972p"), codec x264 or HEVC (never H264), audio AAC / AC3 / EAC3 / DTS...
  New downloads are read with MediaInfo (comes with MediaPortal); the movie renamer uses Moving Pictures' reading
  of each file (width / height / codecs).
- Renaming keeps the genre already in a movie's name; TMDb's genre is only used when the name has none.
  No empty [] brackets.
- Episodes get their quality from the video file too.

## 1.3.78

Names now follow the same rules as HTPC Library Manager (19 Nov 2026), so both programs agree:
- Characters: ": " becomes ", " (Title: Sub -> Title, Sub), ":" with no space becomes "-" (15:17 -> 15-17),
  "?" becomes "_", * " < > | / \ become "-", and no dangling "-" before (Year) or at the end. (They used to be
  dropped: "What If...?" became "What If...", "15:17" became "1517".)
- Double episodes are named S01E01-E02 (was S01E01E02).
- Subtitles in a Subs folder travel with the video, named like it (Subs\2_English.srt -> Movie.English.srt,
  Subs\<episode>\*.srt for a season).
- A movie in the drop folder at least a quarter bigger than the copy you have replaces it. The old copy isn't
  deleted - it goes to "Replaced copies" in the drop folder for you to delete. Smaller ones stay in the drop folder.
- "Best available" movie upgrades also need to be a quarter bigger (like "better copy" ones already did).
- New naming styles "like HTPC Library Manager": Title (Year) [6.9][1080p BluRay x264 AAC][Genre] for movies,
  Show (Year) [Genre] for show folders.

## 1.3.77

- "How MyTorrents works" (Automatic / Manual) moved to the top of the Downloading tab, in Configuration and on the
  web page - it's about what gets downloaded (it was on Show list and the TV shows tab).

## 1.3.76

- Whole seasons are checked as strictly as single episodes: only the show's own name may come before "Season 1".
  Lupin got "Lupin the Third - Mine Fujiko to Iu Onna (Season 1)", a Japanese cartoon, because the name only had to
  start with "Lupin". A release group in front ("[Judas] Show - S01") is still fine.
- Manual now really means "only looks when I ask": the Auto grabber stops searching by itself but keeps its show
  list - no show checks, no RSS feeds, no wanted movies, no Trakt list. MyTorrents only looks when you press Check
  now, Grab the missing ones, Search again, Look now or Search, or add a movie to Wanted movies. A dead or removed
  copy of something you asked for is still replaced, and finished downloads are still copied. MediaPortal and the
  background helper both obey it. Automatic works as before. (One switch for this - "How MyTorrents works" - not a
  second "Auto grabber searches by itself" switch, so the two can't contradict each other.)
- Movie upgrades have their own on/off ("Movie upgrades on"), Manual or Automatic: when it's on, stereo movies'
  5.1 / better copies are added and looked for by themselves; when it's off, upgrades are only looked for when you
  ask (Look now, or when you add one).
- No doubles with other programs: before downloading an episode, a whole season or a movie, MyTorrents checks what
  is already in the torrent program - added by another program that uses it (Sonarr, Radarr...) or by hand - and
  leaves it alone ("already in the torrent program"). Counted: still downloading, or added in the last 3 days.

## 1.3.75

- The Library tab is now called File management (Configuration and the web page).
- Changing the torrent program (e.g. qBittorrent to BiglyBT) now takes effect straight away in MediaPortal and the
  background helper - before, they kept sending downloads to the old program (and started it again) until restarted.

## 1.3.74

- New "Start fresh" on the Check-up tab (Configuration and the web page): MyTorrents forgets what it has tried -
  what it downloaded and is waiting for, dead copies, the Can't find list, relaxed seeders, too-big waits, stuck
  downloads, the check-up notes - and looks for everything again with the settings as they are now. Settings, the
  show list, Wanted movies, hidden items and moved-aside duplicates are kept. It warns when downloads are still
  going, and "Undo start fresh" puts everything back. No need to close MediaPortal.

## 1.3.73

- The Search sites and Torrent program tabs moved to the right of Background (Configuration and the web page) -
  they are set once and rarely changed.

## 1.3.72

- A show with the same name but a different year is no longer grabbed by mistake (Lupin got an
  episode of "Lupin 1977", the Japanese cartoon Lupin the Third).
- New per-show choice: "English-dubbed copies are fine for this show" - for foreign-language shows
  like Lupin. Right-click the show in the Show list (Configuration), or the "Dubbed" button on the web
  Show list. Copies marked DUB or DUBBED are then grabbed; other languages (Italian, Polish...) are
  still left out. Works for single episodes and whole seasons.

## 1.3.71

- **Tabs grouped and in the order a download goes through them** (MyTorrents Configuration and the web page alike): TV shows, Show list, Movies - Search sites, Torrent program, Downloading - Library, Check-up - Background, About.
- **Renamed:** Downloads is now **Downloading**, When finished is now **Library**, and the Torrent client tab and the program's own tab (qBittorrent) are one **Torrent program** tab.
- **Moved, nothing changed or lost:** Downloads at once and Stuck downloads (from TV shows), Nearly finished and Clean up after seeding (from When finished) are on **Downloading** (Settings... button; Downloading settings on the web page). The MP-TVSeries scan (from TV shows) and the Movies tab's "New movies" box - filing into Movies / Collections, the .nfo, Waiting for your OK - are on **Library**, so one tab says where everything goes.
- Web page: the Torrent program tab also has the update frequency and notifications.
- **TV shows: no more "More settings..." window.** When nothing good turns up, Shows and episodes, and Whole seasons are on the TV shows tab itself, under the everyday settings (the tab scrolls on a small window) - like the web page.

## 1.3.70

- MediaPortal Wanted movies (from Mark's TV photo): the long state text ("downloading / got it - <the whole torrent name>") pushed the movie's name off the line. Each line now has a short state (downloading, none fit yet, nothing found yet...) so the name fits; the full state is the heading of the movie's menu. Only the movies still wanted are listed (downloading first) - the ones already in your movies are one line at the end. The button is now just "Options" (it was cut off).

## 1.3.69

- **Web page: everything MyTorrents Configuration does, with the same tabs in the same order** - Movies, TV shows, Show list, Downloads, When finished, Check-up, Torrent client, Search sites, Background (plus Search). Every setting of the Movies, TV shows (including "More settings"), When finished and Torrent client tabs can be changed and saved there; the Duplicates check (one at a time, with Hidden copies) and Rename to match my naming are there too. Background shows what is running; switching the helper or the web page itself off stays in Configuration so the page can't cut itself off.
- **MediaPortal: simpler - three buttons, no menus inside menus.** Downloads (straight to the list), Wanted movies (a list on screen: Add a movie..., then each movie - OK gives Look for it now / Search all torrents / Remove), Search, and **Options for this line** - the same as Info / right-click, always on screen. Under the buttons it says \"On your phone, tablet or laptop, open:\" with the web address - all the settings and more. Labels, Sort, RSS, Log, Download folder and the Show list moved to the web page (universal screens v1.6).
- A movie filed from the drop folder now asks Moving Pictures to look for new movies straight away (as a finished download already did), so it shows up within a minute or so instead of waiting for Moving Pictures' own scan.

## 1.3.68

- The "..." (look for a folder) buttons next to the Movies folder, Collections folder, TV / movie destination and Drop folder boxes now always sit just right of their box. The window stretches the boxes to its width, and the stretched box covered the button, so you could only paste a path.

## 1.3.67

- Setup / update window: the line under the progress bar now says what it's doing ("Updating - please wait", "Checking the connection", "Getting your search sites from Jackett and testing them - your sites list opens in a few seconds") instead of still saying "Click Update" during the pause before the sites list. The text under the title is shorter so it isn't cut off.
- Updating MyTorrents: the last page of the installer now says that a MyTorrents window opens a few seconds after Finish (the check of Jackett, FlareSolverr, the torrent program and your search sites) and that it's normal; the window itself says MyTorrents is installed and you can close it when you've had a look.

## 1.3.66

- **Duplicates check: a much stronger safeguard.** Two files with the same episode number are now treated as *different* episodes - never offered as copies - when their names show different air dates (more than two months apart), different season folders, or different real titles. The old check deleted about 45 real episodes this way (Monster's three seasons all called S01, The Haunting's Hill House vs Bly Manor, Trailer Park Boys Out of the Park, The Twilight Zone 2002, one Inspector George Gently).
- MyTorrents Configuration opens on the tab you were on last time (unless a setup step still needs doing).
- A "season pack" that turns out to be short of episodes (The Haunting S02 was just episode 1) no longer blocks the missing ones for a week - they're looked for again at the next search.

## 1.3.65

- Poor copies and Stereo-only movies are one list now - **Movies to improve** - with the buttons in each row and a Year column (click a heading to sort, by year too; "Sort by year" in MediaPortal).
- Each movie looks for the best it can: a poor copy that's also stereo gets a better copy - 5.1 if there is one, a better stereo one if not (it used to fix only half: Find 5.1 took any 5.1 copy, even a poor one, and Find a better one didn't try for 5.1). A poor copy gets a better copy; a stereo-only movie with a good picture gets a 5.1 copy.
- "Not this one" hides it, whatever the reason it was on the list - and the hidden ones have their own list (under Movies to improve on the web page, "Hidden..." in Configuration, "Hidden" in the MediaPortal menu) to look over and **Put back**, one at a time or all at once.
- Same in MediaPortal, MyTorrents Configuration, the web page and the Check-up.

## 1.3.64

- Web page: pressing a button further down a list (Find 5.1, Not this one, Ignore, Add...) brings you back to the same spot instead of the top of the page.

## 1.3.63

- Age tags at the front of a download's name ("18+.2050.2019...", "[18+]", "R18") are left out of the movie's name - "18+ 2050 (2019)" sorted at the top of Movies and Moving Pictures didn't recognise it. Titles that really start with a number (1917, 13 Hours, 12.12: The Day) aren't affected.
- When finished: show-folder and movie styles without the rating - "Show, The (Year) [Genre]" and "Title, The (Year) [1080p BluRay HEVC AC3][Genre]" - since ratings change and the names would change with them.
- "Use it now" works again on a download that was copied earlier when that copy isn't there any more (moved, renamed or deleted since) - it used to say "it has already been copied".

## 1.3.62

- Drop folder: when you already have everything in something you dropped in (a whole season pack you've got), the check-up says so - "you already have everything in it (96 files) - you can delete it" - and Fix it says "Delete it - you have it all" (several at once too).
- Drop folder: what was already tried is remembered across restarts, so an unchanged pack isn't gone through again at every start.
- A sample-video note said "film" - now "movie" like everywhere else.

## 1.3.61

- Duplicates: two files MP-TVSeries matched to one episode whose names say different episodes ("S01E04" / "S02E01", or "Terror of the Zygons (1)" / "(3)") are a wrong match, not copies - never suggested, never in "Do the easy ones", and the reason says so. 1.3.59 / 1.3.60 hid 33 such episodes on Mark's HTPC (put back with "Put back wrongly hidden episodes.bat").
- Installer: the desktop icon step finds the MediaPortal folder from the installer's own location (MediaPortal's Config class isn't always available to installer scripts).

## 1.3.60

- "Movies" everywhere: the last places that said "film" (Duplicates, Collections, Rename my movies, the web page, the installer and the README) now say "movie", the same as the Movies tab, Moving Pictures, Plex and Trakt. Settings keep their old internal names, so nothing is lost.
- The desktop icon stays where you put it: updates no longer delete it and make a new one (Windows put the new one in the next free spot). It's only made when it's missing; "Remove MyTorrents" takes it away.
- Duplicates, done again so it's easy to follow: one at a time - "which copy do you want to keep?" - with the reason it's on the list in plain words (two files matched to one episode, in different shows' folders = probably a wrong match, an extended version, a part, the same file twice...), each copy's picture, sound, size, file and folder (click to open), and big buttons: "Keep this one" (the others are hidden, nothing deleted), "Keep them all - they're different", "Skip for now", "< Back". "Do the easy ones for me" sorts out the ones with no doubt in one go (the same file twice, or one clearly better picture / English sound). "Hidden copies" puts them back or deletes them for good.
- Every list can be sorted by clicking a column heading (click again to reverse; remembered): now also Wanted movies (with new Year and Added columns), the Check-up, Collections you could make, Rename my movies, File new movies and Can't find.
- Check-up: pick several lines at once (Ctrl / Shift) - Fix it and Ignore work on all of them ("Delete these 5 copies", "Ignore these 12"), as long as they need the same kind of fix.
- Duplicates: files that are "Part 1" and "Part 2" of one episode or movie aren't shown as duplicates any more; copies in different shows' folders (Prime Suspect (1991) matched to Prime Suspect 1973) are never ticked - the note says it's probably a wrong match in MP-TVSeries.

## 1.3.59

- Check-up tab -> "Find duplicates...": every episode MP-TVSeries has two files for, and every film Moving Pictures has in two copies (parts 2, 3... of a film don't count). The copy to keep is suggested and shown in bold - the better picture, then English sound, then better sound, then the bigger file - and the others are ticked. Different versions (extended, director's cut, uncut...) are never ticked for you.
- "Set aside the ticked ones" adds ".duplicate" to the end of the name, right where the file is (with its subtitles / .nfo / thumbnails), so nothing is deleted yet and MP-TVSeries / Moving Pictures drop it at their next scan. "Set aside earlier..." puts them all back or deletes them for good. Every copy of something can never be set aside. "Not duplicates - stop showing this one" for ones that really are different.

- Check-up: a film counts as "in place" with a year either side too - Media Center Master often renames with the local release year ("City of God (2002)" -> "(2003)"), which made notes say it wasn't where MyTorrents put it.
- Check-up tab: the Fix it / Open folder / Ignore buttons sit under the explanation, above the list, and the explanation is no longer cut off.

## 1.3.58

- No more endless menus: every MyTorrents menu and pop-up stops when MediaPortal is closing, or when menus keep closing faster than anyone could press a button (MediaPortal froze when closed with the Auto grabber menu open - 24,000 reopenings in 2 1/2 minutes).
- Setup: the ticked search sites are at the top of the list, with "x sites selected".
- Setup: qBittorrent is ticked and "(recommended)" - the fully tested one; setup installs it, sets it up and starts it with Windows. Underneath, "I already use a different torrent program: choose it..." opens a window with BiglyBT (setup still installs and sets it up), uTorrent, Deluge, Transmission and Vuze/Azureus, each with step-by-step help for turning on its remote control (Vuze/Azureus taken out of setup - BiglyBT is its replacement and says so; still on the Torrent client tab; uTorrent marked "not recommended" with the reasons), the port, user name / password where it needs them (Deluge's 'deluge' filled in), and a "Check it now" button.
- Movies tab: Browse (...) buttons for the Movies folder and Collections folder.

## 1.3.57

- After an update the background helper starts again by itself: the installer starts it (as the ordinary user) once the new files are in, and MediaPortal starts it if it is switched on but not running - no need to open MyTorrents Configuration.
- Auto grabber: the show's name must come before the episode number - "Identity" had grabbed "The New Negroes ... S01E01 Identity" and "Manhattan" grabbed "The Wil Wheaton Project S01E08 Wil Takes Manhattan" (a year or US / UK / AU after the name is fine).
- The daily log files now really start each day (the day is kept in MyTorrents.logday.txt - the other parts writing the same log had always touched it after midnight first, so it never moved on).
- Check-up matches films by year and most of the title words, so the renamer's names ("48 Hrs. I (1982)", "Sinbad I, 7th Voyage of (1958)", "Alone in the Dark II (2008)") are no longer "isn't where MyTorrents put it"; episodes in folders MP-TVSeries doesn't scan are no longer looked for in Moving Pictures.

## 1.3.56

- The "duplicate column name" messages in the MediaPortal and Configuration error logs are gone (only missing columns are added).
- The "no seeds / nothing coming in" clock for stuck and nearly-finished downloads is kept in MyTorrents.stalled.txt, so it doesn't start again each time MediaPortal or the helper restarts.
- Copies dubbed into another language are left out (PLDUB, DUBBED, iTA, Hindi, TRUEFRENCH ...; for TV also FRENCH, GERMAN, SPANISH ...) unless the name also says ENG / MULTi / DUAL - the Auto grabber had picked a Polish-dubbed whole season of Love, Death & Robots (5 Nov). Films keep FRENCH / GERMAN etc. since that is often a foreign film's own language.
- "[show] is not in MP-TVSeries - skipped" is logged once a day per show, not on every pull.
- Drop folder never replaces or doubles up what you have: a film you already have (by title and year), an episode you already have (even a single one), or a different file with the same name in the library - it stays in the drop folder and the Check-up's "left in the drop folder" says why (e.g. "you already have it (2.1 GB, this one 1.4 GB)"). It used to delete the library copy and put the dropped one in its place when the names matched.
- qBittorrent not running on this computer (e.g. after a restart): MyTorrents starts it, minimised, and only says so every few minutes instead of on every call (700 "Unable to connect" lines after the 6 Nov restart).
- Check-up no longer says "isn't where MyTorrents put it any more" for films you moved (Movies New -> Movies / Collections) or the movie renamer renamed, as long as Moving Pictures still has the film; same for TV episodes in a renamed folder.
- The background helper no longer logs "no list called MCM" every hour - it isn't signed in to Trakt; MediaPortal does the Trakt list.

## 1.3.55

- MyTorrents.log starts a new file each day: MyTorrents.log is today, MyTorrents.1.log yesterday ... MyTorrents.7.log, older ones deleted (it had grown to 12.6 MB in 9 days). The Check-up and the site counts read across the files.

## 1.3.54

- First install: qBittorrent is set to start with Windows, minimised to the tray (done once - change it in qBittorrent's own options after that).

## 1.3.53

- Downloads stuck at 99%+ with no seeds are checked (start and end of the video are there) and copied to the library anyway - after 6 hours by default (When finished > Nearly finished; MediaPortal and web settings too), or straight away with right-click > "Use it now (nearly finished)" in Downloads (95%+). The torrent stays in the torrent program in case the last bit turns up.
- A download 90% or more done is never removed as "stuck", and the stuck time now counts from when data last came in (movies used to count from when they were added). Pausing a movie download protects it.
- First install: the search sites that tested best (TorrentGalaxyClone, TorrentDownload, TorrentProject2) are ticked already, so OK on its own gives working searches.

## 1.3.52
-: Check-up finds sample clips and the sites' advert videos left in the library
  ("... - RARBG COM.mp4", "...sample.mkv", "www.YTS..."): such a name AND under 100 MB (an episode only when it
  says "sample"; trailers kept in an extras folder are left alone) - "Delete it", or "Delete all the samples /
  adverts" in Configuration. In MediaPortal and on the web page too.
-: Search sites tab moved after qBittorrent (before Background).
- Fixed (Mark, 5 Nov - The Black Shield of Falworth (1954) finished but never copied): it came with
  the site's advert video ("RARBG COM.mp4"), so it counted as a 2-part film and got a name ending
  "- The Black Shield of Falworth 1954 1080p BluRay H264 AAC-RARBG" - with the long Media Center Master folder
  that's over Windows' path limit ("Could not find a part of the path"). Now: advert videos are left out, a
  second part gets a short name (CD2 / part 2 / 40 letters), and any name too long for the path is shortened -
  TV episodes too.
-: right-click "Look it up on TheTVDB" / "Look it up on TMDb" - opens the page in
  the web browser: Show list (TheTVDB goes straight to the show by its id), Downloads (episode or movie, from the
  name), Wanted movies (tab and window), Poor copies, Stereo-only movies, Rename my movies, Collections you could
  make (TMDb collection), New movies waiting, Can't find, Missing report, Removed shows, the Check-up. On the web page: TVDB /
  TMDb links next to each show (Show list) and movie (Wanted movies). One right-click menu per list (Open location
  and the look-ups together).
-: Downloads tab (and the web page): "Peers" - connected (in the swarm) - and
  "Seeds:Peers" - seeds for every downloader in the swarm (30 seeds, 10 peers = 3.0; higher = faster; "all seeds"
  when nobody else is downloading). Both sort; a saved column order keeps working (the new ones go after Seeds).
-: Movies is the first tab in Configuration (then TV shows); "My shows" is now
  called "Show list" (Configuration and the web page); on the web page the movie pages come first too.
- Fixed (Mark, 5 Nov - Along Came a Spider (2001) shown as "240p - you want 1080p"): the file is
  1080p but also carries its cover as a tiny second video (154x231), and Moving Pictures read that one. Poor
  copies now take the picture size from the file's name first, and a tiny picture in a big file is ignored.
  130 of the 616 "lower than 1080p" in Mark's library were this or are 1080p by name.

## 1.3.51
- (Mark, 4 Nov - "more info in the web UI, and sort the columns like in Configuration"): every
  table on the web page: click a heading to sort (again = the other way, with an arrow), a Find box for long
  lists, and a Columns button to hide / show columns - remembered in that browser. Downloads: the same columns as
  the Downloads tab (Down, Up, Seeds, Ratio, Time left, Library, Added) and "Delete and search again". My shows:
  Looks for (new / missing episodes) and which episodes are missing. Wanted movies: Year, Added, Last look.
- (Mark, 4 Nov - "if I delete the bad ones in qBittorrent will they be tried and found
  again?"): a download removed by hand before it finished now counts as a bad copy - the episode / movie is
  looked for again straight away with another copy (episodes used to wait a week; movies stayed "downloading"
  for ever). Finished and copied ones are left alone; nothing happens if qBittorrent can't be reached.
  You don't need to know which ones you deleted: MyTorrents compares what it started with what qBittorrent still
  has - including movies grabbed before 1.3.50 (found again by the copy's name).

## 1.3.50
- (Mark, 4 Nov - 50 downloading, most "Fetching details"): the Trakt list never managed to take
  anything off "MCM" (Trakt said "400 Bad Request" to the whole records), so every run added the same 28 shows
  again and started their searches over (161 movies went on Wanted movies at 3:08 pm too). Now: only the ids are
  sent, 50 at a time; and what was handled is remembered, so it's never added twice even if Trakt says no.
  "Downloads at once" (TV shows -> More settings, MediaPortal settings menu, web page; 10 to start with): while
  that many are downloading, the Auto grabber and Wanted movies wait - ones you ask for never wait.
  Wanted movies now swap a copy that never gets going (no details after 2 hours - the "Fetching details" ones -
  nothing after 6, or stuck) for another, like the Auto grabber already did for episodes.
-: right-click -> "Open location", or double-click, on the lists of files: Rename
  my movies, Poor copies, Stereo-only movies, Collections you could make, New movies waiting, the Check-up, the
  x265-butler tool.
-: the installer stops the background helper (and MyTorrents Configuration)
  before it copies the files - the helper keeps MyTorrents.dll and MP-TVSeries.dll open, so 1.3.47 - 1.3.49 and
  MP-TVSeries 4.8.0.5 / 4.8.0.6 "installed" but the old files stayed. MyTorrents Configuration starts the helper
  again when it opens. (Uninstalling stops it too.) For an MP-TVSeries update: stop the helper first (tray icon).
- (Mark, 4 Nov - "where would I put a movie or episode file so MyTorrents picks it up?"): the
  drop folder (When finished tab, MediaPortal settings menu, web page; empty = off). A movie or episode put there
  (a file, or its folder) is filed exactly like a finished download - named your way, into the right show /
  season or Movies / collection folder, .nfo, MP-TVSeries / Moving Pictures told - and MOVED out of the drop
  folder. Only once it hasn't changed for 2 minutes (still copying = left alone). What can't be worked out stays,
  and the Check-up says why. Works with MediaPortal closed too (the helper).

## 1.3.49
- Check-up (Mark, 4 Nov - "how will I know it's all working... I wouldn't know where to look"): once a day by
  itself, in the background, MyTorrents checks what it filed in the last 7 days (read from its log): still where
  it put it, in MP-TVSeries / Moving Pictures, not there twice, not a poor copy; plus movies not named your way,
  Can't find, search sites that keep timing out, the NAS nearly full, lots of errors in the log. Check-up tab in
  Configuration (Fix it: Add to MP-TVSeries / Delete this copy / Look for a better copy / Rename...; Open folder;
  Ignore), "Check-up: ..." in MediaPortal's Auto grabber menu, and a Check-up page on the web page. No pop-ups.
- Poor copies - look for a better one: Movies tab -> Your movies -> "Poor copies (find a better
  one)...", MediaPortal Wanted movies -> "Poor copies...", web page My movies. Movies smaller than they should be
  for their picture (allowing for x265 and for short films), or a lower picture than your movie quality. The
  better copy must be your quality and size and clearly bigger (a quarter more); once it's safely in place the
  old one is deleted (or kept - the same setting as the 5.1 upgrades).
- "Smallest movie" Automatic now allows for x265: 1080p 1.2 GB (x265 700 MB), 720p 500 MB (300), 4K 3 GB (1.8).
- (Mark, 4 Nov - "need movies rename settings like tv series", "and a check to rename ones
  that have been missed", screenshot of his folders): When finished tab -> "Movie names" and "Movie folders",
  like the TV ones, with a live example. Styles: Title (Year) (as before) / with [1080p] / - 1080p / [1080p 5.1] /
  Title.Year.1080p / like Media Center Master: "Revenant, The (2016) [7.5][1080p BluRay HEVC AC3][Western]" -
  or your own pattern from {title} {sorttitle} {year} {rating} {quality} {source} {codec} {audio} {sound}
  {genre}. Rating and genres come from TMDb when the movie is filed. Same settings in MediaPortal's settings menu
  and on the web page.
  "Rename my movies to match..." (When finished tab and Movies tab -> Your movies): lists the films whose file or
  folder doesn't have the chosen shape (a rating that has moved since doesn't count; "[7] " collection numbers are
  kept), tick and Rename - .nfo / subtitles / artwork with the same name go too, a folder holding more than one
  film is never renamed, and Moving Pictures is told the new place so it keeps watched status and artwork. Only
  with MediaPortal closed. (Mark's library: 484 of 4,295 films don't match the Media Center Master style.)
  Collections (Mark, 4 Nov - MCM never renamed collections well and has been crashing): works inside collections
  too, nested ones included (Marvel Rising\X Men\[1] X-Men ...): the "[1] " order number is kept on the folder
  only - not on the file, a collection's own folder (holds other folders or more than one film) is never renamed, and
  artwork videos Moving Pictures picked up (backdrop-x265.mkv, folder-x265.mkv) are left alone. In Movies
  Collections: 318 of 1,617 file names and 145 film folders don't match.
- (Mark, 4 Nov - "and the series folder naming also"): When finished tab -> "New show folders":
  as MP-TVSeries names the show (as before) / Show (Year) / like Media Center Master "Law & Order, Special Victims
  Unit (1999) [7.9][Thriller]" (year, rating and genres from MP-TVSeries). Shows you already have keep their
  folder. Same setting in MediaPortal's settings menu and on the web page. Folder matching now also knows
  "Diplomat, The" = "The Diplomat".

## 1.3.48 (with MP-TVSeries 4.8.0.6)
-: "Tick all" becomes "Untick all" once everything is ticked - in Stereo-only
  movies, and now also in Collections you could make and New movies waiting for your OK.
- Fixed: Movies tab - the "release groups / sites, with commas" note sat next to the
  Sound box instead of Favourites. The Favourites box is shorter now with the note beside it; a long list scrolls
  in the box, and hovering shows all of it.
- (Mark, 4 Nov - got a 700 MB "1080p" The Elephant Man): "Smallest movie" setting next to
  "Biggest movie" (Movies tab, MediaPortal settings menu, web page). Automatic to start with: 720p 500 MB, 1080p
  1.2 GB, 4K 3 GB - or pick a size, or no minimum. Smaller copies are skipped by Wanted movies, the Trakt list and
  5.1 upgrades, and left out of a movie search when there are copies that fit.
- MP-TVSeries 4.8.0.6: "Scan remote shares every ... minutes" was saved but never read - MP-TVSeries read the
  whole NAS at start and every 5 minutes anyway (the lag Mark saw while rating an episode). Now unticked = off.

## 1.3.47 (with MP-TVSeries 4.8.0.5)
- "Open the web page" works as soon as the web page box is ticked: it saves, starts the background helper, waits
  for the page and opens it - or says plainly why not. A helper that can't start writes why to MyTorrents.log.
- Fixed: MyTorrents asked MP-TVSeries' database for "OriginalName" (it's "origName"), so since 3 Nov every "is this
  show / episode already in my library?" look-up failed. New episodes go into the show's own folder again.
- MP-TVSeries scan (Mark, 4 Nov - MediaPortal sluggish): TV shows -> More settings -> "MP-TVSeries scan": every
  night (default) / once a week / never. Between 2 and 5 am, never while something plays, folders only (the online
  update keeps its own timing). So MP-TVSeries' "scan when starting MediaPortal" and "Scan remote shares every"
  can be unticked. Same setting in MediaPortal's settings menu and on the web page.
- MP-TVSeries 4.8.0.5 (branch tvmaze-4.8): the tick by each import folder is "Scan" - unticked folders are skipped
  by scans and keep their episodes (before, the next scan removed them all); right-click a folder: "Scan this
  folder now" (new files in, gone ones out) or "Remove this folder..." (asks first); adding a folder offers to scan
  just it; episodes MyTorrents hands over no longer start a full online update of every show; the remote-share
  scan compares its file lists far faster.

## 1.3.46
- Collections you could make: uses what Moving Pictures already knows (each film's collection), so
  nothing has to be looked up and it doesn't slow the HTPC. Movies tab -> "Your movies" -> "Collections you could
  make...", in MediaPortal Wanted movies -> "Collections you could make...", and the web page "My movies". Shows
  collections where you have 2 or more films not yet in a collection folder, or films that belong to a collection
  folder you already have. Move them (the films' folders go into Collections\[name]\ - Moving Pictures finds them
  at its next scan), Not this one (never suggested again), What's missing (TMDb, through MP-TVSeries - the films
  you don't have can go on Wanted movies). Nothing moves without your OK.
- Sound upgrades: "Stereo-only movies (find 5.1)..." lists the movies Moving Pictures says are stereo
  or mono. "Look for 5.1" puts one on Wanted movies as an upgrade (only surround copies, your movie quality and
  size). When the 5.1 copy is in place the stereo one is deleted - or kept, your choice ("Delete the stereo copy").
  Optional: look for 3 a day by itself (off to start with). Same settings in Configuration, MediaPortal's settings
  menu and the web page.
- TV shows tab tidied (Mark, 4 Nov - "overwhelming"): the tab keeps Quality, Biggest episode, Video type, Sound,
  Minimum seeders and Favourites. The rest is under "More settings...": When nothing good turns up, Whole seasons,
  Smallest episode, Shows searching at once, No copy fits, Skip placeholders, New shows look for.
- "Remove MyTorrents..." is now "Uninstall MyTorrents..."; boxes grow with the window; Automatic / Manual is one
  line at the top of My shows ("How MyTorrents works"); Background tab note fixed; "Waiting for your OK" on its own line.

## 1.3.45
- Background helper ("MyTorrents Configuration.exe /helper", new Background tab): starts with Windows (HKCU Run),
  tray icon, does the Auto grabber, Wanted movies, the Trakt list and copying finished downloads while MediaPortal is
  closed; pauses while MediaPortal runs (WatchList.Paused - only one of them works at a time; saves the list when
  MediaPortal starts, reads it again when it closes). Loads MP-TVSeries and the Trakt plugin's settings itself; no
  pop-ups (MyTorrents.InMediaPortal); log lines start "[helper]". WHY: before, nothing was looked for and finished
  downloads sat in the download folder while MediaPortal was closed.
- Web page (helper, http://<pc>:9118, home network only, optional password - user "mytorrents"): My shows (Check now,
  Grab the missing ones), Wanted movies (add / look now / remove, Read my movies again, new movies waiting for your
  OK), Not found, Downloads (pause / resume / remove), Search (download a copy), Search sites (results and time-outs
  per site from the log, remove / add a Jackett site, Update Jackett - Jackett API, works when Jackett has no admin
  password), Settings (the main ones). "Allow it through Windows Firewall..." adds a Private-network rule.
- Sound preference: Any / Prefer surround / Prefer stereo / Only surround / Only stereo - TV (TV shows tab > Shows and
  episodes) and movies (Movies tab) separately, read from the copy's name; also MediaPortal settings menu.
- Movie favourites (Movies tab; "Films.Favourites" - the TV ones are separate).
- Tabs: TV shows · Movies · My shows · Downloads · When finished · Search sites · Torrent client · <client> ·
  Background · About; opens on the next set-up step (torrent program, search sites, library folders) else TV shows.
- New movies: "File them for me, but show me first" - they wait in the new-movies folder; Movies tab "Waiting for
  your OK...", MediaPortal Wanted movies menu and the web page: file / leave each one (file MyTorrents.movies-to-file.txt).
- Removing a wanted movie also takes it off the Trakt list (MCM) when the list put it there.

## 1.3.44 - no more Media Center Master
- WHY: MCM used to fetch details for new downloads and Mark filed movies from Movies New into Movies / Collections
  by hand ("the less I have to input the more time I have to watch").
- Movies: each new movie is looked up on TMDb (through MP-TVSeries' TMDb client, called by reflection - MyTorrents
  has no key of its own) or, without MP-TVSeries, Trakt. A small .nfo with the IMDb / TMDb id is written next to it
  (Moving Pictures reads the IMDb id - the exact film first time). Movies tab > New movies: "I file them myself"
  (as before) or "File them for me" - into the Movies folder, or Collections\<collection>\<Title (Year)>\ when
  TMDb says the film is part of a collection (an existing collection folder with nearly the same name is used).
  Folders picked from Moving Pictures' folders; also in the MediaPortal settings menu. Filed movies are added to the
  remembered movie list straight away.
- TV: straight after episodes are copied in, they are handed to MP-TVSeries' importer (its List_Add queue, as its
  folder watcher would) so they show up in TV Series within a minute.
- Movies tab: "Save what's not found..." - wanted movies not found yet plus every episode not found yet as a text file on the Desktop (also copied, ready to paste) so Mark can look for them himself.
- Still to come: "file them for me but show me first" (a list to OK).

## 1.3.43
- (Mark, 3 Nov - "I don't want it to bog down"): at most 2 shows search at the same time (all
  of them started together before - sites timed out), Auto grabber searches run at below-normal priority, the
  movies on the NAS are read in the background: Movies New every 30 minutes; the rest of the library (Movies, Collections) once when MediaPortal starts and when you press "Read my movies again" (Wanted movies, both places) - Mark files movies from Movies New himself, the whole way down (Collections too), so screens
  never wait for the NAS; "Shows searching at once" setting (1-4, default 2) - a show you ask for now (Check now,
  Grab the missing ones, Search again, Look now) never waits its turn and runs at normal priority. Rule from now on:
  heavy work (shrinking) only when nothing is playing, at low priority; everything else small and spread out
-: "Can't find" list - episodes and Wanted movies looked for 14 days (setting
  "AutoGrabber.CantFindDays": 3/7/14/30/60) without a copy that fits, with why (nothing found / N found, none fit -
  smallest ...); then looked for once a day. Shows tab "Can't find..." (Look now / Stop looking) and MediaPortal
  Auto grabber menu (also Search all torrents). File MyTorrents.cant-find.txt; episodes you now have drop off.
  Also: no pop-ups from background work (garbled double writing, 3 Nov), dead links -> next best copy
-: MyTorrents Configuration window can be made bigger (drag or maximise) and the
  tabs grow with it, buttons stay at the bottom; it also opens taller when the screen has room (up to 950 high)
- OK / left-click on an Auto grabber show opens its menu ("Show details" first) - right-click is Back in MediaPortal
- Movies: Movies New read every 30 minutes; Movies / Collections at start and on "Read my movies again"
-: Movies tab (first part) - quality, biggest movie, how often wanted movies are
  looked for (1/3/6/12/24 h, also in the MediaPortal settings menu), the Trakt list, and the Wanted movies list on
  the tab (add / look now / remove / Read my movies again, Can't find ones in orange). Movies box gone from the
  Auto grabber tab, "Wanted movies..." button gone from the Shows tab. Still to come on it: sound, shrinking sizes

## 1.3.42
- Your own Trakt list (setting "Trakt.ListName", default MCM; Auto grabber tab > Movies, MediaPortal settings
  menu "My Trakt list"): read every 3 hours with the Trakt plugin's login; movies -> Wanted movies, taken off the
  list once safely in place (Moving Pictures has it, or a whole file 100 MB+ untouched for 10 minutes); movies
  you already have come off straight away; shows -> Auto grabber (whole series, or missing episodes if you have
  some) and come off. Replaces what Media Center Master did with the MCM list. File MyTorrents.trakt-list.txt
- Torrent Search button in the hidden menu of the Trakt plugin's screens (Recommendations, Related, Trending,
  Popular, Anticipated, Watchlist - movies and shows - and list items), Xtreme skin, edited on the HTPC (backups
  *.original): uses #Trakt.Movie.Title/Year, #Trakt.Show.Title/Year, #Trakt.List.ItemType; start parameter
  mytorrents:trakt-movie | trakt-show | trakt-list -> Download now / Add to Wanted movies, Search now / Add to the
  Auto grabber
- "Already have it?" for movies also looks in the movies folder(s) (MyTorrents' movies folder and Moving
  Pictures' import folders; folder and file names, title + year); Download now asks first when you have it
- 1080p if no good 720p (off by default; 10/20/30/50 % over the size limit), episodes and whole seasons
- Fewer seeders over time (on by default): after 24 hours of looking, 1 less every 12 hours, never under 1; back
  to normal for the next episode; file MyTorrents.first-looked.txt
- Stuck downloads (default after 6 hours with no seeds and nothing coming in): the Auto grabber deletes the copy
  and tries another; Downloads tab shows "No seeds - stuck?" in orange
- Downloads tab: "Downloading + Seeding" (the new first view) splits the page - downloading on top, seeding and
  finished underneath; right-click "Delete and search again" (Auto grabber copies)
- Search again: MediaPortal Auto grabber menu on a show "Search again..." (look now / try a different copy of an
  episode got this week - delete or keep the old copy); Configuration Shows tab right-click "Try a different
  copy..."; the episode is wanted again straight away
- Remove MyTorrents... (button in MyTorrents Configuration): ticks for qBittorrent / BiglyBT / Jackett /
  FlareSolverr - only ones the setup installed (recorded in %LOCALAPPDATA%\MyTorrents\installed-by-setup.txt;
  older installs show them with "can't tell" and unticked); keep or remove settings; then opens the Extension
  Manager for MyTorrents itself; never touches downloads or the library
- Also in this version (asked for on 3 Nov): Movies box moved up and "film" -> "movie" everywhere; "How MyTorrents
  works" (Automatic / Manual) at the top of the Auto grabber tab; whole seasons only for finished seasons (last
  episode 2+ days ago) with a set share missing (25/50/75/100 %, default 50), one search then single episodes,
  tried again a day later
- For other skins: the Trakt-screen button is a skin edit (only done on Mark's Xtreme skin)

## 1.3.41
- Wanted films: add a film you want (MediaPortal: Torrent Search in Moving Pictures, or the Auto grabber menu -
  pick the right film from Trakt; Configuration: Shows tab, "Wanted films..."). MediaPortal looks for 3 at a time
  every few hours (film quality and size, enough seeds, no cam / TS copies, favourites and x265 first); films
  you already have are ticked off by themselves; file MyTorrents.wanted-films.txt
- Whole seasons first: when 2 or more episodes of a season are missing, the Auto grabber looks for the whole
  season ("Show S02" / "Show Season 2") for a set time (60 minutes) before getting them one by one; size limit =
  biggest episode x episodes (only the total counts) or a "biggest season" cap; separate favourite season
  groups (e.g. GalaxyTV); episodes already in the library are not copied again from a pack. Settings in the
  Auto grabber tab and the MediaPortal settings menu
- What's missing: MediaPortal Auto grabber menu on a show, and the Shows tab ("Missing" column, right-click
  "What's missing..." / "Grab the missing ones") - season by season, plus what's coming
- Missing episodes report for the whole library: MediaPortal (Torrent Search in TV Series, or the Auto grabber
  menu) and Configuration (Shows tab, "Missing report..."); "Grab the missing ones" switches a show to missing
  episodes (adds it to the Auto grabber if needed); file MyTorrents.missing.txt
- Trakt lists in MediaPortal: films / shows recommended for you, your watchlist, similar to the highlighted
  film or show, trending, popular, most anticipated - things you have are marked; a film -> Download now / Add
  to Wanted films, a show -> Search now / Add to the Auto grabber
- STILL TO DO for these: the Trakt lists in MyTorrents Configuration (it has no Trakt login of its own yet);
  per-episode status (downloading / not aired) in the what's-missing view; unticking episodes you have inside a
  season pack before it downloads (now they are just not copied)

## 1.3.40
- "Torrent Search" button in the left menu of TV Series (search this show / Add a new series) and Moving
  Pictures (search this film / another film) - Xtreme skin; MyTorrents start parameter "mytorrents:tv|film"
- Films: own quality (1080p) and size limit (2.5 GB) - Auto grabber tab and MediaPortal settings menu; film
  searches show the copies that fit first (all copies when none fit); search asks TV / film / anything
- Add a new series: only new episodes / whole series / one season; makes its folder with the <tvdb id>.tvdb
  file; first check straight away
- Automatic or Manual (the "add running shows by themselves" setting, renamed); Manual when never set on a new
  install
- STILL TO DO for these: Add a new series in MyTorrents Configuration; the left-menu buttons for other skins

## 1.3.39
- Naming choices (When finished tab): ready-made styles (incl. Mark's "Show S01E01 Title [720p][2026-08-07]") or
  your own pattern; season folder style for new seasons; live example; title / air date from MP-TVSeries
- BUG fixed: after a download the next episode kept the previous episode's air date
- Shows tab: Airs sorts by calendar even with a list written by an older version

## 1.3.38
- BUG fixed: finished episodes go into the show / season folders you already have (found through MP-TVSeries,
  any library folder, same-named shows kept apart by year); new folders only for a show you have none of
- BUG fixed: "Switch Torrent View" in MediaPortal showed an empty list (the torrent list stayed hidden after the
  details page / folder browser); Seeding now means finished ones, Downloading leaves out paused ones
- "Check now" (one show or all) and right-click "Search again" / "Remove from the Auto grabber" on the Shows tab
- "Removed shows..." list with Put back
- Click column headings to sort (Shows and Downloads tabs; dates and sizes sort properly; remembered)
- Clean up after seeding (When finished tab, on by default; never deletes files inside the library)
- "Configuration" tab renamed "Torrent client"
- Plain log wording ("Episodes you don't have yet" / "Next to get"); shows with nothing announced are checked
  every 3 hours instead of every 30 minutes, with a clearer message

## 1.3.37
- Own settings file MyTorrents.settings.xml (copied from MediaPortal.xml the first time); both programs pick up changes
- "Get SxxEyy now - just this once" (MediaPortal Auto grabber list menu, and the Shows tab); MyTorrents Configuration
  hands the chosen copy to MediaPortal (MyTorrents.requests.txt, checked every 20 seconds)
- Size record MyTorrents.sizes.txt + size report (Shows tab, MediaPortal Auto grabber menu); started from MyTorrents.log
- Smallest episode (default 100 MB) skips fakes / samples; placeholder episodes (E99, no air date) skipped (switchable)
- Removing an unfinished Auto grabber download in MediaPortal asks "Look for another copy?"; retries picked up in 20 s
- Auto grabber list A-Z; "Clear list" asks first
- MediaPortal writes MyTorrents.autograbber.txt (the shows and their next episode) for the Shows tab

## 1.3.33 - 1.3.36
- Downloads tab in MyTorrents Configuration (progress, pause / resume, remove, "try another copy")
- Auto grabber favourites (release groups / sites)
- Running shows in the TV library folder added to the Auto grabber by themselves
- Biggest episode: any size (typed in MediaPortal too)
- Downloads columns can be dragged into any order (remembered in MyTorrents.downloads-columns.txt)
- About tab (the project page link is AboutPage.ProjectPage)

## 1.1 - first 2026 build

Rebuilt from the 2018 build (decompiled), because the old SVN source stops in June 2013 and is missing
Jackett, Trakt, the MP-TVSeries watch list and more.

**Gets it loading again**
- Targets .NET Framework 4.7.2 (same as MP 1.38) and **AnyCPU**. The 2018 `MyTorrents.dll` and
  `AzureusEngine.dll` were built 32-bit-only, so 64-bit MediaPortal can't load them.
- Built against MP 1.38.3 DLLs, MP-TVSeries 4.6 and TraktPlugin 6.0.7; plugin version 1.0.0.
- MPNotificationBar dependency removed (it doesn't exist for MP x64); popups use MP's own notify dialog.
- Trakt "add series to watch list" ported to the current Trakt API (TraktPlugin 6).
- TLS 1.2/1.3 switched on for all web requests.

**New torrent clients**
- **qBittorrent** (Web UI API v2, works with 4.x and 5.x). Labels = qBittorrent categories.
- **BiglyBT** through its *Web Remote* plugin (Transmission RPC). Also works with Transmission itself.
  Labels = BiglyBT tags/Transmission labels.
- Settings dialog now has a drop-down with all five backends, setup hints and default ports.

**Search**
- Jackett address and API key are set in `MyTorrents.xml` (`<jackett>` section) instead of hardcoded `127.0.0.1:9117`.
- Works when Jackett has an admin password (uses the Torznab API with the key).
- New **"Jackett (all indexers)"** engine: searches every indexer in one request.
- Search text is URL-encoded, results sorted (Seeds/Leech/Size/Date/Name), null-safe.
- No more crash when Jackett isn't running or there are no engines.
- Dead hand-written site scrapers (KAT, Mininova, PublicHD, Demonoid…) removed from the default config.

**Bug fixes**
- Download links that redirect to `magnet:` links (common with Jackett) now work.
- Magnet info-hash read properly (base32 hashes, `xt` not first in the link).
- Deluge always connected to localhost:8112, whatever was configured.
- Settings dialog saved Deluge as "azureus".
- Plugin crashed if the log file couldn't be written.
- `CodePage` check in the generic search engine compared object references.
- Leftover Trakt test code removed (`WatchList.GetMovies`).
