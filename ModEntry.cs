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
            // Evita registrar a mesma coisa duas vezes
            if (_unpairedObelisks.Contains(newKey) || _connectedPairs.ContainsKey(newKey)) return;

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
            if (!e.Button.IsActionButton() && e.Button != SButton.MouseLeft) return;
            if (!Context.IsWorldReady) return;

            Vector2 cursorTile = e.Cursor.GrabTile;
            Vector2 playerFrontTile = Game1.player.GetGrabTile();

            Vector2? targetTile = null;

            if (IsObeliskAt(Game1.currentLocation, cursorTile))
                targetTile = cursorTile;
            else if (IsObeliskAt(Game1.currentLocation, playerFrontTile))
                targetTile = playerFrontTile;

            if (targetTile.HasValue)
            {
                Vector2 tile = targetTile.Value;
                string clickedKey = $"{Game1.currentLocation.NameOrUniqueName}|{(int)tile.X}|{(int)tile.Y}";

                // === PLANO B PARA ANDROID ===
                // Se o evento ObjectListChanged falhou, o obelisco não está no dicionário.
                // Nós o registramos na hora do clique para garantir que a conexão aconteça.
                if (!_connectedPairs.ContainsKey(clickedKey) && !_unpairedObelisks.Contains(clickedKey))
                {
                    RegisterNewObelisk(clickedKey);
                }

                // Verifica se agora ele tem um par
                if (_connectedPairs.TryGetValue(clickedKey, out var targetKey))
                {
                    var parts = targetKey.Split('|');
                    string locationName = parts[0];
                    int x = int.Parse(parts[1]);
                    int y = int.Parse(parts[2]);

                    // Checagem de segurança: O obelisco de destino ainda existe?
                    var loc = Game1.getLocationFromName(locationName);
                    if (loc != null && IsObeliskAt(loc, new Vector2(x, y)))
                    {
                        Game1.warpFarmer(locationName, x, y + 1, 2);
                        Game1.showGlobalMessage("Teleportado!");
                    }
                    else
                    {
                        // Se o destino foi quebrado sem avisar, desfaz a conexão
                        UnregisterObelisk(clickedKey);
                        Game1.showRedMessage("O obelisco de destino não existe mais. Conexão desfeita.");
                    }
                    
                    this.Helper.Input.Suppress(e.Button);
                }
                else
                {
                    Game1.showRedMessage("Este Obelisco aguarda um par. Coloque outro em algum lugar.");
                    this.Helper.Input.Suppress(e.Button);
                }
            }
        }

        private bool IsObeliskAt(GameLocation location, Vector2 tile)
        {
            return location.Objects.TryGetValue(tile, out var obj) && obj.QualifiedItemId == ItemId;
        }
    }
}
