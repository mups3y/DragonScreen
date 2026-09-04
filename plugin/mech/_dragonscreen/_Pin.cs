// ─────────────────────────────────────────────────────────────────────────────────────────
//  THE PIN.  This file is DragonScreen's, not MechJeb's — it is the one piece of the shipped
//  `DragonScreen.Mech` assembly that is NOT vendored source, and it exists so the pin travels
//  inside the DLL rather than only in a document that can drift away from it.
//
//  §B12.1: "GPLv3 (§B2): public distribution ⇒ ship DragonScreen + the embedded MechJeb source
//  under GPLv3; pin+record the exact upstream commit."
//  §B12.1a: "take the NEWEST source at port time, then PIN it and RECORD the commit (hash +
//  date + branch) in this section and in the shipped source header."
//
//  The full record — what was excluded and why, both licence checks, and the rename shell —
//  is `plugin/mech/VENDOR.md`.  §B12.1a's entry is in `docs/BUILD_PLAN.md`.
// ─────────────────────────────────────────────────────────────────────────────────────────
namespace DragonScreen.Mech
{
    /// <summary>
    /// Provenance of the vendored MechJeb2 tree.  Read-only, compile-time constants: nothing in
    /// the flight path depends on them, they are here so the shipped binary can answer "which
    /// MechJeb is this?" without reference to the repository.
    /// </summary>
    public static class MechPin
    {
        /// <summary>Upstream repository the tree was taken from.</summary>
        public const string Repository = "MuMech/MechJeb2";

        /// <summary>
        /// Upstream BRANCH.  `dev` is MuMech's development branch — what "most up to date"
        /// (§B12.1a) resolves to for this project.  "Most up to date" governed what was
        /// fetched; "pinned" governs everything after, and there is no obligation to track
        /// upstream from here.  A re-pin is its own task.
        /// </summary>
        public const string Branch = "dev";

        /// <summary>Exact upstream commit. This, not the branch, is the pin.</summary>
        public const string Commit = "c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa";

        /// <summary>Date of that commit.</summary>
        public const string CommitDate = "2026-08-08";

        /// <summary>Date DragonScreen vendored it (register T15a).</summary>
        public const string VendoredOn = "2026-09-05";

        /// <summary>Licence of the combined work. See `plugin/mech/VENDOR.md` for both checks.</summary>
        public const string License = "GPLv3";
    }
}
