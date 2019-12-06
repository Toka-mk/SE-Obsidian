using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{

		//from here

		MyCommandLine _commandLine = new MyCommandLine();
		Dictionary<string, Action> _commands = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

		//Solar Assembly
		List<IMyTerminalBlock> solarBlocks = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> solarPistons = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> solarRotors = new List<IMyTerminalBlock>();
		Dictionary<string, float> solarHome = new Dictionary<string, float>();

		IMyProgrammableBlock solarProgram;
		IMyPistonBase solarArmPiston;
		IMyMotorStator solarArmRotor;

		string solarStatus = "down";

		//Drill Assembly
		List<IMyTerminalBlock> drillBlocks = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> drillPistons = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> drills = new List<IMyTerminalBlock>();

		IMyMotorStator drillRotor1;
		IMyMotorStator drillRotor2;

		string drillStatus = "down";

		//Misc
		int tic = 0;
		IMyTextSurfaceProvider LCD;
		string debug = "";

		public Program()
		{
			Runtime.UpdateFrequency = UpdateFrequency.Update10;

			//Solar Assembly
			IMyBlockGroup solarGroup = GridTerminalSystem.GetBlockGroupWithName("[Obsidian] Solar Assembly");
			solarGroup.GetBlocks(solarBlocks);

			solarProgram = GridTerminalSystem.GetBlockWithName("Solar Program") as IMyProgrammableBlock;
			solarArmPiston = GridTerminalSystem.GetBlockWithName("Solar Arm Piston") as IMyPistonBase;
			solarArmRotor = GridTerminalSystem.GetBlockWithName("Solar Arm Rotor") as IMyMotorStator;

			for (int i = 0; i < solarBlocks.Count; i++)
			{
				var block = solarBlocks[i] as IMyTerminalBlock;

				if (!block.CustomName.Contains("Arm"))
				{
					if (block is IMyPistonBase) { solarPistons.Add(block); }
					else if (block is IMyMotorStator) { solarRotors.Add(block); }
				}
			}

			solarHome["Azimuth"] = ToRad(-90);
			solarHome["Elevation 0"] = ToRad(90);
			solarHome["Elevation 1"] = ToRad(0);
			
			//Drill Aseembly
			IMyBlockGroup drillGroup = GridTerminalSystem.GetBlockGroupWithName("[Obsidian] Drill Assembly");
			drillGroup.GetBlocks(drillBlocks);

			for (int i = 0; i < drillBlocks.Count; i++)
			{
				var block = drillBlocks[i] as IMyTerminalBlock;

				if (block is IMyPistonBase) { drillPistons.Add(block); }
				else if (block is IMyShipDrill) { drills.Add(block); }
				else if (block.CustomName.Contains("Drill Rotor 1")) { drillRotor1 = block as IMyMotorStator; }
				else if (block.CustomName.Contains("Drill Rotor 2")) { drillRotor2 = block as IMyMotorStator; }
			}

			//Misc
			LCD = GridTerminalSystem.GetBlockWithName("Test Program") as IMyTextSurfaceProvider;

			_commands["solar"] = SolarToggle;
			_commands["da"] = DrillArmToggle;
			_commands["sd"] = StartDrilling;

		}

		public void Main(string argument, UpdateType updateSource)
		{
			debug = drillStatus + "\n" + drillRotor1.RotorLock + "\n" + drillRotor2.RotorLock;
			LCD.GetSurface(0).WriteText(debug);

			if (tic % 2 == 0) { if (solarStatus != "up" && solarStatus != "down") { SolarToggle(); } }
			else { if (drillStatus == "raising" || drillStatus == "lowering") { DrillArmToggle(); } }

			if (_commandLine.TryParse(argument))
			{
				Action commandAction;

				string command = _commandLine.Argument(0);

				if (_commands.TryGetValue(_commandLine.Argument(0), out commandAction)) { commandAction(); }
				else { Echo($"Unknown command {command}"); }
			}
		}
		
		public void DrillArmToggle()
		{
			if (drillStatus == "down")
			{
				drillStatus = "raising";
				drillRotor1.RotorLock = false;
				drillRotor1.SetValue<bool>("ShareInertiaTensor", false);
				drillRotor1.TargetVelocityRPM = -1;
				drillRotor2.RotorLock = false;
				drillRotor2.TargetVelocityRPM = -1;
			}
			else if (drillStatus == "raising")
			{
				if (RotorMoving(drillRotor1) || RotorMoving(drillRotor2)) { return; }
				drillStatus = "up";
				drillRotor1.RotorLock = true;
				drillRotor1.SetValue<bool>("ShareInertiaTensor", true);
				drillRotor2.RotorLock = true;
			}
			else if (drillStatus == "up")
			{
				drillStatus = "lowering";
				drillRotor1.RotorLock = false;
				drillRotor1.SetValue<bool>("ShareInertiaTensor", false);
				drillRotor1.TargetVelocityRPM = 1;
				drillRotor2.RotorLock = false;
				drillRotor2.TargetVelocityRPM = 1;
			}
			else if (drillStatus == "lowering")
			{
				if (RotorMoving(drillRotor1) || RotorMoving(drillRotor2)) { return; }
				drillStatus = "down";
				drillRotor1.RotorLock = true;
				drillRotor1.SetValue<bool>("ShareInertiaTensor", true);
				drillRotor2.RotorLock = true;
			}
		}
		
		public void StartDrilling()
		{
			foreach (IMyPistonBase piston in drillPistons) { debug += ("\n" + piston.CustomName); }
		}
		
		public void SolarToggle()
		{
			//retract solar
			if (solarStatus == "up")
			{
				solarStatus = "retracting panels";
				PanelRetract();
			}
			else if (solarStatus == "retracting panels")
			{
				bool cont = true;

				foreach (IMyMotorStator rotor in solarRotors)
				{
					if (RotorMoving(rotor)) { cont = false; }
				}

				if (cont)
				{
					solarStatus = "retracting arm";
					solarArmPiston.Retract();
				}
			}
			else if (solarStatus == "retracting arm" && solarArmPiston.CurrentPosition == solarArmPiston.LowestPosition)
			{
				solarStatus = "down";
				solarArmRotor.TargetVelocityRPM = 3;
			}

			//extend solar
			else if (solarStatus == "down")
			{
				solarStatus = "rotating arm";
				solarArmRotor.TargetVelocityRPM = -3;
			}
			else if (solarStatus == "rotating arm" && solarArmRotor.Angle - solarArmRotor.LowerLimitRad < 0.01)
			{
				solarStatus = "extending arm";
				solarArmPiston.Extend();
			}
			else if (solarStatus == "extending arm" && solarArmPiston.CurrentPosition == solarArmPiston.HighestPosition)
			{
				solarStatus = "up";
				PanelExtend();
			}
		}

		bool RotorMoving(IMyMotorStator rotor)
		{
			if (rotor.UpperLimitDeg >= 360 && rotor.LowerLimitDeg <= -360 && rotor.TargetVelocityRPM != 0) { return true; }
			else if (Math.Abs(rotor.Angle - rotor.UpperLimitRad) > 0.01 && Math.Abs(rotor.Angle - rotor.LowerLimitRad) > 0.01) { return true; }
			return false;
		}

		void PanelRetract()
		{
			solarProgram.Enabled = false;

			foreach (IMyPistonBase piston in solarPistons) { piston.Retract(); }
			foreach (IMyMotorStator rotor in solarRotors)
			{
				foreach (KeyValuePair<string, float> homePos in solarHome)
				{
					if (rotor.CustomName.Contains(homePos.Key))
					{
						RotateTo(rotor, homePos.Value, 2);
					}
				}
			}
		}

		void PanelExtend()
		{
			foreach (IMyPistonBase piston in solarPistons) { piston.Extend(); }
			foreach (IMyMotorStator rotor in solarRotors)
			{
				rotor.TargetVelocityRPM = 0;
				rotor.LowerLimitDeg = -1000;
				rotor.UpperLimitDeg = 1000;
			}
			solarProgram.Enabled = true;
		}

		void RotateTo(IMyMotorStator rotor, float targetAngle, float speed)
		{
			float pi = (float)Math.PI;
			float angleDiff = targetAngle - rotor.Angle;

			while (angleDiff > pi) { angleDiff -= (2 * pi); }
			while (angleDiff < -pi) { angleDiff += (2 * pi); }

			if (angleDiff > 0)
			{
				rotor.UpperLimitRad = targetAngle;
				rotor.TargetVelocityRPM = speed;
			}
			else
			{
				rotor.LowerLimitRad = targetAngle;
				rotor.TargetVelocityRPM = -speed;
			}
		}

		float ToRad(float deg)
		{
			while (deg < 0) { deg += 360; }
			return deg * (float)Math.PI / 180;
		}

		//to here
	}
}