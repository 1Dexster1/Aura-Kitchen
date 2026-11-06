# Session State - Aura Kitchen

## Current Objective
Connected the game client (Web and Unity) directly to the user's remote Discord bot host endpoint `panel.sharebotdex.xyz:30015` with Colyseus multiplayer, fallback handling, and dynamic server configuration.

## Recent Changes
- **Backend Configuration (`server/`):**
  - Configured default port in `server/server.js` to `30015` (`process.env.PORT || 30015`).
  - Set entry point to `index.js` in `package.json` for 1-click startup on Pterodactyl/Discord bot host panels.
- **Game Client Connection:**
  - Updated `index.html` and `web/index.html` with default Colyseus endpoint `panel.sharebotdex.xyz:30015`.
  - Added live Backend status bar in the Online Lobby with an "Edit" button to allow changing the server address anytime without editing code.
  - Implemented graceful connection handling (waiting indicator `Connecting to panel.sharebotdex.xyz:30015... ⏳` if the server is offline or still starting up).
- **Unity C# Client:**
  - Configured default `serverUrl` in `AuraWebSocketClient.cs` to `ws://panel.sharebotdex.xyz:30015`.

## Next Action
- Guide the user on uploading the `server/` files to their bot panel and pressing Start.
- Confirm ready state for live multiplayer play.

## Key Decisions & Constraints
- Server URL default set to `panel.sharebotdex.xyz:30015`.
- Client gracefully handles server startup delays so the user can test the UI and offline gameplay even before the remote bot host is powered on.
