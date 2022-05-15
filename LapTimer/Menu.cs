using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GTA;
using NativeUI;


namespace LapTimer
{
	class NativeUIMenu
	{
		RaceControl race;

		public MenuPool _menuPool;
		public UIMenu mainMenu;

		public NativeUIMenu(ref RaceControl raceControl)
		{
			race = raceControl;

			// create new menu pool & add the main menu to it
			_menuPool = new MenuPool();
			_menuPool.Add(buildMainMenu());
		}


		#region publicMethods

		/// <summary>
		/// Toggle visibility of menus. If no menu is currently open, open main menu.
		/// </summary>
		/// <returns>boolean indicating whether a menu is now open</returns>
		public bool toggleMenu()
		{
			if (_menuPool.IsAnyMenuOpen())
			{
				_menuPool.CloseAllMenus();
				return false;
			}
			else
			{
				mainMenu.Visible = true;
				return true;
			}
		}

		#endregion



		#region menus

		private UIMenu buildMainMenu()
		{
			mainMenu = new UIMenu("Race Timer", "~b~by iLike2Teabag");

			// add a submenu to handle race imports
			UIMenu raceImportMenu = _menuPool.AddSubMenu(mainMenu, "Race Import Menu", "Choose races to import from file");
			raceImportMenu.OnMenuOpen += buildRaceImportMenu;
			buildRaceImportMenu(raceImportMenu);

			// add a submenu for race control
			UIMenu raceControlMenu = _menuPool.AddSubMenu(mainMenu, "Race Control Menu", "Modify checkpoints and race mode");
			raceControlMenu.OnMenuOpen += buildRaceControlMenu;
			//buildRaceControlMenu(raceControlMenu);

			// add a submenu to handle race imports
			UIMenu replayImportMenu = _menuPool.AddSubMenu(mainMenu, "Replay Import Menu", "Choose replays to import from file");
			replayImportMenu.OnMenuOpen += buildReplayImportMenu;
			buildReplayImportMenu(replayImportMenu);

			// add a submenu for settings
			UIMenu settingsMenu = _menuPool.AddSubMenu(mainMenu, "Settings Menu");
			settingsMenu.OnMenuOpen += loadSettingsMenu;

			// add a submenu for Timing Sheet
			UIMenu lapTimeMenu = _menuPool.AddSubMenu(mainMenu, "Lap Times", "Display lap times for the current race");
			lapTimeMenu.OnMenuOpen += loadLapTimeMenu;

			// add controls to enter placement, race, debug modes
			UIMenuItem placementToggle = new UIMenuItem("Toggle Placement Mode");
			UIMenuItem raceToggle = new UIMenuItem("Toggle Race Mode");
			placementToggle.Activated += (menu, sender) =>
			{
				race.togglePlacementMode();
			};
			raceToggle.Activated += (menu, sender) => {
				race.toggleRaceMode();
			};
			mainMenu.AddItem(placementToggle);
			mainMenu.AddItem(raceToggle);

			

			/**
			// add a submenu for debug settings
			UIMenu debugSettingsMenu = _menuPool.AddSubMenu(mainMenu, "Debug Mode Settings");
			debugSettingsMenu.OnMenuOpen += loadDebugSettingsMenu;

			// add a submenu for camera settings
			UIMenu cameraSettingsMenu = _menuPool.AddSubMenu(mainMenu, "Camera Settings");
			cameraSettingsMenu.OnMenuOpen += loadCameraSettingsMenu;
			**/

			// add control to export race
			UIMenuItem exportRaceItem = new UIMenuItem("Export Race");
			exportRaceItem.Activated += (menu, sender) => race.exportRace();
			mainMenu.AddItem(exportRaceItem);

			UIMenuItem exportReplayItem = new UIMenuItem("Export Replay");
			exportReplayItem.Activated += (menu, sender) => race.exportReplay(askname: true);
			mainMenu.AddItem(exportReplayItem);

			mainMenu.RefreshIndex();
			return mainMenu;
		}
		
		private void loadSettingsMenu(UIMenu submenu)
        {
			submenu.Clear();

			UIMenuCheckboxItem debugToggle = new UIMenuCheckboxItem("Toggle Debug Mode", race.debugMode);
			UIMenuCheckboxItem ghostToggle = new UIMenuCheckboxItem("Toggle Ghost Mode", race.tasPlaybackGhostMode);
			debugToggle.CheckboxEvent += (menu, sender) => {
				race.toggleDebugMode();
				debugToggle.Checked = race.debugMode;
			};
			ghostToggle.CheckboxEvent += (menu, sender) => race.tasPlaybackGhostMode = ghostToggle.Checked;
			submenu.AddItem(debugToggle);
			submenu.AddItem(ghostToggle);

			// add a submenu for debug settings
			UIMenuItem debugSettingsMenu = new UIMenuItem("Debug Mode Settings >>>");
			debugSettingsMenu.Activated += (menu, sender) => { 
				loadDebugSettingsMenu(submenu); 
			};
			submenu.AddItem(debugSettingsMenu);

			// add a submenu for camera settings
			UIMenuItem cameraSettingsMenu = new UIMenuItem("Race Settings >>>");
			cameraSettingsMenu.Activated += (menu, sender) => {
				loadCameraSettingsMenu(submenu);
			};
			submenu.AddItem(cameraSettingsMenu);

			submenu.RefreshIndex();
		}
		private void loadDebugSettingsMenu(UIMenu submenu)
        {
			// clear the menu
			submenu.Clear();
			{
				UIMenuCheckboxItem item = new UIMenuCheckboxItem("Show Checkpoint Hitboxes", race.debugShowCheckpointHitbox);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.debugShowCheckpointHitbox = item.Checked;
				};
				submenu.AddItem(item);
			}
			{
				UIMenuCheckboxItem item = new UIMenuCheckboxItem("Show Global XYZ Axes", race.debugShowXYZAxes);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.debugShowXYZAxes = item.Checked;
				};
				item.Description = "Red: +X (East), Green: +Y (North), Blue: +Z (Up)";
				submenu.AddItem(item);
			}
			{
				UIMenuCheckboxItem item = new UIMenuCheckboxItem("Show Player XYZ Axes", race.debugShowPlayerXYZAxes);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.debugShowPlayerXYZAxes = item.Checked;
				};
				item.Description = "LightRed: Right, LightGreen: Forward, LightBlue: Up";
				submenu.AddItem(item);
			}
			{
				UIMenuCheckboxItem item = new UIMenuCheckboxItem("Show Player Position", race.debugShowPlayerPosition);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.debugShowPlayerPosition = item.Checked;
				};
				submenu.AddItem(item);
			}
			{
				UIMenuCheckboxItem item = new UIMenuCheckboxItem("Show Midair Acceleration", race.debugShowMidairAcceleration);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.debugShowMidairAcceleration = item.Checked;
				};
				item.Description = "Estimated direction of bonus midair acceleration, not current midair acceleration.";
				submenu.AddItem(item);
			}
			{
				UIMenuCheckboxItem item = new UIMenuCheckboxItem("Show Debug Info", race.debugShowInfo);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.debugShowInfo = item.Checked;
				};
				item.Description = "Orange numbers are negative.";
				submenu.AddItem(item);
			}
			{
				UIMenuCheckboxItem item = new UIMenuCheckboxItem("Toggle Player Opacity", race.debugPlayerIsOpaque);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.debugPlayerIsOpaque = item.Checked;
				};
				submenu.AddItem(item);
			}
			{
				UIMenuSliderItem item = new UIMenuSliderItem("Player Opacity");
				item.Maximum = 255;
				item.Value = race.debugPlayerOpacityLevel;
				item.OnSliderChanged += (menu, sender) =>
				{
					race.debugPlayerOpacityLevel = (byte)item.Value;
					item.Description = item.Value.ToString();
					int idx = submenu.CurrentSelection;
					submenu.RefreshIndex();
					submenu.CurrentSelection = idx;
				};
				submenu.AddItem(item);
            }
			{
				UIMenuSliderItem item = new UIMenuSliderItem("Vehicle Opacity");
				item.Maximum = 255;
				item.Value = race.debugVehicleOpacityLevel;
				item.OnSliderChanged += (menu, sender) =>
				{
					race.debugVehicleOpacityLevel = (byte)item.Value;
					item.Description = item.Value.ToString();
					int idx = submenu.CurrentSelection;
					submenu.RefreshIndex();
					submenu.CurrentSelection = idx;
				};
				submenu.AddItem(item);
			}
			submenu.RefreshIndex();
		}

		private void loadCameraSettingsMenu(UIMenu submenu)
		{
			// clear the menu
			submenu.Clear();
			{
				UIMenuCheckboxItem item;
				item = new UIMenuCheckboxItem("Use Default Camera", race.useDefaultCamera);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.useDefaultCamera = item.Checked;
				};
				submenu.AddItem(item);
			}
			{
				UIMenuCheckboxItem item;
				item = new UIMenuCheckboxItem("GTA Online Style Checkpoints", race.gtaoStyleCheckpoints);
				item.CheckboxEvent += (menu, sender) =>
				{
					race.gtaoStyleCheckpoints = item.Checked;
				};
				submenu.AddItem(item);
			}
			submenu.RefreshIndex();
		}

		private void loadLapTimeMenu(UIMenu sender)
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
				sender.AddItem(new UIMenuItem(TimingData.msToReadable(entry.Value, false, true) + " - " + entry.Key));
			}

			sender.RefreshIndex();
		}

		private string subdir = null;
		private void buildRaceImportMenu(UIMenu submenu)
		{
			submenu.Clear();
			if (subdir != null)
			{
				UIMenuItem item = new UIMenuItem("Back <<<");
				item.Activated += (menu, sender) =>
				{
					subdir = null;
					buildRaceImportMenu(submenu);
				};
				submenu.AddItem(item);
			}

			foreach (string dir in RaceExporter.getSubdirectories(subdir))
            {
				char[] separators = { '/', '\\' };
				UIMenuItem item = new UIMenuItem(dir.Substring(dir.LastIndexOfAny(separators) + 1) + " >>>");
				item.Activated += (menu, sender) =>
				{
					subdir = dir;
					buildRaceImportMenu(submenu);
				};
				submenu.AddItem(item);
            }

			// get a List all races that can be imported
			List<ImportableRace> races = RaceExporter.getImportableRaces(subdir);
			
			// iterate over each race & add to menu, along with their handlers
			foreach (ImportableRace r in races){
				string descriptionString = r.name + 
					"\nMode: " + (r.lapMode ? "circuit" : "point-to-point") + 
					"\nVersion: " + r.version ?? "v1.x";
				UIMenuItem item = new UIMenuItem(r.name, descriptionString);
				item.Activated += (menu, sender) =>
				{
					race.importRace(r.filePath);
					_menuPool.CloseAllMenus();
				};
				submenu.AddItem(item);
			}

			submenu.RefreshIndex();
			return;
			//return submenu;
		}



		private void buildRaceControlMenu(UIMenu submenu)
		{
			submenu.Clear();

			// add checkbox to toggle lap mode
			string lapModeDescription = "If checked, race is a circuit, and automatically restarts. If unchecked, race is point-to-point";
			UIMenuCheckboxItem lapModeItem = new UIMenuCheckboxItem("Lap Mode", race.lapRace, lapModeDescription);
			lapModeItem.CheckboxEvent += (sender, status) => race.lapRace = status;
			submenu.AddItem(lapModeItem);

			// add button to place checkpoint
			UIMenuItem addCheckpointBtn = new UIMenuItem("Place checkpoint", "Place a checkpoint at the player's current location");
			addCheckpointBtn.Activated += (m, i) => race.createSectorCheckpoint();
			submenu.AddItem(addCheckpointBtn);

			// undo last placed checkpoint
			UIMenuItem undoCheckpointBtn = new UIMenuItem("Undo last checkpoint", "Remove the last checkpoint");
			undoCheckpointBtn.Activated += (m, i) => race.deleteLastSectorCheckpoint();
			submenu.AddItem(undoCheckpointBtn);

			// delete all checkpoints
			UIMenuItem deleteAllCheckpointsBtn = new UIMenuItem("Delete all checkpoints");
			deleteAllCheckpointsBtn.Activated += (m, i) => race.clearAllSectorCheckpoints();
			submenu.AddItem(deleteAllCheckpointsBtn);

			submenu.RefreshIndex();
			//return submenu;
		}

		private void buildReplayImportMenu(UIMenu submenu)
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
				UIMenuItem item = new UIMenuItem(f);
				item.Activated += (menu, sender) =>
				{
					race.importReplay(r.filePath);
					_menuPool.CloseAllMenus();
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
				submenu.AddItem(item);
			}

			submenu.RefreshIndex();
			return;
			//return submenu;
		}

		#endregion

	}




}
