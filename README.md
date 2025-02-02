# Stunt Race Timer
Fork of [Lap Timer](https://github.com/DavidLiuGit/GTAV_Lap_Timer). This is a ScriptHookVDotNet script for GTA5. 

Test your car's performance any way you want with Race Timer. Set your own checkpoints (in Placement Mode), and take any car through the race you created.

As you drive, Race Timer shows you at each checkpoint:
- **Elapsed time**
- **Fastest split time**: delta between your current time and the best time previously achieved in any vehicle
- **Vehicle split time**: delta between your current time and best time previously achieved in the same vehicle


---
## Installation
Extract `LapTimer.dll`, `LapTimer.ini`, and `LapTimer` folder into your `scripts` folder.  
If you have already installed an earlier version of the script, make sure to merge the `LapTimer` folder to preserve your old races and timing sheets.
If you want to spawn in the props, you need to install Menyoo and add the XML files to your `menyooStuff/Spooner` folder, then load them in the Object Spooner menu.

### Requirements
- .NET 4.8
- [ScriptHookVDotNet v3](https://github.com/crosire/scripthookvdotnet/releases/latest)
- [LemonUI](https://github.com/LemonUIbyLemon/LemonUI/releases/latest)
- [MenyooSP](https://github.com/MAFINS/MenyooSP/releases/latest) (to spawn vehicles and load props for tracks)
- [Simple Trainer](https://www.gta5-mods.com/scripts/simple-trainer-for-gtav) (to prevent cars from despawning)

---
## Usage

### Menu
The menu has all the controls you need to use all features of this script.

- `Ctrl+L`: show menu


### Placement Mode
In this mode, you will create your custom race by placing checkpoints. Enter "Placement Mode" with F5.
- `Ctrl+X`: place new checkpoint
- `Ctrl+Z`: undo last checkpoint
- `Ctrl+D`: delete all checkpoints


### Race Mode
Once you've placed at least 2 checkpoints, get in a vehicle and press F6. You will be teleported to the first checkpoint, and the timer will start. Times will be displayed at each checkpoint and at the end of the race.
- `Ctrl+Shift+R`: restart race
- `Ctrl+R` or `Vehicle Exit`: respawn
- `Ctrl+Z`: stop car instantly
- `Character Wheel`: simulated GTAO character swap stop
- `Ctrl+I`: toggle recording
- `Ctrl+O`: toggle playback
- `Cinematic Cam`: toggle playback (while in record mode)
- `Phone Up`: pause/unpause playback
- `Phone Left`: slow down time
- `Phone Right`: speed up time
- `Vehicle Aim+Look Left/Right`: seek backwards/forwards (while paused in playback mode)

### Circuit vs point-to-point
In v3, support for circuit races were implemented. Whereas all races in earlier versions ended when you reached the last checkpoint, if "Lap Mode" checkbox is checked, you will begin a new lap when you reach the last checkpoint. Some preset races, including Prison Loop, Grove Street, Spa Franchorchamps, Redwood Lights, and Broughy1322's famous test track have been updated to run in Lap Mode!

Similar to qualifying in motorsports like F1, when racing in Lap Mode, your first lap is an "out lap", and subsequent laps are "flying laps". As before, your timing sheet is exported when you complete a lap.

`Menu > Race Controls > Lap Mode`

### Colors for elapsed time
- **Purple**: overall fastest time
- **Green**: fastest time for the vehicle
- **White**: neither of the above


### Import/Export races
You can export races you've created to replay in another session (or share them with other users). Select `Menu > Export Race`, and give your race a descriptive name. The race will be saved as a `.json` file in your `scripts/LapTimer/races` directory.

To import races, select `Menu > Race Import Menu` in the menu. A list of importable races will show up. To import races created by other users, place their race `.json` file in your `scripts/LapTimer/races` directory. This script comes with a number of races already created.


### Timing sheets
You can view your lap times for the current race in `Menu > Lap Times`

When you complete a race, your time is automatically saved and exported in a file. Times are saved on a per-vehicle basis, and are recorded for each checkpoint in the race. 

Your race's timing sheet is automatically imported when you import a race, as long as you haven't modified it! If you modify the race (i.e. adding or deleting checkpoints), your previously recorded lap times will be invalidated.


### Replays
A replay is compressed and saved in your `scripts/LapTimer/replays` folder whenever you stop recording. You can import them in `Menu > Replay Import Menu`.



---
## Development
### Links
- [GTA5 Mods release](https://www.gta5-mods.com/scripts/race-timer)
- [GitHub source code](https://github.com/DavidLiuGit/GTAV_Lap_Timer)

### Change Log
#### 3.1.1
- add pre-packaged tracks again
- update Spa Francorchamps for v3.0
- add option to display speed in KM/h or MPH; KM/h by default

#### 3.1
- add the option to display speed at each checkpoint

#### 3.0.1
- added Quarter Mile and Half Mile preset races

#### 3.0
- implemented lap (circuit) mode! Toggle found at `Menu > Race Controls > Lap Mode`
- modified preset races to properly support lap mode
- improved stability with under-the-hood changes

#### 2.2.1
- added Grove Street preset race

#### 2.2:
- added a submenu to display best lap times of all vehicles for the current race

#### 2.1:
- Implemented exportable/importable timing sheets. Now your times are automatically saved (when you finish a race) and reloaded when you import the same race again

#### 2.0.2:
- More preset races, including North Yankton!

#### 2.0.1:
- Added 5 more preset races

#### 2.0
- Implemented NativeUI menu
- able to choose races to import from a list in the menu
- 
#### 1.2
- implemented freeze time when starting a race. 750ms by default
- implemented import/export of race in placement mode. 6 preset races are included 

#### 1.1
- added support for INI, allowing custom hotkeys 

#### 1.0
- initial release
