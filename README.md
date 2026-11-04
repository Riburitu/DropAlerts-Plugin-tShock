# DropAlerts

**English** · **[Español](#español)**

## English
TShock plugin that warns nearby players when someone gets a **rare drop**, using four "star" levels. It is a port of the tModLoader mod *Drop Alerts* to TShock.

- **Compatible with:** Terraria 1.4.5.8 · TShock 6.2.1 (.NET 9) · plugin API 2.1
- **Status:** finished and tested by the author on Terraria 1.4.5.8 / TShock 6.2.1. Compiled `DropAlerts.dll` files are published on the project's *Releases* page.
- **License:** [GNU GPL v3](LICENSE) — you may use, modify and share it, but any modified version you distribute must also be open source under the same license, so improvements come back to everyone.

## Installation
1. Download `DropAlerts.dll` from *Releases*.
2. Copy it to your server's `ServerPlugins` folder.
3. Restart the server. On first start `DropAlerts.json` and `DropAlertsPlayers.json` are created in the `tshock` folder.

## Permissions
- `dropalerts.test`: Required to use the `/dropsound` administrative command.

## Commands

| Command | Permission | What it does |
|---|---|---|
| `/dropon <1-4>` | *None* | Turn on alerts for that star level (for you only). |
| `/dropoff <1-4>` | *None* | Turn off alerts for that star level (for you only). |
| `/showdrops` | *None* | Show which levels are on or off for you and their percentages. |
| `/droplang <en/es>` | *None* | Choose the language in which the plugin will be displayed to the player. |
| `/dropsound <args>` | `dropalerts.test` | Administrative command to test and preview drop sounds. |

Each player's choice is saved, so it persists between sessions.

## Configuration (`tshock/DropAlerts.json`)

| Option | Default | Meaning |
|---|---|---|
| `Enable1StarDropNotice` … `Enable4StarDropNotice` | `true` | Enable each level server-wide. |
| `Percent1Star` / `2Star` / `3Star` / `4Star` | `10` / `5` / `2` / `1` | Drop-chance threshold (%) of each level. |
| `AlertDistance` | `3000` | Distance in pixels within which players are warned. |
| `PlaySounds` | `true` | Play a sound with the alert. |

## Building from source
Install the .NET 9 SDK, create a `References` folder and copy `TShockAPI.dll`, `OTAPI.dll`, `TerrariaServer.dll` and `ModFramework.dll` from your TShock into it (do not redistribute them), then run `Compilar.bat` or `dotnet build -c Release`.

## Credits
- Author: **Riburitu**.
- Based on the tModLoader mod **Drop Alerts (Server Side)** and its original author's [Narnia](https://github.com/xNarnia/DropAlerts/tree/main).

---

## Español

Plugin de TShock que avisa a los jugadores cercanos cuando alguien consigue un **drop raro**, con cuatro niveles de «estrellas». Es una adaptación a TShock del mod de tModLoader *Drop Alerts*.

- **Compatible con:** Terraria 1.4.5.8 · TShock 6.2.1 (.NET 9) · API de plugins 2.1
- **Estado:** terminado y probado por el autor en Terraria 1.4.5.8 / TShock 6.2.1. Los `DropAlerts.dll` ya compilados se publican en la página de *Releases* del proyecto.
- **Licencia:** [GNU GPL v3](LICENSE) — puede usarlo, modificarlo y compartirlo, pero toda versión modificada que distribuya debe seguir siendo de código abierto con la misma licencia, de modo que las mejoras vuelvan a todos.

## Instalación
1. Descargue `DropAlerts.dll` desde *Releases*.
2. Cópielo a la carpeta `ServerPlugins` de su servidor.
3. Reinicie el servidor. En el primer arranque se crean `DropAlerts.json` y `DropAlertsPlayers.json` en la carpeta `tshock`.

## Permisos
- `dropalerts.test`: Requerido para utilizar el comando administrativo `/dropsound`.

## Comandos

| Comando | Permiso | Qué hace |
|---|---|---|
| `/dropon <1-4>` | *Ninguno* | Activa los avisos de ese nivel de estrellas (solo para usted). |
| `/dropoff <1-4>` | *Ninguno* | Desactiva los avisos de ese nivel (solo para usted). |
| `/showdrops` | *Ninguno* | Muestra qué niveles tiene activados o desactivados y sus porcentajes. |
| `/droplang <en/es>` | *Ninguno* | Elige el lenguaje que se mostrara el plugin con el jugador. |
| `/dropsound <args>` | `dropalerts.test` | Comando administrativo para probar y reproducir los sonidos de los avisos. |

La elección de cada jugador se guarda y se mantiene entre sesiones.

## Configuración (`tshock/DropAlerts.json`)

| Opción | Por defecto | Significado |
|---|---|---|
| `Enable1StarDropNotice` … `Enable4StarDropNotice` | `true` | Activa cada level para todo el servidor. |
| `Percent1Star` / `2Star` / `3Star` / `4Star` | `10` / `5` / `2` / `1` | Umbral de probabilidad (%) de cada nivel. |
| `AlertDistance` | `3000` | Distancia en píxeles a la que se avisa a los jugadores. |
| `PlaySounds` | `true` | Reproduce un sonido con el aviso. |

## Compilar desde el código fuente
Instale el SDK de .NET 9, cree una carpeta `References` y copie en ella `TShockAPI.dll`, `OTAPI.dll`, `TerrariaServer.dll` y `ModFramework.dll` de su TShock (no las redistribuya); después ejecute `Compilar.bat` o `dotnet build -c Release`.

## Créditos
- Autor: **Riburitu**.
- Basado en el mod de tModLoader **Drop Alerts (Server Side)** y en su autor original [Narnia](https://github.com/xNarnia/DropAlerts/tree/main).
