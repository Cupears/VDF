# Cupears VDF mobile

This folder holds the web app built from this fork's VDF.Web project.
Run Start-Mobile.ps1 to start it and connect the Tailscale phone link.

The launcher shows the phone address. Tailscale must be on on both devices.
The desktop shortcut VDF Mobile starts this launcher.

The web app has its own scans and results. It does not control the desktop window.
Start the desktop through scripts/Start-Desktop.ps1 (or the VDF - Cupears Fork shortcut).
Both launchers enable two-way shared scan settings. Changes sync within a few seconds
while the apps are idle; each running scan keeps its settings until it finishes.
The first setup uses desktop settings as the starting point. After that, edits merge
by setting; the latest change to the same setting wins. Folder lists count as one setting.
Database locations, previews, browser appearance, and window layout remain separate.
The local .shared-scan-settings.json and its lock file are excluded from Git.
Settings includes disk limits, drive types, pHash frame agreement, deleted-content memory,
quality rule ordering, and results display preferences. Shared scan choices save automatically;
use Save Settings for web display preferences.
Results offers cards, a desktop-style list, compact rows, filters, and paged groups.
The comparer can check either copy or check the group and move to the next one.
Browse uses one-tap folder navigation, drive labels and free space, breadcrumbs,
quick locations, filtering, and a separate Use this folder action.
Login details are stored locally in Phone-login.txt; they are excluded from Git.
Build files, databases and logs are also excluded from Git.

To rebuild from the repository root:

    dotnet publish VDF.Web/VDF.Web.csproj -c Release --no-self-contained -o mobile
