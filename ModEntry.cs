private bool CheckConditions()
{
    // 1. Ter 5 corações com o Mago
    if (Game1.player.getFriendshipHeartLevelForNPC("Wizard") < 5) return false;

    // 2. Verificar se o artefato foi doado ao museu
    var museum = Game1.getLocationFromName("ArchaeologyHouse");
    if (museum == null) return false;

    string targetId = $"(O){Config.RequiredArtifactId}";
    bool donated = museum.Objects.Values.Any(obj => obj.QualifiedItemId == targetId);

    return donated;
}
