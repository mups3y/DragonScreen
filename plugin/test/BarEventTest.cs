// Tests for pure/BarEvent.cs — S179: the bottom bar's centre-cell event announcement.
//
// ⛔ THE THREE THINGS THIS FILE EXISTS TO STOP, in the order they would actually happen:
//
//  1. A CALLOUT THAT DOES NOT FIT. The cell is 475 design px and the type is at the glanceable floor,
//     which is 13.4 characters a line. Every claim about the fit in BarEvent's header is re-derived
//     here, at BOTH shipped widths, for ALL 40 — because "I measured it once" is how QC C-05 was
//     recorded as safe twice.
//  2. A WORD WIDER THAN A LINE. A wrap can never break a word, so the average-advance estimator is not
//     enough on its own: one wide word inside a short string passes a character count and overflows on
//     the glass. Checked per word, not per string.
//  3. AN EVENT WITH NO SOURCE. `BarEvent` owns no trigger, and the catalogue must stay exactly as long
//     as the enum — a value added to one and not the other would draw an empty box.
using System;
using DragonScreen;

public static class BarEventTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const int W1 = 1280, H1 = 703;
    const int W2 = 2560, H2 = 1406;

    public static int Run()
    {
        Console.WriteLine("DragonScreen bottom-bar event announcement (S179)");
        checks = 0; failures = 0;

        TheCatalogueIsComplete();
        EveryCalloutFitsTheCell();
        NoWordIsWiderThanALine();
        TheWrapValidatesBothLines();
        TheBoxSitsBetweenTheRules();
        NoneDrawsNothing();
        AnEventDoesNotMoveTheBar();
        TheMutationsThatSurvived();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (" + BarEvent.Count + " callouts, "
                          + BarEvent.Usable.ToString("0") + " design px of text width)");
        return failures;
    }

    // ---- 1. THE CATALOGUE ----------------------------------------------------------------------
    static void TheCatalogueIsComplete()
    {
        BarCallout[] all = (BarCallout[])Enum.GetValues(typeof(BarCallout));
        Check("the enum carries None plus the owner's 40", all.Length == BarEvent.Count + 1,
              all.Length + " values, expected " + (BarEvent.Count + 1));

        int blank = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == BarCallout.None) continue;
            if (BarEvent.Text(all[i]).Length == 0) { blank++; Console.WriteLine("    no label: " + all[i]); }
        }
        Check("every callout has a label", blank == 0, blank + " without one");

        // ⛔ An out-of-range value must draw NOTHING, never a nearby callout. A byte enum can hold a
        // value no member names - a save from a later build, say - and the honest answer is silence.
        Check("an unknown value has no text", BarEvent.Text((BarCallout)200).Length == 0, "");
        Check("None has no text", BarEvent.Text(BarCallout.None).Length == 0, "");

        // No duplicates: two callouts reading the same words would make the box ambiguous about which
        // event raised it, which is the whole point of announcing one.
        int dupes = 0;
        for (int i = 0; i < all.Length; i++)
            for (int j = i + 1; j < all.Length; j++)
                if (all[i] != BarCallout.None && all[j] != BarCallout.None
                    && BarEvent.Text(all[i]) == BarEvent.Text(all[j])) dupes++;
        Check("no two callouts read the same", dupes == 0, dupes + " duplicate pair(s)");
    }

    // ---- 2. THE FIT, FOR ALL 40, AT BOTH SHIPPED WIDTHS ----------------------------------------
    static void EveryCalloutFitsTheCell()
    {
        int[,] sizes = { { W1, H1 }, { W2, H2 } };
        for (int s = 0; s < sizes.GetLength(0); s++)
        {
            int w = sizes[s, 0], h = sizes[s, 1];
            string at = " @" + w + "x" + h;
            // ⛔ THE SIZE THE CODE ACTUALLY DRAWS AT — `Typography.BarDesign`, the bar's own named
            // floor (owner D1/D2, S176 edit 3). It was `MinDesignFor` here until 2026-09-07, and a
            // test that fits against a size the draw does not use proves nothing about the draw.
            float size = Typography.BarDesign;

            int over = 0, twoLine = 0, oneLine = 0;
            string worst = ""; float worstW = 0f;
            foreach (BarCallout c in (BarCallout[])Enum.GetValues(typeof(BarCallout)))
            {
                if (c == BarCallout.None) continue;
                string text = BarEvent.Text(c), l1, l2;
                BarEvent.Wrap(text, size, out l1, out l2);

                // It must wrap to at most TWO lines, and both must fit. A third line has nowhere to go:
                // the box is the vertical rules' own 97-design-px band.
                Check(c + ": line 1 fits" + at, BarEvent.Width(l1, size) <= BarEvent.Usable + 0.01f,
                      "\"" + l1 + "\" is " + BarEvent.Width(l1, size).ToString("0.0")
                      + " of " + BarEvent.Usable.ToString("0.0"));
                if (l2 == null) oneLine++;
                else
                {
                    twoLine++;
                    Check(c + ": line 2 fits" + at, BarEvent.Width(l2, size) <= BarEvent.Usable + 0.01f,
                          "\"" + l2 + "\" is " + BarEvent.Width(l2, size).ToString("0.0")
                          + " of " + BarEvent.Usable.ToString("0.0"));
                    Check(c + ": the wrap loses nothing" + at, l1 + " " + l2 == text,
                          "\"" + l1 + "\" + \"" + l2 + "\" != \"" + text + "\"");
                }
                if (BarEvent.Width(l1, size) > BarEvent.Usable + 0.01f) over++;
                if (l2 != null && BarEvent.Width(l2, size) > worstW)
                { worstW = BarEvent.Width(l2, size); worst = l2; }
                if (BarEvent.Width(l1, size) > worstW) { worstW = BarEvent.Width(l1, size); worst = l1; }
            }
            Check("nothing overflows" + at, over == 0, over + " callout(s) overflow");
            Check("the whole catalogue is covered" + at, oneLine + twoLine == BarEvent.Count,
                  (oneLine + twoLine) + " of " + BarEvent.Count);
            Console.WriteLine("  note  " + oneLine + " one-line, " + twoLine + " two-line" + at
                              + "; widest line \"" + worst + "\" " + worstW.ToString("0.0")
                              + " of " + BarEvent.Usable.ToString("0.0") + " design px");
        }
    }

    // ---- 3. THE ONE THE CHARACTER COUNT CANNOT SEE ----------------------------------------------
    // ⛔ A WRAP CAN NEVER BREAK A WORD. `Width` is a per-character AVERAGE, so a short string with one
    // wide word can pass every check above and still put ink outside the box. This is the check that
    // makes the header's claim - "not one word is too wide for a line" - a fact rather than a note.
    static void NoWordIsWiderThanALine()
    {
        float size = Typography.BarDesign;   // as drawn — see EveryCalloutFitsTheCell
        string widest = ""; float widestW = 0f;
        int bad = 0;
        foreach (BarCallout c in (BarCallout[])Enum.GetValues(typeof(BarCallout)))
        {
            if (c == BarCallout.None) continue;
            string[] words = BarEvent.Text(c).Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                float ww = BarEvent.Width(words[i], size);
                if (ww > widestW) { widestW = ww; widest = words[i]; }
                if (ww > BarEvent.Usable) bad++;
            }
        }
        Check("no single word is wider than a line", bad == 0, bad + " word(s) too wide");
        Console.WriteLine("  note  widest single word \"" + widest + "\" " + widestW.ToString("0.0")
                          + " of " + BarEvent.Usable.ToString("0.0") + " design px");
    }

    // ---- 3b. THE WRAP'S CONTRACT, ON A STRING THE CATALOGUE DOES NOT CONTAIN --------------------
    // ⛔ WRITTEN BECAUSE A MUTATION SURVIVED. Reverting `Wrap` to "fill line 1, dump the rest on line
    // 2" failed nothing — not because the greedy version is correct, but because at today's 395-px
    // budget the two algorithms happen to agree on all 40 strings. They did NOT agree at 379, where
    // greedy left `DEORBIT BURN` 3.9 px outside the box. So the catalogue cannot prove this property
    // and a SYNTHETIC string must: one whose greedy split overflows line 2 and whose balanced split
    // does not. The function has to be right for the 41st callout, not just the 40 that exist.
    //
    // ⛔ CORRECTED IN PLACE 2026-09-07 (S179 job 3) — THE SENTENCE "They did NOT agree at 379" IS
    // FALSE, and it is the same wrong claim `BarEvent.Wrap` carried in its own body. C1.16 / G12: kept
    // and marked, not deleted. Re-measured over all 40 callouts at 48.07 AND 29 design px, and at
    // padding 16 AND 24 — 120 comparisons — the two forms are BYTE-IDENTICAL, 0 differences. At 379
    // they agreed too: greedy left `DEORBIT BURN` outside the box and so did the validating form,
    // because when no split satisfies both bounds it falls back to the balanced one, which for that
    // string IS the greedy one. The padding is what fixed it, not the algorithm.
    //
    // ⭐ THE REST OF THE PARAGRAPH STANDS, AND SO DOES THIS TEST. The catalogue genuinely cannot prove
    // the contract — now less than ever, since at 29 all 40 fit on ONE line and never reach the split
    // path at all. A synthetic string is the only way to exercise it, and it is exercised for the 41st
    // callout. ⚠ Mutation `R1` remains an EQUIVALENT MUTANT against the catalogue and is reported as
    // one; this check is what gives the contract teeth on a string the catalogue does not contain.
    static void TheWrapValidatesBothLines()
    {
        float size = Typography.BarDesign;   // as drawn — see EveryCalloutFitsTheCell
        float per = BarEvent.Usable / (MarginAffordance.CapAdvance * size);   // chars a line holds

        // "AA…A B…B": a short first word and a long second, sized so greedy (which takes the longest
        // fitting line 1) would leave the tail over the budget, while splitting earlier fits both.
        int shortW = (int)(per * 0.35f), longW = (int)(per * 0.80f);
        string text = new string('A', shortW) + " " + new string('B', shortW)
                    + " " + new string('C', longW);

        string l1, l2;
        BarEvent.Wrap(text, size, out l1, out l2);
        Check("a synthetic three-word callout still wraps to two lines", l2 != null, "did not split");
        Check("...line 1 fits", BarEvent.Width(l1, size) <= BarEvent.Usable + 0.01f,
              l1 + " -> " + BarEvent.Width(l1, size).ToString("0.0"));
        Check("...and line 2 fits, which is the half greedy did not check",
              l2 != null && BarEvent.Width(l2, size) <= BarEvent.Usable + 0.01f,
              (l2 ?? "") + " -> " + (l2 == null ? 0f : BarEvent.Width(l2, size)).ToString("0.0"));
        Check("...and nothing is lost or duplicated", l2 != null && l1 + " " + l2 == text, "");

        // A single word longer than a line cannot be split at all - the honest answer is to leave it
        // whole and overflow visibly, never to cut a word in half.
        string mono = new string('W', (int)(per * 1.5f));
        BarEvent.Wrap(mono, size, out l1, out l2);
        Check("an unsplittable word is left whole rather than cut", l1 == mono && l2 == null,
              "got \"" + l1 + "\" / \"" + (l2 ?? "null") + "\"");
    }

    // ---- 4. THE BOX IS INSIDE THE CELL, ON EVERY FIT --------------------------------------------
    // The cell is bounded by the bar's two vertical rules, which are MEASURED asset geometry. The box
    // must clear both on all three BarFit maps - including Split, where the cell straddles the reflow
    // step and the two edges are mapped by different branches.
    static void TheBoxSitsBetweenTheRules()
    {
        int[,] sizes = { { W1, H1 }, { W2, H2 } };
        BarFit[] fits = { BarFit.Frame, BarFit.Stretch, BarFit.Split };
        for (int s = 0; s < sizes.GetLength(0); s++)
        {
            int w = sizes[s, 0], h = sizes[s, 1];
            for (int f = 0; f < fits.Length; f++)
            {
                BarFit fit = fits[f];
                string at = " @" + w + "x" + h + " " + fit;
                float rule1 = BottomBar.MapX(BottomBar.Rule1X, w, h, fit);
                float rule2 = BottomBar.MapX(1943f, w, h, fit);
                // ⛔ READ OFF THE COMMANDS THE DRAW ACTUALLY EMITTED, not recomputed from the same
                // constants the draw used. Written the recomputing way first, and a mutation that
                // pulled the box's right edge in by a whole Inset SAILED STRAIGHT PAST it: the check
                // moved with the defect because both read `CellR - Inset`. This is the P7 shape again
                // and it is the second time in this session, so it is written down here too.
                float bx0, bx1;
                DrawnBox(w, h, fit, out bx0, out bx1);

                Check("the box clears the first rule" + at, bx0 > rule1, bx0 + " vs " + rule1);
                Check("the box clears the second rule" + at, bx1 < rule2, bx1 + " vs " + rule2);
                Check("the box has positive width" + at, bx1 > bx0, "w " + (bx1 - bx0));

                // ⭐ AND IT IS SYMMETRIC IN THE CELL. Under Split the cell straddles the reflow step,
                // so the two gaps are computed through DIFFERENT branches of SplitReflow.X and agreeing
                // is a real property rather than an identity.
                float gapL = bx0 - BottomBar.MapX(BarEvent.CellL, w, h, fit);
                float gapR = BottomBar.MapX(BarEvent.CellR, w, h, fit) - bx1;
                Check("the box is centred in its cell" + at, Math.Abs(gapL - gapR) < 0.01f,
                      "left " + gapL.ToString("0.0") + ", right " + gapR.ToString("0.0"));
            }
        }
    }

    /// <summary>The x-extent of the box the draw ACTUALLY emitted, from its own commands. RoundRect
    /// lays a full-height middle rect between two edge rects, so the union of the Hairline-coloured
    /// rects is the box.</summary>
    static void DrawnBox(int w, int h, BarFit fit, out float x0, out float x1)
    {
        DisplayList dl = new DisplayList(64);
        BarEvent.Draw(dl, BarCallout.GoForDeorbitBurn, w, h, fit);
        x0 = float.MaxValue; x1 = float.MinValue;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (!Same(c.Colour, DragonPalette.Hairline)) continue;
            if (c.A < x0) x0 = c.A;
            if (c.A + c.C > x1) x1 = c.A + c.C;
        }
    }

    static bool Same(Rgba a, Rgba b)
    {
        return Math.Abs(a.R - b.R) < 1e-4f && Math.Abs(a.G - b.G) < 1e-4f
            && Math.Abs(a.B - b.B) < 1e-4f;
    }


    // ---- 7. ⛔ THE FIVE THINGS A MUTATION RUN PROVED NOTHING GUARDED (S179, 2026-09-07) ----------
    // The R1-R8 set was re-run against this file after the owner's D1 landed, and SEVEN OF EIGHT
    // SURVIVED. Two of those are equivalent mutants and are reported as such, below. The other five
    // were real holes, and this section is them. ⭐ THE WORST WAS R3 — reverting the box to the
    // glanceable floor, i.e. UNDOING THE OWNER'S OWN RULING — and it failed nothing at all, because
    // every check in this file computed its own `size` and none ever asked what the DRAW used. That
    // is the same shape as S176 edit 3's M1, one file over: a suite derived entirely from the value
    // under test cannot notice that value changing.
    static void TheMutationsThatSurvived()
    {
        int[,] sizes = { { W1, H1 }, { W2, H2 } };
        BarFit[] fits = { BarFit.Frame, BarFit.Stretch, BarFit.Split };

        // ---- R2: the padding is BORROWED, not chosen. Pin the decision, not just the number. ----
        Check("R2: TextPad is Inset, so the box's inner and outer breathing space match",
              BarEvent.TextPad == BarEvent.Inset,
              "TextPad " + BarEvent.TextPad + ", Inset " + BarEvent.Inset
              + " - 16 was a squeeze at the glanceable floor and that floor is gone");

        // ---- R7: the gate -> callout map, now that it is PURE and reachable at all ----
        Check("R7: a gate the stream does not call maps to None, not to something near it",
              BarEvent.ForGate(GateId.HatchCloseG3) == BarCallout.None, "G3 raised something");
        Check("R7: G5 raises LES ARMED", BarEvent.ForGate(GateId.LesArmG5) == BarCallout.LesArmed,
              "got " + BarEvent.ForGate(GateId.LesArmG5));
        Check("R7: G15 raises GO FOR DEORBIT BURN",
              BarEvent.ForGate(GateId.DeorbitGoG15) == BarCallout.GoForDeorbitBurn,
              "got " + BarEvent.ForGate(GateId.DeorbitGoG15));
        // ⛔ AND NO TWO GATES MAY RAISE THE SAME CALLOUT. R7 mutated G5 to Meco and nothing noticed;
        // a collision is the general form of that, and it makes the box ambiguous about its cause.
        int collisions = 0, mapped = 0;
        GateId[] gates = (GateId[])Enum.GetValues(typeof(GateId));
        for (int i = 0; i < gates.Length; i++)
        {
            BarCallout a = BarEvent.ForGate(gates[i]);
            if (a == BarCallout.None) continue;
            mapped++;
            for (int j = i + 1; j < gates.Length; j++)
                if (BarEvent.ForGate(gates[j]) == a) collisions++;
        }
        Check("R7: no two gates raise the same callout", collisions == 0,
              collisions + " collision(s)");
        Check("R7: the gate map covers the 11 gates the stream calls", mapped == 11,
              mapped + " gates mapped, expected 11");

        for (int si = 0; si < sizes.GetLength(0); si++)
        {
            int w = sizes[si, 0], h = sizes[si, 1];
            float k = BottomBar.Scale(h);
            for (int f = 0; f < fits.Length; f++)
            {
                BarFit fit = fits[f];
                string at = " @" + w + "x" + h + " " + fit;

                DisplayList dl = new DisplayList(64);
                BarEvent.Draw(dl, BarCallout.GoForDeorbitBurn, w, h, fit);

                // ---- R3: THE SIZE THE DRAW ACTUALLY EMITTED. The owner's D1, pinned. ----
                float drawn = -1f;
                for (int i = 0; i < dl.Count; i++)
                    if (dl.At(i).Kind == DrawKind.Text) drawn = dl.At(i).C;
                Check("R3: the box draws at the BAR's own floor, not the glanceable one" + at,
                      drawn > 0f && Math.Abs(drawn - Typography.BarDesign * k) < 0.01f,
                      "drew " + drawn.ToString("0.000") + ", BarDesign*k is "
                      + (Typography.BarDesign * k).ToString("0.000") + ", the glanceable floor would be "
                      + (Typography.MinDesignFor(w, k) * k).ToString("0.000"));

                // ---- R5: the box's VERTICAL extent. Only its x was ever checked. ----
                float y0 = float.MaxValue, y1 = float.MinValue;
                for (int i = 0; i < dl.Count; i++)
                {
                    DrawCmd c = dl.At(i);
                    if (c.Kind != DrawKind.Rect || !Same(c.Colour, DragonPalette.Hairline)) continue;
                    if (c.B < y0) y0 = c.B;
                    if (c.B + c.D > y1) y1 = c.B + c.D;
                }
                Check("R5: the box's top is where BoxTop says" + at,
                      Math.Abs(y0 - BarEvent.BoxTop * k) < 0.01f,
                      "top " + y0.ToString("0.0") + ", want " + (BarEvent.BoxTop * k).ToString("0.0"));
                Check("R5: the box's bottom is where BoxBottom says" + at,
                      Math.Abs(y1 - BarEvent.BoxBottom * k) < 0.01f,
                      "bottom " + y1.ToString("0.0") + ", want "
                      + (BarEvent.BoxBottom * k).ToString("0.0"));
                // ⭐ AND IT IS INSIDE THE BAR, which is the property a reader actually cares about:
                // the box may not hang out of the component that owns it, at any fit.
                float bx, by, bw, bh;
                BottomBar.Rect(w, h, fit, out bx, out by, out bw, out bh);
                Check("R5: the box sits inside the bar's own box" + at,
                      y0 >= by - 0.01f && y1 <= by + bh + 0.01f,
                      "box " + y0.ToString("0.0") + ".." + y1.ToString("0.0")
                      + ", bar " + by.ToString("0.0") + ".." + (by + bh).ToString("0.0"));

                // ---- R6: CAPACITY. `Commands` is a promise every page's constant is built on. ----
                DisplayList tight = new DisplayList(BarEvent.Commands);
                BarEvent.Draw(tight, BarCallout.GoForDeorbitBurn, w, h, fit);
                Check("R6: the worst case fits in BarEvent.Commands" + at, !tight.Overflowed,
                      "overflowed a list of " + BarEvent.Commands);
            }
        }

        // ⛔ AND THE BAR'S OWN CONSTANT MUST COVER THE BOX IT NOW DRAWS. R6 reverted
        // `BottomBar.Commands` to 20 and NOTHING failed - every page sizes its list off that constant,
        // so the first page to draw an event would have silently dropped commands off the end.
        Check("R6: BottomBar.Commands covers the box it draws",
              BottomBar.Commands >= 20 + BarEvent.Commands,
              "BottomBar.Commands " + BottomBar.Commands + ", needs 20 + " + BarEvent.Commands);
        // ⚠ ONE DRAW CALL, NOT TWO. `BottomBar.Draw` already calls `BarEvent.Draw` itself - written
        // the other way first here, and the double draw overflowed and read exactly like a product
        // defect. Worth the comment: the box has ONE caller and the bar owns it.
        PageState ev = new PageState(); ev.Event = BarCallout.GoForDeorbitBurn;
        BarFit[] allFits = { BarFit.Frame, BarFit.Stretch, BarFit.Split };
        for (int f = 0; f < allFits.Length; f++)
        {
            DisplayList bar = new DisplayList(BottomBar.Commands);
            BottomBar.Draw(bar, W2, H2, ev, allFits[f]);
            Check("R6: the bar with an event raised fits BottomBar.Commands " + allFits[f],
                  !bar.Overflowed, "overflowed a list of " + BottomBar.Commands);
        }
    }

    // ---- 5. NO EVENT, NO BOX --------------------------------------------------------------------
    static void NoneDrawsNothing()
    {
        DisplayList a = new DisplayList(64);
        BarEvent.Draw(a, BarCallout.None, W2, H2, BarFit.Frame);
        Check("None draws nothing at all", a.Count == 0, a.Count + " command(s)");

        DisplayList b = new DisplayList(64);
        BarEvent.Draw(b, (BarCallout)200, W2, H2, BarFit.Frame);
        Check("an unknown value draws nothing", b.Count == 0, b.Count + " command(s)");

        // ⛔ AND THE DEFAULT PageState RAISES NOTHING. A struct's default must be silence, or every
        // page that does not set Event would announce whatever value 0 happened to name.
        PageState fresh = new PageState();
        Check("a default PageState raises no callout", fresh.Event == BarCallout.None,
              "got " + fresh.Event);

        DisplayList bar = new DisplayList(64);
        BottomBar.Draw(bar, W2, H2, fresh, BarFit.Frame);
        int boxes = 0;
        for (int i = 0; i < bar.Count; i++)
            if (bar.At(i).Kind == DrawKind.ArcBand) boxes++;
        Check("...so the bar draws no box on a fresh state", boxes == 0, boxes + " arc(s)");
    }

    // ---- 6. AN EVENT MUST NOT MOVE THE BAR ------------------------------------------------------
    // ⛔ The announcement is a READOUT: it takes no touch and it must not shift anything the crew aims
    // at. This is the S176 coupling applied to the new element - draw, hit and marker move together or
    // not at all, and here "not at all" is the correct answer.
    static void AnEventDoesNotMoveTheBar()
    {
        PageState quiet = new PageState(); quiet.Valid = true; quiet.Phase = "ORBIT";
        PageState loud = quiet; loud.Event = BarCallout.GoForDeorbitBurn;

        for (int i = 0; i < BottomBar.IconX.Length; i++)
        {
            float bx, by, bw, bh;
            BottomBar.Rect(W2, H2, BarFit.Split, out bx, out by, out bw, out bh);
            float k = BottomBar.Scale(H2);
            float cx = bx + (BottomBar.IconX[i] + BottomBar.IconS * 0.5f) * k;
            float cy = by + (BottomBar.IconY - 1877f + BottomBar.IconS * 0.5f) * k;
            Check("icon " + i + " still hits itself while an event is up",
                  BottomBar.Hit(cx, cy, W2, H2, BarFit.Split) == i,
                  "got " + BottomBar.Hit(cx, cy, W2, H2, BarFit.Split));
        }

        // CURRENT STATE is the bar's one live readout and sits immediately left of the cell; an event
        // must not displace it by a pixel.
        Check("CURRENT STATE is drawn in the same place either way",
              Math.Abs(ValueX(quiet) - ValueX(loud)) < 0.001f,
              ValueX(quiet) + " vs " + ValueX(loud));
    }

    static float ValueX(PageState s)
    {
        DisplayList dl = new DisplayList(64);
        BottomBar.Draw(dl, W2, H2, s, BarFit.Split);
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str == "ORBIT") return dl.At(i).A;
        return float.NaN;
    }
}
