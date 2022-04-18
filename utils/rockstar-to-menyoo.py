import json
import os
import sys
import time

#This counter is necessary to avoid all the markers being attached to the first prop
handle_count = 1

weather_lookup = {0:  None, #Current
                  1:  'ExtraSunny', #Bright
                  2:  'Rain', #Raining
                  3:  'Snow',
                  4:  'Smog',
                  5:  'Halloween', #Halloween with rain
                  6:  None, #should be Halloween without rain
                  7:  'Clear', 
                  8:  'Clouds',
                  9:  'Overcast',
                  10: 'Thunder',
                  11: 'Foggy',
                  }

def add_tabs(L):
    return ['\t'+i for i in L]

def get_xyz(d):
    return d['x'], d['y'], d['z']

def print_description(j):
    L = []
    desc = ''.join(j['mission']['gen']['dec'])
    L.append('<Note>'+desc+'</Note>')
    return L

def print_weather(j):
    L = []
    weth = j['mission']['rule'].get('weth', 0) #Use Current if no weather rule
    weather = weather_lookup[weth]
    if weather:
        L.append('<WeatherToSet>'+weather+'</WeatherToSet>')
    return L

def print_startpos(j):
    L = []
    L.append('<ReferenceCoords>')
    grid = j['mission']['race']['grid']
    x, y, z = get_xyz(grid)
    L.append('\t<X>'+str(x)+'</X>')
    L.append('\t<Y>'+str(y)+'</Y>')
    L.append('\t<Z>'+str(z)+'</Z>')
    L.append('</ReferenceCoords>')
    return L

def print_prop(model, loc, vRot, is_dynamic=False, clr=None, lod=None):
    global handle_count
    L = []
    L.append('<Placement>')

    #ModelHash
    if model < 0:
        model += 1<<32
    model = f'0x{model:X}'
    L.append('\t<ModelHash>'+model+'</ModelHash>')

    #Type
    L.append('\t<Type>3</Type>')

    #Dynamic
    if is_dynamic:
        L.append('\t<Dynamic>true</Dynamic>')
    else:
        L.append('\t<Dynamic>false</Dynamic>')

    #FrozenPos
    L.append('\t<FrozenPos>true</FrozenPos>')

    #InitialHandle
    L.append('\t<InitialHandle>'+str(handle_count)+'</InitialHandle>')
    handle_count += 1

    #ObjectProperties
    L.append('\t<ObjectProperties>')
    if clr is not None:
        texture = clr
    else:
        texture = 0
    L.append('\t\t<TextureVariation>'+str(texture)+'</TextureVariation>')
    L.append('\t</ObjectProperties>')

    #OpacityLevel
    L.append('\t<OpacityLevel>255</OpacityLevel>')

    #LodDistance
    if lod is None:
        lod = -1
    L.append('\t<LodDistance>'+str(lod)+'</LodDistance>')

    #IsVisible
    L.append('\t<IsVisible>true</IsVisible>')

    #other stuff
    L.append('\t<MaxHealth>1000</MaxHealth>')
    L.append('\t<Health>1000</Health>')
    L.append('\t<HasGravity>false</HasGravity>')
    L.append('\t<IsOnFire>false</IsOnFire>')
    L.append('\t<IsInvincible>false</IsInvincible>')
    L.append('\t<IsBulletProof>false</IsBulletProof>')
    L.append('\t<IsCollisionProof>false</IsCollisionProof>')
    L.append('\t<IsExplosionProof>false</IsExplosionProof>')
    L.append('\t<IsFireProof>false</IsFireProof>')
    L.append('\t<IsMeleeProof>false</IsMeleeProof>')
    L.append('\t<IsOnlyDamagedByPlayer>false</IsOnlyDamagedByPlayer>')

    #PositionRotation
    x, y, z = get_xyz(loc)
    pitch, roll, yaw = get_xyz(vRot)
    L.append('\t<PositionRotation>')
    L.append('\t\t<X>'+str(x)+'</X>')
    L.append('\t\t<Y>'+str(y)+'</Y>')
    L.append('\t\t<Z>'+str(z)+'</Z>')
    L.append('\t\t<Pitch>'+str(pitch)+'</Pitch>')
    L.append('\t\t<Roll>'+str(roll)+'</Roll>')
    L.append('\t\t<Yaw>'+str(yaw)+'</Yaw>')
    L.append('\t</PositionRotation>')
    
    L.append('</Placement>')

    return L

def print_props(j):
    L = []

    #Regular Props
    propno = j['mission']['prop']['no']
    for i in range(propno):
        model = j['mission']['prop']['model'][i]
        clr = None
        prpclr = j['mission']['prop'].get('prpclr')
        if prpclr:
            clr = prpclr[i]
        lod = None
        prplod = j['mission']['prop'].get('prplod')
        if prplod:
            lod = prplod[i]
        loc = j['mission']['prop']['loc'][i]
        vRot = j['mission']['prop']['vRot'][i]
        head = j['mission']['prop']['head'][i] #unused
        L += print_prop(model, loc, vRot, is_dynamic=False, clr=clr, lod=lod)

    #Dynamic Props
    dpropno = j['mission']['dprop']['no']
    for i in range(dpropno):
        model = j['mission']['dprop']['model'][i]
        clr = None
        prpdclr = j['mission']['dprop'].get('prpdclr')
        if prpdclr:
            clr = prpdclr[i]
        loc = j['mission']['dprop']['loc'][i]
        vRot = j['mission']['dprop']['vRot'][i]
        head = j['mission']['dprop']['head'][i] #unused
        L += print_prop(model, loc, vRot, is_dynamic=True, clr=clr)
        
    return L

def print_checkpoint(loc, num=None, is_secondary=False, is_circular=False, warp=None, warph=0, chevron='default'):
    global handle_count
    L = []

    if get_xyz(loc) == (0.0, 0.0, 0.0): #doesn't exist
        return L
    
    L.append('<Marker>')
    name = 'cp'+str(num)
    if is_secondary:
        name += 's'

    L.append('\t<Name>'+name+'</Name>')
    
    L.append('\t<InitialHandle>'+str(handle_count)+'</InitialHandle>')
    handle_count += 1

    if is_circular:
        L.append('\t<Type>6</Type>')
        L.append('\t<RotateContinuously>true</RotateContinuously>')
        scale = 20.0
        opacity = 150
        zadd = scale/2
    else:
        L.append('\t<Type>1</Type>')
        L.append('\t<RotateContinuously>false</RotateContinuously>')
        scale = 11.5
        zadd = 0.0
        opacity = 40
    L.append('\t<Scale>'+str(scale)+'</Scale>')
    
    L.append('\t<ShowName>false</ShowName>')
    L.append('\t<AllowVehicles>true</AllowVehicles>')

    if is_secondary:
        L.append('\t<Colour R="255" G="150" B="0" A="'+str(opacity)+'" />') #orange secondary
    else:
        L.append('\t<Colour R="255" G="255" B="150" A="'+str(opacity)+'" />') #yellow primary

    x, y, z = get_xyz(loc)
    z += zadd
    L.append('\t<Position>')
    L.append('\t\t<Position X="'+str(x)+'" Y="'+str(y)+'" Z="'+str(z)+'" />')
    L.append('\t\t<Rotation X="0" Y="0" Z="0" />')
    L.append('\t</Position>')
    if warp:
        wx, wy, wz = get_xyz(warp)
    else:
        wx, wy, wz = 0, 0, 0
        warph = 0
    L.append('\t<Destination>')
    L.append('\t\t<LinkInitHandle>0</LinkInitHandle>')
    L.append('\t\t<Position X="'+str(wx)+'" Y="'+str(wy)+'" Z="'+str(wz)+'" />')
    L.append('\t\t<Rotation X="0" Y="0" Z="0" />')
    L.append('\t</Destination>')
    L.append('\t<DestinationHeading>'+str(warph)+'</DestinationHeading>')
    L.append('</Marker>')

    
    #Add Chevron or other marker
    cpitch, croll, cyaw = 0, 0, 0
    if chevron in ('default', 'lap', 'warp'): #Chevron, Replay Marker or Warp
        if chevron == 'default':
            ctype = 20
        elif chevron == 'lap':
            ctype = 24
        elif chevron == 'warp':
            ctype = 27
            cpitch = 90.0
        cscale = scale/2
        if is_circular:
            cx, cy, cz = x, y, z
        else:
            cx, cy, cz = x, y, z+4.0
    elif chevron == 'finish': #Checkered Flag
        if is_circular:
            ctype = 5 
            cscale = scale
            cx, cy, cz = x, y, z
        else:
            ctype = 4
            cscale = 8.0
            cx, cy, cz = x, y, z+8.0
    else:
        return L
    L.append('<Marker>')
    L.append('\t<Name>'+name+'_c</Name>')
    L.append('\t<InitialHandle>'+str(handle_count)+'</InitialHandle>')
    handle_count += 1
    L.append('\t<Type>'+str(ctype)+'</Type>')
    L.append('\t<RotateContinuously>true</RotateContinuously>')
    L.append('\t<Scale>'+str(cscale)+'</Scale>')
    L.append('\t<ShowName>false</ShowName>')
    L.append('\t<AllowVehicles>true</AllowVehicles>')
    L.append('\t<Colour R="150" G="255" B="255" A="128" />')
    L.append('\t<Position>')
    L.append('\t\t<Position X="'+str(cx)+'" Y="'+str(cy)+'" Z="'+str(cz)+'" />')
    L.append('\t\t<Rotation X="'+str(cpitch)+'" Y="'+str(croll)+'" Z="'+str(cyaw)+'" />')
    L.append('\t</Position>')
    L.append('\t<Destination>')
    L.append('\t\t<LinkInitHandle>0</LinkInitHandle>')
    L.append('\t\t<Position X="0" Y="0" Z="0" />')
    L.append('\t\t<Rotation X="0" Y="0" Z="0" />')
    L.append('\t</Destination>')
    L.append('\t<DestinationHeading>0</DestinationHeading>')
    L.append('</Marker>')

    return L


def print_checkpoints(j):
    L = []
    
    cps_p = j['mission']['race']['chl'] #Primary checkpoint locations
    chh = j['mission']['race']['chh'] #Primary checkpoint headings
    cps_s = j['mission']['race'].get('sndchk') #Secondary checkpoint locations
    chh_s = j['mission']['race'].get('sndrsp') #Secondary checkpoint headings

    cpbs1 = j['mission']['race'].get('cpbs1') #Checkpoint bit flags (newer races)
    rndchk = j['mission']['race'].get('rndchk') #Circular primary checkpoints (older races)
    rndchks = j['mission']['race'].get('rndchks') #Circular secondary checkpoints (older races)

    for i in range(len(cps_p)):
        is_circular_p = False
        is_circular_s = False
        is_warp = False
        if cpbs1:
            is_circular_p = 0b10 & cpbs1[i]
            is_circular_s = 0b100 & cpbs1[i]
            is_warp = (1<<27) & cpbs1[i]
        else:
            if rndchk:
                is_circular_p = rndchk[i]
            if rndchks:
                is_circular_s = rndchks[i]

        warp = None
        warph = 0
        if i == len(cps_p)-1:
            if j['mission']['race']['ptp']: #Point-to-Point race
                chevron = 'finish'
            else: #Lap race
                chevron = 'lap'
        elif is_warp:
            warp = cps_p[i+1]
            chevron = 'warp'
            warph = chh[i+1]
        else:
            chevron = 'default'
            
        L += print_checkpoint(cps_p[i], num=i, is_secondary=False, is_circular=is_circular_p, warp=warp, warph=warph, chevron=chevron)
        if cps_s:
            L += print_checkpoint(cps_s[i], num=i, is_secondary=True, is_circular=is_circular_s, chevron=chevron)
            
    return L

def convert_json(j):
    global handle_count
    handle_count = 1
    L = []
    
    L.append('<?xml version="1.0" encoding="utf-8"?>')
    L.append('<SpoonerPlacements>')
    L += add_tabs(print_description(j))
    L.append('\t<ClearDatabase>true</ClearDatabase>')
    L.append('\t<ClearWorld>true</ClearWorld>')
    L += add_tabs(print_weather(j))
    L += add_tabs(print_startpos(j))
    L += add_tabs(print_props(j))
    L += add_tabs(print_checkpoints(j))
    L.append('</SpoonerPlacements>')
    return L


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
            L = convert_json(j)
            title = j['mission']['gen']['nm']
            p = 'menyooStuff'+os.path.sep+'Spooner'+os.path.sep+curdir
            os.makedirs(p, exist_ok=True)
            f = open(p+title+'.xml', mode='w')
            f.write('\n'.join(L))
            f.close()
            
    except Exception as e:
        print(track, 'conversion failed!', e)
        time.sleep(3)
        


#vspn(s)0,1,2 = vehicle respawns for checkpoints
#chs(2) = checkpoint size, e.g. cp30 on Trench III is 3x bigger (chs[29] is 3)
#chvs = checkpoint visibility float
#cpbs1 1<<10 = respawnable
