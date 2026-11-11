import re, html, json, sys, datetime
out = sys.argv[1]
def jd(y, m, d):
    # Gregorian calendar date (d may be fractional) -> Julian Day
    if m <= 2: y -= 1; m += 12
    A = y // 100; B = 2 - A + A // 4
    return int(365.25*(y+4716)) + int(30.6001*(m+1)) + d + B - 1524.5
def cells(s):
    rows = re.findall(r'<tr>(.*?)</tr>', s, re.S)
    for r in rows:
        c = [re.sub(r'\s+', ' ', html.unescape(re.sub(r'<[^>]+>', '', x))).strip() for x in re.findall(r'<td[^>]*>(.*?)</td>', r, re.S)]
        yield c
radius = {}
for c in cells(open('phys.html', encoding='utf8').read()):
    if len(c) > 4:
        try: radius[c[2]] = float(c[4].split()[0])
        except: pass
n = 0
with open(f'{out}/satellites.tsv', 'w', encoding='utf8', newline='\n') as f:
    f.write('# JPL mean satellite elements (ssd.jpl.nasa.gov/sats/elem) + mean radii (sats/phys_par)\n')
    f.write('id\tplanet\tname\tframe\tepoch_jd\ta_km\te\tw_deg\tM_deg\ti_deg\tnode_deg\tP_days\tPapsis_yr\tPnode_yr\tpole_ra\tpole_dec\tradius_km\n')
    for c in cells(open('sats.html', encoding='utf8').read()):
        if len(c) < 19: continue
        y, mo, d = c[6].split('-'); e = jd(int(y), int(mo), float(d))
        f.write('\t'.join([c[3], c[1], c[2], c[5], f'{e:.5f}'] + [x if x not in ('-', 'n/a') else '' for x in c[7:18]] + [str(radius.get(c[3], ''))]) + '\n')
        n += 1
print('satellites', n)
def sb(files, path, header):
    seen = set(); rows = []
    for fn in files:
        for r in json.load(open(fn))['data']:
            if r[0] in seen: continue
            seen.add(r[0]); rows.append(r)
    with open(path, 'w', encoding='utf8', newline='\n') as f:
        f.write(header + '\n')
        for r in rows:
            f.write('\t'.join('' if v is None else str(v).strip() for v in r) + '\n')
    print(path, len(rows))
sb(['ast5k.json', 'neo.json'], f'{out}/asteroids.tsv', 'spkid\tname\ta\te\ti\tom\tw\tma\tepoch\tH\tdiameter\tclass')
sb(['com.json'], f'{out}/comets.tsv', 'spkid\tname\tq\te\ti\tom\tw\ttp\tepoch\tclass\tdiameter')
