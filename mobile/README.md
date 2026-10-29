# Cupears VDF mobile

This folder holds the web app built from this fork's VDF.Web project.
Run Start-Mobile.ps1 to start it and connect the Tailscale phone link.

The launcher shows the phone address. Tailscale must be on on both devices.
The desktop shortcut VDF Mobile starts this launcher.

The desktop shortcut now opens this same web interface on the PC. Both devices use
the one VDF.Web server, one scan engine, and its database. Progress, results, and
checked files are shared. Starting either shortcut reuses the running server.
Use scripts/Start-Desktop.ps1 (or VDF - Cupears Fork) for desktop access.
Use VDF Mobile and the Tailscale phone address for phone access.
The old native GUI and its database are preserved separately. That older cache has
not been merged into the shared web database. Opening VDF.GUI.exe directly starts
the old standalone interface, so use the desktop shortcut for the shared session.
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
Build files, databases and logs are also excluded from Git.

To rebuild from the repository root:

    dotnet publish VDF.Web/VDF.Web.csproj -c Release --no-self-contained -o mobile
