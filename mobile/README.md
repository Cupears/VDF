# Cupears VDF mobile

This folder holds the web app built from this fork's VDF.Web project.
Run Start-Mobile.ps1 to start it and connect the Tailscale phone link.

The launcher shows the phone address. Tailscale must be on on both devices.
The desktop shortcut VDF Mobile starts this launcher.

The web app has its own scans and results. It does not control the desktop window.
Settings includes disk limits, drive types, pHash frame agreement, deleted-content memory,
quality rule ordering, and results display preferences. Use Save Settings to keep them.
Results offers cards, a desktop-style list, compact rows, filters, and paged groups.
The comparer can check either copy or check the group and move to the next one.
Login details are stored locally in Phone-login.txt; they are excluded from Git.
Build files, databases and logs are also excluded from Git.

To rebuild from the repository root:

    dotnet publish VDF.Web/VDF.Web.csproj -c Release --no-self-contained -o mobile
