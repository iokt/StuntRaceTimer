import json
import sys
import time
import os
from scipy.spatial.transform import Rotation

def get_xyz(d):
    return d['x'], d['y'], d['z']

def to_vector3(d):
    x, y, z = get_xyz(d)
    return {'X': x, 'Y': y, 'Z': z, '_padding': 0}

def to_quat(h):
    q = Rotation.from_euler('z', h, degrees=True).as_quat()
    return {'W': q[3], 'X': 0.0, 'Y': 0.0, 'Z': q[2]}

def convert_json(j):
    chl = j['mission']['race']['chl']
    sndchk = j['mission']['race'].get('sndchk')
    chs = j['mission']['race'].get('chs')
    chs2 = j['mission']['race'].get('chs2')
    chh = j['mission']['race']['chh']
    sndrsp = j['mission']['race'].get('sndrsp')
    cpbs1 = j['mission']['race'].get('cpbs1')
    rndchk = j['mission']['race'].get('rndchk')
    rndchks = j['mission']['race'].get('rndchks')
    dhprop = j['mission'].get('dhprop')
    prop = j['mission'].get('prop')

    d = dict()
    d['checkpoints'] = []
    d['description'] = None
    ptp = j['mission']['race']['ptp']
    d['lapMode'] = not ptp
    d['name'] = j['mission']['gen']['nm']
    d['numCheckpoints'] = len(chl)
    d['version'] = 'v3.0'
    if ptp:
        d['spawn'] = to_vector3(j['mission']['race']['vspn0'][0])
    else:
        d['spawn'] = to_vector3(j['mission']['race']['vspn0'][-1])
    d['dhprops'] = []
    if dhprop:
        for i in range(len(dhprop['mn'])):
            d['dhprops'].append({'pos': to_vector3(dhprop['pos'][i]), 'modelHash': dhprop['mn'][i]})
    d['props'] = []
    if prop:
        prpsba = prop.get('prpsba')
        for i in range(len(prop['model'])):
            speedup = -1
            if prpsba:
                speedup = prpsba[i]
            d['props'].append({'pos': to_vector3(prop['loc'][i]), 'model': prop['model'][i], 'speedup': speedup})

    num = 0
    for i in range(len(chl)):
        pos = to_vector3(chl[i])
        quat = to_quat(chh[i])
        cp = {'number': num, 'position': pos, 'quaternion': quat}
        if cpbs1:
            cp['cpbs1'] = cpbs1[i]
        if rndchk:
            cp['rndchk'] = rndchk[i]
        if rndchks:
            cp['rndchks'] = rndchks[i]
        if chs:
            cp['chs'] = chs[i]
        if sndchk:
            pos2 = to_vector3(sndchk[i])
            quat2 = to_quat(sndrsp[i])
            cp['position2'] = pos2
            cp['quaternion2'] = quat2
        if chs2:
            cp['chs2'] = chs2[i]
            
        d['checkpoints'].append(cp)
        num += 1
    return d


if __name__ == '__main__':
    try:
        tracks = sys.argv[1:]
        for track in tracks:
            print(track)
            curdir = ''
            if isinstance(track, tuple):
                curdir, track = track[0], track[1]
            if os.path.isdir(track):
                tracks += [(curdir+os.path.basename(track)+os.path.sep, a.path) for a in os.scandir(track)]
                continue
            if os.path.splitext(track)[1].lower() != '.json':
                continue
            f = open(track, 'rb')
            j = json.load(f)
            f.close()
            d = convert_json(j)
            title = j['mission']['gen']['nm']
            p = 'LapTimer'+os.path.sep+'races'+os.path.sep+curdir
            os.makedirs(p, exist_ok=True)
            f = open(p+title+'_c.json', mode='w')
            json.dump(d, f)
            f.close()
            
    except Exception as e:
        print(track, 'conversion failed!', e)
        time.sleep(3)
        
