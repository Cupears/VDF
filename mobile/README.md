# Cupears VDF mobile

This folder holds the phone's web assets and launcher. The native desktop loads
the web server code from its own application folder; a separate mobile executable
and duplicate FFmpeg/AI files are not needed here.
Run Start-Mobile.ps1 to start it and connect the Tailscale phone link.
For double-click launching, use Start VDF Mobile.cmd in the repository root.
Start VDF.cmd opens the native desktop GUI. These launch PowerShell directly, so
Windows does not need to associate .ps1 files with PowerShell.

The launcher shows the phone address. Tailscale must be on on both devices.
The desktop shortcut VDF Mobile starts this launcher.

The desktop shortcut opens the native VDF GUI. That desktop process also hosts the
mobile web interface using the very same ScanEngine and loaded database.
There is one database writer, not two databases copying changes between each other.
Start VDF.cmd or Start VDF Mobile.cmd opens/reuses the desktop and its mobile server.
Keep VDF open on the PC while using the phone. Closing the desktop stops mobile too.
Desktop and phone share live scan progress, scan settings, results and checked files.
Phone start/pause/resume/stop actions go through the desktop controls.
This PC keeps its existing database and AI cache files in VDF.GUI/bin/Debug/net10.0.
The verified backup is in Documents/VDF Backups.
A database lock prevents a second standalone VDF process from opening the same library.
Browser appearance and list filters can differ between devices; checked files are shared.
Delete confirmation holds a fixed list so checks changed on another device do not
silently add files to an already-open delete confirmation.
The earlier settings exchange remains available for the standalone GUI. Changes
merge by setting; the latest change to the same setting wins. Folder lists are one setting.
The local .shared-scan-settings.json and its lock file are excluded from Git.
Settings includes disk limits, drive types, pHash frame agreement, deleted-content memory,
quality rule ordering, and results display preferences. Shared scan choices save automatically;
use Save Settings for web display preferences.
Results offers cards, a desktop-style list, compact rows, filters, and paged groups.
The comparer can check either copy or check the group and move to the next one.
Browse uses one-tap folder navigation, drive labels and free space, breadcrumbs,
quick locations, filtering, and a separate Use this folder action.
Login details are stored locally in Phone-login.txt; they are excluded from Git.
The Log tab on mobile shows the same live activity messages as the desktop.
Long tasks report every 10 seconds, including the latest file/folder and counters.
After 30 seconds with no reported work change, a warning explains that VDF may be
waiting on a file, drive, network share, or decoder. It does not claim a proven hang.
Folder listing shows visited folders, checked entries and found media before its total is known.
HDD scans find moved-file candidates by cached size and fingerprint in memory before
checking old paths. New fingerprints are kept for the analysis step, and files below
the fingerprint's 64 KiB minimum are not opened just to attempt that fingerprint.
The live log includes an HDD cache reuse summary. The existing database format stays the same.
Build files, databases and logs are also excluded from Git.

To rebuild from the repository root (close VDF first):

    dotnet publish VDF.Web/VDF.Web.csproj -c Release --no-self-contained -o mobile
    dotnet build VDF.GUI/VDF.GUI.csproj
