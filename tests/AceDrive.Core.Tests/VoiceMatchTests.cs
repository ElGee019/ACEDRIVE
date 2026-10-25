using AceDrive.Core;

namespace AceDrive.Core.Tests;

public sealed class VoiceMatchTests
{
    [Theory]
    [InlineData("hey mazda", "mazda", true)]
    [InlineData("Hey Mazda", "mazda", true)]
    [InlineData("hey mazda!", "mazda", true)]
    [InlineData("um hey mazda can you hear me", "mazda", true)]
    [InlineData("hey ford", "mazda", false)]
    [InlineData("hey", "mazda", false)]
    [InlineData("mazda", "mazda", false)]
    [InlineData("hey alfa romeo", "alfa romeo", true)]
    [InlineData("hey alfa romeo", "mazda", false)]
    [InlineData("", "mazda", false)]
    public void IsWake_MatchesSpec(string text, string brand, bool expected) =>
        Assert.Equal(expected, VoiceMatch.IsWake(text, brand));

    [Theory]
    [InlineData("hey lancer", "lancia", true, new[] { "lancer" })]
    [InlineData("hey lancia", "lancia", true, new[] { "lancer" })]
    [InlineData("hey lancer", "lancia", false, new string[0])]
    [InlineData("hey lancer", "mazda", false, new string[0])] // no alias leak across brands
    [InlineData("hey ford", "lancia", false, new[] { "lancer" })]
    [InlineData("hey odie", "audi", true, new[] { "odie" })] // beyond brand slack: data decides
    [InlineData("hey he'll die", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey he um die", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey young guy", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey you know", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey you'll die", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey you die", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey he and i", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey and i", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey he die", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey he one day", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey he'll ny", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey he'll lay", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey jan di", "hyundai", true, new[] { "you know", "young guy", "he'll die", "he um die", "you'll die", "you die", "he and i", "and i", "he die", "he one day", "he'll ny", "he'll lay", "jan di" })]
    [InlineData("hey he die", "honda", false, new string[0])] // "he die" must not wake a honda either
    [InlineData("hey he'll die", "honda", false, new string[0])] // hyundai garble must not wake a honda
    [InlineData("the pay your lamborghini", "lamborghini", true, new[] { "your lamborghini" })] // logged 2026-10-14 wake miss
    [InlineData("pay your lamborghini", "lamborghini", false, new string[0])] // without the door the filler stands
    // Literal 2026-10-14 Maserati session: "hey maserati" kept arriving as
    // "hey majority"/"hey massage therapy"/"hey mother"/"hey missouri"/"hey
    // marseilles variety" — the interjection clean, the brand swallowed by
    // the English vocabulary. Every retry answers through the alias table.
    [InlineData("hey majority", "maserati", true, new[] { "majority", "massage therapy", "mother", "missouri", "marseilles variety" })] // logged 16:34:33, 16:34:40
    [InlineData("hey massage therapy", "maserati", true, new[] { "majority", "massage therapy", "mother", "missouri", "marseilles variety" })] // logged 16:34:37
    [InlineData("hey mother", "maserati", true, new[] { "majority", "massage therapy", "mother", "missouri", "marseilles variety" })] // logged 16:34:43
    [InlineData("hey missouri the", "maserati", true, new[] { "majority", "massage therapy", "mother", "missouri", "marseilles variety" })] // logged 16:34:46
    [InlineData("hey marseilles variety", "maserati", true, new[] { "majority", "massage therapy", "mother", "missouri", "marseilles variety" })] // logged 16:34:49
    [InlineData("hey majority", "lotus", false, new[] { "lord of" })] // a Maserati garble never answers to a Lotus brand: aliases are looked up per brand
    // Literal 2026-10-14 Mazda session: "hey mazda" kept arriving as "the
    // hey muster" and inside "... mister hey muslim" — the interjection
    // clean, the brand swallowed by the English vocabulary ("the pasta
    // pasta" shows the same substitution). The alias table answers every
    // logged retry; nothing within brand slack ever did.
    [InlineData("the hey muster", "mazda", true, new[] { "muster", "muslim", "pasta" })] // logged 16:50:05
    [InlineData("the continual duck ship mister hey muslim", "mazda", true, new[] { "muster", "muslim", "pasta" })] // logged 16:51:03
    [InlineData("hey majority", "mazda", false, new[] { "muster", "muslim", "pasta" })] // a Maserati garble stays a Maserati garble
    // Live 2026-10-14 17:13 session: "hey mazda" now lands as "hey many",
    // and after the brand switch "hey datsun" as "hey nissan" and
    // "have dodson" — the same English-vocabulary swallow, same cure.
    [InlineData("hey many", "mazda", true, new[] { "muster", "muslim", "pasta", "many" })] // logged 17:13:20
    [InlineData("hey nissan", "datsun", true, new[] { "nissan", "dodson" })] // logged 17:13:55
    [InlineData("a nissan have dodson hey nissan", "datsun", true, new[] { "nissan", "dodson" })] // logged 17:14:08
    [InlineData("hey nissan", "mazda", false, new[] { "muster", "muslim", "pasta", "many" })] // a Datsun garble stays a Datsun garble
    // Same session, post-restart 17:17: the model keeps finding new common
    // words: mazda as the two-word run "me some", datsun as "nathan".
    [InlineData("the pay me some the have me some", "mazda", true, new[] { "many", "me some" })] // logged 17:17:34
    [InlineData("hey nathan", "datsun", true, new[] { "nissan", "dodson", "nathan" })] // logged 17:17:38
    public void IsWake_AliasesCoverMishearings(string text, string brand, bool expected, string[] aliases) =>
        Assert.Equal(expected, VoiceMatch.IsWake(text, brand, aliases));

    [Theory]
    // Non-native speakers land outside the model's hearing of the brand;
    // a clean "hey" buys each brand word one Damerau slip (two for long
    // names): "awdi" substitutes, "auid" transposes, "lambogini" deletes.
    [InlineData("hey awdi", "audi", true)]
    [InlineData("hey auid", "audi", true)]
    [InlineData("hey aud", "audi", true)]
    [InlineData("hey lambogini", "lamborghini", true)]
    [InlineData("hey odie", "audi", false)]   // 3 steps: the alias table owns this case
    // The interjection itself is what gets garbled ("pay alpine" is a
    // literal transcript we logged); the brand stays exact there.
    [InlineData("pay alpine", "alpine", true)]
    [InlineData("hay mazda", "mazda", true)]
    [InlineData("say audi", "audi", true)]
    [InlineData("pay audy", "audi", false)]   // never both halves garbled
    [InlineData("they alpine", "alpine", true)] // clipped "hey" lands on a stop word; exact brand opens it
    [InlineData("the alpine", "alpine", true)]
    [InlineData("way too alpine", "alpine", false)] // brand must follow the interjection
    public void IsWake_TolerantForNonNativeSpeech(string text, string brand, bool expected) =>
        Assert.Equal(expected, VoiceMatch.IsWake(text, brand));

    // The wake scan consults the same logged-mishearing table buttons resolve
    // through, as a second voice of the transcript: the literal 2026-10-14
    // garble "hair lamborghini" (interjection three Damerau steps from "hey",
    // past the slack) wakes only because the table says hair means hey.
    // Without the table the strict rule stands.
    [Theory]
    [InlineData("hair lamborghini", "lamborghini", true)]
    [InlineData("hey lamborghini", "lamborghini", true)] // exact hearing unaffected
    [InlineData("hair mazda", "mazda", true)]
    [InlineData("a lamborghini", "lamborghini", false)] // stop-word guard stands
    [InlineData("hairlamborghini", "lamborghini", false)] // one glued word is not two
    public void IsWake_ConfusionTable_ResolvesLoggedGarbles(string text, string brand, bool expected)
    {
        var confusions = new Dictionary<string, string> { ["hair"] = "hey" };
        Assert.Equal(expected, VoiceMatch.IsWake(text, brand, null, confusions));
        Assert.False(VoiceMatch.IsWake("hair lamborghini", "lamborghini"));
    }

    // Literal 2026-10-14 Lotus session wake misses: "hey lotus" kept arriving
    // as "a lotus", "the a lotus" and "have lotus" — the interjection clipped
    // three Damerau steps past the slack — and as "the have lord of …" where
    // the brand was swallowed whole. The table's second voice wakes every
    // retry; "lot" stays doorless on purpose, because an "a lot" hearing is
    // common chatter while a cut-off "lotus" retry is not. The wall of bare
    // "the"s in the same log is what keeps stop words out as interjections
    // unless the exact brand follows: the floor never carries the driven
    // brand, so "…massive the lotus" (logged 16:04:16) is the player.
    [Theory]
    [InlineData("a lotus", true)]           // logged 14:48:22, 14:48:28, 14:48:34
    [InlineData("the a lotus", true)]       // logged 14:48:34, 14:51:19
    [InlineData("have lotus", true)]        // logged 14:50:21, 14:50:26
    [InlineData("lotus the have lotus", true)] // logged 14:50:26
    [InlineData("the have lord of the have lotus the a lotus have", true)] // logged 14:50:21
    [InlineData("hey lotus", true)]         // exact hearing unaffected
    [InlineData("halo lotus", true)]        // logged 15:55:33: "hey" heard as "halo"
    [InlineData("the have a lotus", true)]  // logged 15:19:24
    [InlineData("all said you're a massive the lotus", true)] // logged 16:04:16
    [InlineData("the lotus", true)]         // clipped interjection + exact brand
    [InlineData("lotus", false)]            // brand alone never wakes
    [InlineData("the a lot", false)]        // no "lot" door by design
    [InlineData("the a lot of", false)]     // logged 15:34:01: "a lot of" stays common speech
    [InlineData("they lotus", true)]        // clipped "hey" lands on a stop word
    [InlineData("the lord of", false)]      // aliases need a hearable interjection, not a stop word
    [InlineData("the halo to show", false)] // logged 16:01:02: no brand witness left, nothing to open
    public void IsWake_ConfusionTable_ResolvesLoggedLotusGarbles(string text, bool expected)
    {
        var confusions = new Dictionary<string, string> { ["a"] = "hey", ["have"] = "hey", ["halo"] = "hey" };
        Assert.Equal(expected, VoiceMatch.IsWake(text, "lotus", ["lord of"], confusions));
        Assert.False(VoiceMatch.IsWake("a lotus", "mazda", null, confusions)); // no brand leak
    }

    // Literal 2026-10-14 17:30:04 Mazda miss: "hey mazda" arrived as
    // "the him" — the interjection clipped onto "the" and the brand
    // garbled past alias reach. The clipped door takes exact brand runs
    // only, so the cure lives in the confusion table's second voice:
    // resolved, "the him" IS "the mazda", and the door the raw reading
    // could not open opens. Without the entry the raw reading stays
    // chatter, and the garble answers to Mazda alone, never to Datsun.
    [Theory]
    [InlineData("the him", true)]  // logged 17:30:04
    [InlineData("the them", false)] // no table entry, no door
    public void IsWake_ConfusionTable_ResolvesLoggedMazdaGarbles(string text, bool expected)
    {
        var confusions = new Dictionary<string, string> { ["him"] = "mazda" };
        Assert.Equal(expected, VoiceMatch.IsWake(text, "mazda",
            ["muster", "muslim", "pasta", "many", "me some"], confusions));
        Assert.False(VoiceMatch.IsWake("the him", "datsun", ["nissan", "dodson"], confusions)); // no brand leak
    }

    // The same storm's second class of miss: the interjection died whole
    // and the garble stands ALONE as the transcript — the logged 17:30:07
    // "pasta" (Mazda) and 17:17:36 "henderson" (Datsun). Only final
    // utterances open the solo door (a partial "pasta" is mid-utterance,
    // "pasta sauce" still to come), only whole single-word aliases walk
    // through it, and the bare brand word never does: "mazda" alone is
    // what a race report says, "pasta" alone is what the model hears the
    // player say.
    [Theory]
    [InlineData("pasta", "mazda", new[] { "muster", "muslim", "pasta", "many", "me some" }, true, true)] // logged 17:30:07
    [InlineData("pasta", "mazda", new[] { "muster", "muslim", "pasta", "many", "me some" }, false, false)] // partial: still growing
    [InlineData("henderson", "datsun", new[] { "nissan", "dodson", "nathan", "henderson" }, true, true)] // logged 17:17:36
    [InlineData("mazda", "mazda", new[] { "muster", "muslim", "pasta", "many", "me some" }, true, false)] // brand alone never wakes
    [InlineData("me some", "mazda", new[] { "muster", "muslim", "pasta", "many", "me some" }, true, false)] // not one word
    public void IsWake_FinalUtterance_SoloAliasWakesLoggedGarbles(
        string text, string brand, string[] aliases, bool isFinal, bool expected) =>
        Assert.Equal(expected, VoiceMatch.IsWake(text, brand, aliases, null, isFinal));

    [Theory]
    [InlineData("continue", "Continue")]
    [InlineData("please continue now", "Continue")]
    [InlineData("Continue!", "Continue")]
    [InlineData("hey continue", "Continue")] // wake prefix tolerated, page owns routing
    [InlineData("could you pick up the syrup", "Pick Up")]
    [InlineData("pickup", null)] // partial word is not a whole-word match
    [InlineData("go on", null)]
    [InlineData("", null)]
    public void MatchLabel_MatchesSpec(string text, string? expected) =>
        Assert.Equal(expected, VoiceMatch.MatchLabel(text, ["Continue", "Pick Up"]));

    // One utterance's tail fragment after a navigation is recognised by the
    // fired label's word run, which survives Vosk's endpoint re-score of the
    // leading words (logged 2026-10-14: "we continue" -> final "the
    // continue"). Runs must stay whole and consecutive, like MatchLabel's
    // own first door.
    [Theory]
    [InlineData("CONTINUE", "the continue", true)] // logged re-scored tail
    [InlineData("CONTINUE", "we continue", true)]
    [InlineData("CONTINUE", "continue", true)] // exact tail
    [InlineData("Where is Willy", "where is willy", true)]
    [InlineData("Where is Willy", "where is", false)] // partial word run only
    [InlineData("Continue", "taxi shift", false)] // another utterance
    [InlineData("Continue", "continues", false)] // partial word is no run
    [InlineData("Cancel Order", "the cancel order please", true)]
    public void AnswersText_WholeLabelRunSurvivesRescoring(string label, string text, bool expected) =>
        Assert.Equal(expected, VoiceMatch.AnswersText(label, text));

    [Fact]
    public void MatchLabel_EmptyCommands_NoMatch() =>
        Assert.Null(VoiceMatch.MatchLabel("anything", []));

    [Fact]
    public void MatchLabel_LongestWins() =>
        Assert.Equal("Pick Up", VoiceMatch.MatchLabel("pick up", ["Pick", "Pick Up"]));

    [Theory]
    [InlineData("where is willy", "Where is Willy")] // full label beats its keyword
    [InlineData("cento millia", "cento")]           // garbled tail rescued by keyword
    [InlineData("where is willie", "where is")]     // ditto for "Willy"
    public void MatchLabel_KeywordAliases_RescueGarbledNames(string text, string expected) =>
        Assert.Equal(expected, VoiceMatch.MatchLabel(text, ["cento", "Cento Miglia", "where is", "Where is Willy"]));

    [Theory]
    // Buttons answer to their leading words, so a long label survives being
    // cut off mid-utterance: "open new" fires what prints "OPEN NEW CAREER".
    [InlineData("open new", new[] { "OPEN NEW CAREER", "DELIVER", "BACK" }, "OPEN NEW CAREER")]
    [InlineData("okay open new please", new[] { "OPEN NEW CAREER" }, "OPEN NEW CAREER")]
    [InlineData("nieuw carriere", new[] { "NIEUW CARRIERE OPENEN", "BEZORGEN" }, "NIEUW CARRIERE OPENEN")]
    [InlineData("start taxi", new[] { "START TAXI SHIFT", "CONTINUE TAXI SHIFT" }, "START TAXI SHIFT")]
    [InlineData("continue taxi", new[] { "START TAXI SHIFT", "CONTINUE TAXI SHIFT" }, "CONTINUE TAXI SHIFT")]
    // One word is never a slice: too generic to fire anything. Two-word
    // labels stay whole-word demands ("cancel" alone does nothing).
    [InlineData("open", new[] { "OPEN NEW CAREER", "DELIVER" }, null)]
    [InlineData("cancel", new[] { "CANCEL ORDER", "BACK" }, null)]
    [InlineData("nieuw", new[] { "NIEUW CARRIERE OPENEN" }, null)]
    // A tail is not a head.
    [InlineData("new career", new[] { "OPEN NEW CAREER" }, null)]
    public void MatchLabel_LeadingWordsFireTheButton(string text, string[] labels, string? expected) =>
        Assert.Equal(expected, VoiceMatch.MatchLabel(text, labels));

    // Whole-word mishearings the English Vosk model produces on logged
    // sessions (2026-10-13 open-career retries, 2026-10-14 tier-two retries:
    // "dear to" x7, "the or two" x2, "the year two", "fear two", "gil to")
    // resolve through the data table, and the digit half of "TIER 1" labels
    // answers to the spoken number word and its homophones ("to", "for").
    [Theory]
    [InlineData("open your career", "OPEN NEW CAREER")]
    [InlineData("open you open your career", "OPEN NEW CAREER")] // stuttered retry: tail still a run
    [InlineData("open your", "OPEN NEW CAREER")]                 // slice works on resolved words too
    [InlineData("dear two", "TIER 2")]
    [InlineData("tier three", "TIER 3")]                         // word number hits the digit label
    [InlineData("please tear one", "TIER 1")]
    [InlineData("dear to", "TIER 2")]
    [InlineData("tier too", "TIER 2")]
    [InlineData("the or two", "TIER 2")]
    [InlineData("the year two", "TIER 2")]
    [InlineData("fear two", "TIER 2")]
    [InlineData("gear to", "TIER 2")]
    [InlineData("do you do", "TIER 2")]
    [InlineData("the or to", "TIER 2")]
    [InlineData("gil to", "TIER 2")]
    [InlineData("dear for", "TIER 4")]
    [InlineData("blah blah", null)]                              // table never invents a match
    [InlineData("open new career", "OPEN NEW CAREER")]            // exact hearing unaffected
    [InlineData("one", "ONE")]                                    // raw word wins its own label
    [InlineData("tier one", "TIER 1")]                            // resolved whole beats raw "ONE"
    public void MatchLabel_ConfusionTable_ResolvesLoggedGarbles(string text, string? expected)
    {
        var confusions = new Dictionary<string, string>
        {
            ["your"] = "new",
            ["knew"] = "new",
            ["dear"] = "tier",
            ["tear"] = "tier",
            ["the"] = "tier",
            ["or"] = "tier",
            ["year"] = "tier",
            ["fear"] = "tier",
            ["gear"] = "tier",
            ["do"] = "2",
            ["gil"] = "tier",
            ["one"] = "1",
            ["two"] = "2",
            ["to"] = "2",
            ["too"] = "2",
            ["you"] = "tier",
            ["three"] = "3",
            ["four"] = "4",
            ["for"] = "4",
            ["five"] = "5",
        };
        string[] labels = ["OPEN NEW CAREER", "DELIVER", "ONE", "TIER 1", "TIER 2", "TIER 3", "TIER 4"];
        Assert.Equal(expected, VoiceMatch.MatchLabel(text, labels, confusions));
    }

    [Theory]
    [InlineData("open your career")] // without a table the old strict rule stands
    [InlineData("dear two")]
    public void MatchLabel_NoTable_StaysExact(string text)
    {
        Assert.Null(VoiceMatch.MatchLabel(text, ["OPEN NEW CAREER", "TIER 2"]));
        Assert.Null(VoiceMatch.MatchLabel(text, ["OPEN NEW CAREER", "TIER 2"],
            new Dictionary<string, string>()));
    }

    // 2026-10-23 menu sessions: the English model never heard "filth" whole.
    // The attempts landed as "felt" (10:56:07, 10:56:13), "self" (10:56:10),
    // "phil" (09:44:17) and "filled" (2026-10-22 21:07:19) — the tile only
    // fired on the one attempt the model scored literally ("filled filth",
    // 10:56:24). The table is the door those readings walk through. The swap
    // is one word wide, so a phrase carrying the garbled word answers too;
    // FILTH only ever reads out, never navigates, so a stray glow costs a
    // three-second number on the menu.
    [Theory]
    [InlineData("felt", "FILTH")]
    [InlineData("phil", "FILTH")]
    [InlineData("phil s", "FILTH")]   // "phil's" normalizes to two words
    [InlineData("filled", "FILTH")]
    [InlineData("self", "FILTH")]
    [InlineData("hunger", "HUNGER")]  // the exact hearing is untouched
    [InlineData("blah blah", null)]   // the table never invents a match
    public void MatchLabel_FilthMishearings_ResolveThroughTheTable(string text, string? expected)
    {
        var confusions = new Dictionary<string, string>
        {
            ["felt"] = "filth",
            ["phil"] = "filth",
            ["filled"] = "filth",
            ["self"] = "filth",
        };
        string[] labels =
        [
            "WALLET", "JOBS", "CALENDAR",
            "TRUNK", "EMPLOYEES", "HOME",
            "HUNGER", "FILTH", "SETTINGS",
        ];

        Assert.Equal(expected, VoiceMatch.MatchLabel(text, labels, confusions));
    }

    // 2026-10-25 store session: five spoken "buy" attempts at 09:52:41-50
    // landed as "my", "boy" x3 and "why" — never the word — while BACK four
    // seconds later matched first try, so the page's voice pass was live and
    // the three-letter label simply had no door. The 2026-10-21 shop visit
    // failed the same way ("by" x2). BUY is a ONE-word label, so the table row
    // is what makes these readings reach it; the exact hearing is untouched.
    // Accepted consequence: the swap is phrase-wide, so an ambient sentence
    // carrying "my"/"why" while the shop page is up also resolves to "buy".
    // The spend is one grocery line and only with a shelf row already picked
    // (BUY hidden otherwise), while the dealer's "BUY IT" needs the second
    // word too — that is why the risk stops at the shop receipt.
    [Theory]
    [InlineData("my", "BUY")]
    [InlineData("boy", "BUY")]
    [InlineData("why", "BUY")]
    [InlineData("by", "BUY")]
    [InlineData("buy", "BUY")]        // the exact hearing is untouched
    [InlineData("back", "BACK")]      // the page's other door keeps working
    [InlineData("blah blah", null)]   // the table never invents a match
    public void MatchLabel_BuyMishearings_ResolveThroughTheTable(string text, string? expected)
    {
        var confusions = new Dictionary<string, string>
        {
            ["my"] = "buy",
            ["boy"] = "buy",
            ["why"] = "buy",
            ["by"] = "buy",
        };
        string[] labels = ["BUY", "BACK"];

        Assert.Equal(expected, VoiceMatch.MatchLabel(text, labels, confusions));
    }
}
