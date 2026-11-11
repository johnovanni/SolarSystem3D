# Catalog tools

These scripts regenerate the offline catalogs in `src/SolarSystem3D.Core/Data/Embedded/`.
The app doesn't need them at run time.

1. **`build_catalogs.py <out-dir>`** reads these files from the current folder:
   - `sats.html` from https://ssd.jpl.nasa.gov/sats/elem/
   - `phys.html` from https://ssd.jpl.nasa.gov/sats/phys_par/
   - `ast5k.json`: SBDB query `fields=spkid,full_name,a,e,i,om,w,ma,epoch,H,diameter,class&sb-kind=a&sb-ns=n&sort=H&limit=5000`
   - `neo.json`: the same fields with `sb-group=neo&sort=H&limit=500`
   - `com.json`: SBDB query `fields=spkid,full_name,q,e,i,om,w,tp,epoch,class,diameter&sb-kind=c`

   It writes `satellites.tsv`, `asteroids.tsv` and `comets.tsv`.

2. **`calibrate_satellites.py <satellites.tsv> <out.tsv>`** queries JPL Horizons for every moon at ten
   epochs from J2000 to 2026, then fits each moon's mean anomaly and rate (`M_fit_deg`, `n_fit_deg_d`)
   using exactly the same orbit model as `KeplerOrbit.ForSatellite`. Allow about 8 minutes.
   For strongly perturbed irregular moons (residual > 10°), it keeps JPL's mean period and fits only the phase.

Two adjustments were made by hand after calibration:

- **Irregular moons** have their phase anchored to the 2026‑11‑10 Horizons sample, so they are most accurate
  near the present date.
- **Triton's orbit** precesses about the Neptune system's Laplace pole, not Neptune's equator.
  Its row was refitted from Horizons orbit normals: pole RA 119.410°, Dec −43.264°, i = 22.978°,
  node 182.269° at J2000, nodal period 681.6 years. It was then recalibrated with step 2.

The **Uranus** "equatorial" frame uses the spin (right-hand) pole, RA 77.311° and Dec +15.175°.
That is the opposite of the IAU "north" pole, and it matches JPL's Uranian satellite elements.
