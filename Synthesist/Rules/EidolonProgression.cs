using System;
using System.Collections.Generic;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    // Advanced Player's Guide, Table 2-9. These are Summoner levels, not pet class levels.
    // https://www.aonprd.com/ClassDisplay.aspx?ItemName=Eidolon
    internal sealed class EidolonLevel
    {
        public int Level { get; }
        public int HitDice { get; }
        public int BaseAttackBonus => HitDice;
        public int GoodSave { get; }
        public int BadSave { get; }
        public int SkillRanks { get; }
        public int Feats { get; }
        public int ArmourBonus { get; }
        public int StrengthDexterityBonus { get; }
        public int EvolutionPoints { get; }
        public int MaximumNaturalAttacks { get; }
        public int AbilityIncreases => HitDice / 4;

        public EidolonLevel(int level, int hd, int good, int bad, int skills, int feats,
            int armour, int physical, int points, int attacks)
        {
            Level = level; HitDice = hd; GoodSave = good; BadSave = bad;
            SkillRanks = skills; Feats = feats; ArmourBonus = armour;
            StrengthDexterityBonus = physical; EvolutionPoints = points;
            MaximumNaturalAttacks = attacks;
        }
    }

    internal enum EidolonForm { Biped, Quadruped, Serpentine }

    internal static class EidolonProgression
    {
        private static readonly EidolonLevel[] table =
        {
            new( 1,  1, 2, 0,  4, 1,  0, 0,  3, 3),
            new( 2,  2, 3, 0,  8, 1,  2, 1,  4, 3),
            new( 3,  3, 3, 1, 12, 2,  2, 1,  5, 3),
            new( 4,  3, 3, 1, 12, 2,  2, 1,  7, 4),
            new( 5,  4, 4, 1, 16, 2,  4, 2,  8, 4),
            new( 6,  5, 4, 1, 20, 3,  4, 2,  9, 4),
            new( 7,  6, 5, 2, 24, 3,  6, 3, 10, 4),
            new( 8,  6, 5, 2, 24, 3,  6, 3, 11, 4),
            new( 9,  7, 5, 2, 28, 4,  6, 3, 13, 5),
            new(10,  8, 6, 2, 32, 4,  8, 4, 14, 5),
            new(11,  9, 6, 3, 36, 5,  8, 4, 15, 5),
            new(12,  9, 6, 3, 36, 5, 10, 5, 16, 5),
            new(13, 10, 7, 3, 40, 5, 10, 5, 17, 5),
            new(14, 11, 7, 3, 44, 6, 10, 5, 19, 6),
            new(15, 12, 8, 4, 48, 6, 12, 6, 20, 6),
            new(16, 12, 8, 4, 48, 6, 12, 6, 21, 6),
            new(17, 13, 8, 4, 52, 7, 14, 7, 22, 6),
            new(18, 14, 9, 4, 56, 7, 14, 7, 23, 6),
            new(19, 15, 9, 5, 60, 8, 14, 7, 25, 7),
            new(20, 15, 9, 5, 60, 8, 16, 8, 26, 7)
        };

        public static IReadOnlyList<EidolonLevel> Levels { get; } = Array.AsReadOnly(table);

        public static EidolonLevel At(int summonerLevel)
        {
            if (summonerLevel < 1 || summonerLevel > 20)
                throw new ArgumentOutOfRangeException(nameof(summonerLevel));
            return table[summonerLevel - 1];
        }

        public static int MakersJumpUses(int level) => level < 6 ? 0 : Math.Min(3, level / 6);
        public static int MeldBonus(int level) => level >= 12 ? 4 : level >= 4 ? 2 : 0;

        public static int BaseStrength(EidolonForm form) => form switch
        {
            EidolonForm.Biped => 16, EidolonForm.Quadruped => 14, EidolonForm.Serpentine => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(form))
        };
        public static int BaseDexterity(EidolonForm form) => form switch
        {
            EidolonForm.Biped => 12, EidolonForm.Quadruped => 14, EidolonForm.Serpentine => 16,
            _ => throw new ArgumentOutOfRangeException(nameof(form))
        };
        public const int BaseConstitution = 13;
    }
}
