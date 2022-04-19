using System;
using System.Drawing;

using GTA;
using GTA.Native;
using GTA.Math;

namespace LapTimer
{
	public class SectorCheckpoint
	{
		// defaults
		private Color defaultColor = Color.FromArgb(150, 255, 255, 150);
		private Color defaultColor2 = Color.FromArgb(150, 255, 150, 0);
		private Color defaultIconColor = Color.FromArgb(200, 150, 255, 255);

		// placement data (primary)
		public Vector3 position;			// Entity.Position
		public Quaternion quaternion;		// Entity.Quaternion
		public float chs;
		public Marker marker;
		// placement data (secondary)
		public Vector3 position2;			// Entity.Position
		public Quaternion quaternion2;		// Entity.Quaternion
		public float chs2;
		// other data
		public int number;
		public int cpbs1;
		private int _checkpointHashcode;

		// placement constants
		public const float checkpointRadius = 12.0f;
		public const float checkpointAirRadius = 22.5f;
		public const float zClamp = -9.25f; //random guess
		private readonly float doubleArrowThresh = 45.0f; //degrees
		private readonly float tripleArrowThresh = 90.0f; //degrees
		private readonly Vector3 checkpointOffset = new Vector3(0.0f, 0.0f, 0.0f); //new Vector3(0.0f, 0.0f, -1.0f);	// visually, the checkpoint will be offset by this vector

		// race data - all times are tracked as milliseconds
		public TimingData timing = new TimingData();



		#region publicMethods
		public SectorCheckpoint(int _number)
			: this(_number, Game.Player.Character.Position, Game.Player.Character.Quaternion)
		{ }

		public SectorCheckpoint(int _number, Vector3 pos, Quaternion quat, float _chs = 1.0f, Vector3? pos2 = null, Quaternion? quat2 = null, float _chs2 = 1.0f, int _cpbs1 = 0, bool verbose = true)
		{
			// assign metadata
			number = _number;
			position = pos;
			quaternion = quat;
			chs = _chs;
			position2 = pos2 ?? Vector3.Zero;
			quaternion2 = quat2 ?? Quaternion.Zero;
			chs2 = _chs2;
			cpbs1 = _cpbs1;

			// place a marker (placement mode)
			marker = placeMarker(MarkerType.placement, number);

			// compute and store the checkpoint's hashcode
			_checkpointHashcode = computeCheckpointHashcode();

			// debug printout if verbose
			if (verbose)
				GTA.UI.Notification.Show("Lap Timer: created checkpoint #" + number);
		}

		private float getAngle (Vector3 p1, Vector3 p2, Vector3 p3)
        {
			//c^2 = a^2 + b^2 - 2abcos(theta)
			float a = p1.DistanceTo2D(p2);
			float b = p2.DistanceTo2D(p3);
			float c2 = p1.DistanceToSquared2D(p3);
			if (a == 0f || b == 0f) return 0f;
			float res = (float) Math.Acos((a * a + b * b - c2) / (2 * a * b));  //0 <= Math.Acos(x) <= PI
			res *= 360f / (2f * (float) Math.PI); //to degrees
			res = 180f - res; //deviation from straight line
			return res;
        }

		private CheckpointIcon getCheckpointIcon(float angle, bool air)
        {
			if (angle > tripleArrowThresh) return air ? CheckpointIcon.RingTripleArrow : CheckpointIcon.CylinderTripleArrow3;
			else if (angle > doubleArrowThresh) return air ? CheckpointIcon.RingDoubleArrow : CheckpointIcon.CylinderDoubleArrow3;
			else return air ? CheckpointIcon.RingSingleArrow : CheckpointIcon.CylinderSingleArrow3;
        }

		/// <summary>
		/// Place a marker (pair of blip and checkpoint) at the current checkpoint's position.
		/// </summary>
		/// <param name="type">Indicate whether checkpoint is to be used in a race or in placement mode</param>
		/// <param name="number">Placement mode only: the number to display on the checkpoint</param>
		/// <param name="radius">Radius of the checkpoint, in meters</param>
		/// <param name="target">Race mode only: position of the next checkpoint, if applicable. Omit or pass in <c>null</c> if not applicable</param>
		public Marker placeMarker(MarkerType type, int number = 0, Vector3? target = null, Vector3? target2 = null, Vector3? last = null, Vector3? last2 = null, float radius = checkpointRadius)
		{
			// if the current instance of marker is already active, do nothing and return the instance
			if (marker.active == true)
				return marker;

			// instantiate empty Marker
			Marker newMarker = new Marker();


			//PRIMARY------------
			radius = radius * chs;
			if (isAirCheckpoint)
				radius = radius * (checkpointAirRadius/checkpointRadius);

			Vector3 positionAir = new Vector3(position[0], position[1], position[2]+radius/2.0f); //Center rings in the air, not on the ground
			
			float angle = getAngle(last ?? position, position, target ?? position);
			float angle2 = getAngle(last2 ?? position2, position2, target2 ?? position2);
			CheckpointIcon defaultIcon = getCheckpointIcon(angle, isAirCheckpoint);
			CheckpointIcon defaultIcon2 = getCheckpointIcon(angle2, isAirCheckpoint2);

			Vector3 _target = target ?? Vector3.Zero;
			// place a placement mode checkpoint
			if (type == MarkerType.placement)
			{
				newMarker.checkpoint = GTA.World.CreateCheckpoint(
									new GTA.CheckpointCustomIcon(CheckpointCustomIconStyle.Number, Convert.ToByte(number)),
									position + checkpointOffset, position + checkpointOffset,
									radius, defaultColor);
			}

			// place a regular race checkpoint
			else if (type == MarkerType.raceArrow)
				if (isAirCheckpoint)
				{
					newMarker.checkpoint = GTA.World.CreateCheckpoint(defaultIcon, //CheckpointIcon.RingDoubleArrow,
						positionAir + checkpointOffset, checkpointOffset + _target, radius, defaultColor);
				}
				else {
					newMarker.checkpoint = GTA.World.CreateCheckpoint(defaultIcon, //CheckpointIcon.CylinderDoubleArrow,
						position + checkpointOffset, checkpointOffset + _target, radius, defaultColor);
				}
			else if (type == MarkerType.raceFinish)
            {
				if (isAirCheckpoint)
				{
					newMarker.checkpoint = GTA.World.CreateCheckpoint(CheckpointIcon.RingCheckerboard,
							positionAir + checkpointOffset, position + checkpointOffset, radius, defaultColor);
				}
				else
                {
					newMarker.checkpoint = GTA.World.CreateCheckpoint(CheckpointIcon.CylinderCheckerboard3,
							position + checkpointOffset, position + checkpointOffset, radius, defaultColor);
				}
            }
			
			//doesn't work
			//newMarker.checkpoint.IconColor = defaultIconColor;

			// create blip
			newMarker.blip = GTA.World.CreateBlip(position);
			newMarker.blip.NumberLabel = number;
			//-----------------------
			//SECONDARY
			if (hasSecondaryCheckpoint) {
				float radius2 = checkpointRadius * chs2;
				if (isAirCheckpoint2)
					radius2 = radius2 * (checkpointAirRadius/checkpointRadius);

				Vector3 positionAir2 = new Vector3(position2[0], position2[1], position2[2]+radius2/2.0f);

				target2 = target2 ?? _target;
				if (target2.Equals(Vector3.Zero)) target2 = _target;
				// place a placement mode checkpoint
				if (type == MarkerType.placement)
				{
					newMarker.checkpoint2 = GTA.World.CreateCheckpoint(
										new GTA.CheckpointCustomIcon(CheckpointCustomIconStyle.Number, Convert.ToByte(number)),
										position2 + checkpointOffset, position2 + checkpointOffset,
										radius2, defaultColor2);
				}

				// place a regular race checkpoint
				else if (type == MarkerType.raceArrow)
				{
					if (isAirCheckpoint2)
					{
						newMarker.checkpoint2 = GTA.World.CreateCheckpoint(defaultIcon2, //CheckpointIcon.RingDoubleArrow,
							positionAir2 + checkpointOffset, checkpointOffset + target2 ?? new Vector3(0, 0, 0), radius2, defaultColor2);
					}
					else
					{
						newMarker.checkpoint2 = GTA.World.CreateCheckpoint(defaultIcon2, //CheckpointIcon.CylinderDoubleArrow,
							position2 + checkpointOffset, checkpointOffset + target2 ?? new Vector3(0, 0, 0), radius2, defaultColor2);
					}
				}

				else if (type == MarkerType.raceFinish)
				{
					if (isAirCheckpoint2)
					{
						newMarker.checkpoint2 = GTA.World.CreateCheckpoint(CheckpointIcon.RingCheckerboard,
							positionAir2 + checkpointOffset, position2 + checkpointOffset, radius2, defaultColor2);
					}
					else
					{
						newMarker.checkpoint2 = GTA.World.CreateCheckpoint(CheckpointIcon.CylinderCheckerboard3,
							position2 + checkpointOffset, position2 + checkpointOffset, radius2, defaultColor2);
					}
				}
			

				// create blip
				newMarker.blip2 = GTA.World.CreateBlip(position2);
				newMarker.blip2.NumberLabel = number;
				newMarker.blip2.Color = BlipColor.Orange;
			}
			//-----------------------------------


			// flag the marker as active and return this instance of Marker
			newMarker.active = true;
			return newMarker;
		}

		public bool isAirCheckpoint
        {
			get
            {
				return Convert.ToBoolean(cpbs1 & 0b10);
            }
        }

		public bool isAirCheckpoint2
        {
			get
            {
				return Convert.ToBoolean(cpbs1 & 0b100);
            }
        }

		public bool hasSecondaryCheckpoint
        {
            get
            {
				return !(position2.Equals(Vector3.Zero));
            }
        }

		/// <summary>
		/// Clear active marker of this checkpoint
		/// </summary>
		public void hideMarker()
		{
			if (marker.active)
			{
				// The game apparently won't create any more checkpoints if there are too
				// many loaded, leading to potentially null marker.checkpoint values.
				marker.checkpoint?.Delete(); 
				marker.blip.Delete();
				if (hasSecondaryCheckpoint) {
					marker.checkpoint2?.Delete();
					marker.blip2.Delete();
				}
			}
			marker.active = false;
		}



		/// <summary>
		/// Get the hashcode of this checkpoint.
		/// The hashcode depends on the checkpoint's position, quaternion (orientation), and number.
		/// </summary>
		/// <returns>hashcode for this instance of <c>SectorCheckpoint</c></returns>
		public override int GetHashCode()
		{
			return _checkpointHashcode;
		}



		public SimplifiedCheckpointTimingData getSimplifiedTimingData()
		{
			return new SimplifiedCheckpointTimingData()
			{
				fastestTime = timing.fastestTime,
				vehicleFastestTime = timing.vehicleFastestTime,
				checkpointHashcode = _checkpointHashcode
			};
		}



		public bool setTimingDataFromSimplified(SimplifiedCheckpointTimingData timingData)
		{
			if (timingData.checkpointHashcode != GetHashCode())
				return false;

			timing.fastestTime = timingData.fastestTime;
			timing.vehicleFastestTime = timingData.vehicleFastestTime;
			return true;
		}
		#endregion



		/// <summary>
		/// Compute hashcode based on the checkpoint's position, quaternion (orientation), and number.
		/// </summary>
		/// <returns>hashcode for this instance of <c>SectorCheckpoint</c></returns>
		private int computeCheckpointHashcode()
		{
			return unchecked((position.GetHashCode() + quaternion.GetHashCode() + position2.GetHashCode() + quaternion2.GetHashCode()) * number);
		}
	}





	public struct Marker
	{
		public Blip blip;
		public Checkpoint checkpoint;
		public Blip blip2;
		public Checkpoint checkpoint2;
		public bool active;
	}

	public enum MarkerType
	{
		placement,
		raceArrow,
		raceFinish,
		//raceAirArrow,
		//raceAirFinish,
	}
}
