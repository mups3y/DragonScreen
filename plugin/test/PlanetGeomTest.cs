// Tests for PlanetGeom (pure/PlanetGeom.cs) - the scaled-space camera placement, its projection and
// its occlusion, plus the no-signal marking the NAV 3D view shows until S10b builds the camera.
//
// The geometry is set up in a frame chosen so every answer is hand-checkable: the body at the origin
// with radius 1, an EQUATORIAL orbit whose normal is +Y and whose vehicle sits on +X, so the orbit
// plane is the XZ plane and along-track (n x r-hat) is -Z... which is worth stating rather than
// assuming: Cross((0,1,0),(1,0,0)) = (0,0,-1). A prograde KSP orbit has n = r x v, so n x r-hat = v-hat
// and the camera's negative default azimuth therefore swings it BEHIND the vehicle, which is what
// "orbital chase" means and what the azimuth test pins down.
using System;
using DragonScreen;

public static class PlanetGeomTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F6") + " vs " + want.ToString("F6")); }

    static readonly ScaledVec Origin = new ScaledVec(0, 0, 0);
    static readonly ScaledVec North = new ScaledVec(0, 1, 0);

    /// <summary>The reference frame: unit body at the origin, equatorial orbit (normal +Y), vehicle on
    /// +X at 1.06 radii. 60 degrees of vertical field of view, which is the order KSP's cameras use.</summary>
    const double Fov = 60.0;

    static bool Std(double rotDeg, double pitchDeg, int zoom, out PlanetCamFrame f)
    {
        return PlanetGeom.Frame(Origin, 1.0, new ScaledVec(1.06, 0, 0),
                                North, North, rotDeg, pitchDeg, zoom, Fov, out f);
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen planet-geometry tests (S10a)");

        // ---------------------------------------------------------------- fill and distance

        // Zoom 0 must be the SAME framing the textured disc draws at - that is the whole point of the
        // shared constant, and it is what stops the view jumping when a camera appears behind it.
        Near("fill at zoom 0 is the disc's own fill", PlanetGeom.Fill(0),
             PlanetGeom.DiscFillOfHalfHeight, 1e-12);
        Near("one zoom step is the disc's own 1.25", PlanetGeom.Fill(1) / PlanetGeom.Fill(0),
             PlanetGeom.ZoomBase, 1e-12);
        Check("zooming out shrinks the fill", PlanetGeom.Fill(-3) < PlanetGeom.Fill(0), "");

        // The solve and its inverse must agree, or the camera is not where the page thinks it is.
        double d0 = PlanetGeom.Distance(1.0, Fov, PlanetGeom.Fill(0));
        Near("distance round-trips through ApparentFill",
             PlanetGeom.ApparentFill(1.0, d0, Fov), PlanetGeom.Fill(0), 1e-9);
        Check("distance is outside the body", d0 > 1.0, d0.ToString("F4"));

        // ⛔ THE INVARIANT THAT MATTERS: no zoom step, at either end of MapProjection's range, may put
        // the camera inside the planet. sin(atan(x)) < 1 for every finite x, so d > R always - this
        // asserts the algebra actually behaves that way at the extremes rather than trusting it.
        for (int z = MapProjection.PlanetZoomMin; z <= MapProjection.PlanetZoomMax; z++)
        {
            double d = PlanetGeom.Distance(1.0, Fov, PlanetGeom.Fill(z));
            Check("zoom " + z + " keeps the camera outside the body", d > 1.0, d.ToString("F4"));
        }
        Check("zooming in moves the camera closer",
              PlanetGeom.Distance(1.0, Fov, PlanetGeom.Fill(3)) <
              PlanetGeom.Distance(1.0, Fov, PlanetGeom.Fill(0)), "");

        // A bad body or a nonsense field of view returns 0 rather than an infinity that would fly the
        // camera to the edge of the universe.
        Near("no radius, no distance", PlanetGeom.Distance(0.0, Fov, 0.88), 0.0, 1e-12);
        Near("no field of view, no distance", PlanetGeom.Distance(1.0, 0.0, 0.88), 0.0, 1e-12);

        // ---------------------------------------------------------------- the frame

        PlanetCamFrame f;
        Check("the standard frame is valid", Std(0, 0, 0, out f), "");
        Near("eye is at the solved distance", f.Distance, d0, 1e-9);
        Near("eye distance matches its position",
             ScaledVec.Sub(f.Eye, Origin).Length, d0, 1e-9);
        Near("forward is a unit vector", f.Forward.Length, 1.0, 1e-12);
        Near("up is a unit vector", f.Up.Length, 1.0, 1e-12);
        Near("forward is perpendicular to up", ScaledVec.Dot(f.Forward, f.Up), 0.0, 1e-12);

        // Looking AT the body centre: forward must be exactly the reverse of the eye direction.
        ScaledVec toCentre = ScaledVec.Norm(ScaledVec.Sub(Origin, f.Eye));
        Near("forward points at the body centre",
             ScaledVec.Dot(f.Forward, toCentre), 1.0, 1e-12);

        // The default pitch lifts the eye out of the orbit plane by exactly DefaultPitchDeg, on the
        // normal's side. sin(30) = 0.5, so the eye's Y is half its distance.
        Near("default pitch lifts the eye above the plane",
             f.Eye.Y / f.Distance, Math.Sin(PlanetGeom.DefaultPitchDeg * Math.PI / 180.0), 1e-12);
        Check("the eye is on the orbit-normal side", f.Eye.Y > 0.0, "");

        // The default azimuth is negative, i.e. BEHIND the vehicle along-track. Along-track here is
        // n x r-hat = (0,0,-1), so "behind" is +Z; the eye's Z must be positive.
        Check("the default view sits behind the vehicle along-track", f.Eye.Z > 0.0,
              f.Eye.Z.ToString("F4"));
        Check("...and still on the vehicle's side of the body", f.Eye.X > 0.0,
              f.Eye.X.ToString("F4"));

        // The vehicle must be on the visible hemisphere, or the "orbital chase" framing is a lie.
        Check("the vehicle is not hidden by the body",
              !PlanetGeom.Occluded(f.Eye, new ScaledVec(1.06, 0, 0), Origin, 1.0), "");

        // Pan spins the eye about the normal WITHOUT changing the distance or the pitch - that is what
        // makes the pan a rotation of the view rather than a re-frame of it.
        PlanetCamFrame g;
        Std(90.0, 0, 0, out g);
        Near("pan does not change the distance", g.Distance, f.Distance, 1e-9);
        Near("pan does not change the height above the plane", g.Eye.Y, f.Eye.Y, 1e-9);
        Check("pan actually moved the eye",
              ScaledVec.Sub(g.Eye, f.Eye).Length > 0.1 * f.Distance, "");

        // A full 360 of pan comes back to where it started - so a crew that keeps pressing right does
        // not wind the view into somewhere it cannot get out of.
        PlanetCamFrame h;
        Std(360.0, 0, 0, out h);
        Near("360 degrees of pan is a no-op", ScaledVec.Sub(h.Eye, f.Eye).Length, 0.0, 1e-9);

        // Pitch is clamped short of the pole, where up would be parallel to forward.
        PlanetCamFrame pole;
        Check("an over-the-pole pitch still gives a valid frame", Std(0, 400.0, 0, out pole), "");
        Near("...clamped to MaxPitchDeg", pole.Eye.Y / pole.Distance,
             Math.Sin(PlanetGeom.MaxPitchDeg * Math.PI / 180.0), 1e-9);
        Near("...and up stays perpendicular to forward",
             ScaledVec.Dot(pole.Forward, pole.Up), 0.0, 1e-12);

        // Degenerate inputs: no orbit normal falls back to north; neither, and there is no frame.
        PlanetCamFrame fb;
        Check("a degenerate orbit normal falls back to the body's north axis",
              PlanetGeom.Frame(Origin, 1.0, new ScaledVec(1.06, 0, 0),
                               Origin, North, 0, 0, 0, Fov, out fb) && fb.Valid, "");
        PlanetCamFrame none;
        Check("no normal at all is Valid=false, not an invented view",
              !PlanetGeom.Frame(Origin, 1.0, new ScaledVec(1.06, 0, 0),
                                Origin, Origin, 0, 0, 0, Fov, out none), "");
        Check("no radius is Valid=false",
              !PlanetGeom.Frame(Origin, 0.0, new ScaledVec(1.06, 0, 0),
                                North, North, 0, 0, 0, Fov, out none), "");
        // A vehicle sitting exactly on the orbit axis has no radial direction; the frame must still
        // come back rather than dividing by zero, because "no fix yet" is a real state.
        Check("a vehicle on the axis still gives a valid frame",
              PlanetGeom.Frame(Origin, 1.0, North, North, North, 0, 0, 0, Fov, out fb) && fb.Valid, "");

        // ---------------------------------------------------------------- occlusion

        Std(0, 0, 0, out f);
        // Straight through the middle of the body: the far surface is hidden.
        ScaledVec far = ScaledVec.Mul(ScaledVec.Norm(ScaledVec.Sub(Origin, f.Eye)), 2.0 * f.Distance);
        Check("a point directly beyond the body is occluded",
              PlanetGeom.Occluded(f.Eye, far, Origin, 1.0), "");
        // The body's own near pole faces the camera.
        ScaledVec near = ScaledVec.Mul(ScaledVec.Norm(ScaledVec.Sub(f.Eye, Origin)), 1.0);
        Check("the near surface point is visible",
              !PlanetGeom.Occluded(f.Eye, near, Origin, 1.0), "");
        // Underground is hidden even when it is on the near side.
        Check("a point below the surface is occluded",
              PlanetGeom.Occluded(f.Eye, ScaledVec.Mul(near, 0.5), Origin, 1.0), "");
        // Wide of the limb, nothing is in the way however far away it is.
        Check("a point wide of the limb is visible",
              !PlanetGeom.Occluded(f.Eye, new ScaledVec(0, 40, 0), Origin, 1.0), "");
        // The eye itself is never occluded, and a zero radius body occludes nothing.
        Check("the eye is not occluded from itself",
              !PlanetGeom.Occluded(f.Eye, f.Eye, Origin, 1.0), "");
        Check("a body with no radius hides nothing",
              !PlanetGeom.Occluded(f.Eye, far, Origin, 0.0), "");

        // ---------------------------------------------------------------- projection

        double vx, vy; bool inFront;
        PlanetGeom.Project(f, Fov, 16.0 / 9.0, Origin, out vx, out vy, out inFront);
        Check("the body centre is in front of the camera", inFront, "");
        Near("the body centre projects to the middle of the render x", vx, 0.5, 1e-9);
        Near("the body centre projects to the middle of the render y", vy, 0.5, 1e-9);

        // A point behind the lens is refused rather than projected mirrored through the origin, which
        // is the classic way an orbit line ends up drawn across a view it is not in.
        ScaledVec behind = ScaledVec.Add(f.Eye, ScaledVec.Mul(f.Forward, -1.0));
        PlanetGeom.Project(f, Fov, 16.0 / 9.0, behind, out vx, out vy, out inFront);
        Check("a point behind the camera is not in front", !inFront, "");

        // ---- THE LIMB LANDS WHERE THE TEXTURED DISC'S LIMB LANDS ----
        // This is the check that the shared fill constant actually ties the two globes together,
        // rather than merely being spelled the same in two files. Fill of the half-height above the
        // centre is viewport 0.5 + Fill/2.
        //
        // ⚠ AND IT MUST BE THE TANGENT POINT, NOT "one radius straight up". A sphere's silhouette
        // under perspective is NOT the projection of the great circle facing you: the horizon you see
        // is the tangent circle, tilted toward the camera and nearer than the centre, so it projects
        // FURTHER out. Aiming this test at the equator point instead reads 0.89 where the limb is at
        // 0.94, which is a real 5%-of-a-radius error and exactly the kind of near-miss that would have
        // been written off as rounding. cos(alpha) = R/d puts the tangent where it belongs.
        double cosA = 1.0 / f.Distance;                      // R = 1
        double sinA = Math.Sqrt(1.0 - cosA * cosA);
        ScaledVec eyeDir = ScaledVec.Norm(ScaledVec.Sub(f.Eye, Origin));
        ScaledVec limb = ScaledVec.Add(ScaledVec.Mul(eyeDir, cosA), ScaledVec.Mul(f.Up, sinA));
        PlanetGeom.Project(f, Fov, 16.0 / 9.0, limb, out vx, out vy, out inFront);
        Check("the top limb is in front", inFront, "");
        Near("the top limb lands at the disc's own radius", vy,
             0.5 + PlanetGeom.Fill(0) * 0.5, 1e-6);
        Near("...and stays on the render's centreline", vx, 0.5, 1e-9);

        // Zoomed in one step, the limb moves out by exactly the disc's own 1.25.
        PlanetCamFrame z1;
        Std(0, 0, 1, out z1);
        double cosZ = 1.0 / z1.Distance, sinZ = Math.Sqrt(1.0 - cosZ * cosZ);
        ScaledVec eyeZ = ScaledVec.Norm(ScaledVec.Sub(z1.Eye, Origin));
        ScaledVec limbZ = ScaledVec.Add(ScaledVec.Mul(eyeZ, cosZ), ScaledVec.Mul(z1.Up, sinZ));
        double zx, zy; bool zf;
        PlanetGeom.Project(z1, Fov, 16.0 / 9.0, limbZ, out zx, out zy, out zf);
        Near("one zoom step moves the limb out by the disc's 1.25",
             (zy - 0.5) / (vy - 0.5), PlanetGeom.ZoomBase, 1e-6);

        // Up in the world is up in the render (viewport y runs UP), and the panel conversion flips it.
        Check("a point above the centre projects above the middle", vy > 0.5, vy.ToString("F4"));
        float px, py;
        PlanetGeom.ViewportToPanel(0.5, 1.0, 100f, 200f, 400f, 300f, out px, out py);
        Near("viewport top is the panel's top edge", py, 200.0, 1e-4);
        Near("viewport centre x is the rect's middle", px, 300.0, 1e-4);
        PlanetGeom.ViewportToPanel(0.0, 0.0, 100f, 200f, 400f, 300f, out px, out py);
        Near("viewport bottom-left is the panel's bottom-left", px, 100.0, 1e-4);
        Near("...and its y is the bottom edge", py, 500.0, 1e-4);

        // An invalid frame projects nothing - S10b must not be able to aim at a frame that failed.
        PlanetCamFrame bad = new PlanetCamFrame();
        PlanetGeom.Project(bad, Fov, 1.0, Origin, out vx, out vy, out inFront);
        Check("an invalid frame projects nothing", !inFront, "");

        // ---------------------------------------------------------------- the marking

        // The label must name the task that clears it, or it stops being true the day one lands. Same
        // rule as Turntable.PlaceholderLabel, and asserted for the same reason.
        Check("the no-signal label says NO SIGNAL",
              PlanetGeom.NoSignalLabel.IndexOf("NO SIGNAL", StringComparison.Ordinal) >= 0,
              PlanetGeom.NoSignalLabel);
        Check("the no-signal detail names S10b",
              PlanetGeom.NoSignalDetail.IndexOf("S10b", StringComparison.Ordinal) >= 0,
              PlanetGeom.NoSignalDetail);

        // The seam: the live 3D image is a RUNTIME image - no file, no shipped bytes. LayoutTest
        // enforces the filename half; this is the half that says the id is wired up at all.
        Check("ScaledPlanetLive is a runtime image", Images.IsRuntime(ImageId.ScaledPlanetLive), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
