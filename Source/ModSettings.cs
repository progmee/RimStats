using Verse;

namespace RimStats {
    public class RimStatsSettings : ModSettings {
        public float statsRegisterIntervalDays = 1f;
        public bool logEnabled = true;
        public bool registerEventsEnabled = true;
        public bool registerStatsEnabled = true;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref statsRegisterIntervalDays, "statsRegisterIntervalDays", 1f);
            Scribe_Values.Look(ref logEnabled, "logEnabled", true);
            Scribe_Values.Look(ref registerEventsEnabled, "registerEventsEnabled", true);
            Scribe_Values.Look(ref registerStatsEnabled, "registerStatsEnabled", true);
        }


    }
}