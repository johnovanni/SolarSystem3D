# Solar System 3D

An interactive 3D map of the solar system for Windows, written in C# with WPF.
You can set the calendar and clock to any moment from 1000 AD to 3000 AD and see where everything is:
the Sun, all eight planets, the dwarf planets, all 461 known planetary moons, thousands of asteroids
(up to 200,000 if you download them), and every comet in JPL's Small-Body Database.

## Features

- **Settable calendar and clock**, in UTC or local time. Step buttons (±1 hour, day, month, year),
  forward and reverse play, and speeds from real time to 10 years per second.
- **A 3D view you can explore.** Left-drag rotates, right-drag pans and the mouse wheel zooms.
  Click an object to select it. Double-click to centre on it: the camera then follows that object while time runs.
- **Orbits for every kind of body**, each switchable on or off: planets, dwarf planets, moons
  (shown when you zoom in), comets near perihelion or all periodic comets, the brightest N asteroids,
  and the selected object (highlighted).
- **Names for planets, dwarf planets, moons, asteroids and comets**, each switchable, with overlap avoidance.
- **True-scale bodies.** Spheres are drawn at real size and get bigger as you zoom in, with Saturn's rings.
  A slider can exaggerate their size.
- **Information panel** for the selected object: distances from the Sun and Earth, light time,
  heliocentric coordinates, radius, orbital elements, period and data source.
- **Search** across all objects by name or ID.
- **Live catalog updates.** One click downloads current asteroid and comet orbits from NASA/JPL
  (20,000 to 200,000 asteroids). The program also works offline with the built-in snapshot.

## Build and run

You need the .NET 10 SDK or later on Windows.

The shared astronomy code comes from [AstroCore](https://github.com/johnovanni/AstroCore), a git submodule
in `external/AstroCore`, so clone with `--recursive`:

```bash
git clone --recursive https://github.com/johnovanni/SolarSystem3D.git
```

If you already cloned without it, fetch the submodule with:

```bash
git submodule update --init
```

Then build and run:

```bash
dotnet run --project src/SolarSystem3D.App -c Release
```

Or open `SolarSystem3D.sln` in Visual Studio 2026 and run `SolarSystem3D.App`.
To run the accuracy tests, which compare positions with JPL Horizons:

```bash
dotnet test
```

`DownloadTests` needs an internet connection. To skip it, run `dotnet test --filter Category!=Network`.

> If the solution sits in a very deep folder, `Directory.Build.props` puts `bin`/`obj` under
> `%TEMP%\SolarSystem3D.build` instead, to stay under Windows' 260-character path limit.

## How positions are computed

| Objects | Model | Accuracy vs. JPL Horizons (2026‑11‑10) |
|---|---|---|
| Planets | JPL "Keplerian elements for approximate positions" (Standish), 1800–2050 | 0.002°–0.024° |
| Earth | Earth–Moon barycentre minus the Moon's offset | 0.002° |
| Moon | ELP‑2000/82 principal terms (Meeus, ch. 47) | 0.002° |
| Planetary moons | JPL mean elements in Laplace or equatorial frames with node and apsis precession; phase and rate **calibrated against JPL Horizons** (2000–2026) | Galileans 0.06°–0.8°, Titan 0.5°, Titania 0.04°, Triton 0.007° |
| Irregular (outer) moons | JPL mean elements, phase anchored to Horizons at 2026‑11‑10 | a few degrees near now; drifts years away |
| Dwarf planets, asteroids | JPL SBDB osculating elements, two-body propagation | Ceres 0.014°, Eris 0.01° |
| Comets | JPL SBDB elements (elliptic, parabolic and hyperbolic) | Encke 0.6° (planetary perturbations) |

Orbital elements are two-body approximations. Expect asteroid and comet positions to drift slowly
as you move away from their element epochs, mostly because of Jupiter's pull. Planet accuracy degrades outside 1800–2050,
and the status bar warns you when you go there.

## Project layout

```
external/AstroCore         Shared astronomy core (git submodule): Vector3d, Matrix3d, AstroTime (Julian dates, ΔT),
                           Frames, Conic (Kepler solvers), IOrbitModel → PlanetOrbit, LunarOrbit, EarthOrbit
src/SolarSystem3D.Core     Solar-system model (no UI), built on AstroCore
  Orbits/                  KeplerOrbit (two-body orbits for moons, asteroids and comets)
  Bodies/                  CelestialBody → Star, Planet, DwarfPlanet, NaturalSatellite, Asteroid, Comet
  Data/                    Catalog store/reader/downloader, built-in planet data, SolarSystemFactory
  Data/Embedded/           Offline catalog snapshot (moons, 5,500 asteroids, 4,088 comets)
  SolarSystem.cs           The body collection and per-instant update
src/SolarSystem3D.App      WPF application (MVVM)
  ViewModels/              MainViewModel (clock, selection, search, catalogs), converters
  Simulation/              SimulationClock, TimeRate
  Rendering/               SceneRenderer (HelixToolkit), LabelOverlay, Projector, OrbitCache, Palette
  Controllers/             SceneController (frame loop, picking, camera focus)
tests/SolarSystem3D.Tests  Accuracy tests against JPL Horizons, maths and live-download tests
tools/                     Python scripts that build and calibrate the embedded catalogs
```

## Data sources

Data comes from the NASA/JPL Solar System Dynamics group:
[Small-Body Database](https://ssd-api.jpl.nasa.gov/doc/sbdb_query.html),
[planetary satellite mean elements](https://ssd.jpl.nasa.gov/sats/elem/) and
[physical parameters](https://ssd.jpl.nasa.gov/sats/phys_par/),
[approximate planetary positions](https://ssd.jpl.nasa.gov/planets/approx_pos.html) and
[Horizons](https://ssd.jpl.nasa.gov/horizons/), which was used for calibration and testing.
Downloaded catalogs are cached in `%LOCALAPPDATA%\SolarSystem3D`.
