# The SpaceX stream callout vocabulary, assembled from IN-REPO transcribed sources only (C7), and
# sized against the bottom bar's measured centre cell.
RefW, RefH = 3427.0, 2112.0
h = 1406
sc = h / RefH
CELL = 475.0                 # design px between the bar's two rules - MEASURED off component_48
PAD = 24.0
CAP = 0.6638                 # MarginAffordance.CapAdvance, D-DIN caps, measured off a render
floor = 32.0 / sc            # Typography.MinDesignFor at the shipped 2560x1406
usable = CELL - 2 * PAD
per_line = usable / (CAP * floor)

# (phase, callout as the stream says it, in-repo source)
ROWS = [
    ("Countdown", "GO FOR PROP LOAD",       "CREW_MISSION_TELEMETRY 5, G4"),
    ("Countdown", "LES ARMED",              "CREW_MISSION_TELEMETRY 5, G5"),
    ("Countdown", "DRAGON ON INT POWER",    "CREW_MISSION_TELEMETRY 5, G6"),
    ("Countdown", "GO FOR LAUNCH",          "CREW_MISSION_TELEMETRY 5, G7"),
    ("Ascent",    "LIFTOFF",                "CREW_MISSION_TELEMETRY 5, MET 0:00:00"),
    ("Ascent",    "MAX-Q",                  "CREW_MISSION_TELEMETRY 5, MET 0:00:58"),
    ("Ascent",    "MECO",                   "CREW_MISSION_TELEMETRY 5, MET 0:02:37"),
    ("Ascent",    "STAGE SEPARATION",       "CREW_MISSION_TELEMETRY 5, MET 0:02:40"),
    ("Ascent",    "SES-1",                  "CREW_MISSION_TELEMETRY 5, MET 0:02:48"),
    ("Ascent",    "SECO-1",                 "CREW_MISSION_TELEMETRY 5, MET 0:08:50"),
    ("Ascent",    "DRAGON SEPARATION",      "CREW_MISSION_TELEMETRY 5, MET 0:12:03"),
    ("Ascent",    "NOSECONE OPEN",          "CREW_MISSION_TELEMETRY 5, MET 0:12:48"),
    ("Booster",   "BOOSTBACK BURN",         "PHASE/booster docs (13 mentions)"),
    ("Booster",   "ENTRY BURN",             "CREW_MISSION_TELEMETRY 5, MET 0:07:29"),
    ("Booster",   "LANDING BURN",           "CREW_MISSION_TELEMETRY 5, MET 0:08:59"),
    ("Rendezvous", "PHASE BURN",            "CREW_MISSION_TELEMETRY 6a"),
    ("Rendezvous", "BOOST BURN",            "CREW_MISSION_TELEMETRY 6a"),
    ("Rendezvous", "CLOSE BURN",            "CREW_MISSION_TELEMETRY 6a"),
    ("Rendezvous", "TRANSFER BURN",         "CREW_MISSION_TELEMETRY 6a"),
    ("Rendezvous", "COELLIPTIC BURN",       "CREW_MISSION_TELEMETRY 6a"),
    ("Rendezvous", "GO FOR AI BURN",        "CREW_MISSION_TELEMETRY 6a, G9"),
    ("Rendezvous", "AI BURN",               "CREW_MISSION_TELEMETRY 6a"),
    ("Rendezvous", "MIDCOURSE BURN",        "CREW_MISSION_TELEMETRY 6a"),
    ("Prox-ops",  "WAYPOINT 0 - 400 M",     "CREW_MISSION_TELEMETRY 6a, G10"),
    ("Prox-ops",  "WAYPOINT 1 - 220 M",     "CREW_MISSION_TELEMETRY 6a, G11"),
    ("Prox-ops",  "WAYPOINT 2 - 20 M",      "CREW_MISSION_TELEMETRY 6a, G12"),
    ("Prox-ops",  "GO FOR DOCKING",         "CREW_MISSION_TELEMETRY 6a, G12"),
    ("Docking",   "SOFT CAPTURE",           "CREW_MISSION_TELEMETRY 6a, contact/capture"),
    ("Docking",   "DOCKING COMPLETE",       "CREW_MISSION_TELEMETRY 6a, hard capture"),
    ("Return",    "GO FOR UNDOCK",          "CREW_MISSION_TELEMETRY 6b, G14"),
    ("Return",    "DEPARTURE BURN 0",       "CREW_MISSION_TELEMETRY 6b"),
    ("Return",    "PHASING BURN",           "CREW_MISSION_TELEMETRY 6b"),
    ("Return",    "TRUNK JETTISON",         "CREW_MISSION_TELEMETRY 6b"),
    ("Return",    "GO FOR DEORBIT BURN",    "CREW_MISSION_TELEMETRY 6b, G15"),
    ("Return",    "DEORBIT BURN",           "CREW_MISSION_TELEMETRY 6b"),
    ("Return",    "NOSECONE CLOSED",        "EXTRACT_RETURN_CONTROL 8"),
    ("Entry",     "ENTRY INTERFACE",        "PHASE_6 2, ~120 km"),
    ("Entry",     "DROGUES DEPLOYED",       "PHASE_6 4, ~5.5 km"),
    ("Entry",     "MAINS DEPLOYED",         "PHASE_6 4, ~1.8 km"),
    ("Entry",     "SPLASHDOWN",             "PHASE_6 4"),
]

print("cell %.0f design px, %.0f usable, floor %.2f design px" % (CELL, usable, floor))
print("budget: %.1f chars per line, %.1f over two lines" % (per_line, 2 * per_line))
print()
one = two = over = 0
worst = []
for phase, text, src in ROWS:
    n = len(text)
    if n <= per_line:
        verdict, one = "1 line", one + 1
    elif n <= 2 * per_line:
        verdict, two = "2 lines", two + 1
    else:
        verdict, over = "OVERFLOW", over + 1
        worst.append((n, text))
    print("  %-11s %-22s %2d  %-8s  %s" % (phase, text, n, verdict, src))
print()
print("%d callouts: %d fit on ONE line, %d need two, %d overflow" % (len(ROWS), one, two, over))
if worst:
    print("  overflow:", worst)
