using System;
using System.Collections.Generic;

namespace Loab.Sim
{
    /// <summary>
    /// Brings a loaded GameState up to the current version. Runs for every game created from a save.
    /// Rules: only ever add fields (never rename or remove), give each new field a safe default here,
    /// and bump <see cref="Current"/> whenever the saved shape changes. See DEVELOPMENT_PLAN.md task E5.
    /// </summary>
    public static class SaveMigration
    {
        /// Version 1: the original slice. Version 2: migration step introduced (no shape change).
        public const int Current = 2;

        public static void Migrate(GameState s)
        {
            if (s == null) return;

            // Anything a save might be missing (older versions, or fields JsonUtility skipped).
            s.items ??= new List<PlacedItem>();
            s.menu ??= new List<MenuEntry>();
            s.staff ??= new List<StaffMember>();
            s.applicants ??= new List<StaffMember>();
            s.regulars ??= new List<Regular>();
            s.agency ??= new List<Act>();
            s.events ??= new List<BookedEvent>();
            s.reviews ??= new List<Review>();
            s.tags ??= new List<string>();
            s.history ??= new List<NightRecord>();
            s.unlockedItems ??= new List<string>();
            s.flags ??= new List<Flag>();
            s.seenTips ??= new List<string>();
            s.cardHistory ??= new List<string>();
            s.signatures ??= new List<string>();
            s.residencyAct ??= new Act();
            s.residencyOffer ??= new Act();
            s.awareness = Resize(s.awareness, Content.Segments.Count, 0.15f);
            foreach (var m in s.staff) m.traits ??= new List<string>();
            foreach (var m in s.applicants) m.traits ??= new List<string>();
            foreach (var h in s.history)
            {
                h.vibe = Resize(h.vibe, Axis.Count, 0f);
                h.moments ??= new List<string>(); h.thoughts ??= new List<string>(); h.segCounts ??= new List<int>();
                h.reviews ??= new List<Review>(); h.stockOuts ??= new List<string>();
            }

            // Drinks added after the save was made appear off-menu with no stock.
            foreach (var d in Content.Drinks)
                if (s.menu.Find(m => m.id == d.Id) == null)
                    s.menu.Add(new MenuEntry { id = d.Id, onMenu = false, price = d.DefaultPrice, stock = 0 });

            // Items that no longer exist would break the layout; drop them (refunds aren't worth the complexity).
            s.items.RemoveAll(i => Content.Item(i.id) == null);

            if (s.version < 2)
            {
                // v1 -> v2: nothing structural; the defaults above cover it.
            }
            s.version = Current;
        }

        static float[] Resize(float[] a, int n, float fill)
        {
            if (a != null && a.Length == n) return a;
            var r = new float[n];
            for (int i = 0; i < n; i++) r[i] = a != null && i < a.Length ? a[i] : fill;
            return r;
        }
    }
}
