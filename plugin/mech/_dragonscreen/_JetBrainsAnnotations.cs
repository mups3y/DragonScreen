// ─────────────────────────────────────────────────────────────────────────────────────────
//  DragonScreen's, not MechJeb's.  A BUILD DEPENDENCY SUBSTITUTION, recorded in VENDOR.md.
//
//  Upstream MechJeb2 pulls `JetBrains.Annotations` 2023.3.0 from NuGet, and 46 of its files
//  open with `extern alias JetBrainsAnnotations;`.  This build is `csc.exe` straight against
//  KSP's managed assemblies — no IDE, no MSBuild, no NuGet (build.py's opening comment) — so
//  that package cannot be resolved here, and an `extern alias` needs a real second assembly
//  to alias, which a single-assembly build cannot give it.
//
//  So the rename shell folds `using JetBrainsAnnotations::JetBrains.Annotations;` down to a
//  plain `using JetBrains.Annotations;`, and this file supplies that namespace INSIDE
//  `DragonScreen.Mech`.  Because every vendored file is wrapped in `DragonScreen.Mech`, C#'s
//  enclosing-namespace lookup binds their `using JetBrains.Annotations;` to this one — so the
//  private namespace (§B3) still contains everything and no global `JetBrains` type is
//  published from our assembly.
//
//  These attributes are inert markers: they are read by ReSharper, never at runtime.  Only the
//  three MechJeb actually uses are defined — UsedImplicitly (208 uses), MeansImplicitUse (1),
//  and the two enums UsedImplicitly's constructors take.  Adding more would be inventing API
//  the tree does not ask for; if a later re-pin needs another, the compiler will name it.
//  Signatures follow JetBrains.Annotations 2023.3.0.
// ─────────────────────────────────────────────────────────────────────────────────────────
using System;

namespace DragonScreen.Mech
{
    namespace JetBrains.Annotations
    {
        [Flags]
        internal enum ImplicitUseKindFlags
        {
            Default = Access | Assign | InstantiatedWithFixedConstructorSignature,

            /// <summary>Members of the type are used.</summary>
            Access = 1,

            /// <summary>Members of the type are assigned.</summary>
            Assign = 2,

            /// <summary>Type is instantiated via a constructor with the exact signature.</summary>
            InstantiatedWithFixedConstructorSignature = 4,

            /// <summary>Type is instantiated via any constructor.</summary>
            InstantiatedNoFixedConstructorSignature = 8
        }

        [Flags]
        internal enum ImplicitUseTargetFlags
        {
            Default = Itself,

            Itself = 1,

            /// <summary>Members of the type are also marked as used.</summary>
            Members = 2,

            /// <summary>Inherited entities are also marked as used.</summary>
            WithInheritors = 4,

            /// <summary>Entity marked with the attribute and all its members are used.</summary>
            WithMembers = Itself | Members
        }

        /// <summary>
        /// Tells the analyser that the marked symbol is used implicitly (via reflection, in an
        /// external library, …), so it must not be reported as unused.  KSP's `ConfigNode`
        /// persistence and MechJeb's own module discovery are exactly that case.
        /// </summary>
        [AttributeUsage(AttributeTargets.All, Inherited = false)]
        internal sealed class UsedImplicitlyAttribute : Attribute
        {
            public UsedImplicitlyAttribute()
                : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

            public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags)
                : this(useKindFlags, ImplicitUseTargetFlags.Default) { }

            public UsedImplicitlyAttribute(ImplicitUseTargetFlags targetFlags)
                : this(ImplicitUseKindFlags.Default, targetFlags) { }

            public UsedImplicitlyAttribute(ImplicitUseKindFlags useKindFlags,
                                           ImplicitUseTargetFlags targetFlags)
            {
                UseKindFlags = useKindFlags;
                TargetFlags  = targetFlags;
            }

            public ImplicitUseKindFlags   UseKindFlags { get; }
            public ImplicitUseTargetFlags TargetFlags  { get; }
        }

        /// <summary>
        /// Put on an attribute class to say that anything carrying THAT attribute is used
        /// implicitly.  MechJeb marks its custom-info-window attributes with it.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class, Inherited = false)]
        internal sealed class MeansImplicitUseAttribute : Attribute
        {
            public MeansImplicitUseAttribute()
                : this(ImplicitUseKindFlags.Default, ImplicitUseTargetFlags.Default) { }

            public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags)
                : this(useKindFlags, ImplicitUseTargetFlags.Default) { }

            public MeansImplicitUseAttribute(ImplicitUseTargetFlags targetFlags)
                : this(ImplicitUseKindFlags.Default, targetFlags) { }

            public MeansImplicitUseAttribute(ImplicitUseKindFlags useKindFlags,
                                             ImplicitUseTargetFlags targetFlags)
            {
                UseKindFlags = useKindFlags;
                TargetFlags  = targetFlags;
            }

            public ImplicitUseKindFlags   UseKindFlags { get; }
            public ImplicitUseTargetFlags TargetFlags  { get; }
        }
    }
}
