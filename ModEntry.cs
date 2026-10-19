using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using Microsoft.Xna.Framework;

namespace ObeliskPairs
{
    public class ModData
    {
        public List<string> Unpaired { get; set; } = new();
        public Dictionary<string, string> Pairs { get; set; } = new();
    }

    public class ModEntry : Mod
    {
        private const string ItemId = "(BC)ObeliscoPares_Item";
        private ModData Data;

        public override void Entry(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
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
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            bool changed = false;

            foreach (var added in e.Added)
            {
                if (IsObelisk(added.Value))
                {
                    string key = $"{e.Location.NameOrUniqueName}|{(int)added.Key.X}|{(int)added.Key.Y}";
                    if (RegisterNew(key)) changed = true;
                }
            }

            foreach (var removed in e.Removed)
            {
                if (IsObelisk(removed.Value))
                {
                    string key = $"{e.Location.NameOrUniqueName}|{(int)removed.Key.X}|{(int)removed.Key.Y}";
                    if (Unregister(key)) changed = true;
                }
            }

            if (changed)
            {
                this.Helper.Data.WriteSaveData("obelisk-data", Data);
            }
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

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // CORREÇÃO 1: Apenas o botão de AÇÃO (não intercepta o toque de andar na tela)
            if (!e.Button.IsActionButton()) return;
            if (!Context.IsWorldReady) return;

            Vector2? targetTile = FindObeliskTile(Game1.currentLocation, Game1.currentCursorTile) 
                               ?? FindObeliskTile(Game1.currentLocation, Game1.player.GetGrabTile());

            if (targetTile.HasValue)
            {
                string clickedKey = $"{Game1.currentLocation.NameOrUniqueName}|{(int)targetTile.Value.X}|{(int)targetTile.Value.Y}";

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
                        this.Helper.Input.Suppress(e.Button);
                        return;
                    }
                }

                Game1.showRedMessage("Este Obelisco não está conectado.");
                this.Helper.Input.Suppress(e.Button);
            }
        }

        private bool IsObelisk(StardewValley.Object obj)
        {
            return obj.QualifiedItemId == ItemId || obj.ItemId == "ObeliscoPares_Item" || obj.Name == "ObeliscoPares_Item";
        }

        private Vector2? FindObeliskTile(GameLocation location, Vector2 tile)
        {
            // CORREÇÃO 2: Retorna a posição BASE do objeto (TileLocation), não o bloco que foi clicado
            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                Vector2 checkTile = new Vector2(tile.X, tile.Y + yOffset);
                if (location.Objects.TryGetValue(checkTile, out var obj) && IsObelisk(obj))
                {
                    return obj.TileLocation; 
                }
            }
            return null;
        }
    }
}
