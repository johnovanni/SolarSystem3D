"""Calibrate JPL mean satellite elements against JPL Horizons.

For every moon, fetch planet-centric J2000-ecliptic vectors at a geometric ladder of epochs, convert each
to a mean anomaly using the SAME orbit model as the C# KeplerOrbit.ForSatellite (frame, i, precessing node and
periapsis), unwrap whole revolutions step by step, and least-squares fit M(t) = M0 + n (t - epoch).
Writes satellites.tsv with two extra columns: M_fit_deg (at the row's epoch) and n_fit_deg_d.
"""
import math, json, sys, time, urllib.request, urllib.parse
D = math.pi/180; EPS = 23.4392911*D
POLES = {'Mars':(317.681,52.887),'Jupiter':(268.057,64.495),'Saturn':(40.589,83.537),'Uranus':(77.311,15.175),
         'Neptune':(299.36,43.46),'Pluto':(132.993,-6.163),'Earth':(0,90)}
CENTER = {'Mars':'499','Jupiter':'599','Saturn':'699','Uranus':'799','Neptune':'899','Pluto':'999','Earth':'399'}
def rx(a): c,s=math.cos(a),math.sin(a); return [[1,0,0],[0,c,-s],[0,s,c]]
def rz(a): c,s=math.cos(a),math.sin(a); return [[c,-s,0],[s,c,0],[0,0,1]]
def mm(a,b): return [[sum(a[i][k]*b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
def mv(a,v): return [sum(a[i][k]*v[k] for k in range(3)) for i in range(3)]
def tr(a): return [[a[j][i] for j in range(3)] for i in range(3)]
def cross(a,b): return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def frame(ra,dec):
    a,d=ra*D,dec*D; P=[math.cos(d)*math.cos(a),math.cos(d)*math.sin(a),math.sin(d)]; N=[-math.sin(a),math.cos(a),0]; Y=cross(P,N)
    C=[[N[0],Y[0],P[0]],[N[1],Y[1],P[1]],[N[2],Y[2],P[2]]]
    return mm(rx(-EPS),C)
def f(x, default=0.0):
    try: return float(x)
    except: return default

src, dst = sys.argv[1], sys.argv[2]
lines = open(src, encoding='utf8').read().splitlines()
comment, header, rows = lines[0], lines[1].split('\t'), [l.split('\t') for l in lines[2:]]
LADDER = [0,1,3,10,30,100,300,1000,3000,9810]
out = [comment.replace(')', ') calibrated to JPL Horizons', 1), '\t'.join(header + ['M_fit_deg','n_fit_deg_d'])]
log = open('calibrate.log','w',encoding='utf8')
samples_log = open('samples.log','w',encoding='utf8')
for idx, c in enumerate(rows):
    r = dict(zip(header, c + ['']*(len(header)-len(c))))
    extra = ['','']
    if r['id'] != '301' and r['planet'] in CENTER:
        try:
            a,e,w,M,i,node,P = (f(r[k]) for k in ['a_km','e','w_deg','M_deg','i_deg','node_deg','P_days'])
            pa, pn = f(r['Papsis_yr']), f(r['Pnode_yr'])
            ep = float(r['epoch_jd'])
            fr = r['frame'].lower()
            if fr == 'laplace':
                ra, dec = (f(r['pole_ra']), f(r['pole_dec'])) if r['pole_ra'] else POLES[r['planet']]
                F = frame(ra, dec)
            elif fr == 'equatorial': F = frame(*POLES[r['planet']])
            else: F = [[1,0,0],[0,1,0],[0,0,1]]
            apsis = 360/(pa*365.25) if pa > 0 else 0.0
            nrate = -360/(pn*365.25) if pn > 0 else 0.0
            if i > 90: apsis, nrate = -apsis, -nrate
            jds = [2451545.0 + k for k in LADDER]
            q = {'format':'json','COMMAND':f"'{r['id']}'",'EPHEM_TYPE':'VECTORS','CENTER':f"'500@{CENTER[r['planet']]}'",
                 'REF_PLANE':'ECLIPTIC','REF_SYSTEM':'J2000','TLIST_TYPE':'JD','VEC_TABLE':'1','OUT_UNITS':'AU-D','CSV_FORMAT':'YES','TIME_TYPE':'TDB'}
            url = 'https://ssd.jpl.nasa.gov/api/horizons.api?' + urllib.parse.urlencode(q) + '&TLIST=' + '%20'.join(f"'{j}'" for j in jds)
            for attempt in range(3):
                try:
                    res = json.load(urllib.request.urlopen(url, timeout=60))['result']; break
                except Exception as ex:
                    time.sleep(3); res = ''
            seg = res[res.index('$$SOE')+5:res.index('$$EOE')].strip().splitlines()
            samples = []
            for line in seg:
                p = [x.strip() for x in line.split(',')]
                t = float(p[0]); v = [float(p[2]), float(p[3]), float(p[4])]
                dt = t - ep
                Om = (node + nrate*dt)*D; om = (w + apsis*dt)*D
                R = mm(F, mm(rz(Om), rx(i*D)))         # orbit-node frame -> ecliptic
                o = mv(tr(R), v)                         # vector in orbit plane (x toward ascending node)
                u = math.atan2(o[1], o[0])               # argument of latitude
                nu = u - om
                E = 2*math.atan(math.sqrt((1-e)/(1+e))*math.tan(nu/2)) if e < 1 else nu
                Mm = math.degrees(E - e*math.sin(E))
                samples.append((t, Mm))
            # progressive unwrapping
            n_est = 360.0/P
            t0, m0 = samples[0]
            unw = [(t0, m0)]
            for t, m in samples[1:]:
                tp, mp = unw[-1]
                pred = mp + n_est*(t - tp)
                m_u = m + 360*round((pred - m)/360)
                unw.append((t, m_u))
                n_est = (m_u - m0)/(t - t0)
            # least squares M = M0 + n (t - ep)
            xs = [t-ep for t,_ in unw]; ys = [m for _,m in unw]
            xm = sum(xs)/len(xs); ym = sum(ys)/len(ys)
            n_fit = sum((x-xm)*(y-ym) for x,y in zip(xs,ys))/sum((x-xm)**2 for x in xs)
            m_fit = ym - n_fit*xm
            resid = max(abs(y - (m_fit + n_fit*x)) for x,y in zip(xs,ys))
            mode = 'fit'
            if resid > 10:
                # Strongly perturbed (irregular) moon: keep JPL's mean period, fit the phase to recent epochs only.
                n_fit = 360.0/P
                late = [(t, m) for t, m in samples if t - 2451545.0 >= 1000]
                sx = sum(math.sin(math.radians(m - n_fit*(t-ep))) for t, m in late)
                cx = sum(math.cos(math.radians(m - n_fit*(t-ep))) for t, m in late)
                m_fit = math.degrees(math.atan2(sx, cx))
                resid = max(abs(((m - (m_fit + n_fit*(t-ep))) + 180) % 360 - 180) for t, m in late)
                mode = 'phase-only'
            samples_log.write(r['id'] + chr(9) + ' '.join(f'{t}:{m:.3f}' for t, m in samples) + chr(10))
            extra = [f'{m_fit % 360:.5f}', f'{n_fit:.9f}']
            log.write(f"{r['id']}\t{r['name']}\t{r['planet']}\tM {M:.2f}->{m_fit%360:.2f}\tn {360/P:.6f}->{n_fit:.6f} ({(n_fit*P/360-1)*100:+.3f}%)\tmaxresid {resid:.2f}\n")
        except Exception as ex:
            log.write(f"{r['id']}\t{r['name']}\tFAILED {ex}\n")
        log.flush()
        time.sleep(0.3)
    out.append('\t'.join(c + extra))
    if idx % 20 == 0: print(idx, flush=True)
open(dst, 'w', encoding='utf8', newline='\n').write('\n'.join(out) + '\n')
print('done')
