using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using SolarSystem3D.Core;
using AstroCore.Astronomy;
using SolarSystem3D.Core.Bodies;
using static SolarSystem3D.App.Rendering.Palette;

namespace SolarSystem3D.App.Rendering;

/// <summary>
/// Builds and updates the 3D scene. Scene coordinates are AU in the J2000 ecliptic frame, shifted so that
/// the current <see cref="Origin"/> body sits at (0,0,0) — this keeps precision when zoomed in on a moon
/// and lets the camera follow a planet while time runs.
/// </summary>
public sealed class SceneRenderer
{
    private const double AuKm = 149_597_870.7;
    private static readonly double[] GridRadii = { 1, 2, 3, 5, 10, 20, 30, 40, 50, 100 };

    private readonly HelixViewport3D _viewport;
    private readonly LabelOverlay _labels;
    private readonly DisplayOptions _options;
    private readonly OrbitCache _orbitCache = new();
    private SolarSystem? _system;

    // Bodies drawn as true-scale (optionally exaggerated) spheres.
    private readonly MeshGeometry3D _sphereHi = MeshFactory.UnitSphere(48, 24);
    private readonly MeshGeometry3D _sphereLo = MeshFactory.UnitSphere(16, 10);
    private readonly Model3DGroup _sceneGroup = new();
    private readonly Model3DGroup _majorSpheres = new();
    private readonly Model3DGroup _moonSpheres = new();
    private readonly Dictionary<CelestialBody, GeometryModel3D> _spheres = new();
    private GeometryModel3D? _rings;
    private Planet? _ringedPlanet;
    private readonly PointLight _sunLight = new(Colors.White, new Point3D());

    // Screen-space markers.
    private readonly PointsVisual3D _sunMarker = new() { Color = Color.FromRgb(255, 220, 90), Size = 9 };
    private readonly PointsVisual3D _dwarfMarkers = new() { Color = DwarfMarker, Size = 4.5 };
    private readonly PointsVisual3D _moonMarkers = new() { Color = MoonMarker, Size = 3.5 };
    private readonly PointsVisual3D _cometMarkers = new() { Color = CometMarker, Size = 2.5 };
    private readonly Dictionary<Planet, PointsVisual3D> _planetMarkers = new();
    private readonly Dictionary<AsteroidGroup, PointsVisual3D> _asteroidMarkers = new();

    // Orbit and grid lines.
    private readonly Dictionary<Planet, LinesVisual3D> _planetOrbits = new();
    private readonly LinesVisual3D _dwarfOrbits = new() { Color = DwarfOrbit, Thickness = 1 };
    private readonly LinesVisual3D _moonOrbits = new() { Color = MoonOrbit, Thickness = 1 };
    private readonly LinesVisual3D _asteroidOrbits = new() { Color = AsteroidOrbit, Thickness = 0.8 };
    private readonly LinesVisual3D _cometOrbits = new() { Color = CometOrbit, Thickness = 0.8 };
    private readonly LinesVisual3D _selectedOrbit = new() { Color = Selection, Thickness = 1.6 };
    private readonly LinesVisual3D _grid = new() { Color = Grid, Thickness = 0.8 };

    // Pre-sorted body lists.
    private readonly Dictionary<AsteroidGroup, List<Asteroid>> _asteroidGroups = new();
    private List<Asteroid> _asteroidsByBrightness = new();
    private List<Comet> _orbitComets = new();
    private List<Comet> _labelComets = new();
    private readonly List<NaturalSatellite> _visibleMoons = new();
    private string _moonOrbitSignature = "";
    private int _worldVersion;
    private double _layersJd = double.NaN;
    private bool _layersDirty = true;
    private readonly System.Diagnostics.Stopwatch _sinceSmallBodies = System.Diagnostics.Stopwatch.StartNew();
    private readonly Dictionary<CelestialBody, double> _orbitBuiltJd = new();

    public SceneRenderer(HelixViewport3D viewport, LabelOverlay labels, DisplayOptions options)
    {
        _viewport = viewport;
        _labels = labels;
        _options = options;
        _options.PropertyChanged += (_, _) => _layersDirty = true;

        _sceneGroup.Children.Add(new AmbientLight(Color.FromRgb(36, 36, 44)));
        _sceneGroup.Children.Add(_sunLight);
        _sceneGroup.Children.Add(_majorSpheres);
        _sceneGroup.Children.Add(_moonSpheres);

        _viewport.Children.Add(new ModelVisual3D { Content = _sceneGroup });
        _viewport.Children.Add(_grid);
        foreach (var v in new Visual3D[] { _dwarfOrbits, _moonOrbits, _asteroidOrbits, _cometOrbits, _selectedOrbit })
            _viewport.Children.Add(v);
        foreach (AsteroidGroup g in Enum.GetValues<AsteroidGroup>())
        {
            var pv = new PointsVisual3D { Color = ColorOf(g), Size = 2 };
            _asteroidMarkers[g] = pv;
            _asteroidGroups[g] = new List<Asteroid>();
            _viewport.Children.Add(pv);
        }
        foreach (var v in new Visual3D[] { _cometMarkers, _moonMarkers, _dwarfMarkers, _sunMarker })
            _viewport.Children.Add(v);
    }

    public CelestialBody? Origin { get; private set; }
    public CelestialBody? Selected
    {
        get => _selected;
        set { _selected = value; _layersDirty = true; }
    }
    private CelestialBody? _selected;
    public IReadOnlyCollection<NaturalSatellite> VisibleMoons => _visibleMoons;

    /// <summary>Scene position of a heliocentric vector relative to the current origin.</summary>
    public Point3D ToScene(Vector3d helio)
    {
        var o = Origin?.Position ?? Vector3d.Zero;
        return new Point3D(helio.X - o.X, helio.Y - o.Y, helio.Z - o.Z);
    }

    public Point3D ToScene(CelestialBody body) => ToScene(body.Position);

    /// <summary>Changes the body at the scene origin. Returns the scene-space shift that was applied.</summary>
    public Vector3D SetOrigin(CelestialBody body)
    {
        var before = Origin?.Position ?? Vector3d.Zero;
        Origin = body;
        var d = before - body.Position;
        return new Vector3D(d.X, d.Y, d.Z);
    }

    /// <summary>Recreates per-body visuals after the set of bodies changed.</summary>
    public void Rebuild(SolarSystem system)
    {
        _system = system;
        Origin ??= system.Sun;
        _orbitCache.Clear();
        _orbitComets = new List<Comet>();
        _orbitBuiltJd.Clear();
        _layersDirty = true;

        _majorSpheres.Children.Clear();
        _moonSpheres.Children.Clear();
        _spheres.Clear();
        foreach (var pm in _planetMarkers.Values) _viewport.Children.Remove(pm);
        foreach (var po in _planetOrbits.Values) _viewport.Children.Remove(po);
        _planetMarkers.Clear();
        _planetOrbits.Clear();

        foreach (var body in system.MajorBodies)
        {
            var color = FromArgb(body.ColorArgb);
            Material material = body is Star
                ? new MaterialGroup { Children = { new DiffuseMaterial(Brushes.Black), new EmissiveMaterial(new SolidColorBrush(color)) } }
                : new MaterialGroup { Children = { new DiffuseMaterial(new SolidColorBrush(color)), new EmissiveMaterial(new SolidColorBrush(Dim(color, 0.06))) } };
            material.Freeze();
            var model = new GeometryModel3D(body is NaturalSatellite ? _sphereLo : _sphereHi, material);
            _spheres[body] = model;
            (body is NaturalSatellite ? _moonSpheres : _majorSpheres).Children.Add(model);

            if (body is Planet planet)
            {
                var marker = new PointsVisual3D { Color = color, Size = 6 };
                _planetMarkers[planet] = marker;
                _viewport.Children.Add(marker);
                var orbit = new LinesVisual3D { Color = Dim(color, 0.55), Thickness = 1.1 };
                _planetOrbits[planet] = orbit;
                _viewport.Children.Insert(2, orbit);
            }
        }

        // Saturn's rings.
        if (_rings != null) _majorSpheres.Children.Remove(_rings);
        _ringedPlanet = system.Planets.FirstOrDefault(p => p.Rings.HasValue);
        if (_ringedPlanet?.Rings is { } rings)
        {
            var ringMat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(150, 210, 190, 150)));
            ringMat.Freeze();
            _rings = new GeometryModel3D(MeshFactory.Ring(rings.Inner / rings.Outer, 128), ringMat) { BackMaterial = ringMat };
            _majorSpheres.Children.Add(_rings); // transparent — keep it last
        }

        foreach (var list in _asteroidGroups.Values) list.Clear();
        foreach (var a in system.Asteroids) _asteroidGroups[GroupOf(a)].Add(a);
        _asteroidsByBrightness = system.Asteroids.OrderBy(a => a.AbsoluteMagnitude).ToList();
    }

    // ------------------------------------------------------------------ world update

    /// <summary>Repositions everything after the time, origin or display options changed.</summary>
    /// <returns>True if some layers were deferred and another call is needed soon.</returns>
    public bool UpdateWorld()
    {
        if (_system is null) return false;
        _worldVersion++;
        double jd = _system.JulianDateTT;
        var sunP = ToScene(_system.Sun);
        _sunLight.Position = sunP;
        double markerScale = _options.MarkerSize;

        // Spheres.
        foreach (var (body, model) in _spheres)
        {
            double scale = body is Star ? Math.Min(_options.BodyScale, 10) : _options.BodyScale;
            double r = body.RadiusKm / AuKm * scale;
            var p = ToScene(body);
            model.Transform = new MatrixTransform3D(new Matrix3D(r, 0, 0, 0, 0, r, 0, 0, 0, 0, r, 0, p.X, p.Y, p.Z, 1));
        }
        SetChildPresent(_sceneGroup, _moonSpheres, _options.ShowMoons);
        UpdateDwarfSpheres();
        if (_rings != null && _ringedPlanet?.Rings is { } rings)
        {
            double outer = rings.Outer / AuKm * _options.BodyScale;
            var pole = Frames.PoleVector(_ringedPlanet.Pole.Ra, _ringedPlanet.Pole.Dec);
            var m = AlignZ(new Vector3D(pole.X, pole.Y, pole.Z));
            m.Scale(new Vector3D(outer, outer, outer));
            var p = ToScene(_ringedPlanet);
            m.Translate(new Vector3D(p.X, p.Y, p.Z));
            _rings.Transform = new MatrixTransform3D(m);
        }

        // Markers.
        _sunMarker.Points = new Point3DCollection { sunP };
        _sunMarker.Size = 9 * markerScale;
        foreach (var (planet, marker) in _planetMarkers)
        {
            marker.Points = new Point3DCollection { ToScene(planet) };
            marker.Size = 6 * markerScale;
        }
        _dwarfMarkers.Size = 4.5 * markerScale;
        _dwarfMarkers.Points = _options.ShowDwarfPlanets
            ? new Point3DCollection(_system.DwarfPlanets.Select(ToScene))
            : new Point3DCollection();
        // Heliocentric layers (asteroids, comets, their orbits, the grid) are built in Sun-centred coordinates
        // and moved with a single transform, so following a moving planet does not rebuild them every frame.
        var helio = new TranslateTransform3D(sunP.X, sunP.Y, sunP.Z);
        foreach (var v in HeliocentricLayers()) v.Transform = helio;

        // While time runs, the ~10,000 small-body markers are refreshed at most 10 times a second;
        // the controller calls again until the final state has been drawn.
        bool timeChanged = jd != _layersJd;
        bool smallBodiesDue = _layersDirty || (timeChanged && _sinceSmallBodies.ElapsedMilliseconds >= 100);
        bool pending = timeChanged && !smallBodiesDue;
        if (smallBodiesDue)
        {
            _sinceSmallBodies.Restart();
            foreach (var (group, marker) in _asteroidMarkers)
            {
                marker.Size = 2 * markerScale;
                marker.Points = _options.ShowAsteroids
                    ? new Point3DCollection(_asteroidGroups[group].Select(HelioPoint))
                    : new Point3DCollection();
            }
            _cometMarkers.Size = 2.5 * markerScale;
            _cometMarkers.Points = _options.ShowComets
                ? new Point3DCollection(_system.Comets.Where(c => c.Position.LengthSquared < 100 * 100 && c.IsPlausiblyPresent(jd)).Select(HelioPoint))
                : new Point3DCollection();

            var orbitComets = _options.ShowComets
                ? _system.Comets.Where(c =>
                        (_options.ShowCometOrbits && Math.Abs(jd - c.PerihelionJd) < 365) ||
                        (_options.ShowPeriodicCometOrbits && c.IsNumberedPeriodic))
                    .ToList()
                : new List<Comet>();
            if (_layersDirty || !orbitComets.SequenceEqual(_orbitComets))
            {
                _orbitComets = orbitComets;
                _cometOrbits.Points = Segments(_orbitComets.Select(c => _orbitCache.GetPath(c, jd, 96, 60)), new Point3D());
            }

            _labelComets = _system.Comets.Where(c => c.Position.Length < 6 && c.IsPlausiblyPresent(jd)).OrderBy(c => c.Position.Length)
                .Concat(_system.Comets.Where(c => c.IsNumberedPeriodic && c.Position.Length >= 6))
                .ToList();
        }
        if (_layersDirty)
        {
            _asteroidOrbits.Points = _options.ShowAsteroids && _options.ShowAsteroidOrbits
                ? Segments(_asteroidsByBrightness.Take(_options.AsteroidOrbitCount).Select(a => _orbitCache.GetPath(a, jd, 128)), new Point3D())
                : new Point3DCollection();
            _grid.Points = _options.ShowEclipticGrid ? BuildGrid(new Point3D()) : new Point3DCollection();
        }
        bool layersWereDirty = _layersDirty;
        if (smallBodiesDue) _layersJd = jd;
        _layersDirty = false;

        // Planet and dwarf-planet orbits are sampled densely around each body's current position (see DensePath)
        // and resampled only once the body has moved 1/720 of its period.
        foreach (var (planet, lines) in _planetOrbits)
        {
            if (!_options.ShowPlanetOrbits) { if (lines.Points.Count > 0) lines.Points = new Point3DCollection(); _orbitBuiltJd.Remove(planet); continue; }
            if (layersWereDirty || OrbitDue(planet, jd))
                lines.Points = planet == Selected && _options.ShowSelectedOrbit
                    ? new Point3DCollection() // drawn by the selection highlight instead
                    : Segments(DensePath(planet, jd, 360), new Point3D());
        }
        bool showDwarfOrbits = _options.ShowDwarfPlanets && _options.ShowDwarfPlanetOrbits;
        if (!showDwarfOrbits) _dwarfOrbits.Points = new Point3DCollection();
        else if (layersWereDirty || _system.DwarfPlanets.Any(d => OrbitDue(d, jd)))
        {
            foreach (var d in _system.DwarfPlanets) _orbitBuiltJd[d] = jd;
            _dwarfOrbits.Points = Segments(_system.DwarfPlanets.Select(d => DensePath(d, jd, 256)), new Point3D());
        }

        if (_options.ShowSelectedOrbit && Selected?.Orbit != null)
        {
            var parentP = Selected.Parent is null ? sunP : ToScene(Selected.Parent);
            _selectedOrbit.Points = Segments(DensePath(Selected, jd, 720), parentP);
        }
        else _selectedOrbit.Points = new Point3DCollection();
        return pending;
    }

    /// <summary>True (and records the time) when a body's orbit line should be resampled.</summary>
    private bool OrbitDue(CelestialBody body, double jd)
    {
        double period = body.Orbit?.PeriodDays ?? 365;
        if (_orbitBuiltJd.TryGetValue(body, out double built) && Math.Abs(jd - built) < period / 720) return false;
        _orbitBuiltJd[body] = jd;
        return true;
    }

    private static Point3D HelioPoint(CelestialBody b) => new(b.Position.X, b.Position.Y, b.Position.Z);

    private IEnumerable<Visual3D> HeliocentricLayers()
    {
        foreach (var m in _asteroidMarkers.Values) yield return m;
        yield return _cometMarkers;
        yield return _cometOrbits;
        yield return _asteroidOrbits;
        yield return _grid;
        yield return _dwarfOrbits;
        foreach (var o in _planetOrbits.Values) yield return o;
    }

    private void UpdateDwarfSpheres()
    {
        if (_system is null) return;
        foreach (var d in _system.DwarfPlanets)
            if (_spheres.TryGetValue(d, out var model))
                SetChildPresent(_majorSpheres, model, _options.ShowDwarfPlanets, insertAtStart: true);
    }

    private static void SetChildPresent(Model3DGroup parent, Model3D child, bool present, bool insertAtStart = false)
    {
        bool has = parent.Children.Contains(child);
        if (present && !has)
        {
            if (insertAtStart) parent.Children.Insert(0, child);
            else parent.Children.Add(child);
        }
        else if (!present && has) parent.Children.Remove(child);
    }

    // ------------------------------------------------------------------ view update

    /// <summary>Updates everything that depends on the camera: moon level of detail, labels, selection ring, clip planes.</summary>
    public void UpdateView()
    {
        if (_system is null || _viewport.Camera is not PerspectiveCamera camera) return;
        AdjustClipPlanes(camera);
        var proj = new Projector(camera, _viewport.ActualWidth, _viewport.ActualHeight);
        double jd = _system.JulianDateTT;

        // Moons: show a moon only once its orbit is big enough on screen to separate it from its planet.
        _visibleMoons.Clear();
        var orbitMoons = new List<NaturalSatellite>();
        if (_options.ShowMoons)
        {
            foreach (var moon in _system.Moons)
            {
                var parentP = ToScene(moon.Parent!);
                double px = proj.PixelSize((moon.Position - moon.Parent!.Position).Length, parentP);
                if (px < (moon.IsIrregular ? 24 : 4) || !proj.TryProject(parentP, out var ps) || !proj.IsOnScreen(ps, px)) continue;
                _visibleMoons.Add(moon);
                bool orbit = moon.IsIrregular ? _options.ShowIrregularMoonOrbits && px > 25 : px > 10;
                if (_options.ShowMoonOrbits && orbit) orbitMoons.Add(moon);
            }
        }
        string signature = $"{_worldVersion}:{_visibleMoons.Count}:{orbitMoons.Count}:{orbitMoons.FirstOrDefault()?.Id}:{orbitMoons.LastOrDefault()?.Id}";
        if (signature != _moonOrbitSignature)
        {
            _moonOrbitSignature = signature;
            _moonMarkers.Size = 3.5 * _options.MarkerSize;
            _moonMarkers.Points = new Point3DCollection(_visibleMoons.Select(ToScene));
            var pts = new Point3DCollection();
            foreach (var m in orbitMoons)
                AppendSegments(pts, DensePath(m, jd, m.IsIrregular ? 96 : 192), ToScene(m.Parent!));
            _moonOrbits.Points = pts;
        }

        PlaceLabels(proj);
    }

    private void PlaceLabels(Projector proj)
    {
        var sys = _system!;
        _labels.Begin();

        // Selected body first so it always wins the space.
        Point? selScreen = null;
        if (Selected != null && proj.TryProject(ToScene(Selected), out var sp) && proj.IsOnScreen(sp, 20))
        {
            selScreen = sp;
            _labels.TryAdd(Selected.Name, sp, Selection, force: true, fontSize: 13);
        }
        _labels.ShowSelection(selScreen);

        void Label(CelestialBody b, Color c, bool force, double size = 12)
        {
            if (b == Selected) return;
            if (proj.TryProject(ToScene(b), out var s) && proj.IsOnScreen(s)) _labels.TryAdd(b.Name, s, c, force, size);
        }

        if (_options.LabelPlanets)
        {
            Label(sys.Sun, PlanetLabel, true, 13);
            // Outer planets first so the crowded inner system gives way when zoomed out.
            foreach (var p in sys.Planets.Reverse()) Label(p, PlanetLabel, false, 13);
        }
        if (_options.LabelDwarfPlanets && _options.ShowDwarfPlanets)
            foreach (var d in sys.DwarfPlanets) Label(d, DwarfLabel, false);

        if (_options.LabelMoons && _options.ShowMoons)
        {
            var proj2 = proj;
            foreach (var m in _visibleMoons
                         .Where(m => (!m.IsIrregular || _options.LabelIrregularMoons) &&
                                     proj2.PixelSize((m.Position - m.Parent!.Position).Length, ToScene(m.Parent!)) > (m.IsIrregular ? 60 : 30))
                         .OrderBy(m => m.IsIrregular).ThenByDescending(m => m.RadiusKm))
                Label(m, MoonLabel, false, 11);
        }
        if (_options.LabelAsteroids && _options.ShowAsteroids)
        {
            int placed = 0;
            foreach (var a in _asteroidsByBrightness.Take(5000))
            {
                if (proj.TryProject(ToScene(a), out var s) && proj.IsOnScreen(s) && _labels.TryAdd(a.Name, s, SmallBodyLabel, false, 11))
                    if (++placed >= 40) break;
            }
        }
        if (_options.LabelComets && _options.ShowComets)
        {
            int placed = 0;
            foreach (var c in _labelComets)
            {
                if (proj.TryProject(ToScene(c), out var s) && proj.IsOnScreen(s) && _labels.TryAdd(c.Name, s, CometMarker, false, 11))
                    if (++placed >= 30) break;
            }
        }
        if (_options.ShowEclipticGrid)
        {
            var sunP = ToScene(sys.Sun);
            foreach (double r in GridRadii)
            {
                double a = -60 * Math.PI / 180;
                if (proj.TryProject(sunP + new Vector3D(r * Math.Cos(a), r * Math.Sin(a), 0), out var s) && proj.IsOnScreen(s))
                    _labels.TryAdd($"{r:0} AU", s, GridLabel, false, 10);
            }
            if (proj.TryProject(sunP + new Vector3D(GridRadii[^1] * 1.05, 0, 0), out var eq) && proj.IsOnScreen(eq))
                _labels.TryAdd("♈ Vernal equinox", eq, GridLabel, false, 10);
        }
        _labels.End();
    }

    private static void AdjustClipPlanes(PerspectiveCamera camera)
    {
        double dist = camera.LookDirection.Length;
        double near = Math.Max(1e-10, dist * 1e-3);
        if (Math.Abs(camera.NearPlaneDistance - near) > near * 0.25) camera.NearPlaneDistance = near;
        if (!double.IsPositiveInfinity(camera.FarPlaneDistance)) camera.FarPlaneDistance = double.PositiveInfinity;
    }

    // ------------------------------------------------------------------ picking

    /// <summary>Returns the body drawn nearest to a screen point, or null.</summary>
    public CelestialBody? Pick(Point screen)
    {
        if (_system is null || _viewport.Camera is not PerspectiveCamera camera) return null;
        var proj = new Projector(camera, _viewport.ActualWidth, _viewport.ActualHeight);
        CelestialBody? best = null;
        double bestScore = double.MaxValue;

        void Consider(CelestialBody b, double tolerancePx, double bias)
        {
            var p = ToScene(b);
            if (!proj.TryProject(p, out var s)) return;
            double d = (s - screen).Length;
            double scale = b is Star ? Math.Min(_options.BodyScale, 10) : _options.BodyScale;
            double radiusPx = proj.PixelSize(b.RadiusKm / AuKm * scale, p);
            if (d > Math.Max(tolerancePx, radiusPx)) return;
            double score = d + bias;
            if (score < bestScore) { bestScore = score; best = b; }
        }

        Consider(_system.Sun, 14, 0);
        foreach (var p in _system.Planets) Consider(p, 14, 0);
        if (_options.ShowDwarfPlanets) foreach (var d in _system.DwarfPlanets) Consider(d, 12, 1);
        foreach (var m in _visibleMoons) Consider(m, 10, 2);
        if (_options.ShowComets)
            foreach (var c in _system.Comets)
                if (c.Position.LengthSquared < 100 * 100 && c.IsPlausiblyPresent(_system.JulianDateTT))
                    Consider(c, 7, 4);
        if (_options.ShowAsteroids) foreach (var a in _asteroidsByBrightness) Consider(a, 7, 5);
        return best;
    }

    // ------------------------------------------------------------------ geometry helpers

    /// <summary>
    /// Samples a closed orbit by time, densely around the present moment (cubic spacing), so the body sits
    /// exactly on a vertex of its own orbit line and segments near a close-up camera stay short.
    /// Open orbits fall back to the cached geometric path.
    /// </summary>
    private IReadOnlyList<Vector3d> DensePath(CelestialBody body, double jd, int samples)
    {
        if (body.Orbit is not { PeriodDays: double period } orbit || !(period > 0) || period > 5e6)
            return _orbitCache.GetPath(body, jd, samples, 1000);
        var pts = new Vector3d[samples + 1];
        for (int k = 0; k <= samples; k++)
        {
            double u = -1 + 2.0 * k / samples;
            pts[k] = orbit.PositionAt(jd + period / 2 * u * u * u);
        }
        return pts;
    }

    private static Point3DCollection Segments(IReadOnlyList<Vector3d> path, Point3D offset)
    {
        var pts = new Point3DCollection(Math.Max(0, path.Count - 1) * 2);
        AppendSegments(pts, path, offset);
        return pts;
    }

    private static Point3DCollection Segments(IEnumerable<IReadOnlyList<Vector3d>> paths, Point3D offset)
    {
        var pts = new Point3DCollection();
        foreach (var p in paths) AppendSegments(pts, p, offset);
        return pts;
    }

    private static void AppendSegments(Point3DCollection pts, IReadOnlyList<Vector3d> path, Point3D o)
    {
        for (int k = 0; k + 1 < path.Count; k++)
        {
            pts.Add(new Point3D(o.X + path[k].X, o.Y + path[k].Y, o.Z + path[k].Z));
            pts.Add(new Point3D(o.X + path[k + 1].X, o.Y + path[k + 1].Y, o.Z + path[k + 1].Z));
        }
    }

    private static Point3DCollection BuildGrid(Point3D sun)
    {
        var pts = new Point3DCollection();
        const int seg = 120;
        foreach (double r in GridRadii)
            for (int k = 0; k < seg; k++)
            {
                double a0 = 2 * Math.PI * k / seg, a1 = 2 * Math.PI * (k + 1) / seg;
                pts.Add(sun + new Vector3D(r * Math.Cos(a0), r * Math.Sin(a0), 0));
                pts.Add(sun + new Vector3D(r * Math.Cos(a1), r * Math.Sin(a1), 0));
            }
        for (int k = 0; k < 12; k++)
        {
            double a = 2 * Math.PI * k / 12;
            var dir = new Vector3D(Math.Cos(a), Math.Sin(a), 0);
            pts.Add(sun + dir * (k == 0 ? 0 : GridRadii[0]));
            pts.Add(sun + dir * GridRadii[^1] * (k == 0 ? 1.05 : 1));
        }
        return pts;
    }

    /// <summary>Rotation that maps +Z onto <paramref name="target"/>.</summary>
    private static Matrix3D AlignZ(Vector3D target)
    {
        target.Normalize();
        var z = new Vector3D(0, 0, 1);
        var axis = Vector3D.CrossProduct(z, target);
        double angle = Vector3D.AngleBetween(z, target);
        var m = Matrix3D.Identity;
        if (axis.LengthSquared > 1e-12) m.Rotate(new Quaternion(axis, angle));
        return m;
    }
}
