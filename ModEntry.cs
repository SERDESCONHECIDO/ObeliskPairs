using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework;
using xTile.Dimensions;

namespace ObeliskPairs
{
    public class ModData
    {
        public List<string> Unpaired { get; set; } = new();
        public Dictionary<string, string> Pairs { get; set; } = new();
    }

    public class ModEntry : Mod
    {
        public static ModEntry Instance { get; private set; }
        public ModData Data { get; private set; }
        
        // O ID do Mini-Obelisco verdadeiro do jogo
        private const string MiniObeliskId = "(BC)238";

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Data = new ModData();

            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Data = this.Helper.Data.ReadSaveData<ModData>("obelisk-data") ?? new ModData();
            ScanLocation(Game1.currentLocation);
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer) ScanLocation(e.NewLocation);
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            if (e.Removed.Any(r => IsMiniObelisk(r.Value)))
            {
                ScanLocation(e.Location);
            }
        }

        public void ScanLocation(GameLocation loc)
        {
            if (loc == null) return;
            bool changed = false;
            
            var obelisks = loc.Objects.Pairs
                .Where(p => IsMiniObelisk(p.Value))
                .OrderBy(p => p.Key.Y)
                .ThenBy(p => p.Key.X)
                .ToList();

            foreach (var obj in obelisks)
            {
                string key = $"{loc.NameOrUniqueName}|{(int)obj.Key.X}|{(int)obj.Key.Y}";
                if (!Data.Pairs.ContainsKey(key) && !Data.Unpaired.Contains(key))
                {
                    if (RegisterNew(key, false)) changed = true;
                }
            }
            
            var currentKeys = obelisks.Select(o => $"{loc.NameOrUniqueName}|{(int)o.Key.X}|{(int)o.Key.Y}").ToHashSet();
            var deadKeys = Data.Unpaired.Where(k => k.StartsWith(loc.NameOrUniqueName + "|") && !currentKeys.Contains(k)).ToList();
            foreach (var k in deadKeys) { Data.Unpaired.Remove(k); changed = true; }
            
            var deadPairs = Data.Pairs.Keys.Where(k => k.StartsWith(loc.NameOrUniqueName + "|") && !currentKeys.Contains(k)).ToList();
            foreach (var k in deadPairs) { Unregister(k); changed = true; }

            if (changed) this.Helper.Data.WriteSaveData("obelisk-data", Data);
        }

        public bool RegisterNew(string key, bool save = true)
        {
            if (Data.Pairs.ContainsKey(key) || Data.Unpaired.Contains(key)) return false;

            if (Data.Unpaired.Count > 0)
            {
                string partner = Data.Unpaired[0];
                Data.Unpaired.RemoveAt(0);
                Data.Pairs[key] = partner;
                Data.Pairs[partner] = key;
                Game1.showGlobalMessage("Dois Mini-Obeliscos foram conectados!");
            }
            else
            {
                Data.Unpaired.Add(key);
                Game1.showGlobalMessage("Mini-Obelisco aguardando conexão...");
            }
            if (save) this.Helper.Data.WriteSaveData("obelisk-data", Data);
            return true;
        }

        public bool Unregister(string key)
        {
            if (Data.Unpaired.Contains(key))
            {
                Data.Unpaired.Remove(key);
                this.Helper.Data.WriteSaveData("obelisk-data", Data);
                return true;
            }
            else if (Data.Pairs.TryGetValue(key, out var partner))
            {
                Data.Pairs.Remove(key);
                Data.Pairs.Remove(partner);
                Data.Unpaired.Add(partner);
                Game1.showGlobalMessage("Um Mini-Obelisco foi removido. O par voltou a ficar solteiro.");
                this.Helper.Data.WriteSaveData("obelisk-data", Data);
                return true;
            }
            return false;
        }

        internal void HandleInteraction(string locationName, Vector2 tile)
        {
            string clickedKey = $"{locationName}|{(int)tile.X}|{(int)tile.Y}";

            if (Data.Pairs.TryGetValue(clickedKey, out var targetKey))
            {
                var parts = targetKey.Split('|');
                string locName = parts[0];
                int x = int.Parse(parts[1]);
                int y = int.Parse(parts[2]);

                GameLocation loc = Game1.getLocationFromName(locName);
                if (loc != null && loc.Objects.TryGetValue(new Vector2(x, y), out var targetObj) && IsMiniObelisk(targetObj))
                {
                    Game1.warpFarmer(locName, x, y + 1, 2);
                    Game1.showGlobalMessage("Teleportado!");
                    return;
                }
            }

            Game1.showRedMessage("Este Mini-Obelisco não está conectado a nenhum outro.");
        }

        internal bool IsMiniObelisk(StardewValley.Object obj)
        {
            if (obj == null) return false;
            return obj.QualifiedItemId == MiniObeliskId || obj.ItemId == "238";
        }
    }

    // --- PATCH 1: Permite colocar fora da fazenda ---
    [HarmonyPatch(typeof(Utility), nameof(Utility.playerCanPlaceItemHere))]
    public static class PlacementOutsideFarmPatch
    {
        static void Postfix(GameLocation location, Item item, int x, int y, Farmer f, ref bool __result)
        {
            if (!__result && item?.QualifiedItemId == "(BC)238")
            {
                Vector2 tile = new Vector2(x / 64, y / 64);
                if (!location.Objects.ContainsKey(tile) && !location.isWaterTile((int)tile.X, (int)tile.Y))
                {
                    __result = true;
                }
            }
        }
    }

    // --- PATCH 2: Remove o limite e clona o item EXATO (Mantém a textura correta) ---
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.placementAction))]
    public static class MiniObeliskLimitPatch
    {
        static bool Prefix(StardewValley.Object __instance, GameLocation location, int x, int y, Farmer who, ref bool __result)
        {
            if (__instance.QualifiedItemId == "(BC)238")
            {
                Vector2 tile = new Vector2(x / 64, y / 64);
                if (location.Objects.ContainsKey(tile))
                {
                    __result = false;
                    return false;
                }

                int count = 0;
                foreach (var loc in Game1.locations)
                    count += loc.Objects.Pairs.Count(p => p.Value.QualifiedItemId == "(BC)238");

                // Se o jogador já tiver 2 Mini-Obeliscos no mundo, nós forçamos a colocação
                if (count >= 2)
                {
                    // A MÁGICA: Clona o item que está na sua mão (BigCraftable perfeito)
                    var newObelisk = (StardewValley.Object)__instance.getOne();
                    newObelisk.TileLocation = tile;
                    newObelisk.Location = location;
                    
                    // Adiciona ao mapa (colisão e textura nativas)
                    location.setObject(tile, newObelisk);
                    
                    // Remove o item do seu inventário
                    if (who != null && who.ActiveItem != null && who.ActiveItem.Stack > 0)
                        who.reduceActiveItemByOne();
                    else if (Game1.player.ActiveItem != null && Game1.player.ActiveItem.Stack > 0)
                        Game1.player.reduceActiveItemByOne();

                    location.playSound("axe");
                    location.playSound("stoneStep");

                    // Registra a conexão na mesma hora
                    string key = $"{location.NameOrUniqueName}|{(int)tile.X}|{(int)tile.Y}";
                    ModEntry.Instance.RegisterNew(key);

                    __result = true;
                    return false; 
                }
            }
            return true; // Os 2 primeiros passam pela lógica normal do jogo
        }
    }

    // --- PATCH 3: Interação (Teleporte) ---
    [HarmonyPatch]
    public static class CheckActionPatch
    {
        static System.Reflection.MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(GameLocation))
                .FirstOrDefault(m => m.Name == "checkAction" && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(Location));
        }

        static bool Prefix(GameLocation __instance, object[] __args, ref bool __result)
        {
            Location? tileLocation = null;
            foreach (var arg in __args)
            {
                if (arg is Location loc) { tileLocation = loc; break; }
            }
            
            if (tileLocation == null) return true;

            ModEntry.Instance.ScanLocation(__instance);

            Vector2 clickedTile = new Vector2(tileLocation.Value.X, tileLocation.Value.Y);

            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                Vector2 checkTile = new Vector2(clickedTile.X, clickedTile.Y + yOffset);
                if (__instance.Objects.TryGetValue(checkTile, out var obj) && ModEntry.Instance.IsMiniObelisk(obj))
                {
                    ModEntry.Instance.HandleInteraction(__instance.NameOrUniqueName, obj.TileLocation);
                    __result = true;
                    return false;
                }
            }
            return true;
        }
    }
}
