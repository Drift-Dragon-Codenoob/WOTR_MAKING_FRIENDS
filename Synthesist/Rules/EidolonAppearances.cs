namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    // Categories organise browsing only. They never select statistics, limbs or powers.
    internal sealed class EidolonAppearanceDefinition
    {
        internal readonly string Key, Name;
        internal readonly EidolonForm Category;
        internal EidolonAppearanceDefinition(string key, string name, EidolonForm category)
        { Key = key; Name = name; Category = category; }
    }

    internal static class EidolonAppearances
    {
        internal static readonly EidolonAppearanceDefinition[] All =
        {
            new("AirElemental", "Air elemental", EidolonForm.Biped),
            new("Astradaemon", "Astradaemon", EidolonForm.Biped),
            new("AstralDeva", "Astral deva", EidolonForm.Biped),
            new("AxiomiteMasc", "Axiomite", EidolonForm.Biped),
            new("Babau", "Babau", EidolonForm.Biped),
            new("Balor", "Balor", EidolonForm.Biped),
            new("Bralani", "Bralani", EidolonForm.Biped),
            new("Brimorak", "Brimorak", EidolonForm.Biped),
            new("Bythos", "Bythos", EidolonForm.Biped),
            new("Coloxus", "Coloxus", EidolonForm.Biped),
            new("Deathsnatcher", "Deathsnatcher", EidolonForm.Biped),
            new("Dretch", "Dretch", EidolonForm.Biped),
            new("EarthElemental", "Earth elemental", EidolonForm.Biped),
            new("Erinyes", "Erinyes", EidolonForm.Biped),
            new("FireElemental", "Fire elemental", EidolonForm.Biped),
            new("Gargoyle", "Gargoyle", EidolonForm.Biped),
            new("Glabrezu", "Glabrezu", EidolonForm.Biped),
            new("GolemIron", "Iron golem", EidolonForm.Biped),
            new("GolemWood", "Wood golem", EidolonForm.Biped),
            new("Incubus", "Incubus", EidolonForm.Biped),
            new("Lilitu", "Lilitu", EidolonForm.Biped),
            new("MonadicDeva", "Monadic deva", EidolonForm.Biped),
            new("MovanicDeva", "Movanic deva", EidolonForm.Biped),
            new("Nabasu", "Nabasu", EidolonForm.Biped),
            new("Painaji", "Painajai", EidolonForm.Biped),
            new("Schir", "Schir", EidolonForm.Biped),
            new("Shade", "Shade", EidolonForm.Biped),
            new("Spirit", "Spirit", EidolonForm.Biped),
            new("Succubus", "Succubus", EidolonForm.Biped),
            new("Treant", "Treant", EidolonForm.Biped),
            new("Troll", "Troll", EidolonForm.Biped),
            new("Veranallia", "Veranallia", EidolonForm.Biped),
            new("Vrock", "Vrock", EidolonForm.Biped),
            new("WaterElemental", "Water elemental", EidolonForm.Biped),
            new("Werewolf", "Werewolf", EidolonForm.Biped),
            new("Yinudja", "Yinudja", EidolonForm.Biped),
            new("BlackDragon", "Black dragon", EidolonForm.Quadruped),
            new("BlueDragon", "Blue dragon", EidolonForm.Quadruped),
            new("BrassDragon", "Brass dragon", EidolonForm.Quadruped),
            new("Bulette", "Bulette", EidolonForm.Quadruped),
            new("CopperDragon", "Copper dragon", EidolonForm.Quadruped),
            new("GoldDragon", "Gold dragon", EidolonForm.Quadruped),
            new("GreenDragon", "Green dragon", EidolonForm.Quadruped),
            new("HellHound", "Hellhound", EidolonForm.Quadruped),
            new("Manticore", "Manticore", EidolonForm.Quadruped),
            new("Mimic", "Mimic", EidolonForm.Quadruped),
            new("Nightmare", "Nightmare", EidolonForm.Quadruped),
            new("RedDragon", "Red dragon", EidolonForm.Quadruped),
            new("ShamblingMound", "Shambling mound", EidolonForm.Quadruped),
            new("SilverDragon", "Silver dragon", EidolonForm.Quadruped),
            new("Vavakia", "Vavakia", EidolonForm.Quadruped),
            new("WhiteDragon", "White dragon", EidolonForm.Quadruped),
            new("CragLinnorm", "Crag linnorm", EidolonForm.Serpentine),
            new("FairyDragon", "Faerie dragon", EidolonForm.Serpentine),
            new("Keketar", "Keketar", EidolonForm.Serpentine),
            new("Lillend", "Lillend", EidolonForm.Serpentine),
            new("Marilith", "Marilith", EidolonForm.Serpentine),
            new("Salamander", "Salamander", EidolonForm.Serpentine),
            new("WillOWisp", "Will-o'-wisp", EidolonForm.Serpentine)
        };
    }
}
