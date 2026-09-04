// DragonScreen — BoosterHostPlan  (PURE: every decision the §B16 booster HOST makes, minus the flying)
// ============================================================================================
// ⛔ NOT a restored file. Written by W23 (2026-09-04) as the pure half of the booster host — the thing
// that runs `pure/BoosterDescent.cs` on the separated first stage. W8 built the script; nothing ran it.
// The glue is `src/BoosterHost.cs`; everything decidable WITHOUT the game lives here and is headless-
// tested (`test/BoosterHostTest.cs`), per the pure/glue law.
//
// OWNER DIRECTION, 2026-09-04, via the overseer: *"we use MechJeb for all upper stage manoeuvres as
// planned. BOOSTER SCRIPTED."* and *"as soon as the booster gets dropped it runs its script."* Two
// systems, two vessels, and they must not interfere with each other's flights.
//
// ============================================================================================
// WHAT THIS FILE DECIDES — four questions, each separately testable
// ============================================================================================
//  1. WHICH VESSEL is the booster (`Select`) — and, far more importantly, which one is NOT. The Dragon
//     must never be bound. Three INDEPENDENT tests each exclude it on their own (see `Select`).
//  2. WHEN TO STOP (`StopReason`) — destroyed, unloaded, recovered, landed-and-settled, bind lost.
//  3. WHETHER A COMMAND MAY GO OUT AT ALL THIS TICK (`Blocked`) — the arm, the unpack requirement, the
//     octaweb table, and the flight-194334 hold-off.
//  4. WHICH ENGINE SET a `BoosterCommand` names (`CommandedRole`) — including the `EngineMode == 0`
//     ambiguity the command struct warns about.
//
// ⛔ WHAT IT DOES NOT DECIDE — THE STEERING LAW. `BoosterDescent.Guide()` emits a unit `AimForward`;
// turning that into torque is a CONTROL LAW and it is deliberately, explicitly OUT of this task. That
// component already existed once — `AttitudePilot` / `AttitudeController` / `pure/AttitudeLoop.cs` — and
// it is the piece that failed: RCS chatter, a roll under-control, an attitude limit cycle, DS-ASC-007's
// *"RCS loss = ~97% attitude"*, reverted three times, ordered stripped by the owner (`70dc239`), and
// filed ⛔ **RECOVER-REFERENCE ONLY — never live code (owner directive)** by R1 §3.2. The booster's
// steering law gets its OWN register line, its own scrutiny and its own gate (register **W24**).
// Until it lands: attitude is UNCOMMANDED, and `src/BoosterHost.cs` says so on every line it logs.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>Why a bind attempt did or did not produce a booster. Anything but `Ok` = do not fly.</summary>
    public enum BoosterBind : byte
    {
        Ok,
        NoVessel,             // nothing to consider at all
        NoSeparatedBooster,   // no candidate is a lone S1 stage (still stacked, or none present)
        Ambiguous,            // more than one separated booster — REFUSE to pick (§B16.4's rule)
        ForeignVehicle        // a Kartoffelkuchen KK_SPX / KK_F9demo part — the WRONG Falcon 9
    }

    /// <summary>One vessel as the host sees it, reduced to the facts the selection turns on. The glue
    /// fills this from `FlightGlobals.Vessels`; nothing here knows what a `Vessel` is.</summary>
    public struct BoosterCandidate
    {
        public bool Exists;                 // the reference is alive (not destroyed / not null)
        public bool IsActive;               // this is `FlightGlobals.ActiveVessel`
        public bool Loaded;                 // parts in memory (the precondition for everything below)
        public bool HasBoosterPart;         // any part matching `VehicleParts.IsBooster` (the ".S1." marker)
        public bool HasPod;                 // any part matching `VehicleParts.IsPod` — THE DRAGON TEST
        public bool HasForeignBoosterPart;  // any `OctawebBinding.IsForeignBoosterPart` — the other Falcon 9
    }

    /// <summary>Why the host let go of the booster. `None` = keep flying it.</summary>
    public enum BoosterHostStop : byte
    {
        None,
        Destroyed,      // the Vessel reference died
        Unloaded,       // out of load range — no parts, no control path, nothing to command
        Landed,         // down, and §B16.7 step 3's settle has elapsed — hand it to the recovery (W9)
        BecameActive,   // focus moved ONTO the booster — §B16.7 says that never happens; if it does, let go
        BindLost        // the octaweb table no longer matches this vessel
    }

    /// <summary>The bound booster's live state, again reduced to what the host's lifecycle turns on.</summary>
    public struct BoosterFlightSnapshot
    {
        public bool Exists;
        public bool Loaded;
        public bool Packed;              // LOADED-but-PACKED: physics partial, CONTROL NO (see the header of Blocked)
        public bool IsActive;
        public bool LandedOrSplashed;
        public bool OctawebStillValid;   // `OctawebEngines.StillValid(v)`
        public double SinceLandedS;      // seconds since `LandedOrSplashed` first went true (0 = not landed)
    }

    /// <summary>Why NO command left the host this tick. `None` = the command path is open.</summary>
    public enum BoosterCommandBlock : byte
    {
        None,
        NotArmed,             // `BoosterHost.Actuate` is false — the default, and the reason is in its comment
        Packed,               // loaded but PACKED: KSP does not run the control path for a packed vessel
        NoOctaweb,            // the §B16.4 guard refused, or the table went stale — never guess at an engine
        WrongEngineForPhase,  // OCT3 — the commanded bank is illegal in this flight phase (see `PhaseAllows`)
        HoldOff               // too close to the stack / too soon after separation — flight 194334
    }

    public static class BoosterHostPlan
    {
        // =========================================================================================
        // THE HOLD-OFF — the one interlock this file adds, and it is FLIGHT-EVIDENCED, not invented.
        // =========================================================================================
        // Flight 194334 (2026-08-30, commit `8225df7`, finding A1/A2, confirmed ≥2 ways from the
        // upper-stage CSV + the booster probe CSV + KSP.log):
        //
        //   "Booster recovery self-destructs: fires thr=1.0 0.3 s after MECO at 'sep 0 km', attitude
        //    diverges 2→85 deg, LOST in ~10 s — and its 0-km burn kicks the upper stage."
        //   "Separation kicks the upper stage (yaw +14.7, roll −3.7 dps) as the booster ignites at 0 km."
        //
        // The owner's fix was `BoosterControl.LetFall = true` — engines off, hold attitude, fall. That
        // file is deleted and stays deleted; the LESSON is not. It is the single most direct evidence in
        // this repo about the owner's *"two systems that must NOT interfere with each other's flights"*:
        // a booster that lights its engines next to the stack disturbs the stack. So the host commands
        // NOTHING until the booster is both far enough away and long enough gone.
        //
        // ⚠ BOTH FIGURES ARE [UN-CONVERGED] FOR RSS-RO (§B16.8 ruling 2). 194334 gives the failing
        // point (0 km, 0.3 s) and therefore a lower bound; it gives no converged safe value, and no
        // recorded flight in this repo establishes one. They are placeholders that make the interlock
        // exist — evidence of nothing, re-converged from a recorded re-flight like every other booster
        // constant. Raising them only ever costs propellant margin; lowering them costs a booster.
        [Tunable] public static double HoldOffSeparationM = 500.0;   // [UN-CONVERGED] 194334 burned at 0 m
        [Tunable] public static double HoldOffSinceBindS  = 10.0;    // [UN-CONVERGED] 194334 burned at 0.3 s

        /// <summary>§B16.7 step 3's *"+10 s settle after touchdown, so the landed state is stable before
        /// anything is asked of it"* — a PLAN figure, not a new constant. The host holds the binding for
        /// this long after touchdown and then lets go; step 4 (auto-recover) and step 5 (PRE off) belong
        /// to the recovery conductor (register W9), not here.</summary>
        public const double LandedSettleS = 10.0;

        /// <summary>⛔ REFUSE TO BIND THE ACTIVE VESSEL. §B16.7 is categorical — *"FOCUS NEVER LEAVES THE
        /// UPPER STAGE"* — so on the designed protocol the booster is never active and this costs nothing.
        /// It is kept as a THIRD independent guarantee that the Dragon can never be bound, on top of
        /// "carries no pod" and "carries an S1 part". A vessel we are focused on is a vessel the player is
        /// flying; the host does not take it. (Consequence: focusing the booster to watch it stops the
        /// host — see Q2 in the open questions.)</summary>
        public const bool RequireNonActive = true;

        // =========================================================================================
        // 1. SELECTION — which vessel, and above all which NOT
        // =========================================================================================

        /// <summary>
        /// Pick the separated booster out of the loaded vessels, or refuse. `index` is the winner's index
        /// in `c`, or −1.
        ///
        /// ⛔ THE DRAGON IS EXCLUDED THREE TIMES OVER, and each test alone is sufficient:
        ///   (a) it CARRIES A POD (`VehicleParts.IsPod`) — a candidate with a pod is never taken;
        ///   (b) it carries NO `.S1.` booster part — a candidate without one is never taken;
        ///   (c) it is the ACTIVE vessel — refused by `RequireNonActive`.
        /// That redundancy is deliberate. Binding the wrong vessel here is not a bug, it is an autopilot
        /// flying the crew.
        ///
        /// ⛔ AND THE OTHER FALCON 9 IS EXCLUDED FIRST. §B16.4's hard assertion: the owner installed
        /// Kartoffelkuchen's Launchers Pack on 2026-09-03 and it ships its own booster + octaweb. A
        /// `KK_SPX` / `KK_F9demo` part ANYWHERE among the candidates loses outright — we refuse rather
        /// than pick, exactly as `OctawebBinding.Bind` does, because a booster controller that binds the
        /// wrong vehicle is a lost booster with no error message.
        ///
        /// ⛔ AND TWO BOOSTERS ARE AMBIGUOUS, NOT A CHOICE. Same rule, same reason.
        /// </summary>
        public static BoosterBind Select(BoosterCandidate[] c, out int index)
        {
            index = -1;
            if (c == null || c.Length == 0) return BoosterBind.NoVessel;

            // The foreign-vehicle veto runs FIRST and scans EVERYTHING — a KK part on any loaded craft
            // means the wrong Falcon 9 is in this scene and we do not guess which octaweb is ours.
            for (int i = 0; i < c.Length; i++)
                if (c[i].Exists && c[i].Loaded && c[i].HasForeignBoosterPart) return BoosterBind.ForeignVehicle;

            int found = 0, first = -1;
            for (int i = 0; i < c.Length; i++)
            {
                if (!IsSeparatedBooster(c[i])) continue;
                found++;
                if (first < 0) first = i;
            }
            if (found == 0) return BoosterBind.NoSeparatedBooster;
            if (found > 1) return BoosterBind.Ambiguous;

            index = first;
            return BoosterBind.Ok;
        }

        /// <summary>The identity test, per candidate. A SEPARATED booster is loaded, carries at least one
        /// `.S1.` part, carries NO Dragon pod (that is what "separated" means for this stack), and — per
        /// `RequireNonActive` — is not the vessel the player is flying.</summary>
        public static bool IsSeparatedBooster(BoosterCandidate v)
        {
            if (!v.Exists || !v.Loaded) return false;
            if (v.HasForeignBoosterPart) return false;
            if (RequireNonActive && v.IsActive) return false;
            if (v.HasPod) return false;              // ⛔ THE DRAGON TEST — a pod means crew, never ours
            return v.HasBoosterPart;
        }

        /// <summary>One screen/log-ready line for a refusal; null on `Ok` (a good bind is silent, the same
        /// convention `OctawebBinding.Annunciation` uses).</summary>
        public static string Annunciation(BoosterBind b)
        {
            switch (b)
            {
                case BoosterBind.NoVessel: return "BOOSTER HOST — NO VESSELS TO CONSIDER";
                case BoosterBind.NoSeparatedBooster: return "BOOSTER HOST — NO SEPARATED BOOSTER (still stacked, or none)";
                case BoosterBind.Ambiguous: return "BOOSTER HOST — MORE THAN ONE SEPARATED BOOSTER, REFUSING TO PICK";
                case BoosterBind.ForeignVehicle: return "BOOSTER HOST — FOREIGN BOOSTER PART (KK Falcon 9), BINDING REFUSED";
                default: return null;
            }
        }

        // =========================================================================================
        // 2. STOPPING — cleanly, and for a stated reason
        // =========================================================================================

        /// <summary>Should the host let go of the booster this tick, and why? Ordered most-final first,
        /// so the reason reported is the real one. `None` = hold the binding.</summary>
        public static BoosterHostStop StopReason(BoosterFlightSnapshot s)
        {
            if (!s.Exists) return BoosterHostStop.Destroyed;
            if (!s.Loaded) return BoosterHostStop.Unloaded;          // out of range: no parts, no control path
            if (s.IsActive && RequireNonActive) return BoosterHostStop.BecameActive;
            if (s.LandedOrSplashed && s.SinceLandedS >= LandedSettleS) return BoosterHostStop.Landed;
            if (!s.OctawebStillValid) return BoosterHostStop.BindLost;
            return BoosterHostStop.None;
        }

        public static string Annunciation(BoosterHostStop r)
        {
            switch (r)
            {
                case BoosterHostStop.Destroyed: return "booster gone (vessel destroyed)";
                case BoosterHostStop.Unloaded: return "booster unloaded (out of physics range — PRE is register W9's)";
                case BoosterHostStop.Landed: return "booster down and settled (+" + LandedSettleS.ToString("F0")
                                                    + " s, §B16.7 step 3) — handing off";
                case BoosterHostStop.BecameActive: return "focus moved ONTO the booster — releasing (§B16.7)";
                case BoosterHostStop.BindLost: return "octaweb table no longer valid for this vessel";
                default: return null;
            }
        }

        // =========================================================================================
        // 3. THE COMMAND GATE — may anything at all go out this tick?
        // =========================================================================================

        /// <summary>
        /// ⛔ `Packed` IS A REAL BLOCK, NOT DEFENSIVE PADDING. KSP has THREE vessel states, not two
        /// (`docs/BOOSTER_RECOVERY_ARCHITECTURE.md` §1.1): UNLOADED (on rails), LOADED-but-PACKED
        /// (partial physics, **NO control**) and LOADED+UNPACKED (full physics, **control yes**). The
        /// constraint on commanding a booster is UNPACKED, not ACTIVE — see the evidence block in
        /// `src/BoosterHost.cs`. A packed booster accepts nothing, so the host says so rather than
        /// writing into a vessel that cannot act on it. Keeping it unpacked at range is
        /// PhysicsRangeExtender's job (`src/RangeExtender.cs`, §B16.7 step 1) and belongs to the
        /// recovery conductor, register W9 — the host never widens ranges itself, because that writes
        /// `vesselRanges` on EVERY vessel including the Dragon.
        ///
        /// Ordered so the reported reason is the most fundamental one that applies.
        /// </summary>
        /// <summary>
        /// `phase` / `commandedRole` default to `Idle` / `None` — always mutually legal (see
        /// <see cref="PhaseAllows"/>) — so every pre-OCT3 caller (every test that predates the phase gate)
        /// keeps compiling and keeps its answer unchanged; only a caller that names a real phase and role
        /// exercises the new check. `landingShed` (OCT6) is the FSM's one-way shed latch, read only in
        /// `LandingBurn`; it defaults to the un-shed state the burn starts in.
        /// </summary>
        public static BoosterCommandBlock Blocked(bool armed, BoosterFlightSnapshot s,
                                                  double separationM, double sinceBindS,
                                                  BoosterPhase phase = BoosterPhase.Idle,
                                                  EngineRole commandedRole = EngineRole.None,
                                                  bool landingShed = false)
        {
            if (!armed) return BoosterCommandBlock.NotArmed;
            if (s.Packed) return BoosterCommandBlock.Packed;
            if (!s.OctawebStillValid) return BoosterCommandBlock.NoOctaweb;
            if (!PhaseAllows(phase, commandedRole, landingShed)) return BoosterCommandBlock.WrongEngineForPhase;
            if (HoldingOff(separationM, sinceBindS)) return BoosterCommandBlock.HoldOff;
            return BoosterCommandBlock.None;
        }

        /// <summary>Flight 194334's interlock. `separationM &lt;= 0` means the glue could not measure a
        /// separation — that is treated as TOO CLOSE, never as clear, because "we don't know how far away
        /// the crew are" is not a reason to light nine engines.</summary>
        public static bool HoldingOff(double separationM, double sinceBindS)
        {
            if (!(separationM > 0.0) || separationM < HoldOffSeparationM) return true;
            if (!(sinceBindS >= HoldOffSinceBindS)) return true;
            return false;
        }

        public static string Annunciation(BoosterCommandBlock b)
        {
            switch (b)
            {
                case BoosterCommandBlock.NotArmed:
                    return "NOT ARMED — BoosterHost.Actuate is false (no steering law yet; register W24)";
                case BoosterCommandBlock.Packed:
                    return "booster PACKED — KSP runs no control path for a packed vessel (PRE is register W9's)";
                case BoosterCommandBlock.NoOctaweb:
                    return "no valid octaweb table — refusing to command an engine we have not bound";
                case BoosterCommandBlock.WrongEngineForPhase:
                    return "WRONG ENGINE FOR PHASE — an ascent/landing bank commanded outside its phase (OCT3)";
                case BoosterCommandBlock.HoldOff:
                    return "HOLD-OFF — too close to / too soon after the stack (flight 194334: a 0-km burn kicks it)";
                default: return null;
            }
        }

        // =========================================================================================
        // 4. ENGINE-ROLE MAPPING — and the ambiguity the command struct warns about
        // =========================================================================================

        /// <summary>`VehicleParts.Mode*` → the octaweb `EngineRole` the bound table is keyed on. Anything
        /// that is not one of the three modes is `None`, never a guess.</summary>
        public static EngineRole RoleFor(int engineMode)
        {
            if (engineMode == VehicleParts.ModeCentreOnly) return EngineRole.OctawebCentre;   // 2 → CenterOnly
            if (engineMode == VehicleParts.ModeThreeEngine) return EngineRole.OctawebThree;   // 1 → ThreeLanding
            if (engineMode == VehicleParts.ModeAllEngines) return EngineRole.OctawebAll;      // 0 → AllEngines
            return EngineRole.None;
        }

        /// <summary>
        /// ⛔ THE ONE THAT MATTERS. `BoosterCommand.EngineMode == 0` is `ModeAllEngines` AND the struct's
        /// default value — the command struct says so in its own comment — so the mode alone can never be
        /// read as "light the nine". **`EnginesLit` is the authority**: no lit flag, no engine, whatever
        /// the mode says. Every dispatch in `src/BoosterHost.cs` goes through this and nothing else.
        /// </summary>
        public static EngineRole CommandedRole(bool enginesLit, int engineMode)
        {
            return enginesLit ? RoleFor(engineMode) : EngineRole.None;
        }

        // =========================================================================================
        // 4b. THE PHASE GATE — OCT3 (owner, 2026-09-05, verbatim): *"you must differentiate between
        // landing mode etc for the engines as we cannot use landing mode during accent and vice versa"*.
        // =========================================================================================
        // ⛔ `BoosterPhase` (`pure/BoosterDescent.cs`) has NO ascent state — this host runs ONLY on the
        // separated booster, so every phase it knows is a DESCENT phase. `EngineRole.OctawebAll` (the
        // nine-nozzle, liftoff set — `VehicleParts.ModeAllEngines`) therefore belongs to a vehicle state
        // this host never occupies, and is refused OUTRIGHT in every phase, not merely the ones that
        // happen not to ask for it. The three/centre banks are further pinned to the ONE descent phase
        // each is measured to burn in (§B16.2 method); everywhere else, only OFF (`EngineRole.None`) is
        // legal. This is the second half of OCT3, alongside `VehicleParts.ModeOff`: that gave OFF an
        // honest value, this refuses the ambiguous one outright if it is ever commanded anyway.

        // ⛔ OCT6 (owner ruling, 2026-09-05) — THE LANDING BURN HAS TWO LEGAL BANKS, IN A DEFINED ORDER.
        // OCT3-Q1 asked whether the landing burn flies one engine or three; the owner answered *"1. (2)"*
        // — option (2), `ThreeLanding` shedding to `CenterOnly` — and, on what triggers the shed,
        // *"yes to computing from current hover slam solver"*. So `LandingBurn` is no longer a
        // one-bank phase. **It is still a ONE-BANK GATE**: the phase plus the FSM's one-way shed latch
        // names exactly one legal bank, Three before the shed and Centre after, and the other is refused
        // in each half. That is the whole reason the latch is passed in rather than the gate simply being
        // taught to accept both banks in this phase — accepting both would leave `ThreeLanding` legal late
        // in the burn, which the latch forbids but a widened gate would no longer refuse.
        //
        // `landingShed` defaults to FALSE — "the burn has not shed yet", which is the state a landing
        // burn STARTS in, so the default is the burn's own initial state rather than a neutral. A caller
        // that names `LandingBurn` and forgets the latch therefore gets the EARLY half's answer: it
        // refuses the terminal `CenterOnly` command. That is the fail-safe direction (a refusal, never a
        // wrong actuation), and `src/BoosterHost.cs` passes the latch from the SAME `BoosterCommand` it
        // decodes the role from, so the gate and the FSM read one latch on one tick.

        /// <summary>The ONE engine bank a phase may legally light. `EngineRole.None` = no bank may be lit
        /// at all in this phase — see the OCT3 table (Idle · Flip · Coast · AeroDescent · Landed).
        /// `landingShed` is the FSM's one-way shed latch (`BoosterCommand.LandingShedLatched`) and is read
        /// only in `LandingBurn`, where it picks which half of the burn we are in.</summary>
        public static EngineRole AllowedRoleForPhase(BoosterPhase phase, bool landingShed = false)
        {
            switch (phase)
            {
                case BoosterPhase.Boostback:
                case BoosterPhase.EntryBurn:
                    return EngineRole.OctawebThree;
                case BoosterPhase.LandingBurn:
                    // OCT6: three engines brake, one flies the touchdown — and once shed, never back.
                    return landingShed ? EngineRole.OctawebCentre : EngineRole.OctawebThree;
                default:
                    return EngineRole.None;            // Idle, Flip, Coast, AeroDescent, Landed
            }
        }

        /// <summary>May `role` be commanded while the FSM is in `phase`? `None` (off) is always legal —
        /// refusing to command nothing would make the interlock itself a hazard. `OctawebAll` is refused
        /// in EVERY phase this enum can name (the comment block above explains why); any other role must
        /// match the phase's one legal bank exactly — which in `LandingBurn` is the one the shed latch
        /// names (OCT6).</summary>
        public static bool PhaseAllows(BoosterPhase phase, EngineRole role, bool landingShed = false)
        {
            if (role == EngineRole.None) return true;
            if (role == EngineRole.OctawebAll) return false;
            return role == AllowedRoleForPhase(phase, landingShed);
        }

        // =========================================================================================
        // 5. THE TARGET MODE — resolved from an IN-REPO source, never invented
        // =========================================================================================

        /// <summary>
        /// The mode's parameter block for a resolved mission. This is the consumer W8 recorded
        /// `BoosterDescent.TargetModeFor` as lacking: `Missions.Resolve(vesselName)` → `RecoveryMode` →
        /// `TargetMode` → `BoosterProfile`. The catalog is in-repo and its `RecoveryMode` column was
        /// independently sourced against public flight records by S66/LZ1
        /// (`docs/reference/LZ_RECOVERY_TABLE.md`), so nothing here is invented.
        ///
        /// ⛔ AN UNRESOLVED MISSION FALLS BACK TO **ASDS**, NOT RTLS. `MissionProfile.Valid == false`
        /// means the craft name matched nothing, and `Missions.Fallback` must not be flown as if it were
        /// a plan. ASDS's default boostback magnitude is ZERO (§B16.2's C1.8 OVERRIDE), so the fallback
        /// profile is the INERT one — it reproduces the old four-phase "no boostback" behaviour exactly,
        /// which is the right thing to do when we do not know the mission. RTLS would command a return
        /// burn toward an aim point we do not have.
        /// </summary>
        public static BoosterProfile ProfileFor(MissionProfile mission)
        {
            if (!mission.Valid) return BoosterProfile.For(TargetMode.Asds);
            return BoosterProfile.For(BoosterDescent.TargetModeFor(mission.Recovery));
        }
    }
}
