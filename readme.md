# Pi-Hole Tray Controller

A lightweight Windows system tray application to control your Pi-Hole ad blocker.

![shield_icons](https://img.shields.io/badge/platform-Windows-blue)<br /><br />

<img width="154" height="35" alt="Screenshot 2026-03-03 123508" src="https://github.com/user-attachments/assets/10afb874-a6ed-4609-b7d6-23d952923432" /><br />

<img width="242" height="113" alt="Screenshot 2026-03-03 123644" src="https://github.com/user-attachments/assets/fe30a0e1-f5f1-4a32-b753-ebb9e33f39b3" /><br />


<img width="319" height="277" alt="Screenshot 2026-03-03 123547" src="https://github.com/user-attachments/assets/815f5a45-af71-48e1-8995-5e0f25be75a6" /><br />


## Features

- **Tray icon** reflecting current status: green (active), red (disabled), orange (no connection)
- **Left-click** to toggle blocking on/off
- **Right-click menu** with all options
- **Temporarily disable** blocking: 5 min, 10 min, 30 min, 1 h, 2 h, 5 h
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
