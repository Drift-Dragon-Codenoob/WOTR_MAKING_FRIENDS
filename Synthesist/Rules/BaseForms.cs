using System;
using System.Collections.Generic;

namespace WOTR_MAKING_FRIENDS.Synthesist.Rules
{
    internal sealed class FreeEvolution
    {
        public string Key { get; }
        public int Ranks { get; }
        public FreeEvolution(string key, int ranks = 1) { Key = key; Ranks = ranks; }
    }

    internal sealed class BaseForm
    {
        public EidolonForm Form { get; }
        public int Strength => EidolonProgression.BaseStrength(Form);
        public int Dexterity => EidolonProgression.BaseDexterity(Form);
        public int Constitution => EidolonProgression.BaseConstitution;
        public int NaturalArmour => 2;
        public int SpeedFeet { get; }
        public int ClimbSpeedFeet { get; }
        public bool GoodFortitude { get; }
        public bool GoodReflex { get; }
        public bool GoodWill { get; }
        public IReadOnlyList<FreeEvolution> FreeEvolutions { get; }

        public BaseForm(EidolonForm form, int speed, int climb, bool fortitude, bool reflex,
            bool will, params FreeEvolution[] free)
        {
            Form = form; SpeedFeet = speed; ClimbSpeedFeet = climb;
            GoodFortitude = fortitude; GoodReflex = reflex; GoodWill = will;
            FreeEvolutions = Array.AsReadOnly(free);
        }
    }

    internal static class BaseForms
    {
        // The +2 natural armour is already included here. Do not add the same starting
        // evolution a second time. https://www.aonprd.com/EidolonBaseForms.aspx
        private static readonly BaseForm biped = new(EidolonForm.Biped, 30, 0, true, false, true,
            new FreeEvolution("Claws"), new FreeEvolution("LimbsArms"), new FreeEvolution("LimbsLegs"));
        private static readonly BaseForm quadruped = new(EidolonForm.Quadruped, 40, 0, true, true, false,
            new FreeEvolution("Bite"), new FreeEvolution("LimbsLegs", 2));
        private static readonly BaseForm serpentine = new(EidolonForm.Serpentine, 20, 20, false, true, true,
            new FreeEvolution("Bite"), new FreeEvolution("Climb"), new FreeEvolution("ReachBite"),
            new FreeEvolution("Tail"), new FreeEvolution("TailSlap"));

        public static BaseForm For(EidolonForm form) => form switch
        {
            EidolonForm.Biped => biped,
            EidolonForm.Quadruped => quadruped,
            EidolonForm.Serpentine => serpentine,
            _ => throw new ArgumentOutOfRangeException(nameof(form))
        };
    }
}
