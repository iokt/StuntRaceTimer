// LapTimer 1.0 - Abel Software
// You must download and use Scripthook V Dot Net Reference (LINKS AT BOTTOM OF THE TEMPLATE)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Specialized;
using GTA;
using GTA.Native;
using GTA.Math;


namespace LapTimer
{
	public class RaceTimerMain : Script
	{
		#region metadata
		bool firstTime = true;
		string ModName = "Lap Timer";
		string Developer = "iLike2Teabag";
		#endregion


		#region main
		bool isDamaged = false;
		int repairTicker = 0;
		public RaceTimerMain()
		{
			Tick += onTick;
			Tick += (o, e) => menu.processMenus(); // _menuPool.Process();
			Interval = 1;
			Aborted += OnShutdown;
		}


		private void onTick(object sender, EventArgs e)
		{
			/*
			LemonUI.Elements.ScaledText text = new LemonUI.Elements.ScaledText(new PointF(1000, 500), "testing", 1f, GTA.UI.Font.Monospace);
			text.Alignment = GTA.UI.Alignment.Left;
			text.Outline = true;
			text.Draw();
			*/

			if (firstTime) // if this is the users first time loading the mod, this information will appear
			{
				GTA.UI.Screen.ShowSubtitle(ModName + " by " + Developer + " Loaded");
				firstTime = false;

				// setup tasks
				race = new RaceControl();
				readSettings(base.Settings);
				KeyDown += onKeyDown;
				try
				{
					menu = new NativeUIMenu(ref race);
				}
				catch
				{
					GTA.UI.Notification.Show("Lap Timer: ~r~Failed to initialize menu. Make sure you have NativeUI.dll");
				}
			}


			// race mode checkpoint detection
			if (race.raceMode)
			{
				//ORDER IS IMPORTANT: don't re-record last frame of playback
				if (race.tasRecordMode) race.tasRecord();
				if (race.tasPlaybackMode) race.tasPlayback();

				if (race.mustLoadReplay)
					race.loadReplay();

				race.timescaleControl();
				race.removeProps(wait: 250);
				race.removeVehicles(wait: 251);
				race.removePeds(wait: 252);
				race.betterCamera();
				race.preventCharSwitch();
				race.stopCarWait();
				race.preventVehicleExit();
				race.preventTrain();
				//race.checkBikeCollisions();
				race.resetHealth();
				race.activeCheckpointDetection();
				if (race.debugMode)
					race.drawDebug();
				race.updatePools();
				race.pool.Process();
				if (race.waitForCountdown)
                {
					race.countdown();
                }
				
				
			}
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (veh != null)
            {
				/**
				//autorepair 240 frames after damage
				const int repairWait = 240;
				if (!isDamaged) isDamaged = veh.IsDamaged && (veh.IsInvincible || veh.HealthFloat < 980f);
				else if (!veh.IsOnAllWheels) repairTicker = (repairTicker > repairWait - 60) ? repairWait - 60 : repairTicker; //avoid killing momentum in midair
				else if (repairTicker > repairWait)
				{
					veh.Repair();
					isDamaged = false;
					repairTicker = 0;
				}
				else repairTicker++;
				//Game.Player.Character.is
				**/
				if (race.raceMode && veh.IsConsideredDestroyed)
				{
					race.respawn();
					//veh.Repair();
				}
			}
			
		}
		#endregion


		// ------------- PROPERTIES/VARIABLES -----------------
		#region properties
		// references
		NativeUIMenu menu;
		RaceControl race;

		// hotkeys
		Keys menuKey;
		Keys placementActivateKey, addCheckpointKey, undoCheckpointKey, clearCheckpointsKey;
		Keys raceActivateKey, restartRaceKey, respawnKey, stopCarKey, tasPlaybackKey, tasRecordKey;
		#endregion


		// ------------- EVENT LISTENERS/HANDLERS -----------------
		#region eventHandlers
		private void onKeyDown(object sender, KeyEventArgs e)
		{
			// open menu
			if (e.Modifiers == Keys.Control && e.KeyCode == menuKey)
				menu.toggleMenu();

			// enter/exit placement mode with F5
			else if (e.KeyCode == placementActivateKey)
				race.togglePlacementMode();

			// if placement mode is enabled, and the control key was used:
			else if (race.placementMode && e.Modifiers == Keys.Control)
			{
				// Ctrl+X: add a checkpoint
				if (e.KeyCode == addCheckpointKey)
					race.createSectorCheckpoint();

				// Ctrl+Z: delete (undo) last SectorCheckpoint
				else if (e.KeyCode == undoCheckpointKey)
					race.deleteLastSectorCheckpoint();

				// Ctrl+D: clear all SectorCheckpoints, and delete any blips & checkpoints from World
				else if (e.KeyCode == clearCheckpointsKey)
					race.clearAllSectorCheckpoints();
			}

			// enter/exit race mode with F6
			else if (e.KeyCode == raceActivateKey)
				race.toggleRaceMode();

			// if race mode is enabled, and the control key was used:
			else if (race.raceMode && e.Modifiers == (Keys.Control|Keys.Shift))
			{
				// Ctrl+Shift+R: restart race
				if (e.KeyCode == restartRaceKey)
					race.enterRaceMode();
			}
			else if (race.raceMode && e.Modifiers == Keys.Control)
            {
				// Ctrl+R: respawn
				if (e.KeyCode == respawnKey)
					race.respawn();
				// Ctrl+Z: stop car
				else if (e.KeyCode == stopCarKey)
                {
					race.stopCar();
                }
				// Ctrl+O: TAS playback
				else if (e.KeyCode == tasPlaybackKey)
                {
					race.tasPlaybackToggle();
                }
				// Ctrl+I: TAS record toggle
				else if (e.KeyCode == tasRecordKey)
				{
					race.tasRecordToggle();
				}
			}
		}



		/// <summary>
		/// Script destructor. Clean up any objects created to prevent memory leaks.
		/// </summary>
		private void OnShutdown(object sender, EventArgs e)
		{
			race.clearAllSectorCheckpoints();
			race.exitRaceMode();
		}
		
		#endregion

		

		// ------------- HELPER METHODS -----------------
		#region helpers
		

		/// <summary>
		/// Read in INI key settings. Includes default settings if INI read fails.
		/// </summary>
		private void readSettings(ScriptSettings ss)
		{
			// read & parse placement mode settings
			string section = "Placement";
			placementActivateKey = ss.GetValue<Keys>(section, "activate", Keys.F5);
			addCheckpointKey = ss.GetValue<Keys>(section, "addCheckpoint", Keys.X);
			undoCheckpointKey = ss.GetValue<Keys>(section, "undoCheckpoint", Keys.Z);
			clearCheckpointsKey = ss.GetValue<Keys>(section, "clearCheckpoints", Keys.D);

			// read race mode settings
			section = "Race";
			raceActivateKey = ss.GetValue<Keys>(section, "activate", Keys.F6);
			restartRaceKey = ss.GetValue<Keys>(section, "restartRace", Keys.R);
			respawnKey = ss.GetValue<Keys>(section, "respawn", Keys.R);
			stopCarKey = ss.GetValue<Keys>(section, "stopCar", Keys.Z);
			tasPlaybackKey = ss.GetValue<Keys>(section, "tasPlayback", Keys.O);
			tasRecordKey = ss.GetValue<Keys>(section, "tasRecord", Keys.I);
			race.freezeTime = ss.GetValue<int>(section, "freezeTime", 750);
			race.showSpeedTrap = ss.GetValue<bool>(section, "showSpeedTrap", false);
			race.displaySpeedInKmh = ss.GetValue<bool>(section, "useMetric", true);
			race.enterRaceModeOnLoad = ss.GetValue<bool>(section, "enterRaceModeOnLoad", false);
			race.gtaoStyleCheckpoints = ss.GetValue<bool>(section, "gtaoStyleCheckpoints", false);

			// read Script hotkeys
			section = "Script";
			menuKey = ss.GetValue<Keys>(section, "menu", Keys.N);

			// read tas mode settings
			section = "TAS";
			race.tasSaveOnOverwrite = ss.GetValue<bool>(section, "saveOnOverwrite", false);
			race.tasSaveOnStopRecording = ss.GetValue<bool>(section, "saveOnStopRecording", true);

			// read debug mode settings
			section = "Debug";
			race.debugMode = ss.GetValue<bool>(section, "on", false);
			race.debugPlayerIsOpaque = ss.GetValue<bool>(section, "playerIsOpaque", false);
			race.debugShowCheckpointHitbox = ss.GetValue<bool>(section, "showCheckpointHitbox", true);
			race.debugShowInfo = ss.GetValue<bool>(section, "showInfo", true);
			race.debugShowMidairAcceleration = ss.GetValue<bool>(section, "showMidairAcceleration", false);
			race.debugShowPlayerPosition = ss.GetValue<bool>(section, "showPlayerPosition", true);
			race.debugShowPlayerXYZAxes = ss.GetValue<bool>(section, "showPlayerXYZAxes", false);
			race.debugShowXYZAxes = ss.GetValue<bool>(section, "showWorldXYZAxes", false);
			race.debugPlayerOpacityLevel = ss.GetValue<byte>(section, "playerOpacityLevel", 100);
			race.debugVehicleOpacityLevel = ss.GetValue<byte>(section, "vehicleOpacityLevel", 100);

			// read camera settings
			section = "Camera";
			race.useDefaultCamera = ss.GetValue<bool>(section, "useDefaultCamera", false);

		}

		#endregion
	}

}

// Useful Links
// All Vehicles - https://pastebin.com/uTxZnhaN
// All Player Models - https://pastebin.com/i5c1zA0W
// All Weapons - https://pastebin.com/M3kD9pnJ
// GTA V ScriptHook V Dot Net - https://www.gta5-mods.com/tools/scripthookv-net