using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
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
        private const string ItemId = "(BC)ObeliscoPares_Item";

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Data = new ModData();

            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/BigCraftables"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, BigCraftableData>().Data;
                    data["ObeliscoPares_Item"] = new BigCraftableData
                    {
                        Name = "ObeliscoPares_Item",
                        DisplayName = "Obelisco Conectado",
                        Description = "Clique para viajar para o obelisco conectado.",
                        Price = 5000,
                        Texture = "TileSheets/Craftables",
                        SpriteIndex = 238,
                        Fragility = 0,
                        CanBePlacedOutdoors = true,
                        CanBePlacedIndoors = true
                    };
                });
            }
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    data["ObeliscoPares_Receita"] = "390 100 787 5/Home/ObeliscoPares_Item/true/null/null";
                });
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Data = this.Helper.Data.ReadSaveData<ModData>("obelisk-data") ?? new ModData();
            Data.Unpaired = Data.Unpaired.Where(k => !Data.Pairs.ContainsKey(k)).Distinct().ToList();
            ScanLocation(Game1.currentLocation);
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (e.IsLocalPlayer) ScanLocation(e.NewLocation);
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            bool changed = false;

            // Processa as remoções (picaretada)
            foreach (var removed in e.Removed)
            {
                if (IsObelisk(removed.Value))
                {
                    string key = $"{e.Location.NameOrUniqueName}|{(int)removed.Key.X}|{(int)removed.Key.Y}";
                    if (Unregister(key)) changed = true;
                }
            }

            if (changed) this.Helper.Data.WriteSaveData("obelisk-data", Data);

            // FAILSAFE: Em vez de confiar no e.Added (que falha no Android), 
            // nós simplesmente varremos a área inteira para registrar qualquer obelisco novo.
            ScanLocation(e.Location);
        }

        // Varre a área e organiza os obeliscos por ordem de colocação (Y, depois X)
        internal void ScanLocation(GameLocation loc)
        {
            if (loc == null) return;
            bool changed = false;
            
            var obelisks = loc.Objects.Pairs
                .Where(p => IsObelisk(p.Value))
                .OrderBy(p => p.Key.Y)
                .ThenBy(p => p.Key.X)
                .ToList();

            foreach (var obj in obelisks)
            {
                string key = $"{loc.NameOrUniqueName}|{(int)obj.Key.X}|{(int)obj.Key.Y}";
                if (!Data.Pairs.ContainsKey(key) && !Data.Unpaired.Contains(key))
                {
                    if (RegisterNew(key)) changed = true;
                }
            }
            if (changed) this.Helper.Data.WriteSaveData("obelisk-data", Data);
        }

        private bool RegisterNew(string key)
        {
            if (Data.Pairs.ContainsKey(key) || Data.Unpaired.Contains(key)) return false;

            if (Data.Unpaired.Count > 0)
            {
                string partner = Data.Unpaired[0];
                Data.Unpaired.RemoveAt(0);
                Data.Pairs[key] = partner;
                Data.Pairs[partner] = key;
                Game1.showGlobalMessage("Dois Obeliscos foram conectados!");
            }
            else
            {
                Data.Unpaired.Add(key);
                Game1.showGlobalMessage("Obelisco aguardando conexão...");
            }
            return true;
        }

        private bool Unregister(string key)
        {
            if (Data.Unpaired.Contains(key))
            {
                Data.Unpaired.Remove(key);
                return true;
            }
            else if (Data.Pairs.TryGetValue(key, out var partner))
            {
                Data.Pairs.Remove(key);
                Data.Pairs.Remove(partner);
                Data.Unpaired.Add(partner);
                Game1.showGlobalMessage("Um Obelisco foi destruído. O par voltou a ficar solteiro.");
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
                if (loc != null && loc.Objects.TryGetValue(new Vector2(x, y), out var targetObj) && IsObelisk(targetObj))
                {
                    Game1.warpFarmer(locName, x, y + 1, 2);
                    Game1.showGlobalMessage("Teleportado!");
                    return;
                }
                else
                {
                    Data.Pairs.Remove(clickedKey);
                    Data.Pairs.Remove(targetKey);
                    Data.Unpaired.Add(clickedKey);
                    this.Helper.Data.WriteSaveData("obelisk-data", Data);
                }
            }

            Game1.showRedMessage("Este Obelisco não está conectado.");
        }

        internal bool IsObelisk(StardewValley.Object obj)
        {
            return obj.QualifiedItemId == ItemId || obj.ItemId == "ObeliscoPares_Item" || obj.Name == "ObeliscoPares_Item";
        }
    }

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

            // FAILSAFE FINAL: Varre a área no exato milissegundo do clique
            ModEntry.Instance.ScanLocation(__instance);

            Vector2 clickedTile = new Vector2(tileLocation.Value.X, tileLocation.Value.Y);

            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                Vector2 checkTile = new Vector2(clickedTile.X, clickedTile.Y + yOffset);
                if (__instance.Objects.TryGetValue(checkTile, out var obj) && ModEntry.Instance.IsObelisk(obj))
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
