using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.BigCraftables;
using Microsoft.Xna.Framework;

namespace ObeliskPairs
{
    // Classe para guardar a localização exata do obelisco de forma segura
    public class ObeliskPos
    {
        public string Location { get; set; }
        public Vector2 Tile { get; set; }
        
        public override bool Equals(object obj) => obj is ObeliskPos other && Location == other.Location && Tile == other.Tile;
        public override int GetHashCode() => HashCode.Combine(Location, Tile);
    }

    public class ModEntry : Mod
    {
        private const string ItemId = "(BC)ObeliscoPares_Item";

        // Fila de obeliscos solteiros
        private readonly List<ObeliskPos> _unpaired = new();
        // Dicionário que liga A -> B e B -> A
        private readonly Dictionary<ObeliskPos, ObeliskPos> _pairs = new();

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
            _unpaired.Clear();
            _pairs.Clear();
            ScanLocation(Game1.currentLocation);
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            foreach (var added in e.Added)
            {
                if (added.Value.QualifiedItemId == ItemId)
                {
                    var pos = new ObeliskPos { Location = e.Location.NameOrUniqueName, Tile = added.Key };
                    Register(pos);
                }
            }

            foreach (var removed in e.Removed)
            {
                if (removed.Value.QualifiedItemId == ItemId)
                {
                    var pos = new ObeliskPos { Location = e.Location.NameOrUniqueName, Tile = removed.Key };
                    Unregister(pos);
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
                    var pos = new ObeliskPos { Location = loc.NameOrUniqueName, Tile = obj.Key };
                    if (!_unpaired.Contains(pos) && !_pairs.ContainsKey(pos))
                    {
                        Register(pos);
                    }
                }
            }
        }

        private void Register(ObeliskPos pos)
        {
            if (_unpaired.Count > 0)
            {
                var partner = _unpaired[0];
                _unpaired.RemoveAt(0);
                _pairs[pos] = partner;
                _pairs[partner] = pos;
                Game1.showGlobalMessage("Dois Obeliscos foram conectados!");
            }
            else
            {
                _unpaired.Add(pos);
                Game1.showGlobalMessage("Obelisco aguardando conexão...");
            }
        }

        private void Unregister(ObeliskPos pos)
        {
            if (_unpaired.Contains(pos))
            {
                _unpaired.Remove(pos);
                return;
            }

            if (_pairs.TryGetValue(pos, out var partner))
            {
                _pairs.Remove(pos);
                _pairs.Remove(partner);
                _unpaired.Add(partner);
                Game1.showGlobalMessage("Um Obelisco foi destruído. O par voltou a ficar solteiro.");
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!e.Button.IsActionButton() && e.Button != SButton.MouseLeft) return;
            if (!Context.IsWorldReady) return;

            Vector2 cursorTile = Game1.currentCursorTile;
            Vector2 playerFrontTile = Game1.player.GetGrabTile();

            ObeliskPos clickedPos = null;

            if (IsObeliskAt(Game1.currentLocation, cursorTile))
                clickedPos = new ObeliskPos { Location = Game1.currentLocation.NameOrUniqueName, Tile = cursorTile };
            else if (IsObeliskAt(Game1.currentLocation, playerFrontTile))
                clickedPos = new ObeliskPos { Location = Game1.currentLocation.NameOrUniqueName, Tile = playerFrontTile };

            if (clickedPos != null)
            {
                if (_pairs.TryGetValue(clickedPos, out var targetPos))
                {
                    GameLocation loc = Game1.getLocationFromName(targetPos.Location);
                    if (loc != null && loc.Objects.TryGetValue(targetPos.Tile, out var targetObj) && targetObj.QualifiedItemId == ItemId)
                    {
                        // Teleporta para a casinha de baixo (Y + 1) para não ficar preso dentro do obelisco
                        Game1.warpFarmer(targetPos.Location, (int)targetPos.Tile.X, (int)targetPos.Tile.Y + 1, 2);
                        Game1.showGlobalMessage("Teleportado!");
                        this.Helper.Input.Suppress(e.Button);
                        return;
                    }
                    else
                    {
                        Game1.showRedMessage("O obelisco de destino não existe mais. Conexão desfeita.");
                        _pairs.Remove(clickedPos);
                        _pairs.Remove(targetPos);
                        _unpaired.Add(clickedPos);
                        this.Helper.Input.Suppress(e.Button);
                        return;
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
