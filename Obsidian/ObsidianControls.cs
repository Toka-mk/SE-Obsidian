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
		List<IMyPistonBase> solarPistons = new List<IMyPistonBase>();
		Dictionary<IMyMotorStator, float> solarRotors = new Dictionary<IMyMotorStator, float>();

		IMyProgrammableBlock solarProgram;
		IMyPistonBase solarArmPiston;
		IMyMotorStator solarArmRotor;

		bool solarMoving = false;
		bool solarRaising = false;

		//Drill Assembly
		List<IMyTerminalBlock> drillBlocks = new List<IMyTerminalBlock>();
		List<IMyPistonBase> drillPistons = new List<IMyPistonBase>();
		List<IMyShipDrill> drills = new List<IMyShipDrill>();

		IMyMotorStator drillRotor1;
		IMyMotorStator drillRotor2;

		bool drillMoving = false;
		bool drillRaising = false;

		//Misc
		bool sleep = true;

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

			Dictionary<String, float> solarHome = new Dictionary<String, float>() {
				{"Azimuth", -90},
				{"Elevation 0", 90},
				{"Elevation 1", 0}
			};

			for (int i = 0; i < solarBlocks.Count; i++)
			{
				var block = solarBlocks[i] as IMyTerminalBlock;

				if (!block.CustomName.Contains("Arm"))
				{
					if (block is IMyPistonBase) { solarPistons.Add(block as IMyPistonBase); }

					else if (block is IMyMotorStator)
					{
						foreach (KeyValuePair<String, float> rotor in solarHome)
						{
							if (block.CustomName.Contains(rotor.Key))
							{
								solarRotors[block as IMyMotorStator] = ToRad(rotor.Value);
							}
						}
					}
				}
			}

			//Drill Aseembly
			IMyBlockGroup drillGroup = GridTerminalSystem.GetBlockGroupWithName("[Obsidian] Drill Assembly");
			drillGroup.GetBlocks(drillBlocks);

			for (int i = 0; i < drillBlocks.Count; i++)
			{
				var block = drillBlocks[i] as IMyTerminalBlock;

				if (block is IMyPistonBase) { drillPistons.Add(block as IMyPistonBase); }
				else if (block is IMyShipDrill) { drills.Add(block as IMyShipDrill); }
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
			debug = (sleep ? "sleeping" : (solarRaising ? "Solar Raising" : "Solar Lowering"));
			LCD.GetSurface(0).WriteText(debug);

			foreach (KeyValuePair<IMyMotorStator, float> rotor in solarRotors)
			{
				Echo(rotor.Key.CustomName + rotor.Value);
			}

			if (_commandLine.TryParse(argument))
			{
				Action commandAction;

				string command = _commandLine.Argument(0);

				if (_commands.TryGetValue(_commandLine.Argument(0), out commandAction)) { commandAction(); }
				else { Echo($"Unknown command {command}"); }
			}

			if (sleep) { return; }

			if (solarMoving) { SolarToggle(); }

			//if (drillMoving) { DrillArmToggle(); }
		}

		public void DrillArmToggle()
		{
			if (!drillMoving)
			{
				drillMoving = true;
				drillRaising = !drillRaising;

				drillRotor1.RotorLock = false;
				drillRotor1.SetValue<bool>("ShareInertiaTensor", false);
				drillRotor2.RotorLock = false;

				if (drillRaising) { drillRotor2.TargetVelocityRPM = -2; }
				else { drillRotor1.TargetVelocityRPM = 1; }
			}
			else
			{
				if (drillRaising)
				{
					if (drillRotor2.Angle == drillRotor2.LowerLimitRad)
					{
						if (drillRotor1.Angle == drillRotor1.UpperLimitRad)
						{
							drillRotor2.RotorLock = true;
							drillRotor1.TargetVelocityRPM = -1;
						}
						else
						{
							drillRotor1.RotorLock = true;
							drillRotor1.SetValue<bool>("ShareInertiaTensor", false);
							drillMoving = false;
						}
					}
					else { return; }
				}
				else
				{
					//if (drillRotor1.Angle != drillRotor1.UpperLimitRad) { return; }
					//else if (drillrotor)
				}
			}

			/*
				if (!drillMoving && !drillRaising)
				{
					drillMoving = true;
					drillRaising = true;
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
			*/
		}

		public void StartDrilling()
		{
			foreach (IMyPistonBase piston in drillPistons) { debug += ("\n" + piston.CustomName); }
		}

		public void SolarToggle()
		{
			if (!solarMoving)
			{
				sleep = false;
				solarMoving = true;
				solarRaising = !solarRaising;

				if (solarRaising) { solarArmRotor.TargetVelocityRPM = -3; } //rotate arm
				else { PanelRetract(); }
			}
			else
			{
				if (solarRaising)
				{
					if (!rotorMoving(solarArmRotor, false)) //arm rotation complete
					{
						if (solarArmPiston.CurrentPosition == solarArmPiston.LowestPosition)
						{
							solarArmPiston.Extend();
						}
						else if (solarArmPiston.CurrentPosition == solarArmPiston.HighestPosition)
						{
							PanelExtend();
							solarMoving = false;
							sleep = true;
						}
						else { return; }
					}
					else { return; }
				}
				else
				{
					foreach (IMyMotorStator rotor in solarRotors.Keys)
					{
						if (rotorMoving(rotor)) { return; } //return if panels not in position
					}

					if (solarArmPiston.CurrentPosition == solarArmPiston.HighestPosition)
					{
						solarArmPiston.Retract();
					}
					else if (solarArmPiston.CurrentPosition == solarArmPiston.LowestPosition)
					{
						solarArmRotor.TargetVelocityRPM = 3;
						solarMoving = false;
						sleep = true;
					}
					else { return; }
				}
			}
		}

		/*
		public void SolarToggle()
		{
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
		}*/

		bool rotorMoving(IMyMotorStator rotor)
		{
			return (Math.Abs(rotor.Angle - rotor.UpperLimitRad) > 0.01 && Math.Abs(rotor.Angle - rotor.LowerLimitRad) > 0.01);
		}

		bool rotorMoving(IMyMotorStator rotor, bool upper)
		{
			if (upper) { return Math.Abs(rotor.Angle - rotor.UpperLimitRad) > 0.01; }
			else { return Math.Abs(rotor.Angle - rotor.LowerLimitRad) > 0.01; }
		}

		void PanelRetract()
		{
			solarProgram.Enabled = false;

			foreach (IMyPistonBase piston in solarPistons) { piston.Retract(); }
			foreach (KeyValuePair<IMyMotorStator, float> rotor in solarRotors)
			{
				RotateTo(rotor.Key, rotor.Value, 2);
			}
		}

		void PanelExtend()
		{
			foreach (IMyPistonBase piston in solarPistons) { piston.Extend(); }
			foreach (IMyMotorStator rotor in solarRotors.Keys)
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