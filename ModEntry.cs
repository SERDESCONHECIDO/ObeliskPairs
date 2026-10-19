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
        public List<string> ObeliskKeys { get; set; } = new();
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
            // Limpa a lista caso tenha dados corrompidos de testes anteriores
            Data.ObeliskKeys = Data.ObeliskKeys.Distinct().ToList(); 
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            bool changed = false;

            foreach (var added in e.Added)
            {
                if (added.Value.QualifiedItemId == ItemId)
                {
                    string key = $"{e.Location.NameOrUniqueName}|{(int)added.Key.X}|{(int)added.Key.Y}";
                    if (!Data.ObeliskKeys.Contains(key))
                    {
                        Data.ObeliskKeys.Add(key);
                        changed = true;
                    }
                }
            }

            foreach (var removed in e.Removed)
            {
                if (removed.Value.QualifiedItemId == ItemId)
                {
                    string key = $"{e.Location.NameOrUniqueName}|{(int)removed.Key.X}|{(int)removed.Key.Y}";
                    if (Data.ObeliskKeys.Remove(key))
                    {
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                this.Helper.Data.WriteSaveData("obelisk-data", Data);
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!e.Button.IsActionButton() && e.Button != SButton.MouseLeft) return;
            if (!Context.IsWorldReady) return;

            Vector2 cursorTile = e.Cursor.GrabTile;
            Vector2 playerFrontTile = Game1.player.GetGrabTile();

            Vector2? targetTile = null;
            if (IsObeliskAt(Game1.currentLocation, cursorTile)) targetTile = cursorTile;
            else if (IsObeliskAt(Game1.currentLocation, playerFrontTile)) targetTile = playerFrontTile;

            if (targetTile.HasValue)
            {
                Vector2 tile = targetTile.Value;
                string clickedKey = $"{Game1.currentLocation.NameOrUniqueName}|{(int)tile.X}|{(int)tile.Y}";

                int index = Data.ObeliskKeys.IndexOf(clickedKey);
                
                if (index != -1)
                {
                    // Lógica de pares: Índice par (0, 2, 4) conecta com o próximo (1, 3, 5)
                    int pairIndex = (index % 2 == 0) ? index + 1 : index - 1;

                    if (pairIndex >= 0 && pairIndex < Data.ObeliskKeys.Count)
                    {
                        string targetKey = Data.ObeliskKeys[pairIndex];
                        var parts = targetKey.Split('|');
                        string locName = parts[0];
                        int x = int.Parse(parts[1]);
                        int y = int.Parse(parts[2]);

                        // Verifica se o obelisco de destino realmente ainda existe no mapa
                        GameLocation loc = Game1.getLocationFromName(locName);
                        if (loc != null && loc.Objects.TryGetValue(new Vector2(x, y), out var targetObj) && targetObj.QualifiedItemId == ItemId)
                        {
                            Game1.warpFarmer(locName, x, y + 1, 2);
                            Game1.showGlobalMessage("Teleportado!");
                            this.Helper.Input.Suppress(e.Button);
                            return;
                        }
                        else
                        {
                            Game1.showRedMessage("O obelisco de destino não existe mais. Conexão desfeita.");
                            Data.ObeliskKeys.Remove(clickedKey);
                            this.Helper.Data.WriteSaveData("obelisk-data", Data);
                            this.Helper.Input.Suppress(e.Button);
                            return;
                        }
                    }
                }

                Game1.showRedMessage("Este Obelisco ainda não está conectado.");
                this.Helper.Input.Suppress(e.Button);
            }
        }

        private bool IsObeliskAt(GameLocation location, Vector2 tile)
        {
            return location.Objects.TryGetValue(tile, out var obj) && obj.QualifiedItemId == ItemId;
        }
    }
}
