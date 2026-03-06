# Pi-Hole Tray Controller

A lightweight, unofficial Windows system tray application to control your Pi-Hole ad blocker.
[https://github.com/pi-hole/pi-hole](https://github.com/pi-hole/pi-hole)



![shield_icons](https://img.shields.io/badge/platform-Windows-blue)<br /><br />

<img width="248" height="175" alt="Screenshot 2026-03-05 202400" src="https://github.com/user-attachments/assets/0d3d1d9b-562b-46e3-a174-2ad4a9da0d8f" />

<img width="392" height="187" alt="Screenshot 2026-03-05 202551" src="https://github.com/user-attachments/assets/348065fb-c6d5-41f8-9cfe-c2925b24ee62" /></br>

<img width="329" height="349" alt="Screenshot 2026-03-05 202434" src="https://github.com/user-attachments/assets/ced42504-b3be-46c1-8a5c-286ef4571357" /></br>

<img width="511" height="347" alt="Screenshot 2026-03-05 202622" src="https://github.com/user-attachments/assets/ddbc1262-3196-4df6-be3a-0dd30e90a395" /></br>

## Features

- **Tray icon** reflecting current status: green (active), red (disabled), orange (no connection)
- **Left-click** to toggle blocking on/off
- **Right-click menu** with all options
- **NEW: Multi Pi-Hole support** manage all your pi-holes separately or at once
- **NEW: Star a Pi-Hole instance** to set it as default, showing the status in the tray icon.
- **NEW: The not stared instances show up in the context menu including their status.** If there's only one Pi-Hole the context menu stays the same as before.
- **Temporarily disable** blocking: 5 min, 10 min, 30 min, 1 h, 2 h, 5 h
- **NEW: Block-list** with filters
- **NEW: Unblock queries** from the blocklist via context-menu, temporarily or permanent
- **Modern popup settings** — borderless, two-column layout, positioned above the tray
- **Auto-start** with Windows
- **Multi-language UI** — English, German, Spanish, French, Italian (auto-detected from OS)
- **Pi-Hole v5 and v6** API support

| Setting | Description |
|---|---|
| URL | Pi-Hole address (e.g. `http://192.168.1.2`) |
| Password / API Key | v6: admin password, v5: API token |
| Pi-Hole Version | v5 or v6 |
| Poll Interval | Status check frequency in seconds |
| Auto-start | Launch with Windows |
| Language | UI language (auto-detected or manual) |

## License

MIT
