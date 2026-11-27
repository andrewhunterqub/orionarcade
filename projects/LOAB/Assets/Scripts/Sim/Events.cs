using System;

namespace Loab.Sim
{
    /// <summary>
    /// Everything that differs between event types, in one place (brief 7, task K6).
    /// 0 Pub Quiz, 1 Comedy Night, 2 Open Mic, 3 Live Music, 4 Karaoke, 5 Big Match Screening.
    /// </summary>
    public static class EventKinds
    {
        public const int Quiz = 0, Comedy = 1, OpenMic = 2, LiveMusic = 3, Karaoke = 4, Match = 5, Count = 6;

        public static readonly string[] Names = { "Pub Quiz", "Comedy Night", "Open Mic Night", "Live Music", "Karaoke Night", "Big Match Screening" };
        public static readonly string[] Short = { "quiz", "comedy night", "open mic", "gig", "karaoke", "match" };
        public static readonly string[] FlagKey = { "quiz", "comedy", "openmic", "gig", "karaoke", "match" };
        public static readonly string[] ViralTag = { "Best Quiz in Town", "Best Comedy Night", "Legendary Open Mic", "Best Live Music in Town", "Karaoke Legends", "Best Place to Watch the Match" };
        public static readonly string[] Hint =
        {
            "A weekly quiz builds regulars. Locals and students love it.",
            "Comedy is a gamble: high quality is safer, volatile acts can bomb. Needs a stage.",
            "Open mic is cheap and wildly unpredictable. Creatives and students love it. Needs a stage.",
            "Bands draw creatives and tourists and sell tickets on the door. Speakers help. Needs a stage.",
            "Cheap, cheerful and a bit chaotic: expect more rowdiness. Needs a screen.",
            "Sports fans pack out for the big match, especially on Big Tournament nights. Needs a screen.",
        };

        public static bool NeedsStage(int k) => k == Comedy || k == OpenMic || k == LiveMusic;
        public static bool NeedsScreen(int k) => k == Karaoke || k == Match;

        /// Why this event can't run with the current layout, or null if it can.
        public static string Requirement(int k, VibeReport v)
        {
            if (v == null) return null;
            if (NeedsStage(k) && !v.HasStage) return "needs a Small Stage (Build > Stage & Music)";
            if (NeedsScreen(k) && v.Screens == 0) return "needs a screen (Big Screen TV or projector)";
            return null;
        }

        /// Ticketed at the door (everyone pays) rather than to join in (only watchers pay).
        public static bool DoorTicket(int k) => k == Comedy || k == LiveMusic;
        public static int DefaultTicket(int k) => k == Comedy ? 5 : k == LiveMusic ? 8 : 0;
        public static int[] TicketOptions(int k) => k == Comedy ? new[] { 0, 5, 10, 15 } : k == LiveMusic ? new[] { 0, 5, 8, 12 } : new[] { 0, 1, 2 };

        /// Extra customers an event pulls in before its act's following and promotion.
        public static float BaseDraw(int k) => k switch { Quiz => 10f, Comedy => 6f, OpenMic => 7f, LiveMusic => 9f, Karaoke => 7f, _ => 8f };

        /// How keen a segment is on this kind of night (0 to about 1.4).
        public static float Affinity(SegmentDef d, int k) => k switch
        {
            Quiz => d.Quiz,
            Comedy => d.Comedy,
            OpenMic => d.Stage * 0.9f,
            LiveMusic => Math.Min(1.2f, d.Stage * 0.6f + d.Jukebox * 0.5f),
            Karaoke => Math.Min(1.2f, d.Jukebox * 0.6f + d.Games * 0.4f),
            _ => Math.Min(1.4f, d.Screens * 0.8f),
        };

        public static string StartLine(int k, string act, int joined) => k switch
        {
            Quiz => $"Pens at the ready! The quiz begins with {joined} players.",
            Comedy => $"Lights down. Comedy Night begins for a crowd of {joined}.",
            OpenMic => $"The open mic sign-up sheet is full. {joined} people are watching.",
            LiveMusic => $"{act} plug in and kick off. {joined} people at the front.",
            Karaoke => $"The karaoke screen flickers on. {joined} people are pretending they won't sing.",
            _ => $"Kick-off! {joined} people glued to the screen.",
        };

        public static string Tonight(int k, string act) => k switch
        {
            Quiz => $"Tonight: Pub Quiz with {act}, from 8pm.",
            Comedy => $"Tonight: Comedy Night headlined by {act}, from 8pm.",
            OpenMic => $"Tonight: Open Mic Night ({act}), from 8pm. Anything could happen.",
            LiveMusic => $"Tonight: live music from {act}, from 8pm.",
            Karaoke => $"Tonight: Karaoke Night with {act}, from 8pm. Warm up those vocal cords.",
            _ => "Tonight: the big match on the big screen, from 8pm.",
        };

        public static string GoodThought(int k) => k switch
        {
            Quiz => "We're definitely winning this round.",
            Comedy => "I can't breathe, this is so funny.",
            OpenMic => "That last act gave me chills.",
            LiveMusic => "This band is incredible.",
            Karaoke => "I'm doing a duet next. Don't stop me.",
            _ => "What a game!",
        };

        public static string FlopLine(int k, string act) => k switch
        {
            Quiz => "The quiz was a shambles. Two teams walked out.",
            Comedy => $"{act} died on stage. Tumbleweed.",
            OpenMic => "The open mic was... brave. Very brave.",
            LiveMusic => $"{act} spent half the set tuning up. People drifted off.",
            Karaoke => "Karaoke fizzled out after three songs and a lot of awkward silence.",
            _ => "A dull nil-nil. Half the room was on their phones.",
        };
    }
}
