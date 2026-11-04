// DropAlerts - TShock plugin: rare drop alerts / avisos de drops raros
// Copyright (C) 2026 Riburitu
//
// Based on the tModLoader mod "Drop Alerts (Server-Side)". / Basado en el mod de tModLoader "Drop Alerts (Server-Side)".
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace DropAlerts
{
    /// <summary>
    /// Port a TShock del mod de tModLoader "Drop Alerts": avisa a los jugadores cercanos cuando
    /// alguien consigue un drop raro (probabilidad baja), con 4 niveles de "estrellas".
    /// Comandos: /dropon 1-4, /dropoff 1-4, /showdrops
    /// </summary>
    [ApiVersion(2, 1)]
    public class DropAlertsPlugin : TerrariaPlugin
    {
        public override string Author => "Riburitu";
        public override string Description => "Rare drop alerts with 4 levels, per-player settings (EN/ES) / Avisos de drops raros con 4 niveles, configurables por jugador (EN/ES).";
        public override string Name => "DropAlerts";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public DropAlertsPlugin(Main game) : base(game) { }

        public const string DisplayName = "Drop Alerts";
        public const string StarIcon = "[i/s1:75]";
        public const double MaxPercent = 20.0;

        // ───── Config del servidor ─────
        private class Config
        {
            public bool Enable1StarDropNotice { get; set; } = true;
            public bool Enable2StarDropNotice { get; set; } = true;
            public bool Enable3StarDropNotice { get; set; } = true;
            public bool Enable4StarDropNotice { get; set; } = true;
            public float Percent1Star { get; set; } = 10f;
            public float Percent2Star { get; set; } = 5f;
            public float Percent3Star { get; set; } = 2f;
            public float Percent4Star { get; set; } = 1f;
            // Distancia en píxeles dentro de la cual se avisa (3000 = igual que el mod original).
            public float AlertDistance { get; set; } = 3000f;
            public bool PlaySounds { get; set; } = true;
            public int SoundRepeat { get; set; } = 2;
            // Idioma por defecto: "auto" (usa el idioma del servidor), "en" o "es".
            // Cada jugador puede cambiar el suyo con /droplang.
            public string Language { get; set; } = "auto";
        }

        private static Config cfg = new Config();
        private static string CfgPath => Path.Combine(TShock.SavePath, "DropAlerts.json");
        private static string PlayersPath => Path.Combine(TShock.SavePath, "DropAlertsPlayers.json");

        // Preferencias por jugador: bitmask (bit0 = 1 estrella ... bit3 = 4 estrellas). Por defecto 15 (todo activo).
        private static Dictionary<string, int> prefs = new Dictionary<string, int>();
        private static readonly object prefLock = new object();

        // ───── Idioma / Language ─────
        // Bits 4-5 de la máscara de cada jugador: 0 = usar el del servidor, 1 = English, 2 = Español.
        private const int LangShift = 4;
        private enum Lng { En, Es }
        private static string T(Lng l, string en, string es) => l == Lng.Es ? es : en;

        // Idioma por defecto del servidor: config ("en"/"es") o, en "auto", el idioma activo del juego.
        private static Lng ServerLang()
        {
            string c = (cfg.Language ?? "auto").Trim().ToLowerInvariant();
            if (c == "es" || c == "spanish" || c == "español") return Lng.Es;
            if (c == "en" || c == "english") return Lng.En;
            try
            {
                string name = Terraria.Localization.LanguageManager.Instance.ActiveCulture.Name ?? "";
                if (name.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return Lng.Es;
            }
            catch { }
            return Lng.En;
        }

        // Idioma de un jugador: su preferencia (/droplang) o, si no tiene, la del servidor.
        private static Lng LangOf(TSPlayer p)
        {
            int bits = (GetMask(p) >> LangShift) & 3;
            if (bits == 1) return Lng.En;
            if (bits == 2) return Lng.Es;
            return ServerLang();
        }

        public override void Initialize()
        {
            LoadAll();
            ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
            ServerApi.Hooks.NpcLootDrop.Register(this, OnLootDrop);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
                ServerApi.Hooks.NpcLootDrop.Deregister(this, OnLootDrop);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
            }
            base.Dispose(disposing);
        }

        private void OnGameInitialize(EventArgs args)
        {
            ResolveSounds();
            Commands.ChatCommands.Add(new Command("dropalerts.test", DropSound, "dropsound") { HelpText = "Test DropAlerts sounds / Prueba sonidos de DropAlerts." });
            Commands.ChatCommands.Add(new Command("", (a) => Toggle(a, true), "dropon"));
            Commands.ChatCommands.Add(new Command("", (a) => Toggle(a, false), "dropoff"));
            Commands.ChatCommands.Add(new Command("", ShowDrops, "showdrops"));
            Commands.ChatCommands.Add(new Command("", SetLanguage, "droplang") { HelpText = "/droplang en|es|auto" });
        }

        private void OnGreet(GreetPlayerEventArgs args)
        {
            TSPlayer p = TShock.Players[args.Who];
            if (p == null) return;
            p.SendMessage($"[{DisplayName}] /dropon 1-4, /dropoff 1-4, /showdrops, /droplang en|es", 135, 206, 235);
        }

        // ───── Persistencia ─────
        private static void LoadAll()
        {
            try
            {
                if (File.Exists(CfgPath))
                    cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(CfgPath)) ?? new Config();
                else
                    File.WriteAllText(CfgPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));

                if (File.Exists(PlayersPath))
                    prefs = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(PlayersPath))
                            ?? new Dictionary<string, int>();
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{DisplayName}] Error leyendo configuración: {ex.Message}");
            }
        }

        private static void SavePrefs()
        {
            try
            {
                lock (prefLock)
                    File.WriteAllText(PlayersPath, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{DisplayName}] Error guardando preferencias: {ex.Message}");
            }
        }

        // Cuenta de TShock si está logueado; si no, nombre + IP (como el original).
        private static string KeyOf(TSPlayer p)
            => p.Account != null ? "acc:" + p.Account.Name : $"{p.Name}:{p.IP}";

        private static int GetMask(TSPlayer p)
        {
            lock (prefLock)
                return prefs.TryGetValue(KeyOf(p), out int m) ? m : 15;
        }

        private static bool IsRarityEnabled(int mask, int level) => (mask & (1 << (level - 1))) != 0;

        private static float PercentFor(int level)
        {
            switch (level)
            {
                case 1: return cfg.Percent1Star;
                case 2: return cfg.Percent2Star;
                case 3: return cfg.Percent3Star;
                default: return cfg.Percent4Star;
            }
        }

        // ───── Sonidos por nivel ─────
        // El cliente recibe un índice y hace SoundID.SoundByIndex[índice].
        private static readonly string[] LevelSoundNames = { "BalloonDeath", "Item155", "Item35", "BestReforge" }; // nivel 1..4
        private static readonly ushort[] FallbackSounds = { 256, 244, 244, 305 };                         // los antiguos
        private static readonly ushort[] LevelSoundIds = { 256, 244, 244, 305 };
        private static bool soundsResolved;
        private const float SoundVolume = 1f;

        private static void ResolveSounds()
        {
            if (soundsResolved) return;
            soundsResolved = true;

            try
            {
                if (SoundID.IndexByName == null || SoundID.SoundByIndex == null)
                    SoundID.FillAccessMap();
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{DisplayName}] FillAccessMap falló: {ex.Message}");
            }

            for (int i = 0; i < 4; i++)
            {
                bool found = false;
                try
                {
                    if (SoundID.IndexByName != null && SoundID.IndexByName.TryGetValue(LevelSoundNames[i], out var idx))
                    {
                        LevelSoundIds[i] = Convert.ToUInt16(idx);
                        found = true;
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[{DisplayName}] Error resolviendo {LevelSoundNames[i]}: {ex.Message}");
                }

                if (found)
                    TShock.Log.ConsoleInfo($"[{DisplayName}] Nivel {i + 1}: {LevelSoundNames[i]} -> índice {LevelSoundIds[i]}");
                else
                {
                    LevelSoundIds[i] = FallbackSounds[i];
                    TShock.Log.ConsoleError($"[{DisplayName}] {LevelSoundNames[i]} no encontrado; usando índice antiguo {FallbackSounds[i]}.");
                }
            }
        }

        // ───── /dropsound: comando de prueba mejorado ─────
        // /dropsound <nombre|índice>      -> suena para ti y te dice los detalles
        // /dropsound nivel <1-4>          -> suena el sonido configurado para ese nivel
        // /dropsound list <texto>         -> lista nombres que contengan el texto
        private static void DropSound(CommandArgs args)
        {
            TSPlayer me = args.Player;
            Lng L = LangOf(me);
            if (args.Parameters.Count < 1)
            {
                me.SendInfoMessage(T(L,
                    "Usage: /dropsound <name|index> | level <1-4> | list <text>",
                    "Uso: /dropsound <nombre|índice> | nivel <1-4> | list <texto>"));
                return;
            }

            ResolveSounds();
            string first = args.Parameters[0];

            if (first.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                string filter = args.Parameters.Count > 1 ? args.Parameters[1] : "";
                var matches = SoundID.IndexByName == null
                    ? new List<string>()
                    : SoundID.IndexByName
                        .Where(kv => kv.Key.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                        .Take(25)
                        .Select(kv => $"{kv.Key}={kv.Value}")
                        .ToList();
                me.SendInfoMessage(matches.Count == 0 ? T(L, "No results.", "Sin resultados.") : string.Join(", ", matches) + (matches.Count == 25 ? " ..." : ""));
                return;
            }

            ushort index;
            string label;
            string internalName = T(L, "Unknown", "Desconocido");

            if (first.Equals("nivel", StringComparison.OrdinalIgnoreCase) || first.Equals("level", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Parameters.Count < 2 || !int.TryParse(args.Parameters[1], out int lvl) || lvl < 1 || lvl > 4)
                {
                    me.SendErrorMessage(T(L, "Usage: /dropsound level <1-4>", "Uso: /dropsound nivel <1-4>"));
                    return;
                }
                index = LevelSoundIds[lvl - 1];
                internalName = LevelSoundNames[lvl - 1];
                label = T(L, $"level {lvl} ({internalName}) with index {index}", $"nivel {lvl} ({internalName}) con índice {index}");
            }
            else if (ushort.TryParse(first, out ushort num))
            {
                index = num;
                // Búsqueda inversa: encontrar el nombre del string a partir del número de red caótico
                if (SoundID.IndexByName != null)
                {
                    var match = SoundID.IndexByName.FirstOrDefault(kv => Convert.ToUInt16(kv.Value) == index);
                    if (match.Key != null)
                    {
                        internalName = match.Key;
                    }
                }
                label = T(L, $"index {num} (Internal name: [c/FFD700:{internalName}])", $"índice {num} (Nombre interno: [c/FFD700:{internalName}])");
            }
            else
            {
                string key = SoundID.IndexByName?.Keys.FirstOrDefault(k => k.Equals(first, StringComparison.OrdinalIgnoreCase));
                if (key == null)
                {
                    me.SendErrorMessage(T(L, $"'{first}' does not exist. Try /dropsound list <text>.", $"No existe '{first}'. Prueba /dropsound list <texto>."));
                    return;
                }
                index = Convert.ToUInt16(SoundID.IndexByName[key]);
                internalName = key;
                label = T(L, $"[c/FFD700:{key}] (network index: {index})", $"[c/FFD700:{key}] (índice de red: {index})");
            }

            NetMessage.PlayNetSound(
                new NetMessage.NetSoundInfo(me.TPlayer.position, index, -1, SoundVolume, -1f),
                me.Index, -1);
            
            me.SendSuccessMessage(T(L, $"Playing: {label}", $"Reproduciendo: {label}"));
        }

        // ───── Comandos ─────
        private static void Toggle(CommandArgs args, bool enable)
        {
            Lng L = LangOf(args.Player);
            string cmd = enable ? "dropon" : "dropoff";
            if (args.Parameters.Count < 1 || !int.TryParse(args.Parameters[0], out int level) || level < 1 || level > 4)
            {
                string bad = args.Parameters.Count > 0 ? args.Parameters[0] : "";
                args.Player.SendErrorMessage(T(L, $"{bad} is not a valid option. Use /{cmd} 1/2/3/4", $"{bad} no es una opción válida. Usa /{cmd} 1/2/3/4"));
                return;
            }

            lock (prefLock)
            {
                string key = KeyOf(args.Player);
                int mask = prefs.TryGetValue(key, out int m) ? m : 15;
                mask = enable ? (mask | (1 << (level - 1))) : (mask & ~(1 << (level - 1)));
                prefs[key] = mask;
            }
            SavePrefs();

            string stars = string.Concat(Enumerable.Repeat(StarIcon, level));
            args.Player.SendMessage($"[c/FFF014:<{DisplayName}>] Drops {stars} " + (enable ? T(L, "enabled", "activados") : T(L, "disabled", "desactivados")), 255, 255, 255);
        }

        // /droplang [en|es|auto]
        private static void SetLanguage(CommandArgs args)
        {
            TSPlayer p = args.Player;
            Lng cur = LangOf(p);
            if (args.Parameters.Count < 1)
            {
                p.SendInfoMessage(T(cur,
                    "Current language: English. Use /droplang en|es|auto",
                    "Idioma actual: Español. Usa /droplang en|es|auto"));
                return;
            }

            int bits;
            switch (args.Parameters[0].ToLowerInvariant())
            {
                case "en": case "english": case "ingles": case "inglés": bits = 1; break;
                case "es": case "spanish": case "español": case "espanol": bits = 2; break;
                case "auto": bits = 0; break;
                default:
                    p.SendErrorMessage(T(cur,
                        $"{args.Parameters[0]} is not a valid language. Use /droplang en|es|auto",
                        $"{args.Parameters[0]} no es un idioma válido. Usa /droplang en|es|auto"));
                    return;
            }

            lock (prefLock)
            {
                string key = KeyOf(p);
                int mask = prefs.TryGetValue(key, out int m) ? m : 15;
                mask = (mask & ~(3 << LangShift)) | (bits << LangShift);
                prefs[key] = mask;
            }
            SavePrefs();

            Lng now = LangOf(p);
            p.SendSuccessMessage(T(now, "Language set to English", "Idioma cambiado a Español") + (bits == 0 ? " (auto)" : ""));
        }

        private static void ShowDrops(CommandArgs args)
        {
            Lng L = LangOf(args.Player);
            int mask = GetMask(args.Player);
            args.Player.SendMessage($"{DisplayName}:", 255, 240, 20);
            for (int i = 1; i <= 4; i++)
            {
                string line = string.Concat(Enumerable.Repeat("[i:97]", 4 - i)) + string.Concat(Enumerable.Repeat(StarIcon, i));
                line += IsRarityEnabled(mask, i)
                    ? $" [i:546][c/FFFFFF:{T(L, "Enabled", "Activado")}]"
                    : $" [i:97][c/FFFFFF:{T(L, "Disabled", "Desactivado")}]";
                line += $" ({PercentFor(i)}%)";
                args.Player.SendMessage(line, 255, 240, 20);
            }
        }

        // ───── Drop de objetos ─────
        private static void OnLootDrop(NpcLootDropEventArgs e)
        {
            if (e.Handled) return;
            try
            {
                int idx = e.NpcArrayIndex;
                if (idx < 0 || idx >= Main.maxNPCs) return;
                NPC npc = Main.npc[idx];
                if (npc == null) return;

                int closest = Player.FindClosest(npc.position, npc.width, npc.height);
                if (closest < 0 || closest >= Main.maxPlayers) return;
                Player killer = Main.player[closest];
                if (killer == null || !killer.active) return;

                double? chanceOpt = FindDropChance(npc, killer, e.ItemId);
                if (chanceOpt == null) return;
                double chance = Math.Round(chanceOpt.Value, 3);
                if (chance > MaxPercent) return;

                int level;
                byte r, g, b;
                string star = StarIcon;

                if (chance <= cfg.Percent4Star && cfg.Enable4StarDropNotice)
                { level = 4; r = 255; g = 80; b = 80; star += star; }
                else if (chance <= cfg.Percent3Star && cfg.Enable3StarDropNotice)
                { level = 3; r = 175; g = 75; b = 255; star += star; }
                else if (chance <= cfg.Percent2Star && cfg.Enable2StarDropNotice)
                { level = 2; r = 255; g = 0; b = 160; }
                else if (chance <= cfg.Percent1Star && cfg.Enable1StarDropNotice)
                { level = 1; r = 255; g = 240; b = 20; }
                else return;

                string itemName = Lang.GetItemNameValue(e.ItemId);
                string msgEn = $"{star} Lucky {star} [c/FFFFFF:{killer.name} drops: {itemName}] [i:{e.ItemId}] ({chance}%)";
                string msgEs = $"{star} ¡Suerte! {star} [c/FFFFFF:{killer.name} consigue: {itemName}] [i:{e.ItemId}] ({chance}%)";

                foreach (TSPlayer p in TShock.Players)
                {
                    if (p == null || !p.Active || p.TPlayer == null) continue;
                    if (!IsRarityEnabled(GetMask(p), level)) continue;

                    float dx = p.TPlayer.position.X - killer.position.X;
                    float dy = p.TPlayer.position.Y - killer.position.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) >= cfg.AlertDistance) continue;

                    p.SendMessage(LangOf(p) == Lng.Es ? msgEs : msgEn, r, g, b);
                    if (cfg.PlaySounds)
                    {
                        int reps = Math.Max(1, Math.Min(5, cfg.SoundRepeat));
                        for (int k = 0; k < reps; k++)
                            NetMessage.PlayNetSound(
                                new NetMessage.NetSoundInfo(p.TPlayer.position, LevelSoundIds[level - 1], -1, SoundVolume, -1f),
                                p.Index, -1);
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[{DisplayName}] error: {ex}");
            }
        }

        /// <summary>
        /// Devuelve la probabilidad (en %) con la que este NPC suelta el objeto indicado, o null si no
        /// aparece en su tabla de drops. Usa ReportDroprates (la misma API que usa el bestiario del juego),
        /// que recorre TODOS los tipos de regla: DropBasedOnExpertMode, DropBasedOnMasterAndExpertMode,
        /// OneFromOptions, cadenas, etc. Recorrer solo ChainedRules no entra en las reglas que guardan sus
        /// subreglas en campos propios (p. ej. NormalvsExpert, que es lo que usa el Caos elemental).
        /// Si hay varias entradas del mismo objeto, se queda con la menor probabilidad aplicable.
        /// </summary>
        private static double? FindDropChance(NPC npc, Player killer, int itemId)
        {
            var info = new DropAttemptInfo
            {
                npc = npc,
                player = killer,
                rng = Main.rand,
                IsExpertMode = Main.expertMode,
                IsMasterMode = Main.masterMode,
                IsInSimulation = false
            };

            var rates = new List<DropRateInfo>();
            var feed = new DropRateInfoChainFeed(1f);
            foreach (var rule in Main.ItemDropsDB.GetRulesForNPCID(npc.netID, true))
            {
                if (rule == null) continue;
                try { rule.ReportDroprates(rates, feed); }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"[{DisplayName}] ReportDroprates falló en {rule.GetType().Name}: {ex.Message}");
                }
            }

            double? best = null;
            foreach (var r in rates)
            {
                if (r.itemId != itemId) continue;

                // Descarta entradas cuyas condiciones no se cumplen ahora (modo experto/maestro, etc.).
                bool ok = true;
                if (r.conditions != null)
                {
                    foreach (var cond in r.conditions)
                    {
                        if (cond != null && !cond.CanDrop(info)) { ok = false; break; }
                    }
                }
                if (!ok) continue;

                double c = (double)r.dropRate * 100.0;
                if (c <= 0) continue;
                if (best == null || c < best.Value) best = c;
            }

            return best;
        }
    }
}