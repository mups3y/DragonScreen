#!/usr/bin/env python3
# Audit every Crew Dragon recording -> per-flight kerbal outcome, for the KLM scoreboard.
import csv, glob, os, math, json, sys

FOLDERS = [
    r"C:/Program Files (x86)/Steam/steamapps/common/Kerbal Space Program/DragonScreen_capture",
    r"C:/Users/User/Desktop/quarantine/dragonscreen_flightdata",
]
def num(v):
    try:
        x=float(v)
        return None if (math.isnan(x) or math.isinf(x)) else x
    except: return None

def col(hdr, *names):
    for n in names:
        if n in hdr: return n
    return None

def classify(path):
    try:
        rows=list(csv.DictReader(open(path, newline="")))
    except: return None
    if len(rows) < 8: return None            # junk / instant-death / not a real flight
    hdr=rows[0].keys()
    cMet=col(hdr,"met_s","met","ut"); cAlt=col(hdr,"alt_m","alt")
    cVs=col(hdr,"vspeed_mps","vspeed"); cSrf=col(hdr,"srf_speed_mps","srf_speed","srfspeed")
    cPe=col(hdr,"pe_km","pe"); cAp=col(hdr,"ap_km","ap")
    cAb=col(hdr,"fdir_abort","abort"); cAbm=col(hdr,"abort_mode")
    cPh=col(hdr,"mission_phase","phase")
    cDeorb=col(hdr,"deorbit_phase"); cDep=col(hdr,"dep_phase"); cEntry=col(hdr,"entry_phase")
    cMetK=col(hdr,"met_s","met","ut")
    if not cAlt: return None
    last=rows[-1]
    metStart=num(rows[0].get(cMetK)) if cMetK else None
    metEnd=num(last.get(cMetK)) if cMetK else None
    # RETURNING = the crew are actively on their way home (undock/departure, deorbit burn, or a DeorbitReturn
    # abort) when the recording ends, still alive and above the ground. That is NOT "stranded" (no return) —
    # it is a return IN PROGRESS. If a later contiguous segment shows them splashed, the merge upgrades it.
    ACTIVE=lambda v:(v or "").strip() not in ("","-","Idle","None")
    returning=False
    for r in rows:
        if (cDeorb and ACTIVE(r.get(cDeorb))) or (cEntry and ACTIVE(r.get(cEntry))) \
           or (cDep and ACTIVE(r.get(cDep))) or (cAbm and (r.get(cAbm) or "").strip()=="DeorbitReturn"):
            returning=True; break
    fAlt=num(last.get(cAlt))                 # metres (alt_m) — old 'alt' may be km; detect below
    fPh=(last.get(cPh) or "").strip() if cPh else ""
    # IMPACT speed = the PEAK descent speed in the final approach to the surface (alt < 1500 m), NOT the
    # at-rest speed after splashdown (which is ~0 and hid the fatal 122 m/s aborts).
    def peakLowSpeed():
        best=None
        for r in rows:
            a=num(r.get(cAlt))
            if a is None or a>=1500: continue
            v=abs(num(r.get(cVs)) or 0.0) if cVs else 0.0
            s=abs(num(r.get(cSrf)) or 0.0) if cSrf else 0.0
            cand=max(v,s)
            if best is None or cand>best: best=cand
        return best
    impactPeak=peakLowSpeed()
    fVs=num(last.get(cVs)) if cVs else None
    fSrf=num(last.get(cSrf)) if cSrf else None
    # abort fired anywhere?
    abort=False
    if cAb or cAbm:
        for r in rows:
            if cAb and (num(r.get(cAb)) or 0)>0.5: abort=True; break
            if cAbm and (r.get(cAbm) or "").strip() not in ("","None"): abort=True; break
    # reached orbit? pe stayed >100 km with ap>100
    orbit=False
    if cPe and cAp:
        for r in rows:
            pe=num(r.get(cPe)); ap=num(r.get(cAp))
            if pe is not None and ap is not None and pe>100 and ap>100: orbit=True; break
    # normalise final alt to metres (alt_m already m; a bare 'alt' from old schema may be m too — assume m)
    altM = fAlt
    # classify
    fate=None; how=""
    cameDown = (altM is not None and altM < 3000) or fPh in ("Splashdown","Splashed","Landed","Mains","Drogues")
    inSpace = (altM is not None and altM > 70000)
    if cameDown:
        spd = impactPeak if impactPeak is not None else 0.0    # true impact velocity
        if spd <= 12.0:
            if abort: fate="abort"; how="abort - safe splashdown %.0f m/s"%spd
            elif orbit: fate="home"; how="mission flown, safe splashdown %.0f m/s"%spd
            else: fate="abort" if abort else "survived"; how="safe landing %.0f m/s"%spd
        else:
            fate="died"
            how=("abort chutes under-decelerated - " if abort else "")+"impact %.0f m/s"%spd
    elif inSpace:
        if returning: fate="returning"; how="return underway (deorbit/abort) - crew alive, not yet home"
        elif orbit: fate="stranded"; how="left in orbit, no return - mission not completed"
        else: fate="stranded"; how="stuck in space, suborbital/incomplete"
    else:
        spd=abs(fVs) if fVs is not None else 0.0
        if spd>60 and altM is not None and altM<40000: fate="died"; how="uncontrolled descent %.0f m/s"%spd
        else: fate="survived"; how="test ended mid-flight (crew alive)"
    date=os.path.basename(path).replace("Crew-2_","").split("_")[0]  # YYYYMMDD
    d="%s-%s-%s"%(date[0:4],date[4:6],date[6:8]) if len(date)==8 and date.isdigit() else date
    return dict(file=os.path.basename(path), date=d, rows=len(rows), abort=abort, orbit=orbit,
               fAltKm=(altM/1000 if altM is not None else None), metStart=metStart, metEnd=metEnd,
               returning=returning,
               impact=(round(impactPeak,1) if impactPeak is not None else None), phase=fPh, fate=fate, how=how)

# ⛔ COUNT ONLY FLIGHTS FROM THE RESET FORWARD (user 2026-08-28: "count from now on; clear both").
# SINCE is a filename stamp YYYYMMDD_HHMMSS; a recording counts only if its stamp is STRICTLY newer.
# Pass it as argv[2], else read the stored baseline (dashboard/klm_since.txt), else count everything.
import re as _re
SINCE = sys.argv[2] if len(sys.argv) > 2 else None
if SINCE is None:
    try: SINCE = open(os.path.join(os.path.dirname(__file__),"klm_since.txt")).read().strip()
    except: SINCE = "00000000_000000"
def stampOf(path):
    m = _re.search(r"(\d{8}_\d{6})", os.path.basename(path))
    return m.group(1) if m else "00000000_000000"

recs=[]
for fo in FOLDERS:
    for p in sorted(glob.glob(os.path.join(fo,"Crew-2_2*.csv"))):
        if "Probe" in p: continue
        if stampOf(p) <= SINCE: continue          # before/at the reset → excluded
        r=classify(p)
        if r: recs.append(r)
print("counting recorded segments with stamp > %s : %d segment(s)"%(SINCE,len(recs)))

# ---- MERGE CONTIGUOUS SEGMENTS INTO MISSIONS (one crew, not one-per-file) ----
# met is MISSION-elapsed time from the pad (≈0 at launch). A CSV whose met CONTINUES a prior one (metStart
# well above 0 and ≈ the CURRENT mission's metEnd) is the SAME mission reloaded/continued — NOT a fresh crew.
# ⛔ Process in CHRONOLOGICAL (filename-stamp) order, NOT sorted by metStart: a continuation immediately
# follows its own launch in time, so "sorted by metStart" would scramble every continuation onto the wrong
# launch (it once glued three unrelated returns into one mission). A segment starting near met 0 is a fresh
# pad launch = new mission; a high metStart that continues the current mission's metEnd extends it.
recs.sort(key=lambda r: stampOf(r["file"]))     # chronological
LAUNCH_MET=120.0
missions=[]
for r in recs:
    ms=r.get("metStart")
    cont=(missions and ms is not None and ms>=LAUNCH_MET
          and missions[-1]["metEnd"] is not None
          and abs(ms-missions[-1]["metEnd"])<=missions[-1]["metEnd"]*0.02+600.0)  # met continues (small gap)
    if cont:
        m=missions[-1]; m["segs"].append(r); m["metEnd"]=r.get("metEnd"); m["rows"]+=r["rows"]
    else:
        missions.append(dict(segs=[r],metEnd=r.get("metEnd"),rows=r["rows"]))
# collapse each mission to a single rec-like dict, keyed on its LATEST-met segment
recs=[dict(fate=m["segs"][-1]["fate"], how=m["segs"][-1]["how"], date=m["segs"][-1]["date"],
           returning=m["segs"][-1].get("returning",False), rows=m["rows"],
           segs=[s["file"] for s in m["segs"]]) for m in missions]
print("merged -> %d mission(s)"%len(recs))

from collections import Counter
tally=Counter(r["fate"] for r in recs)
print("=== %d Crew-2 mission(s) classified ==="%len(recs))
for k in ["died","home","stranded","abort","returning","survived",None]:
    print("  %-9s %d"%(str(k),tally.get(k,0)))
print("\n=== DIED flights ===")
for r in recs:
    if r["fate"]=="died": print("  %s  rows=%d  %s"%(r["date"],r["rows"],r["how"]))
print("\n=== HOME (hero) flights ===")
for r in recs:
    if r["fate"]=="home": print("  %s  %s"%(r["date"],r["how"]))
print("\n=== ABORT-SAFE flights ===")
for r in recs:
    if r["fate"]=="abort": print("  %s  %s"%(r["date"],r["how"]))
print("\n=== STRANDED flights ===")
for r in recs:
    if r["fate"]=="stranded": print("  %s  %s"%(r["date"],r["how"]))
print("\n=== RETURNING (crew alive, coming home - outcome pending) ===")
for r in recs:
    if r["fate"]=="returning": print("  %s  %s  [%s]"%(r["date"],r["how"],"+".join(r["segs"])))
# save for wall-building
json.dump(recs, open(os.path.join(os.path.dirname(__file__),"audit_recs.json"),"w"), indent=1)

# ---- build the KLM scoreboard data (counter souls + dated memorial plaques) ----
CREW = ["Shane Kimbrough","Megan McArthur","Akihiko Hoshide","Thomas Pesquet"]
died=[r for r in recs if r["fate"]=="died"]
stranded=[r for r in recs if r["fate"]=="stranded"]
abort=[r for r in recs if r["fate"]=="abort"]
home=[r for r in recs if r["fate"]=="home"]
returning=[r for r in recs if r["fate"]=="returning"]
surv=[r for r in recs if r["fate"]=="survived"]
# per-KERBAL rosters (each name ONCE, with a tally of missions in that fate + total flown + the dates).
from collections import defaultdict
diedN=defaultdict(int); homeN=defaultdict(int); flownN=defaultdict(int)
diedDates=defaultdict(list); homeDates=defaultdict(list)
for r in recs:
    for name in CREW:                       # every Crew-2 flight carries the whole crew
        flownN[name]+=1
        if r["fate"]=="died": diedN[name]+=1; diedDates[name].append(r["date"])
        if r["fate"]=="home": homeN[name]+=1; homeDates[name].append(r["date"])
def roster(cnt, dates):
    return [{"name":n,"tally":cnt[n],"flown":flownN[n],
             "first":min(dates[n]) if dates[n] else None,"last":max(dates[n]) if dates[n] else None}
            for n in CREW if cnt[n]>0]
klm={
 "counter":{"died":len(died)*4,"stranded":len(stranded)*4,"abortSafe":len(abort)*4,
            "rescued":0,"home":len(home)*4,"returning":len(returning)*4,
            "flights":len(recs),"incomplete":len(surv)},
 "memorial":roster(diedN, diedDates),       # [{name, tally, flown, first, last}] — dead once, ×tally
 "heroes":roster(homeN, homeDates),
 "crew":CREW,
 "auditedFlights":len(recs),
 "since":SINCE,
}
json.dump(klm, open(os.path.join(os.path.dirname(__file__),"klm_data.json"),"w"), indent=1)
print("\n=== KLM SCOREBOARD (souls, 4 crew/mission) ===")
print("  died %d  stranded %d  abort-safe %d  rescued %d  home %d  returning %d  |  %d mission(s) (%d incomplete-survived)"
      %(klm["counter"]["died"],klm["counter"]["stranded"],klm["counter"]["abortSafe"],
        klm["counter"]["rescued"],klm["counter"]["home"],klm["counter"]["returning"],
        klm["counter"]["flights"],klm["counter"]["incomplete"]))
print("  memorial plaques: %d fatal mission(s)"%len(klm["memorial"]))
print("saved klm_data.json")
