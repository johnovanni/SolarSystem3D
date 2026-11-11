using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using SolarSystem3D.App.Rendering;
using SolarSystem3D.App.ViewModels;
using SolarSystem3D.Core;
using SolarSystem3D.Core.Bodies;
using SolarSystem3D.Core.Data;

namespace SolarSystem3D.App.Controllers;

/// <summary>
/// Drives the simulation: advances the clock every frame, recomputes positions, keeps the renderer in
/// sync with the view model, handles picking and camera focusing.
/// </summary>
public sealed class SceneController
{
    private const double AuKm = 149_597_870.7;
    private static readonly TimeSpan MinWorldInterval = TimeSpan.FromMilliseconds(30);

    private readonly HelixViewport3D _viewport;
    private readonly MainViewModel _vm;
    private readonly SceneRenderer _renderer;
    private readonly Stopwatch _frameClock = Stopwatch.StartNew();
    private SolarSystem? _system;

    private TimeSpan _lastFrame, _lastWorldUpdate, _fpsWindowStart;
    private int _fpsFrames;
    private double _ephemerisMs, _sceneMs, _viewMs;
    private bool _worldDirty = true, _viewDirty = true;
    private Point _mouseDown;

    public SceneController(HelixViewport3D viewport, LabelOverlay overlay, MainViewModel vm)
    {
        _viewport = viewport;
        _vm = vm;
        _renderer = new SceneRenderer(viewport, overlay, vm.Display);

        vm.Clock.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.Clock.Utc)) _worldDirty = true; };
        vm.Display.PropertyChanged += (_, _) => _worldDirty = true;
        vm.PropertyChanged += OnViewModelChanged;
        vm.FocusRequested += (_, body) => Focus(body, zoom: true);
        vm.ResetViewRequested += (_, _) => ResetView();
        vm.CatalogsReloadRequested += async (_, _) => await ReloadSmallBodiesAsync();

        viewport.CameraChanged += (_, _) => _viewDirty = true;
        viewport.SizeChanged += (_, _) => _viewDirty = true;
        viewport.PreviewMouseLeftButtonDown += OnMouseDown;
        viewport.PreviewMouseLeftButtonUp += OnMouseUp;

        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>Loads the catalogs (off the UI thread) and builds the scene.</summary>
    public async Task LoadAsync()
    {
        var store = _vm.Store;
        var (system, report) = await Task.Run(() =>
        {
            var s = new SolarSystemFactory(store).Create(out var r);
            return (s, r);
        });
        _system = system;
        _system.Update(_vm.Clock.JulianDateTT);
        _renderer.Rebuild(system);
        _vm.AttachSystem(system, report);
        ResetView();
        _worldDirty = true;
    }

    private async Task ReloadSmallBodiesAsync()
    {
        if (_system is null) return;
        var store = _vm.Store;
        var (asteroids, comets) = await Task.Run(() =>
        {
            using var ra = store.Open(CatalogStore.AsteroidsFile, out _);
            var (ast, _) = CatalogReader.ReadAsteroids(ra);
            using var rc = store.Open(CatalogStore.CometsFile, out _);
            return (ast, CatalogReader.ReadComets(rc));
        });
        if (_vm.SelectedBody is Asteroid or Comet) _vm.SelectedBody = null;
        _system.ReplaceSmallBodies(asteroids.Cast<CelestialBody>().Concat(comets));
        _renderer.Rebuild(_system);
        _vm.CatalogStatus = $"{_system.Moons.Count():N0} moons, {_system.DwarfPlanets.Count()} dwarf planets, " +
                            $"{asteroids.Count:N0} asteroids, {comets.Count:N0} comets" +
                            (store.HasDownloadedCopy(CatalogStore.AsteroidsFile) ? "  (downloaded from JPL)" : "  (built-in snapshot)");
        _worldDirty = true;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedBody))
        {
            _renderer.Selected = _vm.SelectedBody;
            _worldDirty = true;
        }
    }

    // ------------------------------------------------------------------ frame loop

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _frameClock.Elapsed;
        var elapsed = now - _lastFrame;
        _lastFrame = now;
        if (_system is null) return;

        if (elapsed > TimeSpan.FromSeconds(0.25)) elapsed = TimeSpan.FromSeconds(0.25); // after a stall, don't jump
        _vm.Clock.Advance(elapsed);

        if (_worldDirty && now - _lastWorldUpdate >= MinWorldInterval)
        {
            _worldDirty = false;
            _lastWorldUpdate = now;
            var t0 = _frameClock.Elapsed;
            _system.Update(_vm.Clock.JulianDateTT);
            var t1 = _frameClock.Elapsed;
            if (_renderer.UpdateWorld()) _worldDirty = true; // some layers deferred — finish them shortly
            _ephemerisMs = (t1 - t0).TotalMilliseconds;
            _sceneMs = (_frameClock.Elapsed - t1).TotalMilliseconds;
            _viewDirty = true;
        }
        if (_viewDirty)
        {
            _viewDirty = false;
            var t0 = _frameClock.Elapsed;
            _renderer.UpdateView();
            _viewMs = (_frameClock.Elapsed - t0).TotalMilliseconds;
            UpdateStatus();
        }

        _fpsFrames++;
        if (now - _fpsWindowStart > TimeSpan.FromSeconds(1))
        {
            _vm.FrameRateText = $"{_fpsFrames / (now - _fpsWindowStart).TotalSeconds:F0} fps   ·   ephemeris {_ephemerisMs:F1} ms, scene {_sceneMs:F1} ms, view {_viewMs:F1} ms";
            _fpsFrames = 0;
            _fpsWindowStart = now;
        }
    }

    private void UpdateStatus()
    {
        if (_viewport.Camera is not ProjectionCamera cam || _system is null) return;
        double dist = cam.LookDirection.Length;
        string d = dist < 0.01 ? $"{dist * AuKm:N0} km" : $"{dist:F3} AU";
        _vm.StatusText = $"Centred on {_renderer.Origin?.Name}   ·   camera distance {d}   ·   {_renderer.VisibleMoons.Count} moons in view";
    }

    // ------------------------------------------------------------------ picking

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDown = e.GetPosition(_viewport);
        if (e.ClickCount == 2)
        {
            var body = _renderer.Pick(_mouseDown);
            if (body != null)
            {
                _vm.SelectedBody = body;
                Focus(body, zoom: true);
                e.Handled = true;
            }
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(_viewport);
        if ((p - _mouseDown).Length > 4) return; // that was a drag (rotation), not a click
        var body = _renderer.Pick(p);
        if (body != null) _vm.SelectedBody = body;
    }

    // ------------------------------------------------------------------ camera

    /// <summary>Makes <paramref name="body"/> the scene origin and flies the camera to it.</summary>
    public void Focus(CelestialBody body, bool zoom)
    {
        if (_system is null || _viewport.Camera is not PerspectiveCamera cam) return;
        // Shift the camera by the same amount as the scene so nothing visibly jumps, then animate.
        var shift = _renderer.SetOrigin(body);
        cam.Position += shift;
        _vm.FocusName = body.Name;
        _system.Update(_vm.Clock.JulianDateTT);
        _renderer.UpdateWorld();

        var dir = cam.LookDirection;
        dir.Normalize();
        double distance = zoom ? ViewingDistance(body) : (new Point3D(0, 0, 0) - cam.Position).Length;
        var newPos = new Point3D(-dir.X * distance, -dir.Y * distance, -dir.Z * distance);
        CameraHelper.AnimateTo(cam, newPos, dir * distance, new Vector3D(0, 0, 1), 700);
        _viewDirty = true;
    }

    /// <summary>A comfortable camera distance for looking at a body (and its main moons).</summary>
    private double ViewingDistance(CelestialBody body)
    {
        if (body is Star) return 4.0;
        double radiusAu = body.RadiusKm / AuKm * Math.Max(1, _vm.Display.BodyScale);
        var bigMoons = body.Satellites.Where(m => m.RadiusKm >= 200).ToList();
        if (bigMoons.Count > 0)
        {
            double extent = bigMoons.Max(m => (m.Position - body.Position).Length);
            return Math.Max(extent * 2.6, radiusAu * 8);
        }
        if (body.Satellites.Count > 0)
        {
            double extent = body.Satellites.Min(m => (m.Position - body.Position).Length);
            return Math.Max(extent * 3, radiusAu * 8);
        }
        return Math.Max(radiusAu * 12, 2e-6);
    }

    public void ResetView()
    {
        if (_system is null || _viewport.Camera is not PerspectiveCamera cam) return;
        var shift = _renderer.SetOrigin(_system.Sun);
        cam.Position += shift;
        _vm.FocusName = "Sun";
        _worldDirty = true;
        var pos = new Point3D(0, -11, 7.5);
        CameraHelper.AnimateTo(cam, pos, new Point3D(0, 0, 0) - pos, new Vector3D(0, 0, 1), 700);
        _viewDirty = true;
    }
}
