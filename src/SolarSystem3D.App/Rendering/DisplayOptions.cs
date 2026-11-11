using CommunityToolkit.Mvvm.ComponentModel;

namespace SolarSystem3D.App.Rendering;

/// <summary>User-selectable display settings. Any change raises <see cref="ObservableObject.PropertyChanged"/>,
/// which the renderer listens to.</summary>
public sealed partial class DisplayOptions : ObservableObject
{
    // Objects
    [ObservableProperty] private bool _showDwarfPlanets = true;
    [ObservableProperty] private bool _showMoons = true;
    [ObservableProperty] private bool _showAsteroids = true;
    [ObservableProperty] private bool _showComets = true;

    // Orbits
    [ObservableProperty] private bool _showPlanetOrbits = true;
    [ObservableProperty] private bool _showDwarfPlanetOrbits = true;
    [ObservableProperty] private bool _showMoonOrbits = true;
    [ObservableProperty] private bool _showIrregularMoonOrbits;
    [ObservableProperty] private bool _showAsteroidOrbits;
    [ObservableProperty] private bool _showCometOrbits = true;          // comets within a year of perihelion
    [ObservableProperty] private bool _showPeriodicCometOrbits;         // all numbered periodic comets
    [ObservableProperty] private bool _showSelectedOrbit = true;

    // Labels
    [ObservableProperty] private bool _labelPlanets = true;
    [ObservableProperty] private bool _labelDwarfPlanets = true;
    [ObservableProperty] private bool _labelMoons = true;
    [ObservableProperty] private bool _labelIrregularMoons;
    [ObservableProperty] private bool _labelAsteroids;
    [ObservableProperty] private bool _labelComets;

    // Scene
    [ObservableProperty] private bool _showEclipticGrid = true;

    /// <summary>Exaggeration of planet and moon radii (1 = true scale).</summary>
    [ObservableProperty] private double _bodyScale = 1;

    /// <summary>Screen size of the position markers in pixels.</summary>
    [ObservableProperty] private double _markerSize = 1;

    /// <summary>How many of the brightest asteroids get orbit lines when asteroid orbits are on.</summary>
    [ObservableProperty] private int _asteroidOrbitCount = 100;
}
