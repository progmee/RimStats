using RimWorld.Planet;
using Verse;
using System;
using RimWorld;

namespace RimStats {
    public class StatsWorldComponent : WorldComponent {
        public StatsWorldComponent(World world) : base(world) {}

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            int invervalTicks = (int) (RimStatsMod.settings.statsRegisterIntervalDays * 60_000f);
            if (invervalTicks > 0 && Find.TickManager.TicksGame % invervalTicks == 0) RegisterData();
        }

        private void RegisterData() {
            if (!RimStatsMod.settings.registerStatsEnabled) return;

            Map map = Find.AnyPlayerHomeMap;

            if (map == null) return;

            int randSeed = Find.World.info.Seed;;
            string factionName = Faction.OfPlayer.Name;
            float wealth = map.wealthWatcher.WealthTotal;
            float wealthBuildings = map.wealthWatcher.WealthBuildings;
            float wealthItems = map.wealthWatcher.WealthItems;
            int colonists = map.mapPawns.ColonistCount;
            int tick = Find.TickManager.TicksGame;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            StatsData statsData = new StatsData(randSeed, tick, factionName, wealth, wealthItems, wealthBuildings, colonists, timestamp);

            if (DatabaseManager.InsertData(statsData) && RimStatsMod.settings.logEnabled) {
                Log.Message($"{RimStatsMod.Prefix} Stats data successfully registered");
            }
        }
    }
}