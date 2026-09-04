// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
/*
 * Copyright Lamont Granquist, Sebastien Gaggini and the MechJeb contributors
 * SPDX-License-Identifier: LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+
 */

using MechJebLib.Primitives;
using UnityEngine;

namespace MechJebLibBindings
{
    public static class MathExtensions
    {
        public static V3 WorldToV3Rotated(this Vector3d vector) => (QuaternionD.Inverse(Planetarium.fetch.rotation) * vector).xzy.ToV3();

        public static V3 WorldToV3(this Vector3d vector) => vector.xzy.ToV3();

        public static V3 ToV3(this Vector3d vector) => new V3(vector.x, vector.y, vector.z);

        public static Vector3d ToVector3d(this V3 vector) => new Vector3d(vector.x, vector.y, vector.z);

        public static Vector3d V3ToWorld(this V3 vector) => vector.ToVector3d().xzy;

        public static Vector3d V3ToWorldRotated(this V3 vector) => Planetarium.fetch.rotation * vector.ToVector3d().xzy;

        public static Q3 ToQ3(this QuaternionD q) => new Q3(q.z, q.y, q.x, -q.w);

        public static QuaternionD ToQuaternionD(this Q3 q) => new QuaternionD(q.z, q.y, q.x, -q.w);
    }
}

}
