# AkashaNavigator User Guide

AkashaNavigator is a Windows floating web player designed for watching tutorial/guide videos while gaming.

## Table of Contents

- [Installation & Launch](#installation--launch)
- [Basic Operations](#basic-operations)
- [Hotkeys](#hotkeys)
- [Plugin Management](#plugin-management)
- [Profile Configuration](#profile-configuration)
- [Archive Management](#archive-management)
- [FAQ](#faq)

---

## Installation & Launch

### System Requirements

- Windows 10/11 64-bit
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/latest/runtime)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (usually pre-installed on Windows 10/11)

### Installation Steps

1. Download the latest version from [GitHub Releases](https://github.com/ColinXHL/akasha-navigator/releases)
2. Extract to any directory
3. Run `AkashaNavigator.exe` as administrator

> ⚠️ **Why administrator privileges?**
> Games usually run with administrator privileges. The software needs the same privileges to use global hotkeys in games.

### Data Storage

All data is stored in the `User/` folder relative to the executable:

```
AkashaNavigator/
├── AkashaNavigator.exe
└── User/
    ├── Data/           # History, bookmarks
    ├── Profiles/       # Profile configurations
    ├── Plugins/        # Plugins
    └── WebView2/       # Browser data (cookies, etc.)
```

---

## Basic Operations

### Player Window

- **Drag Window**: Hold and drag window edges
- **Resize**: Drag window corners or edges
- **Edge Snapping**: Window automatically snaps when near screen edges

### Control Bar

Move mouse to the top of the screen to show the control bar:

- **Address Bar**: Enter URL or search
- **Navigation Buttons**: Back, forward, refresh
- **Bookmark Button**: Bookmark current page
- **Menu Button**: Open settings, history, etc.

### Opacity Adjustment

- Use hotkeys `7` / `8` to adjust opacity (20% - 100%)
- Or adjust in settings

### Click-Through Mode

Press `0` to toggle click-through mode:

- **Enabled**: Mouse clicks pass through the player to interact with the game below
- **Disabled**: Normal interaction with the player

---

## Hotkeys

### Default Hotkeys

| Key | Function |
|-----|----------|
| `` ` `` | Play / Pause |
| `5` | Seek Backward (5s) |
| `6` | Seek Forward (5s) |
| `7` | Decrease Opacity |
| `8` | Increase Opacity |
| `0` | Toggle Click-Through |

### Custom Hotkeys

1. Open Settings window
2. Select "Hotkeys" tab
3. Click the hotkey you want to modify and press the new key combination
4. Modifier keys supported: Ctrl, Alt, Shift + any key

---

## Plugin Management

### Install Plugins

1. Open "Plugin Center" (Menu → Plugin Center)
2. Switch to "Available Plugins" tab
3. Refresh the plugin repository when needed, then click "Install"

Plugin Center is backed by the
[AkashaPlugins](https://github.com/ColinXHL/akasha-plugins) catalog.
Installing from this page also creates a subscription. The catalog is the
only authority for plugin versions, packages, and updates.

### Check and Install Updates

The Installed Plugins page, the startup update prompt, and Plugin Center all
use the same subscriptions and catalog, so they report the same updates. The
repository settings provide two independent options:

- Automatically update the plugin repository: refresh the catalog only
- Automatically update subscribed plugins: install newer catalog versions

Application updates are separate from plugin updates. The application update
manifest does not publish plugin versions.

### Enable/Disable Plugins

1. Find the plugin in "Installed" tab
2. Click the toggle switch to enable/disable

### Plugin Settings

Some plugins provide configurable options:

1. Click "Settings" button on the plugin card
2. Adjust configuration (auto-saved)

### Manual Plugin Installation

Use "Install from ZIP" on the Available Plugins page to import a local package.
An arbitrary ZIP is not treated as an official subscription. Install or
subscribe to its catalog entry if you want catalog-based updates.

---

## Profile Configuration

Profiles are game-specific configuration schemes, including window position, hotkeys, plugins, etc.

### Switch Profile

1. Click the Profile button in the control bar
2. Select target profile

### Create Profile

1. Open "Plugin Center" → "Profile" tab
2. Click "Create Profile"
3. Fill in name and description
4. Select plugins to associate

### Install Profile from Marketplace

1. Open "Plugin Center" → "Profile" tab
2. Browse available profiles
3. Click "Install"

---

## Archive Management

The archive feature allows you to save and organize important web content using a tree structure with folder organization.

### Create Archive

1. Click the "Archive" button in the control bar
2. Enter archive title and URL
3. Select target folder (optional)
4. Click "OK" to save

### Manage Archives

Open the Archive window (Menu → Archive Management):

- **New Folder**: Click "New Folder" button to create a folder
- **Search Archives**: Enter keywords in the search box (searches titles and URLs)
- **Sort**: Click sort button to toggle time sorting (newest/oldest)
- **Open Archive**: Double-click an archive item to open the corresponding webpage

### Edit and Move

Right-click on an archive item or folder:

- **Edit**: Modify title and URL
- **Move to...**: Move archive item to another folder
- **Delete**: Delete archive item or folder (deleting a folder removes all its contents)
- **New Subfolder**: Create a subfolder within a folder

### Data Storage

Archive data is stored in the current Profile's `archives.json` file. Switching profiles displays the corresponding archive content.

---

## FAQ

### Q: Hotkeys don't work in games?

Make sure to run AkashaNavigator as administrator.

### Q: Videos won't play?

1. Check network connection
2. Try refreshing the page
3. Ensure WebView2 Runtime is installed

### Q: How to stay logged in to Bilibili?

AkashaNavigator automatically saves cookies. After logging in normally, you'll stay logged in on next launch.

### Q: Window position not saved?

Window position is automatically saved on close. Abnormal exit may cause position loss.

### Q: Plugins not working?

1. Check if plugin is enabled
2. Check if required permissions are granted
3. View log window (Menu → Logs) to troubleshoot errors

---

## Related Documentation

- [Plugin Development Guide](plugin-development.md)
- [Plugin API Reference](api/README.md)
- [Profile Creation Guide](profile-guide.md)
