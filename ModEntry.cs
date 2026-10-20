using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Microsoft.Xna.Framework;
using xTile.Dimensions;

namespace ObeliscoConectado
{
    public enum CraftMode { On, Off, OnSe }

    public class ModConfig
    {
        public CraftMode Mode { get; set; } = CraftMode.On;
        public string RequiredArtifactId { get; set; } = "74";
        public string CraftingIngredients { get; set; } = "334 1 335 1 336 1";
    }

    public class ModData
    {
        public List<string> Unpaired { get; set; } = new();
        public Dictionary<string, string> Pairs { get; set; } = new();
    }

    public class ModEntry : Mod
    {
        public static ModEntry Instance { get; private set; }
        public ModData Data { get; private set; }
        public ModConfig Config { get; private set; }
        
        private const string MiniObeliskId = "(BC)238";
        private const string MailFlag = "WizardObeliskLetterSent";
        private bool _isConditionMet = false;

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Data = new ModData();
            Config = this.Helper.ReadConfig<ModConfig>();

            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.World.ObjectListChanged += OnObjectListChanged;

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.Monitor.Log("Tentando carregar o menu de configuracao (GMCM)...", LogLevel.Info);
            try
            {
                var api = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
                if (api == null)
                {
                    this.Monitor.Log("A API do GMCM nao foi encontrada! O mod GenericModConfigMenu esta instalado?", LogLevel.Error);
                    return;
                }

                this.Monitor.Log("API do GMCM encontrada! Registrando o mod...", LogLevel.Info);
                api.Register(
                    mod: this.ModManifest,
                    reset: () => Config = new ModConfig(),
                    save: () => this.Helper.WriteConfig(Config)
                );

                api.AddTextOption(
                    mod: this.ModManifest,
                    getValue: () => Config.Mode.ToString(),
                    setValue: value => Config.Mode = Enum.Parse<CraftMode>(value),
                    name: () => "Modo de Craft",
                    tooltip: () => "On = Sempre, Off = Nunca, OnSe = Condicional",
                    allowedValues: new[] { "On", "Off", "OnSe" }
                );

                api.AddTextOption(
                    mod: this.ModManifest,
                    getValue: () => Config.RequiredArtifactId,
                    setValue: value => Config.RequiredArtifactId = value,
                    name: () => "ID do Artefato",
                    tooltip: () => "ID do artefato necessario (padrao: 74)"
                );

                api.AddTextOption(
                    mod: this.ModManifest,
                    getValue: () => Config.CraftingIngredients,
                    setValue: value => Config.CraftingIngredients = value,
                    name: () => "Ingredientes do Craft",
                    tooltip: () => "IDs e Quantidades separados por espaco"
                );
                
                this.Monitor.Log("Mod registrado no GMCM com sucesso!", LogLevel.Info);
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Erro ao registrar no GMCM: {ex.Message}", LogLevel.Error);
            }
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/mail"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    data["WizardObeliskLetter"] = "Prezado(a) @,^Sinto uma energia antiga despertar. Sua dedicação ao estudo dos artefatos e nossa amizade revelaram um segredo há muito esquecido.^Os antigos obeliscos podem ser conectados em pares, permitindo viagens instantâneas entre eles. Acredito que você está pronto para dominar essa arte.^Que a magia o guie.^- Mago";
                });
            }
            
            if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    data.Remove("Mini-Obelisk");

                    bool shouldAdd = false;
                    if (Config.Mode == CraftMode.On) shouldAdd = true;
                    else if (Config.Mode == CraftMode.OnSe && _isConditionMet) shouldAdd = true;

                    if (shouldAdd)
                    {
                        data["ObeliscoConectado_Receita"] = $"{Config.CraftingIngredients}/Home/238/true/default/Obelisco Conectado";
                    }
                });
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Data = this.Helper.Data.ReadSaveData<ModData>("obelisk-data") ?? new ModData();
            ScanLocation(Game1.currentLocation);
            UpdateConditions();
            this.Helper.GameContent.InvalidateCache("Data/CraftingRecipes");
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            UpdateConditions();
            if (Config.Mode == CraftMode.OnSe && _isConditionMet)
            {
                if (!Game1.player.mailReceived.Contains(MailFlag))
                {
                    Game1.addMailForTomorrow("WizardObeliskLetter");
                    Game1.player.mailReceived.Add(MailFlag);
                }
            }
            this.Helper.GameContent.InvalidateCache("Data/CraftingRecipes");
        }

        private void UpdateConditions()
        {
            if (Game1.player == null) { _isConditionMet = false; return; }
            if (Game1.player.getFriendshipHeartLevelForNPC("Wizard") < 5) { _isConditionMet = false; return; }

            var museum = Game1.getLocationFromName("ArchaeologyHouse");
            if (museum == null) { _isConditionMet = false; return; }

            string targetId = $"(O){Config.RequiredArtifactId}";
            _isConditionMet = museum.Objects.Values.Any(obj => obj.QualifiedItemId == targetId);
        }

        private void OnWarped(object sender, WarpedEventArgs e) { if (e.IsLocalPlayer) ScanLocation(e.NewLocation); }
        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e) { if (e.Removed.Any(r => IsMiniObelisk(r.Value))) ScanLocation(e.Location); }

        public void ScanLocation(GameLocation loc)
        {
            if (loc == null) return;
            bool changed = false;
            var obelisks = loc.Objects.Pairs.Where(p => IsMiniObelisk(p.Value)).OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X).ToList();
            foreach (var obj in obelisks)
            {
                string key = $"{loc.NameOrUniqueName}|{(int)obj.Key.X}|{(int)obj.Key.Y}";
                if (!Data.Pairs.ContainsKey(key) && !Data.Unpaired.Contains(key)) { if (RegisterNew(key, false)) changed = true; }
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
                string partner = Data.Unpaired[0]; Data.Unpaired.RemoveAt(0);
                Data.Pairs[key] = partner; Data.Pairs[partner] = key;
                Game1.showGlobalMessage("Dois Mini-Obeliscos foram conectados!");
            }
            else { Data.Unpaired.Add(key); Game1.showGlobalMessage("Mini-Obelisco aguardando conexão..."); }
            if (save) this.Helper.Data.WriteSaveData("obelisk-data", Data);
            return true;
        }

        public bool Unregister(string key)
        {
            if (Data.Unpaired.Contains(key)) { Data.Unpaired.Remove(key); this.Helper.Data.WriteSaveData("obelisk-data", Data); return true; }
            else if (Data.Pairs.TryGetValue(key, out var partner))
            {
                Data.Pairs.Remove(key); Data.Pairs.Remove(partner); Data.Unpaired.Add(partner);
                Game1.showGlobalMessage("Um Mini-Obelisco foi removido. O par voltou a ficar solteiro.");
                this.Helper.Data.WriteSaveData("obelisk-data", Data); return true;
            }
            return false;
        }

        internal void HandleInteraction(string locationName, Vector2 tile)
        {
            string clickedKey = $"{locationName}|{(int)tile.X}|{(int)tile.Y}";
            if (Data.Pairs.TryGetValue(clickedKey, out var targetKey))
            {
                var parts = targetKey.Split('|');
                GameLocation loc = Game1.getLocationFromName(parts[0]);
                int x = int.Parse(parts[1]); int y = int.Parse(parts[2]);
                if (loc != null && loc.Objects.TryGetValue(new Vector2(x, y), out var targetObj) && IsMiniObelisk(targetObj))
                {
                    Game1.warpFarmer(parts[0], x, y + 1, 2);
                    Game1.showGlobalMessage("Teleportado!"); return;
                }
            }
            Game1.showRedMessage("Este Mini-Obelisco não está conectado a nenhum outro.");
        }

        internal bool IsMiniObelisk(StardewValley.Object obj) => obj != null && (obj.QualifiedItemId == MiniObeliskId || obj.ItemId == "238");
    }

    [HarmonyPatch(typeof(Utility), nameof(Utility.playerCanPlaceItemHere))]
    public static class PlacementOutsideFarmPatch
    {
        static void Postfix(GameLocation location, Item item, int x, int y, Farmer f, ref bool __result)
        {
            if (!__result && item?.QualifiedItemId == "(BC)238")
            {
                Vector2 tile = new Vector2(x / 64, y / 64);
                if (!location.Objects.ContainsKey(tile) && !location.isWaterTile((int)tile.X, (int)tile.Y)) __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.placementAction))]
    public static class MiniObeliskLimitPatch
    {
        static bool Prefix(StardewValley.Object __instance, GameLocation location, int x, int y, Farmer who, ref bool __result)
        {
            if (__instance.QualifiedItemId == "(BC)238")
            {
                Vector2 tile = new Vector2(x / 64, y / 64);
                if (location.Objects.ContainsKey(tile)) { __result = false; return false; }
                int count = 0;
                foreach (var loc in Game1.locations) count += loc.Objects.Pairs.Count(p => p.Value.QualifiedItemId == "(BC)238");
                if (count >= 2)
                {
                    var newObelisk = (StardewValley.Object)__instance.getOne();
                    newObelisk.TileLocation = tile; newObelisk.Location = location;
                    location.setObject(tile, newObelisk);
                    if (who != null && who.ActiveItem != null && who.ActiveItem.Stack > 0) who.reduceActiveItemByOne();
                    else if (Game1.player.ActiveItem != null && Game1.player.ActiveItem.Stack > 0) Game1.player.reduceActiveItemByOne();
                    location.playSound("axe"); location.playSound("stoneStep");
                    ModEntry.Instance.RegisterNew($"{location.NameOrUniqueName}|{(int)tile.X}|{(int)tile.Y}");
                    __result = true; return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class CheckActionPatch
    {
        static System.Reflection.MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(GameLocation)).FirstOrDefault(m => m.Name == "checkAction" && m.GetParameters().Length >= 2 && m.GetParameters()[0].ParameterType == typeof(Location));
        static bool Prefix(GameLocation __instance, object[] __args, ref bool __result)
        {
            Location? tileLocation = null;
            foreach (var arg in __args) { if (arg is Location loc) { tileLocation = loc; break; } }
            if (tileLocation == null) return true;
            ModEntry.Instance.ScanLocation(__instance);
            Vector2 clickedTile = new Vector2(tileLocation.Value.X, tileLocation.Value.Y);
            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                Vector2 checkTile = new Vector2(clickedTile.X, clickedTile.Y + yOffset);
                if (__instance.Objects.TryGetValue(checkTile, out var obj) && ModEntry.Instance.IsMiniObelisk(obj))
                {
                    ModEntry.Instance.HandleInteraction(__instance.NameOrUniqueName, obj.TileLocation);
                    __result = true; return false;
                }
            }
            return true;
        }
    }
}
