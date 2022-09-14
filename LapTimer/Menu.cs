using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GTA;
//using NativeUI;


namespace LapTimer
{
	class NativeUIMenu
	{
		RaceControl race;

		public LemonUI.ObjectPool _menuPool;
		public LemonUI.Menus.NativeMenu mainMenu;

		public NativeUIMenu(ref RaceControl raceControl)
		{
			race = raceControl;

			// create new menu pool & add the main menu to it
			_menuPool = new LemonUI.ObjectPool();
			_menuPool.Add(buildMainMenu());
		}


		#region publicMethods
		private List<LemonUI.Menus.NativeMenu> submenusToAddToPool = new List<LemonUI.Menus.NativeMenu>();
		private List<LemonUI.Menus.NativeMenu> submenusAddedToPool = new List<LemonUI.Menus.NativeMenu>();

		/// <summary>
		/// Toggle visibility of menus. If no menu is currently open, open main menu.
		/// </summary>
		/// <returns>boolean indicating whether a menu is now open</returns>
		public bool toggleMenu()
		{
			if (_menuPool.AreAnyVisible)
			{
				_menuPool.HideAll();
				return false;
			}
			else
			{
				mainMenu.Visible = true;
				return true;
			}
		}

		public void processMenus()
        {
			_menuPool.Process();
			foreach (LemonUI.Menus.NativeMenu submenu in submenusToAddToPool)
            {
				_menuPool.Add(submenu);
            }
			submenusToAddToPool.Clear();
			if (!_menuPool.AreAnyVisible)
			{
				foreach (LemonUI.Menus.NativeMenu submenu in submenusAddedToPool)
				{
					_menuPool.Remove(submenu);
				}
				submenusAddedToPool.Clear();
			}
		}

		#endregion



		#region menus

		private LemonUI.Menus.NativeMenu buildMainMenu()
		{
			mainMenu = new LemonUI.Menus.NativeMenu("Race Timer", "~b~by iLike2Teabag");

			// add a submenu to handle race imports
			LemonUI.Menus.NativeMenu raceImportMenu = new LemonUI.Menus.NativeMenu("", "Race Import Menu", "Choose races to import from file");
			_menuPool.Add(raceImportMenu);
			mainMenu.AddSubMenu(raceImportMenu);
			raceImportMenu.Shown += (sender, e) => buildRaceImportMenu(raceImportMenu, null);
			buildRaceImportMenu(raceImportMenu, null);

			// add a submenu for race control
			LemonUI.Menus.NativeMenu raceControlMenu = new LemonUI.Menus.NativeMenu("", "Race Control Menu", "Modify checkpoints and race mode");
			_menuPool.Add(raceControlMenu);
			mainMenu.AddSubMenu(raceControlMenu);
			raceControlMenu.Shown += (sender, e) => buildRaceControlMenu(raceControlMenu);
			//buildRaceControlMenu(raceControlMenu);

			// add a submenu to handle race imports
			LemonUI.Menus.NativeMenu replayImportMenu = new LemonUI.Menus.NativeMenu("", "Replay Import Menu", "Choose replays to import from file");
			_menuPool.Add(replayImportMenu);
			mainMenu.AddSubMenu(replayImportMenu);
			replayImportMenu.Shown += (sender, e) => buildReplayImportMenu(replayImportMenu);
			buildReplayImportMenu(replayImportMenu);

			// add a submenu for settings
			LemonUI.Menus.NativeMenu settingsMenu = new LemonUI.Menus.NativeMenu("", "Settings Menu");
			_menuPool.Add(settingsMenu);
			mainMenu.AddSubMenu(settingsMenu);
			settingsMenu.Shown += (sender, e) => loadSettingsMenu(settingsMenu);

			// add a submenu for Timing Sheet
			LemonUI.Menus.NativeMenu lapTimeMenu = new LemonUI.Menus.NativeMenu("", "Lap Times", "Display lap times for the current race");
			_menuPool.Add(lapTimeMenu);
			mainMenu.AddSubMenu(lapTimeMenu);
			lapTimeMenu.Shown += (sender, e) => loadLapTimeMenu(lapTimeMenu);

			// add controls to enter placement, race, debug modes
			LemonUI.Menus.NativeItem placementToggle = new LemonUI.Menus.NativeItem("Toggle Placement Mode");
			LemonUI.Menus.NativeItem raceToggle = new LemonUI.Menus.NativeItem("Toggle Race Mode");
			placementToggle.Activated += (menu, sender) =>
			{
				race.togglePlacementMode();
			};
			raceToggle.Activated += (menu, sender) => {
				race.toggleRaceMode();
			};
			mainMenu.Add(placementToggle);
			mainMenu.Add(raceToggle);



			/**
			// add a submenu for debug settings
			UIMenu debugSettingsMenu = _menuPool.AddSubMenu(mainMenu, "Debug Mode Settings");
			debugSettingsMenu.OnMenuOpen += loadDebugSettingsMenu;

			// add a submenu for camera settings
			UIMenu cameraSettingsMenu = _menuPool.AddSubMenu(mainMenu, "Camera Settings");
			cameraSettingsMenu.OnMenuOpen += loadCameraSettingsMenu;
			**/

			// add control to export race
			LemonUI.Menus.NativeItem exportRaceItem = new LemonUI.Menus.NativeItem("Export Race");
			exportRaceItem.Activated += (menu, sender) => race.exportRace();
			mainMenu.Add(exportRaceItem);

			LemonUI.Menus.NativeItem exportReplayItem = new LemonUI.Menus.NativeItem("Export Replay");
			exportReplayItem.Activated += (menu, sender) => race.exportReplay(askname: true);
			mainMenu.Add(exportReplayItem);

			//mainMenu.RefreshIndex();
			return mainMenu;
		}
		
		private void loadSettingsMenu(LemonUI.Menus.NativeMenu submenu)
        {
			submenu.Clear();

			LemonUI.Menus.NativeCheckboxItem debugToggle = new LemonUI.Menus.NativeCheckboxItem("Toggle Debug Mode", race.debugMode);
			LemonUI.Menus.NativeCheckboxItem ghostToggle = new LemonUI.Menus.NativeCheckboxItem("Toggle Ghost Mode", race.tasPlaybackGhostMode);
			debugToggle.CheckboxChanged += (menu, sender) => {
				race.toggleDebugMode();
				debugToggle.Checked = race.debugMode;
			};
			ghostToggle.CheckboxChanged += (menu, sender) => race.tasPlaybackGhostMode = ghostToggle.Checked;
			submenu.Add(debugToggle);
			submenu.Add(ghostToggle);

			// add a submenu for debug settings
			LemonUI.Menus.NativeMenu debugSettingsMenu = new LemonUI.Menus.NativeMenu("", "Debug Mode Settings");
			debugSettingsMenu.Shown += (menu, sender) => { 
				loadDebugSettingsMenu(debugSettingsMenu); 
			};
			submenu.AddSubMenu(debugSettingsMenu);
			submenusToAddToPool.Add(debugSettingsMenu);

			// add a submenu for camera settings
			LemonUI.Menus.NativeMenu cameraSettingsMenu = new LemonUI.Menus.NativeMenu("", "Race Settings");
			cameraSettingsMenu.Shown += (menu, sender) => {
				loadCameraSettingsMenu(cameraSettingsMenu);
			};
			submenu.AddSubMenu(cameraSettingsMenu);
			submenusToAddToPool.Add(cameraSettingsMenu);

			//submenu.RefreshIndex();
		}
		private void loadDebugSettingsMenu(LemonUI.Menus.NativeMenu submenu)
        {
			// clear the menu
			submenu.Clear();
			{
				LemonUI.Menus.NativeCheckboxItem item = new LemonUI.Menus.NativeCheckboxItem("Show Checkpoint Hitboxes", race.debugShowCheckpointHitbox);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.debugShowCheckpointHitbox = item.Checked;
				};
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeCheckboxItem item = new LemonUI.Menus.NativeCheckboxItem("Show Global XYZ Axes", race.debugShowXYZAxes);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.debugShowXYZAxes = item.Checked;
				};
				item.Description = "Red: +X (East), Green: +Y (North), Blue: +Z (Up)";
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeCheckboxItem item = new LemonUI.Menus.NativeCheckboxItem("Show Player XYZ Axes", race.debugShowPlayerXYZAxes);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.debugShowPlayerXYZAxes = item.Checked;
				};
				item.Description = "LightRed: Right, LightGreen: Forward, LightBlue: Up";
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeCheckboxItem item = new LemonUI.Menus.NativeCheckboxItem("Show Player Position", race.debugShowPlayerPosition);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.debugShowPlayerPosition = item.Checked;
				};
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeCheckboxItem item = new LemonUI.Menus.NativeCheckboxItem("Show Midair Acceleration", race.debugShowMidairAcceleration);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.debugShowMidairAcceleration = item.Checked;
				};
				item.Description = "Estimated direction of bonus midair acceleration, not current midair acceleration.";
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeCheckboxItem item = new LemonUI.Menus.NativeCheckboxItem("Show Debug Info", race.debugShowInfo);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.debugShowInfo = item.Checked;
				};
				item.Description = "Orange numbers are negative.";
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeCheckboxItem item = new LemonUI.Menus.NativeCheckboxItem("Toggle Player Opacity", race.debugPlayerIsOpaque);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.debugPlayerIsOpaque = item.Checked;
				};
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeSliderItem item = new LemonUI.Menus.NativeSliderItem("Player Opacity");
				item.Maximum = 255;
				item.Value = race.debugPlayerOpacityLevel;
				item.ValueChanged += (menu, sender) =>
				{
					race.debugPlayerOpacityLevel = (byte)item.Value;
					item.Description = item.Value.ToString();
					int idx = submenu.SelectedIndex;
					//submenu.RefreshIndex();
					submenu.SelectedIndex = idx;
				};
				submenu.Add(item);
            }
			{
				LemonUI.Menus.NativeSliderItem item = new LemonUI.Menus.NativeSliderItem("Vehicle Opacity");
				item.Maximum = 255;
				item.Value = race.debugVehicleOpacityLevel;
				item.ValueChanged += (menu, sender) =>
				{
					race.debugVehicleOpacityLevel = (byte)item.Value;
					item.Description = item.Value.ToString();
					int idx = submenu.SelectedIndex;
					//submenu.RefreshIndex();
					submenu.SelectedIndex = idx;
				};
				submenu.Add(item);
			}
			//submenu.RefreshIndex();
		}

		private void loadCameraSettingsMenu(LemonUI.Menus.NativeMenu submenu)
		{
			// clear the menu
			submenu.Clear();
			{
				LemonUI.Menus.NativeCheckboxItem item;
				item = new LemonUI.Menus.NativeCheckboxItem("Use Default Camera", race.useDefaultCamera);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.useDefaultCamera = item.Checked;
				};
				submenu.Add(item);
			}
			{
				LemonUI.Menus.NativeCheckboxItem item;
				item = new LemonUI.Menus.NativeCheckboxItem("GTA Online Style Checkpoints", race.gtaoStyleCheckpoints);
				item.CheckboxChanged += (menu, sender) =>
				{
					race.gtaoStyleCheckpoints = item.Checked;
				};
				submenu.Add(item);
			}
			//submenu.RefreshIndex();
		}

		private void loadLapTimeMenu(LemonUI.Menus.NativeMenu sender)
		{
			// clear the menu
			sender.Clear();

			// validate the race; if race is invalid
			if (!race.isValid)
				return;

			// get the last checkpoint in list of checkpoints
			SectorCheckpoint finalChkpt = race.finishCheckpoint;

			// iterate over each k-v in the final checkpoint's timing data
			var times = finalChkpt.timing.vehicleFastestTime.OrderBy(x => x.Value);
			foreach (KeyValuePair<string, int> entry in times)
			{
				sender.Add(new LemonUI.Menus.NativeItem(TimingData.msToReadable(entry.Value, false, true) + " - " + entry.Key));
			}

			//sender.RefreshIndex();
		}

		//private string subdir = null;
		private List<LemonUI.Menus.NativeMenu> racesubmenus = new List<LemonUI.Menus.NativeMenu>();
		private void buildRaceImportMenu(LemonUI.Menus.NativeMenu submenu, string subdir)
		{
			submenu.Clear();
			/**
			if (subdir != null)
			{
				LemonUI.Menus.NativeItem item = new LemonUI.Menus.NativeItem("Back <<<");
				item.Activated += (menu, sender) =>
				{
					subdir = null;
					buildRaceImportMenu(submenu);
				};
				submenu.Add(item);
			}
			**/
			//_menuPool.Remove

			foreach (string dir in RaceExporter.getSubdirectories(subdir))
            {
				char[] separators = { '/', '\\' };
				LemonUI.Menus.NativeMenu item = new LemonUI.Menus.NativeMenu("", dir.Substring(dir.LastIndexOfAny(separators) + 1));
				item.Shown += (menu, sender) =>
				{
					//subdir = dir;
					buildRaceImportMenu(item, dir);
				};
				submenusToAddToPool.Add(item);
				submenu.AddSubMenu(item);
            }

			// get a List all races that can be imported
			List<ImportableRace> races = RaceExporter.getImportableRaces(subdir);
			
			// iterate over each race & add to menu, along with their handlers
			foreach (ImportableRace r in races){
				string descriptionString = r.name + 
					"\nMode: " + (r.lapMode ? "circuit" : "point-to-point") + 
					"\nVersion: " + r.version ?? "v1.x";
				LemonUI.Menus.NativeItem item = new LemonUI.Menus.NativeItem(r.name, descriptionString);
				item.Activated += (menu, sender) =>
				{
					race.importRace(r.filePath);
					_menuPool.HideAll();
				};
				submenu.Add(item);
			}

			//submenu.RefreshIndex();
			return;
			//return submenu;
		}



		private void buildRaceControlMenu(LemonUI.Menus.NativeMenu submenu)
		{
			submenu.Clear();

			// add checkbox to toggle lap mode
			string lapModeDescription = "If checked, race is a circuit, and automatically restarts. If unchecked, race is point-to-point";
			LemonUI.Menus.NativeCheckboxItem lapModeItem = new LemonUI.Menus.NativeCheckboxItem("Lap Mode", lapModeDescription, race.lapRace);
			lapModeItem.CheckboxChanged += (sender, status) => race.lapRace = lapModeItem.Checked;
			submenu.Add(lapModeItem);

			// add button to place checkpoint
			LemonUI.Menus.NativeItem addCheckpointBtn = new LemonUI.Menus.NativeItem("Place checkpoint", "Place a checkpoint at the player's current location");
			addCheckpointBtn.Activated += (m, i) => race.createSectorCheckpoint();
			submenu.Add(addCheckpointBtn);

			// undo last placed checkpoint
			LemonUI.Menus.NativeItem undoCheckpointBtn = new LemonUI.Menus.NativeItem("Undo last checkpoint", "Remove the last checkpoint");
			undoCheckpointBtn.Activated += (m, i) => race.deleteLastSectorCheckpoint();
			submenu.Add(undoCheckpointBtn);

			// delete all checkpoints
			LemonUI.Menus.NativeItem deleteAllCheckpointsBtn = new LemonUI.Menus.NativeItem("Delete all checkpoints");
			deleteAllCheckpointsBtn.Activated += (m, i) => race.clearAllSectorCheckpoints();
			submenu.Add(deleteAllCheckpointsBtn);

			//submenu.RefreshIndex();
			//return submenu;
		}

		private void buildReplayImportMenu(LemonUI.Menus.NativeMenu submenu)
		{
			submenu.Clear();

			// get a List all races that can be imported
			List<ImportableReplay> replays = replayExporter.getImportableReplays();

			// iterate over each race & add to menu, along with their handlers
			foreach (ImportableReplay r in replays)
			{
				//string descriptionString = r.name +
				//	"\nMode: " + (r.lapMode ? "circuit" : "point-to-point") +
				//	"\nVersion: " + r.version ?? "v1.x";
				//UIMenuItem item = new UIMenuItem(r.name, descriptionString);
				string f = r.filePath;
				f = f.Substring(f.LastIndexOf('/')+1).Replace(".json.gz", "");
				LemonUI.Menus.NativeItem item = new LemonUI.Menus.NativeItem(f);
				item.Activated += (menu, sender) =>
				{
					race.importReplay(r.filePath);
					_menuPool.HideAll();
				};
				Regex rgx = new Regex("[0-9]{10,14}", RegexOptions.RightToLeft);
				Match m = rgx.Match(f);
				if (m.Success) {
					DateTimeOffset date;
					if (m.Length < 13)
						date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(m.Value));
					else
						date = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(m.Value));
					date = date.ToLocalTime();
					item.Description = date.ToString();
				}
				submenu.Add(item);
			}

			//submenu.RefreshIndex();
			return;
			//return submenu;
		}

		#endregion

	}




}
