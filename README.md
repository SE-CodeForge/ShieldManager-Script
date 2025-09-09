# ShieldManager

ShieldManager is an advanced programmable block script for Space Engineers that automatically manages Defense Shields shunting based on incoming threats. It integrates with WeaponCore and Defense Shields APIs to provide adaptive, directional shield reinforcement for your ship.

## Features

- **Automatic Threat Detection:** Uses WeaponCore API to detect and track the closest threats.
- **Directional Shield Shunting:** Dynamically redirects shield power to the side facing the threat (front, back, left, right, top, bottom).
- **Defense Shields Integration:** Works with the Defense Shields mod, including Side Redirect and per-face shunt actions.
- **Manual and Auto Modes:** Supports both automatic shunting and manual override via script commands.
- **LCD Status Display:** Shows shield status, shunt mode, and threat info on configured LCD panels.
- **Optimized Performance:** Fast response with minimal overhead; only applies shunt changes when needed.

## Requirements

- [WeaponCore](https://steamcommunity.com/sharedfiles/filedetails/?id=1918681825)
- [Defense Shields](https://steamcommunity.com/sharedfiles/filedetails/?id=1365616918)

## Setup

1. **Install the required mods** (see above).
2. **Add a programmable block** to your ship and upload the ShieldManager script.
3. **Add an LCD panel** (optional) and name it to include `ShieldManager` for status display.
4. **Configure Defense Shields:**
   - Make sure "Side Redirect" and "Show Redirect" are enabled in the shield controller terminal.
5. **Run the script** in your programmable block.

## Commands

- `auto` — Enable automatic threat-based shunting (default).
- `manual` — Switch to manual shunt mode.
- `front`, `back`, `left`, `right`, `top`, `bottom` — Manually shunt to a specific face.
- `balanced` — Evenly distribute shield power.
- `debug` — Toggle debug output for troubleshooting.

## How It Works

- The script polls WeaponCore for threats every few ticks.
- It calculates the direction of the closest threat relative to your ship.
- It applies the appropriate shunt actions to the Defense Shields controller, reinforcing the shield face facing the threat.
- If no threats are detected, it returns shields to balanced mode.

## Troubleshooting

- **Shields not shunting?**  
  - Make sure "Side Redirect" is enabled in your Defense Shields controller.
  - Ensure the programmable block and shield controller are on the same grid.
  - Check that the script is running and not throwing errors.
- **Debugging:**  
  - Use the `debug` command to see detailed output in the programmable block's terminal.

## License

MIT License

---

**Enjoy automated, intelligent shield management for your Space Engineers
