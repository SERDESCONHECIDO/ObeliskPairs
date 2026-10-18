using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using Microsoft.Xna.Framework;

namespace ObeliskPairs
{
    public class ModEntry : Mod
    {
        private const string ItemId = "(BC)ObeliscoPares_Item";

        private readonly List<string> _unpairedObelisks = new();
        private readonly Dictionary<string, string> _connectedPairs = new();

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
                        SpriteIndex = 238, // Textura do Mini-Obelisco
                        Fragility = 0,
                        CanBePlacedOutdoors = true,
                        CanBePlacedIndoors = true
                        // A linha "ExcludeFromShippingCollection" foi REMOVIDA
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
            _unpairedObelisks.Clear();
            _connectedPairs.Clear();
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            foreach (var added in e.Added)
            {
                if (added.Value.QualifiedItemId == ItemId)
                {
                    string key = $"{e.Location.NameOrUniqueName}|{(int)added.Key.X}|{(int)added.Key.Y}";
                    RegisterNewObelisk(key);
                }
            }

            foreach (var removed in e.Removed)
            {
                if (removed.Value.QualifiedItemId == ItemId)
                {
                    string key = $"{e.Location.NameOrUniqueName}|{(int)removed.Key.X}|{(int)removed.Key.Y}";
                    UnregisterObelisk(key);
                }
            }
        }

        private void RegisterNewObelisk(string newKey)
        {
            if (_unpairedObelisks.Count > 0)
            {
                string partnerKey = _unpairedObelisks[0];
                _unpairedObelisks.RemoveAt(0);

                _connectedPairs[newKey] = partnerKey;
                _connectedPairs[partnerKey] = newKey;
                Game1.showGlobalMessage("Dois Obeliscos foram conectados!");
            }
            else
            {
                _unpairedObelisks.Add(newKey);
                Game1.showGlobalMessage("Obelisco aguardando conexão...");
            }
        }

        private void UnregisterObelisk(string brokenKey)
        {
            if (_unpairedObelisks.Contains(brokenKey))
            {
                _unpairedObelisks.Remove(brokenKey);
                return;
            }

            if (_connectedPairs.TryGetValue(brokenKey, out var partnerKey))
            {
                _connectedPairs.Remove(brokenKey);
                _connectedPairs.Remove(partnerKey);

                _unpairedObelisks.Add(partnerKey);
                Game1.showGlobalMessage("Um Obelisco foi destruído. O par voltou a ficar solteiro.");
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!e.Button.IsActionButton() || !Context.IsWorldReady) return;

            Vector2 tile = e.Cursor.GrabTile;
            string clickedKey = $"{Game1.currentLocation.NameOrUniqueName}|{(int)tile.X}|{(int)tile.Y}";

            if (Game1.currentLocation.Objects.TryGetValue(tile, out var obj) && obj.QualifiedItemId == ItemId)
            {
                if (_connectedPairs.TryGetValue(clickedKey, out var targetKey))
                {
                    var parts = targetKey.Split('|');
                    string locationName = parts[0];
                    int x = int.Parse(parts[1]);
                    int y = int.Parse(parts[2]);

                    Game1.warpFarmer(locationName, x, y + 1, 2);
                    Game1.showGlobalMessage("Teleportado!");
                    this.Helper.Input.Suppress(e.Button);
                }
                else
                {
                    Game1.showRedMessage("Este Obelisco ainda não está conectado.");
                    this.Helper.Input.Suppress(e.Button);
                }
            }
        }
    }
}
