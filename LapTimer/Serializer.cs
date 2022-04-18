using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GTA;
using GTA.Native;
using GTA.Math;

using System.Text.RegularExpressions;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;


namespace LapTimer
{
	class RaceExporter
	{
		protected const string rootPath = "./scripts/LapTimer/races/";
		protected const string fileExt = ".json";
		protected const string scriptVersion = "v3.0";
		
		/// <summary>
		/// Create an instance of <c>ExportableRace</c> with data provided.
		/// </summary>
		/// <param name="name">Name of race</param>
		/// <param name="chkpts">List of SectorCheckpoints</param>
		/// <param name="lapMode">Whether the race should run in lap mode</param>
		/// <returns></returns>
		public static ExportableRace createExportableRace (string name, List<SectorCheckpoint> chkpts, bool lapMode, Vector3 spawn){
			// create new instance of ExportableRace & set metadata
			ExportableRace race = new ExportableRace();
			race.name = name;
			race.lapMode = lapMode;
			race.numCheckpoints = chkpts.Count;
			race.version = scriptVersion;
			race.spawn = spawn;

			// iterate over list of SectorCheckpoints and simplify each before adding to ExportableRace
			race.checkpoints = new SimplifiedCheckpoint[chkpts.Count];
			for (int i = 0; i < chkpts.Count; i++)
			{
				SimplifiedCheckpoint sc = new SimplifiedCheckpoint();
				sc.position = chkpts[i].position;
				sc.quaternion = chkpts[i].quaternion;
				sc.chs = chkpts[i].chs;
				sc.position2 = chkpts[i].position2;
				sc.quaternion2 = chkpts[i].quaternion2;
				sc.chs2 = chkpts[i].chs2;
				sc.number = chkpts[i].number;
				sc.cpbs1 = chkpts[i].cpbs1;
				race.checkpoints[i] = sc;
			}

			return race;
		}



		/// <summary>
		/// Serialize and write given object to JSON.
		/// </summary>
		/// <param name="obj">Object to serialize</param>
		/// <param name="fileName">Name of file (without extension)</param>
		/// <returns></returns>
		public static string serializeToJson(object obj, string fileName)
		{
			// create output filestream
			fileName = getFilePath(fileName);
			System.IO.FileStream file = System.IO.File.Create(rootPath + fileName);

			// instantiate JSON serializer
			var serializer = new DataContractJsonSerializer(obj.GetType());
			serializer.WriteObject(file, obj);

			// close file stream & return file name
			file.Close();
			return fileName;
		}
		


		/// <summary>
		/// Deserialize <c>ExportableRace</c> from a JSON file
		/// </summary>
		/// <param name="fileName">name of JSON file to read from</param>
		/// <returns></returns>
		public static ExportableRace deserializeFromJson (string fileName, bool exactPath = false)
		{
			try {
				// attempt to open the file for reading
				fileName = getFilePath(fileName);
				string filePath = exactPath ? fileName : rootPath + fileName;	// if exactPath is false, then prepend rootPath
				System.IO.FileStream file = System.IO.File.OpenRead(filePath);
				
				// instantiate JSON deserializer
				var deserializer = new DataContractJsonSerializer(typeof(ExportableRace));
				return (ExportableRace) deserializer.ReadObject(file);
			}
			catch {
				GTA.UI.Screen.ShowSubtitle("~r~Lap Timer: failed to load race.");
				throw;
			}
		}

		


		/// <summary>
		/// Get a List of <c>ImportableRace</c> from the default script output directory.
		/// </summary>
		/// <returns>List of <c>ImportableRace</c></returns>
		public static List<ImportableRace> getImportableRaces(string subdir = null)
		{
			subdir = subdir ?? rootPath;
			// get all .json files in the script directory
			string[] files = Directory.GetFiles(subdir, "*.json");

			// instantiate list of importable races
			List<ImportableRace> races = new List<ImportableRace>();

			// attempt to deserialize each file to ImportableRace
			foreach (string fileName in files)
			{
				try
				{
					// attempt to deserialize to ImportableRace
					System.IO.FileStream fs = System.IO.File.OpenRead(fileName);
					DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(ImportableRace));
					ImportableRace race = (ImportableRace)deserializer.ReadObject(fs);

					// validate the ImportableRace instance; add to races if valid
					race.filePath = fileName;
					races.Add(race);
				}
				catch { throw; }

			}

			return races;
		}

		public static string[] getSubdirectories(string currentdir = null)
        {
			currentdir = currentdir ?? rootPath;
			string[] dirs = Directory.GetDirectories(currentdir);
			return dirs;
        }

		protected static string getFilePath(string fileName)
		{
			// replace all non-alphanumeric characters (special chars & whitespace chars) with underscore
			Regex.Replace(fileName, @"\W+", "_");

			// append file extension, if it is not there already
			if (!fileName.EndsWith(fileExt)) fileName += fileExt;

			return fileName;
		}
	}



	class TimingSheetExporter : RaceExporter
	{
		new protected const string rootPath = "./scripts/LapTimer/timing_sheets/";


		public static string serializeToJson(ExportableTimingSheet obj, string fileName)
		{
			// create output filestream
			fileName = getFilePath(fileName);
			System.IO.FileStream file = System.IO.File.Create(rootPath + fileName);

			// instantiate JSON serializer
			var serializer = new DataContractJsonSerializer(typeof(ExportableTimingSheet));
			serializer.WriteObject(file, obj);

			// close file stream & return file name
			file.Close();
			return fileName;
		}



		/// <summary>
		/// Deserialize <c>ExportableTimingSheet</c> from a JSON file
		/// </summary>
		/// <param name="fileName">name of JSON file to read from</param>
		/// <returns></returns>
		new public static ExportableTimingSheet deserializeFromJson(string fileName, bool exactPath = false)
		{
			try
			{
				// attempt to open the file for reading
				fileName = getFilePath(fileName);
				string filePath = exactPath ? fileName : rootPath + fileName;	// if exactPath is false, then prepend rootPath
				System.IO.FileStream file = System.IO.File.OpenRead(filePath);

				// instantiate JSON deserializer
				var deserializer = new DataContractJsonSerializer(typeof(ExportableTimingSheet));
				return (ExportableTimingSheet)deserializer.ReadObject(file);
			}
			catch
			{
				throw;
			}
		}


	}

	class replayExporter : RaceExporter
    {
		new protected const string rootPath = "./scripts/LapTimer/replays/";

		public static string serializeToJson(ExportableReplay obj, string fileName)
		{
			fileName = getFilePath(fileName) + ".gz";

			//Prevents lag spikes on export
			new Thread(() => {
				// create output filestream
				System.IO.FileStream file = System.IO.File.Create(rootPath + fileName);
				System.IO.Compression.GZipStream filegz = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionLevel.Optimal);

				// instantiate JSON serializer
				var serializer = new DataContractJsonSerializer(typeof(ExportableReplay));
				serializer.WriteObject(filegz, obj);
				// close file stream
				filegz.Close();
				file.Close();
			}).Start();
			

			
			return fileName;
		}

		new public static ExportableReplay deserializeFromJson(string fileName, bool exactPath = false)
		{
			try
			{
				// attempt to open the file for reading
				//fileName = getFilePath(fileName);
				string filePath = exactPath ? fileName : rootPath + fileName;   // if exactPath is false, then prepend rootPath
				System.IO.FileStream file = System.IO.File.OpenRead(filePath);// + ".gz");
				System.IO.Compression.GZipStream filegz = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);

				// instantiate JSON deserializer
				var deserializer = new DataContractJsonSerializer(typeof(ExportableReplay));
				return (ExportableReplay)deserializer.ReadObject(filegz);
			}
			catch
			{
				throw;
			}
		}

		public static List<ImportableReplay> getImportableReplays()
		{
			// get all .json.gz files in the script directory
			string[] files = Directory.GetFiles(rootPath, "*.json.gz");

			// instantiate list of importable replays
			List<ImportableReplay> replays = new List<ImportableReplay>();

			// attempt to deserialize each file to ImportableReplay
			foreach (string fileName in files)
			{
				try
				{
					// attempt to deserialize to ImportableReplay
					//System.IO.FileStream fs = System.IO.File.OpenRead(fileName);
					//DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(ImportableReplay));
					//ImportableReplay replay = (ImportableReplay)deserializer.ReadObject(fs);
					ImportableReplay replay = new ImportableReplay();

					// validate the ImportableReplay instance; add to replays if valid
					replay.filePath = fileName;
					replays.Add(replay);
				}
				catch { throw; }

			}

			return replays;
		}
	}

	public struct ImportableRace
	{
		public string version;
		public string name;
		public string description;
		public bool lapMode;

		public string filePath;
	}


	public struct ExportableRace
	{
		// metadata
		public string version;	// script version that the race was exported from/intended for
		public string name;		// name of the race
		public string description;

		public Vector3 spawn;
		public bool lapMode;
		public int numCheckpoints;
		public SimplifiedCheckpoint[] checkpoints;
	}


	public struct SimplifiedCheckpoint
	{
		public Vector3 position;
		public Quaternion quaternion;
		public float chs; //Checkpoint size
		public Vector3 position2;
		public Quaternion quaternion2;
		public float chs2; //Checkpoint size (secondary)
		public int cpbs1; //Checkpoint bit flags (both)
		public bool rndchk; //Round checkpoints
		public bool rndchks; //Round checkpoints (secondary)
		public int number;
	}


	public struct ExportableTimingSheet
	{
		public DateTime exportDatetime;
		public SimplifiedCheckpointTimingData[] timingData;
		public int raceHashCode;
	}


	public struct SimplifiedCheckpointTimingData
	{
		public int fastestTime;
		public Dictionary<string, int> vehicleFastestTime;
		public int checkpointHashcode;
	}
	public struct ImportableReplay
    {
		public string filePath;
    }
	public struct ExportableReplay
    {
		public string raceName;
		public DateTime exportDatetime;
		public tasRecEntry[] entries;
		public int raceHashCode;
	}

	public struct tasRecEntry
	{
		//Timing
		public float frametime;
		public int gametime;
		public int offset;
		public int exitplaybacktime;
		//Position, Rotation, Velocity
		public Vector3 pos;
		public Quaternion quat;
		public Vector3 vel;
		public Vector3 rvel;
		//Driving properties
		public float brakepower;
		public float clutch;
		public int currentgear;
		public float rpm;
		public int highgear;
		public bool burnout; //does not work
		public int nextgear;
		public float steeringangle;
		public float steeringscale;
		public float throttle;
		public float throttlepower;
		public float turbo;
		//Wheels
		public wheelProperties[] wheels;
		//Vehicle controls
		public vehControls controls;

	}

/**	public struct wheelProperties
    {
		public float brakepressure;
		public int flags;
		public float health;
		public float power;
		public float rimcollidersize;
		public float rotationspeed;
		public float tirecollidersize;
		public float tirecolliderwidth;
		public float tractionvectorlength;
		public float xoffset;
		//public float xrot;
		public float yrot;
    }**/

	public struct wheelProperties
    {
		public int[] memory;
    }
	public struct vehControls
    {
		public float vehlr;
		public float vehud;
		//public float vehspecial;
		public float vehaccel;
		public float vehbrake;
		public float vehhandbrake;
		//public float vehhorn;
		public float vehflyaccel;
		public float vehflybrake;
		public float vehflyyawleft;
		public float vehflyyawright;
		public float vehfranklinspecial;
		//public float vehstuntud; //???? stunt cam speed?
		//public float vehroof;
		public float vehjump;
		//public float vehgrapplinghook;
		//public float vehshuffle;
		public float vehflyrolllr;
		public float vehflypitchud;
		public float vehflyverticalmode;
		//no subs or bicycles
		public float vehcarjump;
		public float vehrocketboost;
		public float vehflyboost;
		public float vehparachute;
		public float vehbikewings;
		public float vehtransform;
    }
}
