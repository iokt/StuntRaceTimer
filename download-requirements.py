import json
import os
import re
import urllib.parse
import urllib.request
import zipfile
import zlib

LEMONUI_REPO = 'LemonUIbyLemon/LemonUI'
MENYOO_REPO = 'MAFINS/MenyooSP'
SHVDN_REPO = 'crosire/scripthookvdotnet'
SHV_URL = 'https://www.dev-c.com/gtav/scripthookv/'
GTAV_PATH = 'C:/Program Files (x86)/Steam/steamapps/common/Grand Theft Auto V'

def get_scripthookv():
    page_url = urllib.parse.urlparse(SHV_URL)
    resp = urllib.request.urlopen(page_url.geturl())
    text = resp.read().decode()
    m = re.findall(r'"(/files/ScriptHookV_[0-9\.]+\.zip)"', text)
    assert(len(m) == 1)
    download_url = page_url._replace(path=m[0])
    headers = {'Referer': page_url._replace(scheme='http').geturl()}
    req = urllib.request.Request(download_url.geturl(), headers=headers)
    resp = urllib.request.urlopen(req)
    return resp.read()

def get_github_release(repo):
    resp = urllib.request.urlopen(f'https://api.github.com/repos/{repo}/releases/latest')
    j = json.load(resp)
    assert(len(j['assets']) == 1)
    url = j['assets'][0]['browser_download_url']
    resp = urllib.request.urlopen(url)
    return resp.read()

def download_reqs():
    os.makedirs('temp', exist_ok=True)
    with open('temp/ScriptHookV.zip', 'wb') as f:
        f.write(get_scripthookv())
    with open('temp/ScriptHookVDotNet.zip', 'wb') as f:
        f.write(get_github_release(SHVDN_REPO))
    with open('temp/LemonUI.zip', 'wb') as f:
        f.write(get_github_release(LEMONUI_REPO))
    with open('temp/MenyooSP.zip', 'wb') as f:
        f.write(get_github_release(MENYOO_REPO))

# don't use this
def install_reqs():
    #with zipfile.ZipFile('temp/ScriptHookV.zip') as f:
    #    f.extract('bin/ScriptHookV.dll', path=GTAV_PATH)
    with zipfile.ZipFile('temp/ScriptHookVDotNet.zip') as f:
        f.extract('ScriptHookVDotNet2.dll', path=GTAV_PATH)
        f.extract('ScriptHookVDotNet3.dll', path=GTAV_PATH)
    with zipfile.ZipFile('temp/LemonUI.zip') as f:
        f.extract('SHVDN3/LemonUI.SHVDN3.dll', path=GTAV_PATH)
        os.rename(GTAV_PATH+'/SHVDN3', GTAV_PATH+'/scripts')
    #with zipfile.ZipFile('temp/MenyooSP.zip') as f:
    #    f.extract('bin/ScriptHookV.dll', path=GTAV_PATH)

if __name__ == '__main__':
    download_reqs()
    install_reqs()
