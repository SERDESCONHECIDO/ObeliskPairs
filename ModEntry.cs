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

        // Usamos textos simples e exatos como chave (ex: "Farm|10|15")
        private readonly List<string> _unpaired = new();
        private readonly Dictionary<string, string> _pairs = new();

        private string GetKey(string location, Vector2 tile) => $"{location}|{(int)tile.X}|{(int)tile.Y}";

        public override void Entry(IModHelper helper)
        {
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Player.Warped += OnWarped;
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
            _unpaired.Clear();
            _pairs.Clear();
            ScanLocation(Game1.currentLocation);
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            ScanLocation(e.NewLocation);
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            foreach (var added in e.Added)
            {
                if (added.Value.QualifiedItemId == ItemId)
                {
                    string key = GetKey(e.Location.NameOrUniqueName, added.Key);
                    Register(key);
                }
            }

            foreach (var removed in e.Removed)
            {
                if (removed.Value.QualifiedItemId == ItemId)
                {
                    string key = GetKey(e.Location.NameOrUniqueName, removed.Key);
                    Unregister(key);
                }
            }
        }

        private void ScanLocation(GameLocation loc)
        {
            if (loc == null) return;
            foreach (var obj in loc.Objects.Pairs)
            {
                if (obj.Value.QualifiedItemId == ItemId)
                {
                    string key = GetKey(loc.NameOrUniqueName, obj.Key);
                    if (!_unpaired.Contains(key) && !_pairs.ContainsKey(key))
                    {
                        Register(key);
                    }
                }
            }
        }

        private void Register(string key)
        {
            if (_unpaired.Count > 0)
            {
                string partner = _unpaired[0];
                _unpaired.RemoveAt(0);
                _pairs[key] = partner;
                _pairs[partner] = key;
                Game1.showGlobalMessage("Dois Obeliscos foram conectados!");
            }
            else
            {
                _unpaired.Add(key);
                Game1.showGlobalMessage("Obelisco aguardando conexão...");
            }
        }

        private void Unregister(string key)
        {
            if (_unpaired.Contains(key))
            {
                _unpaired.Remove(key);
                return;
            }

            if (_pairs.TryGetValue(key, out var partner))
            {
                _pairs.Remove(key);
                _pairs.Remove(partner);
                _unpaired.Add(partner);
                Game1.showGlobalMessage("Um Obelisco foi destruído. O par voltou a ficar solteiro.");
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!e.Button.IsActionButton() && e.Button != SButton.MouseLeft) return;
            if (!Context.IsWorldReady) return;

            // Verifica o bloco exato e o bloco de cima (para o toque no Android)
            Vector2? targetTile = GetObeliskTile(Game1.currentLocation, Game1.currentCursorTile) 
                               ?? GetObeliskTile(Game1.currentLocation, Game1.player.GetGrabTile());

            if (targetTile.HasValue)
            {
                string clickedKey = GetKey(Game1.currentLocation.NameOrUniqueName, targetTile.Value);
                
                if (_pairs.TryGetValue(clickedKey, out var targetKey))
                {
                    var parts = targetKey.Split('|');
                    string locName = parts[0];
                    int x = int.Parse(parts[1]);
                    int y = int.Parse(parts[2]);

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
                        _pairs.Remove(clickedKey);
                        _pairs.Remove(targetKey);
                        _unpaired.Add(clickedKey);
                        this.Helper.Input.Suppress(e.Button);
                        return;
                    }
                }

                Game1.showRedMessage("Este Obelisco ainda não está conectado.");
                this.Helper.Input.Suppress(e.Button);
            }
        }

        private Vector2? GetObeliskTile(GameLocation location, Vector2 tile)
        {
            // Verifica o bloco base
            if (location.Objects.TryGetValue(tile, out var obj) && obj.QualifiedItemId == ItemId)
                return tile;
            
            // Verifica o bloco de cima (onde fica a parte visual do obelisco)
            Vector2 above = new Vector2(tile.X, tile.Y - 1);
            if (location.Objects.TryGetValue(above, out var objAbove) && objAbove.QualifiedItemId == ItemId)
                return above;
                
            return null;
        }
    }
}
