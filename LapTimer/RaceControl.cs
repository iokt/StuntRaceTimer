extern alias SHVDN2;
//extern alias CFX;
//using static CitizenFX.Core.Native.API;


using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;

using GTA;
using GTA.Native;
using GTA.Math;
using GTA.UI;




using System.Drawing;

namespace LapTimer
{
	class RaceControl
	{
		#region properties

		public string raceName = "unnamed";

		// flags
		public bool placementMode = false;
		public bool raceMode = false;
		public bool lapRace = false;                   // if true, the 1st SectorCheckpoint will be used as the end of a lap

		// constants
		const float checkpointMargin = 1.0f;    // checkpoint's margin multiplier; a checkpoint should be considered reached if player position is within radius * margin from the center of the checkpoint
												//readonly Vector3 checkpointOffset = new Vector3(0.0f, 0.0f, -1.0f);	// modify the standard checkpoint's position by this offset when drawing; cosmetic only!
		const int wheelMemSize = 560; //bytes
		const int intSize = 4;

		// placement mode variables
		public List<SectorCheckpoint> markedSectorCheckpoints = new List<SectorCheckpoint>();     // add to this list when player marks a position; to be used like a stack (i.e. can only delete/pop latest element!)

		// race mode variables
		public SectorCheckpoint activeCheckpoint;       // track the active sector checkpoint
		public bool showSpeedTrap;                      // whether to display speed when checkpoints are crossed
		public bool displaySpeedInKmh;                  // display speed in KM/h; displays in MPH otherwise
		public int freezeTime;                          // time in milliseconds to freeze player's car after race starts. Timer will not run
		public int activeSector;                        // track the active sector number
		public int lapStartTime;
		public int raceStartTime;

		private bool lastHitSecondary = false;
		private bool ignoreActivation = false;
		private const int defaultLapStartTime = -100 * 60 * 60 * 1000;
		public int lapTime
		{
			get
			{
				return Game.GameTime - lapStartTime;
			}
			set
			{
				lapStartTime = Game.GameTime - value;
			}
		}
		public int totalTime
		{
			get
			{
				return Game.GameTime - raceStartTime;
			}
			set
			{
				raceStartTime = Game.GameTime - value;
			}
		}
		public int pbTime
		{
			get
			{
				int ft = markedSectorCheckpoints.Last().getSimplifiedTimingData().fastestTime;
				return ft < -defaultLapStartTime ? ft : -1;
			}
		}
		public Weather weather;
		public Vector3 spawn;

		public NativeUI.TimerBarPool pool = new NativeUI.TimerBarPool();
		private NativeUI.TextTimerBar lapTimerBar;
		private NativeUI.TextTimerBar pbTimerBar;
		private NativeUI.TextTimerBar totalTimerBar;

		#endregion


		#region topLevel

		/// <summary>
		/// Enter/exit "placement mode", which allows user to mark positions as checkpoints in a lap. Can only be entered if raceMode=false
		/// </summary>ef
		public void togglePlacementMode()
		{
			if (raceMode)
			{
				GTA.UI.Screen.ShowSubtitle("~r~Lap Timer: Cannot enter placement mode while race mode is active.");
				return;
			}

			// if entering placement mode
			if (!placementMode)
			{
				placementMode = true;
				redrawAllSectorCheckpoints();           // if any markedSectorPoints already exist, redraw their blips & checkpoints
				GTA.UI.Screen.ShowSubtitle("Lap Timer: Entering placement mode. Mark sector checkpoints using Ctrl+X.");
			}

			// if exiting placement mode
			else
			{
				hideAllSectorCheckpoints();             // hide blips and checkpoints, but keep the metadata of SectorCheckpoints
				placementMode = false;
				GTA.UI.Screen.ShowSubtitle("Lap Timer: Exiting placement mode.");
			}
		}



		/// <summary>
		/// Enter/exit "race mode", which puts the player into the race they created in placement mode, if saved SectorCheckpoints valid.
		/// </summary>
		/// <returns><c>true</c> if race mode activated successfully; <c>false</c> if deactivated.</returns>
		public bool toggleRaceMode()
		{
			// if currently in race mode, try to leave race mode
			if (raceMode)
			{
				exitRaceMode();
				raceMode = false;
				return false;
			}

			// try to enter race mode
			else
			{
				// if currently in placement mode, attempt to exit it first.
				if (placementMode)
				{
					togglePlacementMode();
					// TODO: check that placement mode was exited successfully
				}

				// check that the player can enter race mode
				if (canEnterRaceMode())
				{
					enterRaceMode();
					raceMode = true;
				}
				else
					GTA.UI.Screen.ShowSubtitle("~r~Lap Timer: cannot enter Race Mode.");
			}

			return raceMode;            // return the updated status of the race mode flag
		}



		/// <summary>
		/// Mark player's current position, and create a blip & checkpoint.
		/// </summary>
		/// <returns>Instance of </returns>
		public SectorCheckpoint createSectorCheckpoint(bool verbose = true)
		{
			// instantiate empty SectorCheckpoint
			int checkpointNum = markedSectorCheckpoints.Count;
			SectorCheckpoint newCheckpoint = new SectorCheckpoint(checkpointNum);
			markedSectorCheckpoints.Add(newCheckpoint);

			return newCheckpoint;
		}



		/// <summary>
		/// Race mode only: detect whether the player is within <c>maxDistance</c> of the active checkpoint. Activate next checkpoint and return sector time, if within range.
		/// </summary>
		/// <param name="maxDistance">Maximum distance in meters to trigger checkpoint.</param>
		/// <param name="force3D">Distance computation defaults to 2D (x-y plane only) unless this flag is true</param>
		/// <returns></returns>
		public int activeCheckpointDetection(float margin = checkpointMargin, bool force3D = true)
		{
			// get data on player's current vehicle
			Vehicle veh = Game.Player.Character.CurrentVehicle;

			// if player is not currently in a vehicle, display message and exit race mode
			if (veh == null) {
				Vehicle temp = World.GetClosestVehicle(Game.Player.Character.Position, float.MaxValue);
				if (temp != null)
				{
					Game.Player.Character.SetIntoVehicle(temp, VehicleSeat.Driver);
					respawn();
				}
				veh = Game.Player.Character.CurrentVehicle;
				if (veh == null)
				{
					GTA.UI.Screen.ShowSubtitle("Lap Timer: exited vehicle; leaving Race Mode.");
					exitRaceMode();
					return int.MaxValue;
				}
				else
					respawn();
			}

			if (ignoreActivation) return 0;

			float zAdd = activeCheckpoint.isAirCheckpoint ? 0f : 0f;
			// get player's position and compute distance to the position of the active checkpoint
			float dist;
			if (force3D) dist = Game.Player.Character.Position.DistanceTo(activeCheckpoint.position + new Vector3(0f, 0f, zAdd));
			else dist = Game.Player.Character.Position.DistanceTo2D(activeCheckpoint.position);

			float radius = activeCheckpoint.isAirCheckpoint ? SectorCheckpoint.checkpointAirRadius : SectorCheckpoint.checkpointRadius;

			bool inRange = (dist < margin * radius * activeCheckpoint.chs);
			//const float zFactor = 1/3f; //Guessing

			float zDiff = Game.Player.Character.Position.Z - activeCheckpoint.position.Z;
			bool inRangeZ = true;
			//inRangeZ = Math.Abs(zDiff) < radius * zFactor * activeCheckpoint.chs ; 
			//inRangeZ |= zDiff >= 0f && zDiff < radius * 1f * activeCheckpoint.chs;
			inRangeZ = zDiff > SectorCheckpoint.zClamp; //idfk
			bool inRange2 = false;
			bool inRangeZ2 = false;
			if (activeCheckpoint.hasSecondaryCheckpoint)
			{
				float zAdd2 = activeCheckpoint.isAirCheckpoint2 ? 0f : 0f;
				float radius2 = activeCheckpoint.isAirCheckpoint2 ? SectorCheckpoint.checkpointAirRadius : SectorCheckpoint.checkpointRadius;
				float dist2;
				if (force3D) dist2 = Game.Player.Character.Position.DistanceTo(activeCheckpoint.position2 + new Vector3(0f, 0f, zAdd2));
				else dist2 = Game.Player.Character.Position.DistanceTo2D(activeCheckpoint.position2);

				float zDiff2 = Game.Player.Character.Position.Z - activeCheckpoint.position2.Z;

				inRange2 = (dist2 < margin * radius2 * activeCheckpoint.chs2);
				//inRangeZ2 = Math.Abs(zDiff2) < radius2 * zFactor * activeCheckpoint.chs2; 
				//inRangeZ2 |= zDiff2 >= 0f && zDiff2 < radius2 * 1f * activeCheckpoint.chs2;
				inRangeZ2 = zDiff2 > SectorCheckpoint.zClamp;
			}
			inRange &= inRangeZ;
			inRange2 &= inRangeZ2;
			if (inRange) lastHitSecondary = false;
			else if (inRange2) lastHitSecondary = true;
			try {

				// check if it is within the specified maximum (margin * checkpointRadius)
				if (inRange || inRange2)
				{
					// compute time elapsed since race start
					int elapsedTime = Game.GameTime - lapStartTime;
					float vehSpeed = veh.Speed;

					// save and display elapsed
					TimeType tType = activeCheckpoint.timing.updateTiming(elapsedTime, veh.DisplayName);
					string notifString = string.Format("Checkpoint {0}: ~n~{1}", activeSector, activeCheckpoint.timing.getLatestTimingSummaryString());
					if (showSpeedTrap)
						notifString += String.Format("~n~~s~Speed Trap: {0} km/h", displaySpeedInKmh ? vehSpeed * 3.6f : vehSpeed * 2.23694f);
					GTA.UI.Notification.Show(notifString);

					// detect if the checkpoint reached is the final checkpoint
					if (activeCheckpoint.GetHashCode() == finishCheckpoint.GetHashCode())
					{
						ignoreActivation = true; //fix? for multiple activations and notification spam during lagspike
						if (raceStartTime < 0) raceStartTime = Game.GameTime;
						lapFinishedHandler(activeCheckpoint, lapRace);
						ignoreActivation = false;
					}

					// activate next checkpoint if race mode is still active
					if (raceMode)
					{
						bool warp = Convert.ToBoolean(activeCheckpoint.cpbs1 & (1 << 27));
						activateRaceCheckpoint(activeSector + 1);
						if (warp)
                        {
							warpToCheckpoint(activeSector);
                        }
							
					}
				}
			}
			catch (Exception e)
			{
				GTA.UI.Notification.Show("Lap Timer Exception: " + e.StackTrace.ToString());
			}

			return 0;
		}

		public void toggleDebugMode()
		{
			if (debugMode)
			{
				Function.Call(Hash.RESET_ENTITY_ALPHA, Game.Player.Character.Handle);
				Vehicle veh = Game.Player.Character.CurrentVehicle;
				if (veh != null)
					Function.Call(Hash.RESET_ENTITY_ALPHA, veh.Handle);
			}
			debugMode = !debugMode;
		}
		public bool debugMode = false;
		public void drawDebug()
		{
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (veh == null) return;

			veh.Opacity = 50;
			Game.Player.Character.Opacity = 50;
			Color cyan = Color.FromArgb(100, 0, 255, 255);
			float markerheight = 10f;
			float mr = .035f;
			float zc = SectorCheckpoint.zClamp;
			//Color.

			float radius = activeCheckpoint.isAirCheckpoint ? SectorCheckpoint.checkpointAirRadius : SectorCheckpoint.checkpointRadius;
			radius *= activeCheckpoint.chs;
			GTA.World.DrawMarker(GTA.MarkerType.DebugSphere, activeCheckpoint.position, Vector3.Zero, Vector3.Zero, new Vector3(radius, radius, radius), cyan);
			float zcr = 2f * (float)Math.Sqrt(radius * radius - zc * zc);
			GTA.World.DrawMarker(GTA.MarkerType.VerticalCylinder, activeCheckpoint.position + Vector3.UnitZ * zc, Vector3.Zero, Vector3.Zero, new Vector3(zcr, zcr, 1f), Color.Red);

			Vector3 dir = (veh.Position - activeCheckpoint.position).Normalized * Math.Min(radius, veh.Position.DistanceTo(activeCheckpoint.position));
			if (dir.Z < zc && zc < 0f)
			{
				dir.Z = zc;
				//dir *= zc / dir.Z;
			}
			Vector3 limitpos = activeCheckpoint.position + dir;
			GTA.World.DrawMarker(GTA.MarkerType.DebugSphere, limitpos, Vector3.Zero, Vector3.Zero, new Vector3(mr, mr, mr), Color.HotPink);
			GTA.World.DrawMarker(GTA.MarkerType.DebugSphere, limitpos, Vector3.Zero, Vector3.Zero, new Vector3(1f, 1f, 1f), Color.FromArgb(100, 255, 200, 255));

			if (activeCheckpoint.hasSecondaryCheckpoint)
			{
				float radius2 = activeCheckpoint.isAirCheckpoint2 ? SectorCheckpoint.checkpointAirRadius : SectorCheckpoint.checkpointRadius;
				radius2 *= activeCheckpoint.chs2;
				GTA.World.DrawMarker(GTA.MarkerType.DebugSphere, activeCheckpoint.position2, Vector3.Zero, Vector3.Zero, new Vector3(radius2, radius2, radius2), cyan);
				float zcr2 = 2f * (float)Math.Sqrt(radius2 * radius2 - zc * zc);
				GTA.World.DrawMarker(GTA.MarkerType.VerticalCylinder, activeCheckpoint.position2 + Vector3.UnitZ * zc, Vector3.Zero, Vector3.Zero, new Vector3(zcr2, zcr2, 1f), Color.Red);

				Vector3 dir2 = (veh.Position - activeCheckpoint.position2).Normalized * Math.Min(radius2, veh.Position.DistanceTo(activeCheckpoint.position2));
				if (dir2.Z < zc && zc < 0f)
				{
					dir2.Z = zc;
					//dir *= zc / dir.Z;
				}
				Vector3 limitpos2 = activeCheckpoint.position2 + dir2;
				GTA.World.DrawMarker(GTA.MarkerType.DebugSphere, limitpos2, Vector3.Zero, Vector3.Zero, new Vector3(mr, mr, mr), Color.HotPink);
				GTA.World.DrawMarker(GTA.MarkerType.DebugSphere, limitpos2, Vector3.Zero, Vector3.Zero, new Vector3(1f, 1f, 1f), Color.FromArgb(100, 255, 200, 255));
			}

			
			GTA.World.DrawMarker(GTA.MarkerType.VerticalCylinder, veh.Position - Vector3.UnitZ * markerheight / 2f, Vector3.Zero, 
				Vector3.Zero, new Vector3(mr, mr, markerheight), Color.Blue);
			GTA.World.DrawMarker(GTA.MarkerType.VerticalCylinder, veh.Position - Vector3.UnitY * markerheight / 2f, Vector3.Zero, 
				new Vector3(-90f, 0f, 0f), new Vector3(mr, mr, markerheight), Color.Green);
			GTA.World.DrawMarker(GTA.MarkerType.VerticalCylinder, veh.Position - Vector3.UnitX * markerheight / 2f, Vector3.Zero,
				new Vector3(0f, 90f, 0f), new Vector3(mr, mr, markerheight), Color.Red);

			GTA.World.DrawMarker(GTA.MarkerType.DebugSphere, veh.Position, Vector3.Zero, Vector3.Zero, new Vector3(mr, mr, mr), Color.Purple);
			//GTA.World.DrawMarker(GTA.MarkerType.VerticalCylinder, veh.Position, -veh.UpVector, Vector3.Zero, new Vector3(.1f, .1f, 10f), cyan);
		}

		/// <summary>
		/// Delete the last <c>SectorCheckpoint</c>. First delete its <c>Marker</c>, then remove the checkpoint from <c>markedSectorCheckpoints</c>.
		/// </summary>
		public void deleteLastSectorCheckpoint(bool verbose = true)
		{
			// if markedSectorCheckpoints is empty, do nothing
			if (markedSectorCheckpoints.Count <= 0)
				return;

			// get the last checkpoint in markedSectorCheckpoints
			SectorCheckpoint chkpt = markedSectorCheckpoints.Last();
			int checkpointNum = chkpt.number;

			// delete its Marker (Blip + Checkpoint) from the World, if they are defined
			chkpt.hideMarker();

			// remove the checkpoint from the list
			markedSectorCheckpoints.RemoveAt(markedSectorCheckpoints.Count - 1);

			// print output if verbose
			if (verbose)
				GTA.UI.Screen.ShowSubtitle("Lap Timer: deleted checkpoint #" + checkpointNum);
		}



		/// <summary>
		/// Clear all SectorCheckpoints, and delete all blips & checkpoints from World
		/// </summary>
		public void clearAllSectorCheckpoints(bool verbose = true)
		{
			// iteratively pop saved SectorCheckpoints until the List is empty
			while (markedSectorCheckpoints.Count > 0)
				deleteLastSectorCheckpoint();

			if (verbose)
				GTA.UI.Screen.ShowSubtitle("Lap Timer: All saved SectorCheckpoints cleared. All blips & checkpoints deleted.");
		}
		private int charSwitchStart;

		public bool tasPlaybackMode = false;
		public bool tasRecordMode = false;

		public List<tasRecEntry> tasRecEntries;
		private void tasAppendCurrent(int offset)
		{
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (veh == null) return;
			tasRecEntry current = new tasRecEntry();
			//Meta
			current.model = veh.Model.Hash;

			//Timing
			current.frametime = Game.LastFrameTime;
			current.gametime = Game.GameTime;
			current.offset = offset;
			current.laptime = lapTime;
			current.totaltime = totalTime;
			current.activecheckpoint = activeSector;

			//Position, Rotation, Velocity
			current.pos = veh.Position;
			current.quat = veh.Quaternion;
			current.vel = veh.Velocity;
			current.rvel = veh.WorldRotationVelocity;

			//Driving and Turning
			current.brakepower = veh.BrakePower;
			current.clutch = veh.Clutch;
			current.currentgear = veh.CurrentGear;
			current.rpm = veh.CurrentRPM;
			current.highgear = veh.HighGear;
			//current.burnout = veh.IsInBurnout;
			//veh.IsEngineRunning = true;
			current.nextgear = veh.NextGear;
			current.steeringangle = veh.SteeringAngle;
			current.steeringscale = veh.SteeringScale;
			//veh.SubmersionLevel
			current.throttle = veh.Throttle;
			current.throttlepower = veh.ThrottlePower;
			current.turbo = veh.Turbo;
			//veh.EngineTemperature ???

			//Wheels
			List<wheelProperties> wheelprops = new List<wheelProperties>();
			VehicleWheel[] wheels = veh.Wheels.GetAllWheels();
			for (int i = 0; i < wheels.Length; i++)
			{
				List<int> mem = new List<int>();
				IntPtr addr = wheels[i].MemoryAddress;
				if (addr == IntPtr.Zero) break;
				for (int j = 0; j < wheelMemSize / intSize; j++)
				{
					unsafe
					{
						int* newaddr = (int*)addr;
						mem.Add(*(newaddr + j));
					}
				}
				wheelProperties wheel = new wheelProperties();
				wheel.memory = mem.ToArray();
				wheelprops.Add(wheel);
			}
			current.wheels = wheelprops.ToArray();

			/**IntPtr addr = veh.Wheels.ToList()[0].MemoryAddress;
			IntPtr addr2 = veh.Wheels.ToList()[1].MemoryAddress;
			IntPtr addr3 = veh.Wheels.ToList()[2].MemoryAddress;
			IntPtr addr4 = veh.Wheels.ToList()[3].MemoryAddress;
			string h = (addr.ToString() + " " + addr2.ToString() + " " + addr3.ToString() + " " + addr4.ToString());
			GTA.UI.Screen.ShowSubtitle(h);**/

			/**
			List<wheelProperties> wheels = new List<wheelProperties>();
			Vector3 pos = Game.Player.Character.Position;
			int veh2 = CFX.CitizenFX.Core.Native.API.GetClosestVehicle(pos[0], pos[1], pos[2], 100f, 0, 70);
			int numWheels = CFX.CitizenFX.Core.Native.API.GetVehicleNumberOfWheels(veh2);
			for (int i = 0; i < numWheels; i++)
			{
				wheelProperties wheel = new wheelProperties();
				wheel.brakepressure = CFX.CitizenFX.Core.Native.API.GetVehicleWheelBrakePressure(veh2, i);
				wheel.flags = CFX.CitizenFX.Core.Native.API.GetVehicleWheelFlags(veh2, i);
				wheel.health = CFX.CitizenFX.Core.Native.API.GetVehicleWheelHealth(veh2, i);
				//CitizenFX.Core.Native.API.GetVehicleWheelIsPowered(veh.Handle, i); //this is in the wheel flags
				wheel.power = CFX.CitizenFX.Core.Native.API.GetVehicleWheelPower(veh.Handle, i);
				wheel.rimcollidersize = CFX.CitizenFX.Core.Native.API.GetVehicleWheelRimColliderSize(veh.Handle, i);
				wheel.rotationspeed = CFX.CitizenFX.Core.Native.API.GetVehicleWheelRotationSpeed(veh.Handle, i);
				//CitizenFX.Core.Native.API.GetVehicleWheelSize(veh.Handle);
				wheel.tirecollidersize = CFX.CitizenFX.Core.Native.API.GetVehicleWheelTireColliderSize(veh.Handle, i);
				wheel.tirecolliderwidth = CFX.CitizenFX.Core.Native.API.GetVehicleWheelTireColliderWidth(veh.Handle, i);
				wheel.tractionvectorlength = CFX.CitizenFX.Core.Native.API.GetVehicleWheelTractionVectorLength(veh.Handle, i);
				//CitizenFX.Core.Native.API.GetVehicleWheelWidth(veh.Handle);
				wheel.xoffset = CFX.CitizenFX.Core.Native.API.GetVehicleWheelXOffset(veh.Handle, i);
				//wheel.xrot = CitizenFX.Core.Native.API.GetVehicleWheelXrot(veh.Handle, i); //old name of yrot
				wheel.yrot = CFX.CitizenFX.Core.Native.API.GetVehicleWheelYRotation(veh.Handle, i);
				//CitizenFX.Core.Native.API.GetVehicleWheelieState(veh.Handle);
				wheels.Add(wheel);
			}
			current.wheels = wheels.ToArray();
			**/

			//Vehicle Controls
			vehControls controls = new vehControls();
			//Game.SetControlValueNormalized();
			//Game.GetControlValueNormalized();
			controls.vehlr = Game.GetControlValueNormalized(Control.VehicleMoveLeftRight);
			controls.vehud = Game.GetControlValueNormalized(Control.VehicleMoveUpDown);
			//controls.vehspecial = Game.GetControlValueNormalized(Control.VehicleSpecial);
			controls.vehaccel = Game.GetControlValueNormalized(Control.VehicleAccelerate);
			controls.vehbrake = Game.GetControlValueNormalized(Control.VehicleBrake);
			controls.vehhandbrake = Game.GetControlValueNormalized(Control.VehicleHandbrake);
			//controls.vehhorn = Game.GetControlValueNormalized(Control.VehicleHorn);
			controls.vehflyaccel = Game.GetControlValueNormalized(Control.VehicleFlyThrottleUp);
			controls.vehflybrake = Game.GetControlValueNormalized(Control.VehicleFlyThrottleDown);
			controls.vehflyyawleft = Game.GetControlValueNormalized(Control.VehicleFlyYawLeft);
			controls.vehflyyawright = Game.GetControlValueNormalized(Control.VehicleFlyYawRight);
			controls.vehfranklinspecial = Game.GetControlValueNormalized(Control.VehicleSpecialAbilityFranklin);
			//controls.vehstuntud = Game.GetControlValueNormalized(Control.VehicleStuntUpDown); //???? stunt cam speed?
			//controls.vehroof = Game.GetControlValueNormalized(Control.VehicleRoof);
			controls.vehjump = Game.GetControlValueNormalized(Control.VehicleJump);
			//controls.vehgrapplinghook = Game.GetControlValueNormalized(Control.VehicleGrapplingHook);
			//controls.vehshuffle = Game.GetControlValueNormalized(Control.VehicleShuffle);
			controls.vehflyrolllr = Game.GetControlValueNormalized(Control.VehicleFlyRollLeftRight);
			controls.vehflypitchud = Game.GetControlValueNormalized(Control.VehicleFlyPitchUpDown);
			controls.vehflyverticalmode = Game.GetControlValueNormalized(Control.VehicleFlyVerticalFlightMode);
			//no subs or bicycles
			controls.vehcarjump = Game.GetControlValueNormalized(Control.VehicleCarJump);
			controls.vehrocketboost = Game.GetControlValueNormalized(Control.VehicleRocketBoost);
			//controls.vehflyboost = Game.GetControlValueNormalized((GTA.Control) 352); missing?
			controls.vehparachute = Game.GetControlValueNormalized(Control.VehicleParachute);
			controls.vehbikewings = Game.GetControlValueNormalized(Control.VehicleBikeWings);
			controls.vehtransform = Game.GetControlValueNormalized(Control.VehicleFlyTransform);
			//Game.SetControlValueNormalized(Control.VehicleFlyTransform, 1.0f);
			current.controls = controls;

			tasRecEntries.Add(current);
			/**
			//Wheels
			VehicleWheel[] wheels = veh.Wheels.GetAllWheels();
			for (int i = 0; i < wheels.Length; i++)
            {
				VehicleWheel wheel = wheels[i];
				Hash.SET_VEHICLE_WHEELS_CAN_BREAK;
            }

			//current.wheelspeed = veh.WheelSpeed;
			**/

		}
		private bool disableCinCamToggle = false;
		public void tasRecord()
		{
			if (!tasPlaybackMode)
			{
				int exittime = 0;
				int offset = 0;
				if (tasRecEntries.Count > 0)
				{
					exittime = tasRecEntries.Last().exitplaybacktime;
					offset = tasRecEntries.Last().offset;
				}
				if (exittime > 0)
				{
					offset += (tasRecEntries.Last().gametime - exittime);
				}
				tasAppendCurrent(offset);
				GTA.UI.Screen.ShowSubtitle("~r~~ws~RECORDING " + timescaleValue.ToString() + "x~ws~", 1);

				Game.DisableControlThisFrame(Control.VehicleCinCam);
				if (Game.IsControlJustPressed(Control.VehicleCinCam))
				{
					tasPlaybackToggle();
					disableCinCamToggle = true;
				}
			}

		}

		public void tasRecordToggle()
		{
			//if (tasPlaybackMode) return;
			if (tasRecordMode)
			{
				exportReplay();
			}
			else if (!tasPlaybackMode)
			{
				tasRecEntries = new List<tasRecEntry>();
			}
			tasRecordMode = !tasRecordMode;
		}

		//Index 130 determines glide acceleration
		private static int[] bikeMemIndexes = { 16, 17, 18, 20, 21, 22, 23, 24, 25,
			26, 27, 28, 29, 30, 32, 33, 34, 40, 41, 42, 43, 44, 45, 46, 47,
			48, 49, 50, 51, 88, 89, 90, 91, 92, 93, 94, 
			//96, 97, 98, 99,
			102, 104, 110, 111, 112, 113, 114, 115, 116, 117, 119, 120, 128, 130, 
			136 };
		private static int[] carMemIndexes = { 16, 17, 18, 19, 20, 21, 22, 24, 25,
			26, 27, 28, 29, 30, 31, 32, 33, 34, 40, 41, 42, 43, 44, 45, 46, 47,
			48, 49, 50, 51, 88, 89, 90, 91, 92, 93, 94, 
			//96, 97, 98, 99,
			102, 104, 110, 111, 112, 113, 115, 116, 117, 119, 120, 128, 130 };
		private static HashSet<int> bikeMemIndexSet = new HashSet<int>(bikeMemIndexes);
		private static HashSet<int> carMemIndexSet = new HashSet<int>(carMemIndexes);
		private void tasPlayCurrent(tasRecEntry current)
		{
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (veh == null) return;
			int syncPlaybackFrames = 1;
			if ((syncPlaybackFrames > 0 && (tasPlaybackIndex-tasPlaybackStartIndex) % syncPlaybackFrames == 0) || (tasPlaybackIndex - tasPlaybackStartIndex) < 60)
			{
				//Timing
				if (current.laptime >= 0)
                {
					lapTime = current.laptime;
                }
				if (current.totaltime >= 0)
                {
					totalTime = current.totaltime;
                }
				if (current.activecheckpoint >= 0 && current.activecheckpoint != activeSector)
                {
					activateRaceCheckpoint(current.activecheckpoint);
                }

				//Position, Rotation, Velocity
				veh.PositionNoOffset = current.pos;
				veh.Quaternion = current.quat;
				veh.Velocity = current.vel;
				veh.WorldRotationVelocity = current.rvel;

				//Driving and Steering
				veh.BrakePower = current.brakepower;
				veh.Clutch = current.clutch;
				veh.CurrentGear = current.currentgear;
				veh.CurrentRPM = current.rpm;
				veh.HighGear = current.highgear;
				//veh.IsBurnoutForced = current.burnout;
				veh.NextGear = current.nextgear;
				veh.SteeringAngle = current.steeringangle;
				veh.SteeringScale = current.steeringscale;
				veh.Throttle = current.throttle;
				veh.ThrottlePower = current.throttlepower;
				veh.Turbo = current.turbo;

				//CitizenFX.Core.Native
				//Wheels
				if (current.model == veh.Model.Hash)
				{
					wheelProperties[] wheelprops = current.wheels;
					VehicleWheel[] wheels = veh.Wheels.GetAllWheels();
					for (int i = 0; i < Math.Max(wheels.Length, wheelprops.Length); i++)
					{
						IntPtr addr = wheels[i].MemoryAddress;
						if (addr == IntPtr.Zero) break;
						for (int j = 0; j < wheelMemSize / intSize; j++)
						{
							if ((veh.IsMotorcycle && bikeMemIndexSet.Contains(j)) || 
								(veh.IsRegularAutomobile && carMemIndexSet.Contains(j)))
							{
								unsafe
								{
									int* newaddr = (int*)addr;
									*(newaddr + j) = wheelprops[i].memory[j];
								}
							}
						}
					}
				}
				//IntPtr addr = veh.Wheels.ToList()[0].MemoryAddress;
				/**
				int numWheels = CFX.CitizenFX.Core.Native.API.GetVehicleNumberOfWheels(veh.GetHashCode());
				for (int i = 0; i < Math.Max(numWheels, current.wheels.Length); i++) {
					wheelProperties wheel = current.wheels[i];
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelBrakePressure(veh.Handle, i, wheel.brakepressure);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelFlags(veh.Handle, i, wheel.flags);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelHealth(veh.Handle, i, wheel.health);
					//CitizenFX.Core.Native.API.SetVehicleWheelIsPowered(veh.Handle, i); //this is in the wheel flags
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelPower(veh.Handle, i, wheel.power);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelRimColliderSize(veh.Handle, i, wheel.rimcollidersize);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelRotationSpeed(veh.Handle, i, wheel.rotationspeed);
					//CitizenFX.Core.Native.API.SetVehicleWheelSize(veh.Handle);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelTireColliderSize(veh.Handle, i, wheel.tirecollidersize);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelTireColliderWidth(veh.Handle, i, wheel.tirecolliderwidth);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelTractionVectorLength(veh.Handle, i, wheel.tractionvectorlength);
					//CitizenFX.Core.Native.API.SetVehicleWheelWidth(veh.Handle);
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelXOffset(veh.Handle, i, wheel.xoffset);
					//CitizenFX.Core.Native.API.SetVehicleWheelXrot(veh.Handle, i, wheel.xrot); //old name of yrot
					CFX.CitizenFX.Core.Native.API.SetVehicleWheelYRotation(veh.Handle, i, wheel.yrot);
					//CitizenFX.Core.Native.API.SetVehicleWheelieState(veh.Handle);
				}
				**/
			}

			if (true)
			{
				//Controls
				vehControls controls = current.controls;
				Game.SetControlValueNormalized(Control.VehicleMoveLeftRight, controls.vehlr);
				Game.SetControlValueNormalized(Control.VehicleMoveUpDown, controls.vehud);
				//Game.SetControlValueNormalized(Control.VehicleSpecial, controls.vehspecial);
				Game.SetControlValueNormalized(Control.VehicleAccelerate, controls.vehaccel);
				Game.SetControlValueNormalized(Control.VehicleBrake, controls.vehbrake);
				Game.SetControlValueNormalized(Control.VehicleHandbrake, controls.vehhandbrake);
				//Game.SetControlValueNormalized(Control.VehicleHorn, controls.vehhorn);
				Game.SetControlValueNormalized(Control.VehicleFlyThrottleUp, controls.vehflyaccel);
				Game.SetControlValueNormalized(Control.VehicleFlyThrottleDown, controls.vehflybrake);
				Game.SetControlValueNormalized(Control.VehicleFlyYawLeft, controls.vehflyyawleft);
				Game.SetControlValueNormalized(Control.VehicleFlyYawRight, controls.vehflyyawright);
				Game.SetControlValueNormalized(Control.VehicleSpecialAbilityFranklin, controls.vehfranklinspecial);
				//Game.SetControlValueNormalized(Control.VehicleStuntUpDown, controls.vehstuntud); //???? stunt cam speed?
				//Game.SetControlValueNormalized(Control.VehicleRoof, controls.vehroof);
				Game.SetControlValueNormalized(Control.VehicleJump, controls.vehjump);
				//Game.SetControlValueNormalized(Control.VehicleGrapplingHook, controls.vehgrapplinghook);
				//Game.SetControlValueNormalized(Control.VehicleShuffle, controls.vehshuffle);
				Game.SetControlValueNormalized(Control.VehicleFlyRollLeftRight, controls.vehflyrolllr);
				Game.SetControlValueNormalized(Control.VehicleFlyPitchUpDown, controls.vehflypitchud);
				Game.SetControlValueNormalized(Control.VehicleFlyVerticalFlightMode, controls.vehflyverticalmode);
				//no subs or bicycles
				Game.SetControlValueNormalized(Control.VehicleCarJump, controls.vehcarjump);
				Game.SetControlValueNormalized(Control.VehicleRocketBoost, controls.vehrocketboost);
				//Game.SetControlValueNormalized((GTA.Control)352, controls.vehflyboost); missing?
				Game.SetControlValueNormalized(Control.VehicleParachute, controls.vehparachute);
				Game.SetControlValueNormalized(Control.VehicleBikeWings, controls.vehbikewings);
				Game.SetControlValueNormalized(Control.VehicleFlyTransform, controls.vehtransform);
			}


		}
		private int tasPlaybackIndex = 0;
		private int tasPlaybackStartIndex = 0;
		private int tasPlaybackStartTime = 0;
		private bool tasPlaybackExitNextFrame = false;
		private bool tasPlaybackPaused = false;
		private bool tasPlaybackPauseOnNextFrame = false;
		//private int tasPlaybackPausedAt = 0;
		public void tasPlayback()
        {
			if (!tasPlaybackExitNextFrame && tasRecEntries != null && tasPlaybackIndex < tasRecEntries.Count)
            {
				if (tasPlaybackStartTime <= 0) tasPlaybackStartTime = Game.GameTime;
				tasPlaybackControl();

				tasRecEntry current = tasRecEntries[tasPlaybackIndex];
				if (tasPlaybackIndex + 1 == tasRecEntries.Count)
                {
					tasPlaybackExitNextFrame = true;
					
				}
				bool waitFrame = false;
				int killloop = 0;
				//Timing: skip back or ahead if playback time doesn't match recording time
				while (killloop < 5 && !tasPlaybackPaused && (tasPlaybackIndex - 1 >= 0) && (tasPlaybackIndex + 1 < tasRecEntries.Count))
                {
					//int totalTime = (current.gametime - tasRecEntries[tasPlaybackStartIndex].gametime) + current.offset;
					//int playbackTime = (Game.GameTime - tasPlaybackStartTime);
					int searchFor = (Game.GameTime - tasPlaybackStartTime) - 
						((current.offset - tasRecEntries[tasPlaybackStartIndex].offset) - tasRecEntries[tasPlaybackStartIndex].gametime);
					//int frameTime = (int)(current.frametime * 1000);
					
					tasRecEntry prev = tasRecEntries[tasPlaybackIndex - 1];
					tasRecEntry next = tasRecEntries[tasPlaybackIndex + 1];
					if (Math.Abs(prev.gametime - searchFor) < Math.Abs(current.gametime - searchFor))
					{
						//tasPlaybackIndex--;
						//Wait for the next tick to try playing back this frame
						waitFrame = true;
						break;
					}
					else if (Math.Abs(next.gametime - searchFor) < Math.Abs(current.gametime - searchFor))
					{
						tasPlaybackIndex++;
					}
					else
					{
						break;
					}
					current = tasRecEntries[tasPlaybackIndex];
					killloop++;
				}
				if (!waitFrame)
				{
					tasPlayCurrent(current);
					if (!tasPlaybackPaused)
						tasPlaybackIndex++;
				}
				if (tasRecordMode)
					GTA.UI.Screen.ShowSubtitle("~b~~ws~PLAYBACK ~w~& ~r~RECORD " + timescaleValue.ToString() + "x~ws~~n~", 1);
				else
					GTA.UI.Screen.ShowSubtitle("~b~~ws~PLAYBACK " + timescaleValue.ToString() + "x~ws~~n~", 1);
			}
			else
            {
				tasPlaybackExitNextFrame = true;
			}
			if (tasPlaybackExitNextFrame || tasPlaybackIndex == tasRecEntries.Count)
			{
				if (tasRecordMode)
				{
					tasRecEntries = tasRecEntries.GetRange(0, tasPlaybackIndex);
					tasRecEntry current;
					if (tasPlaybackIndex > 0)
					{
						current = tasRecEntries[tasPlaybackIndex - 1];
						current.exitplaybacktime = Game.GameTime;
						tasRecEntries[tasPlaybackIndex - 1] = current; 
					}
				}
				tasPlaybackMode = false;
				tasPlaybackIndex = 0;
				tasPlaybackStartIndex = 0;
				tasPlaybackStartTime = 0;
				tasPlaybackPaused = false;
				
				tasPlaybackExitNextFrame = false;
			}
			else if (tasPlaybackPauseOnNextFrame) 
			{ 
				//tasPlaybackPaused = true;
				tasPlaybackPauseOnNextFrame = false;
			}

		}
		private int tasPlaybackRecordAt = int.MaxValue;
		private void tasPlaybackControl()
        {
			if (Game.GameTime >= tasPlaybackRecordAt)
			{
				tasPlaybackToggle();
				tasPlaybackRecordAt = int.MaxValue;
			}
			if (tasRecordMode)
            {
				Game.DisableControlThisFrame(Control.VehicleCinCam);
				if (Game.IsControlJustPressed(Control.VehicleCinCam) && !disableCinCamToggle)
                {
					tasPlaybackRecordAt = Game.GameTime + 0;// freezeTime;
                }
				else
                {
					disableCinCamToggle = false;
                }
            }
			Game.DisableControlThisFrame(Control.Phone);
			Game.DisableControlThisFrame(Control.PhoneUp);
			if (Game.IsControlJustPressed(Control.PhoneUp))
			{
				tasPlaybackPaused = !tasPlaybackPaused;
			}
			
			//Game.EnableControlThisFrame(Control.VehicleAim);
			float seekDirection = 0f;
			if (tasPlaybackPaused) 
			{
				if (Game.IsControlPressed(Control.VehicleAim))
				{
					Game.DisableControlThisFrame(Control.LookLeftRight);
					seekDirection = Game.GetDisabledControlValueNormalized(Control.LookLeftRight);
				}
				tasPlaybackSkipRelative((int)(20f * seekDirection));
            }
			
        }
		public void tasPlaybackGoTo(int index)
        {
			if (tasPlaybackMode)
			{
				index = Math.Max(0, index);
				index = Math.Min(index, tasRecEntries.Count - 1);
				tasPlaybackIndex = index;
				tasPlaybackStartIndex = index;
				tasPlaybackStartTime = -1;
			}
		}
		public void tasPlaybackSkipRelative(int skip)
        {
			tasPlaybackGoTo(tasPlaybackIndex + skip);
        }
		public void tasPlaybackToggle()
        {
			if (tasPlaybackMode) //&& tasRecEntries != null && tasRecEntries.Count > 0)
			{
				if (tasRecordMode)
				{
					exportReplay();
				}
				tasPlaybackExitNextFrame = true;
				
			}
			else
			{
				tasPlaybackMode = true;
				if (tasRecordMode)
				{
					//Skip to 10 frames before end of TAS
					tasPlaybackGoTo(tasRecEntries.Count - 10);
					tasPlaybackPaused = true;
				}
			}
		}
		private int timescaleIndex = 3;
		private float[] timescaleValues = {.25f, .5f, .75f, 1f};
		private float timescaleValue
        {
			get
            {
				return timescaleValues[timescaleIndex];
            }
        }
		private void setTimescaleDown()
        {
			timescaleIndex = Math.Max(0, timescaleIndex - 1);
        }
		private void setTimescaleUp()
		{
			timescaleIndex = Math.Min(timescaleValues.Length - 1, timescaleIndex + 1);
		}
		public void timescaleControl()
        {
			Game.DisableControlThisFrame(Control.PhoneLeft);
			Game.DisableControlThisFrame(Control.VehicleRadioWheel);
			Game.DisableControlThisFrame(Control.PhoneRight);
			if (Game.IsControlJustPressed(Control.PhoneLeft))
			{
				setTimescaleDown();
			}
			else if (Game.IsControlJustPressed(Control.PhoneRight))
            {
				setTimescaleUp();
            }
			else
            {
				return;
            }
			Game.TimeScale = timescaleValue;
		}
		public dhprop[] dhprops;
		private int lastPropRemoval = 0;
		public void removeProps(bool toggle = false, int wait = 0)
		{
			if (dhprops == null || Game.GameTime - lastPropRemoval < wait) return;
			for (int i = 0; i < dhprops.Length; i++)
            {
				Prop p = World.GetClosestProp(dhprops[i].pos, 1f, dhprops[i].modelHash);
				if (p != null)
				{
					p.IsCollisionEnabled = toggle;
					p.IsVisible = toggle;
				}
            }
			lastPropRemoval = Game.GameTime;
        }

		private int lastVehicleRemoval = 0;
		public void removeVehicles(int wait = 0)
        {
			
			if (Game.GameTime - lastVehicleRemoval < wait) return;
			Vehicle[] nearby = World.GetNearbyVehicles(Game.Player.Character, 100f);
			for (int i = 0; i < nearby.Length; i++)
            {
				if (!nearby[i].PreviouslyOwnedByPlayer)
					nearby[i].Delete();
            }
			lastVehicleRemoval = Game.GameTime;
        }
		private int lastPedRemoval = 0;
		public void removePeds(int wait = 0)
        {
			if (Game.GameTime - lastPedRemoval < wait) return;
			Ped[] nearby = World.GetNearbyPeds(Game.Player.Character, 100f);
			for (int i = 0; i < nearby.Length; i++)
			{
				nearby[i].Delete();
			}
			lastPedRemoval = Game.GameTime;
		}
		private bool clampInMidair = false;
		public void betterCamera()
        {
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (veh == null) return;
			//Function.Call(Hash._ANIMATE_GAMEPLAY_CAM_ZOOM, 1f, 1000f);
			//GTA.UI.Screen.ShowSubtitle(((int)Function.Call<float>(Hash.GET_GAMEPLAY_CAM_RELATIVE_PITCH)).ToString() + "\t" + 
			//	((int)Function.Call<float>(Hash.GET_GAMEPLAY_CAM_RELATIVE_HEADING)).ToString());
			if (!veh.IsMotorcycle || (!veh.IsInAir && veh.Velocity.Z > 0) || clampInMidair)
			{
				//Function.Call(Hash._CLAMP_GAMEPLAY_CAM_PITCH, Math.Min((Math.Max(20f*veh.ForwardVector.Z, 0f)-1f) * 90f, 0f), 90f);
				Function.Call(Hash._CLAMP_GAMEPLAY_CAM_PITCH, -90f, 90f);
				//if (Function.Call<Vector3>(Hash.GET_GAMEPLAY_CAM_COORD).DistanceTo(veh.Position) < 100f)
				//	Function.Call(Hash.SET_GAMEPLAY_CAM_RELATIVE_PITCH, 20f * veh.UpVector.Z, 1f);

				//Function.Call(Hash._CLAMP_GAMEPLAY_CAM_YAW, -180f, 180f);
				//Function.Call(Hash._ANIMATE_GAMEPLAY_CAM_ZOOM);
				if (Math.Abs(Function.Call<float>(Hash.GET_GAMEPLAY_CAM_RELATIVE_PITCH)) > 88f)
					clampInMidair = false;
				else
					clampInMidair = true;
			}
			if (false && tasPlaybackMode && tasPlaybackPaused)
            {
				Function.Call(Hash._CLAMP_GAMEPLAY_CAM_PITCH, 0f, 0f);
				Function.Call(Hash._CLAMP_GAMEPLAY_CAM_YAW, 0f, 0f);
			}
		}
		public void preventTrain()
        {
			Function.Call(Hash.SET_DISABLE_RANDOM_TRAINS_THIS_FRAME, true);
        }
		public void preventVehicleExit()
        {
			Game.DisableControlThisFrame(Control.VehicleExit);
			if (Game.IsControlPressed(Control.VehicleExit)) respawn();
        }
		public void preventCharSwitch()
		{
			//if (Function.Call<bool>(Hash.IS_PLAYER_SWITCH_IN_PROGRESS)) Function.Call(Hash.STOP_PLAYER_SWITCH);
			//Function.Call(Hash.DISABLE_CONTROL_ACTION, 2, Control.CharacterWheel, true);
			int gt = Game.GameTime;
			const int waitTime = 350; //ms
			bool isWaiting = (gt - charSwitchStart < waitTime);
			
			Game.DisableControlThisFrame(Control.CharacterWheel);
			if (Game.IsControlPressed(Control.CharacterWheel) ||
				Game.IsControlPressed(Control.SelectCharacterFranklin) ||
				Game.IsControlPressed(Control.SelectCharacterMichael) ||
				Game.IsControlPressed(Control.SelectCharacterTrevor))
			{
				string subtitle = "Char Swap";
				char textColor;
				if (canStopCar())
				{
					if (isWaiting) textColor = 'y';
					else
					{
						textColor = 'g';
						subtitle += " Ready!";
					}
				}
				else
				{
					textColor = 'r';
					if (!isWaiting) subtitle += " Unavailable";
				}
				if (isWaiting)
                {
					subtitle += String.Format(" in {0}ms", waitTime - (gt - charSwitchStart));

				}
				GTA.UI.Screen.ShowSubtitle(String.Format("~{0}~{1}", textColor, subtitle), 1);
			}
			else if (!isWaiting) //could bug if frametime longer than 350ms
			{
				stopCar(75);
				charSwitchStart = gt;
			}
			else charSwitchStart = gt;

		}

		public bool canStopCar()
        {
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			return veh != null && (veh.IsAutomobile || veh.IsMotorcycle) && (veh.IsOnAllWheels || (veh.IsUpsideDown && !veh.IsMotorcycle));

		}

		public void stopCar(int delay)
		{
			
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (canStopCar())
            {
				if (delay > 0)
					Script.Wait(delay); //TODO: should fix this
				veh.Velocity = Vector3.Zero;
				veh.RotationVelocity = Vector3.Zero;
			}
		}

		public void stopCar()
        {
			stopCar(0);
        }
		public void checkBikeCollisions()
        {
			Ped ch = Game.Player.Character;
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			if (veh == null || (veh.IsMotorcycle && veh.UpVector.Z < 0))
			{
				//ch.CanBeKnockedOffBike = true;
				//ch.CanRagdoll = true;
				//ch.IsInvincible = false;
				//ch.CanFlyThroughWindscreen = true;
			}
			ch.CanFlyThroughWindscreen = false;
			ch.CanBeKnockedOffBike = false;
			ch.CanRagdoll = false;
			//ch.CanBeDraggedOutOfVehicle = false;
			//ch.CanBeShotInVehicle = false;
			//ch.CanBeTargetted = false;
			ch.IsInvincible = true;

		}
		public void resetHealth()
        {
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			//if (veh != null && (veh.Health < 1000 || veh.EngineHealth < 700))
			if (veh != null)
			{
				veh.Health = 1000;
				veh.EngineHealth = 1000;
				veh.BodyHealth = 1000;
			}
		}
		public void fixCar()
        {
			//veh.RemoveParticleEffects();
			Vehicle veh = Game.Player.Character.CurrentVehicle;
			Function.Call(Hash.STOP_ENTITY_FIRE, veh.Handle);
			veh.Repair();
			veh.IsInvincible = true;
			veh.IsExplosionProof = true;
			veh.IsFireProof = true;
			veh.CanEngineDegrade = false;
			veh.CanTiresBurst = false;
			veh.IsBulletProof = true;
			veh.CanBeVisiblyDamaged = false;
			Function.Call(Hash.SET_CINEMATIC_MODE_ACTIVE, false);
			//veh.hea
			//MaterialHash.StuntRampSurface;
			//veh.SetNoCollision

		}
		public void respawn()
        {
			if (activeSector == 0) enterRaceMode();
			else 
			{
				SectorCheckpoint respawn = markedSectorCheckpoints[activeSector - 1]; //TODO
				Vehicle veh = Game.Player.Character.CurrentVehicle;
				Vector3 vel = veh.Velocity;
				Vector3 rvel = veh.RotationVelocity;
				Quaternion quat = veh.Quaternion;

				if (!lastHitSecondary)
				{
					veh.Position = respawn.position;
					veh.Quaternion = respawn.quaternion;
				}
				else
                {
					veh.Position = respawn.position2;
					veh.Quaternion = respawn.quaternion2;
                }

				fixCar();
				
				//veh.Velocity = vel;
				//veh.RotationVelocity = rvel;
				//veh.Quaternion = quat;
			}
        }

		public void warpToCheckpoint(int idx)
        {
			SectorCheckpoint warpTo = markedSectorCheckpoints[idx];
			warpToPosition(warpTo.position, warpTo.quaternion);
		}

		public void warpToPosition(Vector3 pos, Quaternion quat)
        {
			Vehicle veh = Game.Player.Character.CurrentVehicle;

			tasRecEntry current = new tasRecEntry();

			current.vel = veh.Velocity;
			current.clutch = veh.Clutch;
			current.currentgear = veh.CurrentGear;
			current.rpm = veh.CurrentRPM;
			current.nextgear = veh.NextGear;
			current.throttle = veh.Throttle;
			current.throttlepower = veh.ThrottlePower;
			current.turbo = veh.Turbo;

			veh.Position = pos;
			veh.Quaternion = quat;

			float speed = current.vel.Length();
			veh.Velocity = veh.ForwardVector * speed;
			veh.Clutch = current.clutch;
			veh.CurrentGear = current.currentgear;
			veh.CurrentRPM = current.rpm;
			veh.NextGear = current.nextgear;
			veh.Throttle = current.throttle;
			veh.ThrottlePower = current.throttlepower;
			veh.Turbo = current.turbo;
		}

		private string formatTimestring(int time)
        {
			return String.Format("{0:00}:{1:00}.{2:000}", time / (60 * 1000), (time % (60 * 1000)) / 1000, time % 1000);
		}
		private void drawTimestring(string timestring, int index)
        {
			int interval = index * 10;
			SizeF res = NativeUI.UIMenu.GetScreenResolutionMaintainRatio();
			Point safe = NativeUI.UIMenu.GetSafezoneBounds();

			for (int i = 0; i < timestring.Length; i++)
			{
				int numDigits = timestring.Substring(i).Count(Char.IsDigit);
				//if (i + 1 < timestring.Length && (timestring[i + 1] == ':' || timestring[i + 1] == '.')) mult = 1;
				int addOne = (Char.IsDigit(timestring[i]) ? 0 : 1);
				string Text = timestring.Substring(i, 1).PadRight((timestring.Length - i - 1) + numDigits + addOne, ' ') + '.'; //TODO: better string formatting
				NativeUI.UIResText.Draw(Text, (int)res.Width - safe.X - 10, (int)res.Height - safe.Y - (42 + (4 * interval)), SHVDN2.GTA.Font.ChaletLondon, 0.5f, Color.White,
					NativeUI.UIResText.Alignment.Right, false, false, 0);
			}
		}

		private void drawTimers()
        {
			if (pool == null) return;

			//string mins = String.Format("{0:00}")
			string lapTimestring = formatTimestring(lapTime);
			string totalTimestring = formatTimestring(totalTime);
			string pbTimestring = formatTimestring(pbTime);
			//lapTimerBar.Text = "abcdef\r \033[Azxywvut";
			List<NativeUI.TimerBarBase> poolList = pool.ToList();
			if (poolList == null) return;
			int lapTimerBarIndex = (lapTimerBar != null) ? poolList.IndexOf(lapTimerBar) : -1;
			int totalTimerBarIndex = (totalTimerBar != null) ? poolList.IndexOf(totalTimerBar) : -1;
			int pbTimerBarIndex = (pbTimerBar != null) ? poolList.IndexOf(pbTimerBar) : -1;
			if (lapTimerBarIndex != -1)
			{
				if (lapStartTime >= 0)
				{
					lapTimerBar.Text = "";
					drawTimestring(lapTimestring, lapTimerBarIndex);
				}
				else lapTimerBar.Text = "--:--.---";
			}
			
			if (totalTimerBarIndex != -1)
			{	if (raceStartTime >= 0)
				{ 
					totalTimerBar.Text = "";
					drawTimestring(totalTimestring, totalTimerBarIndex);
				}
				else totalTimerBar.Text = "--:--.---";
			}
			
			if (pbTimerBarIndex != -1)
			{
				if (pbTime >= 0)
				{
					pbTimerBar.Text = "";
					drawTimestring(pbTimestring, pbTimerBarIndex);
				}
				else pbTimerBar.Text = "--:--.---";
			}
			
		}
		public void updatePools()
        {
			drawTimers();
			//.IndexOf(pbTimerBar);
			//lapTimerBar.Text = "";
			//pbTimerBar.Text = "";
			//totalTimerBar.Text = "";


/**			int lapTimerBarIndex = 0;
			int interval = lapTimerBarIndex * 10;
			SizeF res = NativeUI.UIMenu.GetScreenResolutionMaintainRatio();
            Point safe =  NativeUI.UIMenu.GetSafezoneBounds();

			for (int i = 0; i < timestring.Length; i++)
            {
				int numDigits = timestring.Substring(i).Count(Char.IsDigit);
				//if (i + 1 < timestring.Length && (timestring[i + 1] == ':' || timestring[i + 1] == '.')) mult = 1;
				int addOne = (Char.IsDigit(timestring[i]) ? 0 : 1);
				string Text = timestring.Substring(i, 1).PadRight((timestring.Length-i-1)+numDigits+addOne, ' ') +'.'; 
				NativeUI.UIResText.Draw(Text, (int)res.Width - safe.X - 10, (int)res.Height - safe.Y - (42 + (4 * interval)), SVDN2.GTA.Font.ChaletLondon, 0.5f, Color.White,
					NativeUI.UIResText.Alignment.Right, false, false, 0);
			}
**/
			
        }

		/// <summary>
		/// Setup race mode by disabling traffic, clearing weather, and teleporting player to the 1st SectorCheckpoint.
		/// </summary>
		public void enterRaceMode()
		{
			// set weather to extra sunny; save current weather so it can be restored after exiting race mode
			weather = World.Weather;
			//World.Weather = Weather.ExtraSunny;
			//World.Weather = Weather.Neutral;
			Function.Call(Hash._SET_RAIN_LEVEL, 0f);
			Game.Player.Character.IsInvincible = true;
			Game.Player.Character.CanFlyThroughWindscreen = false;

			pool.Remove(totalTimerBar);
			pool.Remove(pbTimerBar);
			pool.Remove(lapTimerBar);
			totalTimerBar = new NativeUI.TextTimerBar("TIME", "--:--.---");
			pbTimerBar = new NativeUI.TextTimerBar("PERSONAL RECORD", "--:--.---");
			lapTimerBar = new NativeUI.TextTimerBar("CURRENT LAP", "--:--.---");
			if (!lapRace) {
				// set the 2nd SectorCheckpoint as active (there must be at least 2 SectorCheckpoints to start race mode); draw the checkpoint
				activateRaceCheckpoint(1);

				// teleport player to the starting checkpoint; set player orientation
				SectorCheckpoint start = markedSectorCheckpoints[0];
				//Game.Player.Character.CurrentVehicle.Position = start.position;
				Vector3 targetForward = start.quaternion * Vector3.RelativeFront;
				//Vector3 targetUp = start.quaternion * Vector3.RelativeTop;

				Game.Player.CanControlCharacter = false;
				Game.Player.Character.CurrentVehicle.Position = spawn - targetForward*30f;
				Script.Wait(40); //let camera update
				Game.Player.Character.CurrentVehicle.Position = spawn;
				Game.Player.CanControlCharacter = true;
				Game.Player.Character.CurrentVehicle.Quaternion = start.quaternion;
				fixCar();

				// freeze time
				Game.Player.CanControlCharacter = false;
				GTA.UI.Screen.ShowSubtitle("~y~Lap Timer: Ready...");
				Script.Wait(freezeTime);
				GTA.UI.Screen.ShowSubtitle("~g~Lap Timer: Go!");
				Game.Player.CanControlCharacter = true;
				

				// start the clock by getting the current GameTime
				lapStartTime = Game.GameTime;
				raceStartTime = lapStartTime;

				
				pool.Add(pbTimerBar);
				pool.Add(totalTimerBar);
			}
			else
            {
				activateRaceCheckpoint(markedSectorCheckpoints.Count - 1);

				// teleport player to the starting checkpoint; set player orientation
				SectorCheckpoint start = markedSectorCheckpoints[markedSectorCheckpoints.Count - 2];
				Game.Player.Character.CurrentVehicle.Position = start.position;
				//Game.Player.Character.CurrentVehicle.Position = spawn;
				Game.Player.Character.CurrentVehicle.Quaternion = start.quaternion;
				fixCar();

				// set the lap start time to -100 hours
				lapStartTime = defaultLapStartTime;
				//race hasn't started yet
				raceStartTime = defaultLapStartTime;

				pool.Add(totalTimerBar);
				pool.Add(pbTimerBar);
				pool.Add(lapTimerBar);
			}
			Function.Call(Hash.SET_STUNT_JUMPS_CAN_TRIGGER, false);




		}

		/// <summary>
		/// Clean up any objects created while in race mode
		/// </summary>
		public void exitRaceMode(bool verbose = true)
		{
			//markedSectorCheckpoints[activeSector].hideMarker();
			hideAllSectorCheckpoints();

			// try to restore Weather, if possible
			World.Weather = weather;

			// Remove timer bars
			pool.Remove(pbTimerBar);
			pool.Remove(lapTimerBar);
			pool.Remove(totalTimerBar);

			// Turn off recording
			if (tasRecordMode)
            {
				tasRecordToggle();
            }

			removeProps(toggle: true);
			Function.Call(Hash.SET_STUNT_JUMPS_CAN_TRIGGER, true);

			raceMode = false;
		}

		#endregion



		#region helpers

		/// <summary>
		/// Hide the blips and checkpoints of all saved SectorCheckpoints.
		/// </summary>
		private void hideAllSectorCheckpoints()
		{
			for (int i = 0; i < markedSectorCheckpoints.Count; i++)
				markedSectorCheckpoints[i].hideMarker();
		}

		/// <summary>
		/// Redraw the blips and checkpoints of all saved SectorCheckpoints. Should only be used in placement mode.
		/// </summary>
		private void redrawAllSectorCheckpoints()
		{
			for (int i = 0; i < markedSectorCheckpoints.Count; i++)
			{
				// copy the instance of SectorCheckpoint and replace marker with a new instance returned by placeMarker
				SectorCheckpoint newCheckpoint = markedSectorCheckpoints[i];
				newCheckpoint.marker = newCheckpoint.placeMarker(MarkerType.placement, markedSectorCheckpoints[i].number);
				markedSectorCheckpoints[i] = newCheckpoint;                         // assign new instance of SectorCheckpoint to the original index in the List
			}
		}



		/// <summary>
		/// Determine whether the player can enter race mode right now. List all reasons why player cannot enter race mode if not.
		/// </summary>
		/// <returns><c>true</c> if possible to enter race mode</returns>
		private bool canEnterRaceMode()
		{
			bool ret = true;

			// markedSectorCheckpoints must be valid
			if (!validateCheckpoints(markedSectorCheckpoints))
				ret = false;

			// must not be actively on a mission and be able to accept missions
			if (!Game.Player.CanStartMission)
			{
				ret = false;
				GTA.UI.Notification.Show("~r~Lap Timer: Player cannot start mission. Cannot enter Race Mode.");
			}

			// must be in control of character
			if (!Game.Player.CanControlCharacter)
			{
				ret = false;
				GTA.UI.Notification.Show("~r~Lap Timer: Player cannot control character. Cannot enter Race Mode");
			}

			// must be in a vehicle
			if (!Game.Player.Character.IsInVehicle())
			{
				ret = false;
				GTA.UI.Notification.Show("~r~Lap Timer: Player must be in a vehicle to enter Race Mode");
			}

			return ret;
		}



		/// <summary>
		/// Activate the provided SectorCheckpoint after deactivating the current active checkpoint. 
		/// By activating, a marker will be placed at the checkpoint, and timer will run until player is in range of the checkpoint.
		/// If the index is out of bounds (>= no. of checkpoints), either end the race or reset the lap.
		/// </summary>
		/// <param name="idx">List index of SectorCheckpoint to activate in <c>markedSectorCheckpoints</c></param>
		/// <returns>The now-active SectorCheckpoint</returns>
		private SectorCheckpoint activateRaceCheckpoint(int idx)
		{
			// deactivate current active checkpoint's marker
			try { markedSectorCheckpoints[activeSector].hideMarker(); }
			catch { }

			// detect if index is out of expected range
			if (idx >= markedSectorCheckpoints.Count && lapRace)
			{
				//// if point-to-point race, then race is completed. Print time and exit race mode.
				//if (!lapRace)
				//{
				//	lapFinishedHandler(activeCheckpoint);
				//	return activeCheckpoint;
				//}

				//// if lapped race, activate the 0th checkpoint
				//else
				//{
				//	idx = 0;
				//}
				idx = 0;
			}

			// set the new SectorCheckpoint as active (by index)
			activeSector = idx;
			activeCheckpoint = markedSectorCheckpoints[idx];

			// determine if this is the final checkpoint based on the index
			bool isFinal = activeCheckpoint.GetHashCode() == finishCheckpoint.GetHashCode(); //idx == markedSectorCheckpoints.Count - 1 || idx == 0;

			// the marker placed should be different, depending on whether this checkpoint is final
			if (isFinal && !lapRace)
				activeCheckpoint.marker = activeCheckpoint.placeMarker(MarkerType.raceFinish, idx);

			// if not final checkpoint, place a checkpoint w/ an arrow pointing to the next checkpoint
			else
			{
				Vector3 nextChkptPosition = getNextCheckpoint(idx).position;
				Vector3 nextChkptPosition2 = getNextCheckpoint(idx).position2;
				if (nextChkptPosition2 == Vector3.Zero) nextChkptPosition2 = nextChkptPosition;
				Vector3 lastChkptPosition = getPrevCheckpoint(idx).position;
				Vector3 lastChkptPosition2 = getPrevCheckpoint(idx).position2;
				if (lastChkptPosition2 == Vector3.Zero) lastChkptPosition2 = lastChkptPosition;
				activeCheckpoint.marker = activeCheckpoint.placeMarker(MarkerType.raceArrow, idx, nextChkptPosition, nextChkptPosition2, lastChkptPosition, lastChkptPosition2);
			}

			return activeCheckpoint;
		}



		/// <summary>
		/// Given the current checkpoint index, return the next checkpoint
		/// </summary>
		/// <param name="currentIndex">Index of current checkpoint</param>
		/// <returns>next <c>SectorCheckpoint</c></returns>
		private SectorCheckpoint getNextCheckpoint(int currentIndex)
		{
			return this.markedSectorCheckpoints[(currentIndex + 1) % this.markedSectorCheckpoints.Count];
		}
		private SectorCheckpoint getPrevCheckpoint(int currentIndex)
		{
			int lastIndex = (currentIndex == 0 ? this.markedSectorCheckpoints.Count : currentIndex) - 1;
			return this.markedSectorCheckpoints[lastIndex];
		}



		/// <summary>
		/// Determine whether the list of saved SectorCheckpooints are valid. Display the failure reason if invalid.
		/// </summary>
		/// <param name="chkpts">List of <c>SectorCheckpoint</c> to validate</param>
		/// <returns><c>true</c> if checkpoints are valid</returns>
		private bool validateCheckpoints(List<SectorCheckpoint> chkpts)
		{
			// there must be 2 or more checkpoints in the list
			if (chkpts.Count < 2)
			{
				GTA.UI.Notification.Show("~r~Lap Timer: Invalid route. You must place at 2 checkpoints in Placement Mode.");
				return false;
			}

			// if all criteria passed, checkpoints are valid
			return true;
		}



		/// <summary>
		/// 
		/// </summary>
		/// <param name="finalChkpt"><c>SectorCheckpoint</c> to extract timing summary from</param>
		/// <param name="lapRaceMode">if <c>true</c>, invoke exitRaceMode()</param>
		private void lapFinishedHandler(SectorCheckpoint finalChkpt, bool lapRaceMode = false)
		{
			// display on screen a summary of the race results
			GTA.UI.Screen.ShowSubtitle("Lap completed. ~n~" + finalChkpt.timing.getLatestTimingSummaryString(), 10000);

			// export timing sheet
			exportTimingSheet();

			// exit race mode if point-to-point (i.e. non-lapped) race
			if (!lapRaceMode)
				exitRaceMode();

			// otherwise, if lapped race, reset the timer
			else {
				lapStartTime = Game.GameTime;
             }

		}
		#endregion



		#region I/O

		/// <summary>
		/// Export the current race to JSON. User will be prompted to enter a name.
		/// </summary>
		public void exportRace()
		{
			// validate checkpoints to make sure the race is valid
			if (!validateCheckpoints(markedSectorCheckpoints))
			{
				GTA.UI.Notification.Show("~r~Lap Timer: cannot export race because validation failed.");
				return;
			}

			// prompt user to enter a name for the race
			string name = GTA.Game.GetUserInput("custom_race");

			// export the race using RaceExporter
			string fileName = RaceExporter.serializeToJson(RaceExporter.createExportableRace(name, markedSectorCheckpoints, dhprops, lapRace, markedSectorCheckpoints.Last().position), name);

			// inform user of the exported file
			GTA.UI.Notification.Show("Lap Timer: exported race as " + fileName);
		}



		/// <summary>
		/// Import a race from a file on disk. The currently placed checkpoints will be overwritten.
		/// </summary>
		public void importRace(string path = null)
		{
			// clean up any existing race/checkpoints
			if (raceMode) exitRaceMode();
			clearAllSectorCheckpoints();

			// set placement mode active; make sure player is not in race mode (exit if need to)
			placementMode = true;

			// prompt user to enter the name of the file (with or without the file extension) to import from
			string name = path == null ? GTA.Game.GetUserInput("custom_race") : path;

			try
			{
				// attempt to import from file
				ExportableRace race = RaceExporter.deserializeFromJson(name, path == null ? false : true);

				// repopulate List<SectorCheckpoint> using the imported race data
				lapRace = race.lapMode;
				spawn = race.spawn;
				raceName = race.name;
				dhprops = race.dhprops;
				for (int i = 0; i < race.checkpoints.Length; i++)
				{
					SimplifiedCheckpoint sc = race.checkpoints[i];
					int cpbs1 = 0;
					cpbs1 |= sc.cpbs1;
					cpbs1 |= Convert.ToInt32(sc.rndchk) << 1;
					cpbs1 |= Convert.ToInt32(sc.rndchks) << 2;
					float chs = sc.chs;
					float chs2 = sc.chs2;
					if (chs == 0.0f) chs = 1.0f;
					if (chs2 == 0.0f) chs2 = chs;
					SectorCheckpoint chkpt = new SectorCheckpoint(sc.number, sc.position, sc.quaternion, chs, sc.position2, sc.quaternion2, chs2, cpbs1, false);
					markedSectorCheckpoints.Add(chkpt);
				}

				// inform user of successful load
				GTA.UI.Notification.Show("Lap Timer: successfully imported race!");
				
				// with the race loaded & reconstructed, try to load timing sheet. make sure all hash codes match!
				int raceHash = GetHashCode();
				ExportableTimingSheet timingSheet = TimingSheetExporter.deserializeFromJson(raceHash.ToString());
				for (int i = 0; i < timingSheet.timingData.Length; i++)
					markedSectorCheckpoints[i].setTimingDataFromSimplified(timingSheet.timingData[i]);
				GTA.UI.Notification.Show("Lap Timer: successfully imported personal timing sheet for the imported race!");
			}
			catch { }
		}



		/// <summary>
		/// Serialize and export timing data of current checkpoints to JSON file.
		/// </summary>
		public void exportTimingSheet()
		{
			// compute the hash code of this race
			int raceHash = GetHashCode();

			// build instance of ExportableTimingSheet
			ExportableTimingSheet timingSheet = new ExportableTimingSheet(){
				exportDatetime = DateTime.UtcNow,
				raceHashCode = GetHashCode(),
				timingData = markedSectorCheckpoints.Select(chkpt => chkpt.getSimplifiedTimingData()).ToArray()
			};

			// export file
			TimingSheetExporter.serializeToJson(timingSheet, raceHash.ToString());
		}

		public bool mustLoadReplay = false;
		public ExportableReplay replay;
		public void importReplay(string path)
		{
			replayExporter.deserializeFromJson(this, path, true);
		}

		public void loadReplay() {
			if (tasRecordMode)
            {
				tasRecordToggle();
            }
			if (tasPlaybackMode)
            {
				tasPlaybackToggle();
            }
			tasRecEntries = replay.entries.ToList();
			mustLoadReplay = false;
			GTA.UI.Screen.ShowSubtitle("Replay Loaded");
		}

		public void exportReplay(bool askname = false)
        {
			if (tasRecEntries == null) return;

			// compute the hash code of this race
			int raceHash = GetHashCode();

			ExportableReplay replay = new ExportableReplay()
			{
				raceName = raceName,
				exportDatetime = DateTime.UtcNow,
				raceHashCode = GetHashCode(),
				entries = tasRecEntries.ToArray()
			};

			string name = raceName + "_" + raceHash.ToString("x8") + "_" + ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds().ToString();

			if (askname)
			{
				// prompt user to enter a name for the replay
				name = GTA.Game.GetUserInput(WindowTitle.EnterSynopsis, name, 125);

				char[] remove = System.IO.Path.GetInvalidFileNameChars();
				foreach (char c in remove)
				{
					name = name.Replace(c, '_');
				}
			}

			replayExporter.serializeToJson(replay, name);
		}

		/// <summary>
		/// Compute the hash code of the current race checkpoints list and other race settings.
		/// </summary>
		/// <returns>hash code of the current <c>markedSectorCheckpoints</c></returns>
		public override int GetHashCode()
		{
			int hash = 0;

			foreach (SectorCheckpoint chkpt in markedSectorCheckpoints)
				hash ^= chkpt.GetHashCode();

			if (lapRace) hash = hash << 1 + 1;

			return hash;
		}
		#endregion



		#region accessors
		/// <summary>
		/// Determines whether the current placement of checkpoints makes a valid race.
		/// </summary>
		public bool isValid
		{
			get	{ return validateCheckpoints(markedSectorCheckpoints); }
		}


		/// <summary>
		/// Get the final checkpoint of the race. Returns null if the race is invalid.
		/// For lapped races, the "finish checkpoint" is the 0th checkpoint. For non-lapped races,
		/// it is the last checkpoint in the list.
		/// </summary>
		public SectorCheckpoint finishCheckpoint
		{
			get {
				if (!isValid) return null;
				//return lapRace ? markedSectorCheckpoints[0] : markedSectorCheckpoints.Last();
				return markedSectorCheckpoints.Last();
			}
		}
		#endregion
	}
}
