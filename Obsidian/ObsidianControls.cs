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
		int tic = 0;
		float pi = (float)Math.PI;

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

			Dictionary<String, float> solarHome = new Dictionary<String, float>()
			{
				{"Azimuth", -90},
				{"Elevation 0", 90},
				{"Elevation 1", 0}
			};

			for (int i = 0; i < solarBlocks.Count; i++)
			{
				var block = solarBlocks[i] as IMyTerminalBlock;

				if (!block.CustomName.Contains("Arm"))
				{
					if (block is IMyPistonBase) solarPistons.Add(block as IMyPistonBase);

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

				if (block is IMyPistonBase) drillPistons.Add(block as IMyPistonBase);
				else if (block is IMyShipDrill) drills.Add(block as IMyShipDrill);
				else if (block.CustomName.Contains("Drill Rotor 1")) drillRotor1 = block as IMyMotorStator;
				else if (block.CustomName.Contains("Drill Rotor 2")) drillRotor2 = block as IMyMotorStator;
			}

			//Misc
			LCD = GridTerminalSystem.GetBlockWithName("Test Program") as IMyTextSurfaceProvider;

			_commands["solar"] = SolarToggle;
			_commands["da"] = DrillArmToggle;
			_commands["sd"] = StartDrilling;

		}

		public void Main(string argument, UpdateType updateSource)
		{
			tic++;

			debug = SleepMode() ? "sleeping\n" : "working\n";
			debug += solarMoving ? (solarRaising ? "solar raising\n" : "solar lowering\n") : "";
			debug += drillMoving ? (drillRaising ? "drill raising\n" : "drill lowering\n") : "";
			debug += "drill rotor 1 lock: " + drillRotor1.RotorLock.ToString()
					+ "\ndrill rotor 2 lock: " + drillRotor2.RotorLock.ToString();

			LCD.GetSurface(0).WriteText(debug);

			if (_commandLine.TryParse(argument))
			{
				Action commandAction;

				string command = _commandLine.Argument(0);

				if (_commands.TryGetValue(_commandLine.Argument(0), out commandAction)) commandAction();
				else { Echo($"Unknown command {command}"); }
			}

			if (SleepMode()) return;
			if (tic%2 == 0 && solarMoving) SolarToggle();
			else if (drillMoving) DrillArmToggle();

		}

		bool SleepMode()
		{
			return !(solarMoving || drillMoving);
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

				if (drillRaising) drillRotor2.TargetVelocityRPM = 2;
				else { drillRotor1.TargetVelocityRPM = -1; }
			}
			else
			{
				if (drillRaising)
				{
					if (!rotorMoving(drillRotor2, true))	//drill rotor 2 is up
					{
						if (!rotorMoving(drillRotor1, false))	//drill rotor 1 is down
						{
							drillRotor2.RotorLock = true;
							drillRotor1.TargetVelocityRPM = 1;
						}
						else if (!rotorMoving(drillRotor1, true))	//drill rotor 1 is up
						{
							drillRotor1.RotorLock = true;
							drillRotor1.SetValue<bool>("ShareInertiaTensor", true);
							drillMoving = false;
						}
						else { return; }
					}
					else { return; }
				}
				else
				{
					if (!rotorMoving(drillRotor1, false))	//drill rotor 1 is down
					{
						if (!rotorMoving(drillRotor2, true)) //drill rotor 2 is up
						{
							drillRotor1.RotorLock = true;
							drillRotor2.TargetVelocityRPM = -2;
						}
						else if (!rotorMoving(drillRotor2, false)) //drill rotor 2 is down
						{
							drillRotor2.RotorLock = true;
							drillRotor1.SetValue<bool>("ShareInertiaTensor", true);
							drillMoving = false;
						}
						else { return; }
					}
					else { return; }
				}
			}
		}

		public void StartDrilling()
		{
			
			foreach (IMyPistonBase piston in drillPistons) debug += ("\n" + piston.CustomName);
		}

		public void SolarToggle()
		{
			if (!solarMoving)	
			{
				solarMoving = true;
				solarRaising = !solarRaising;

				if (solarRaising) solarArmRotor.TargetVelocityRPM = -3;	//rotate arm up
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
							solarArmPiston.Velocity = 3;
						}
						else if (solarArmPiston.CurrentPosition == solarArmPiston.HighestPosition)
						{
							PanelExtend();
							solarMoving = false;
						}
						else { return; }
					}
					else { return; }
				}
				else
				{
					foreach (IMyMotorStator rotor in solarRotors.Keys)
					{
						if (rotorMoving(rotor)) return; //return if panels not in position
					}

					if (solarArmPiston.CurrentPosition == solarArmPiston.HighestPosition)
					{
						solarArmPiston.Velocity = -3;
					}
					else if (solarArmPiston.CurrentPosition == solarArmPiston.LowestPosition)
					{
						solarArmRotor.TargetVelocityRPM = 3;
						solarMoving = false;
					}
					else return;
				}
			}
		}

		bool rotorMoving(IMyMotorStator rotor)
		{
			float diff = Math.Abs(NormalizeRad(rotor.Angle - rotor.UpperLimitRad));
			if (diff < 0.01) return false;

			diff = Math.Abs(NormalizeRad(rotor.Angle - rotor.LowerLimitRad));
			if (diff < 0.01) return false;

			return true;
		}

		bool rotorMoving(IMyMotorStator rotor, bool upper)
		{
			Echo(rotor.CustomName + ": " + rotor.Angle);
			float diff = Math.Abs(NormalizeRad(rotor.Angle - (upper ? rotor.UpperLimitRad : rotor.LowerLimitRad)));
			return diff > 0.01;
		}

		void PanelRetract()
		{
			solarProgram.Enabled = false;

			foreach (IMyPistonBase piston in solarPistons) piston.Velocity = -1;
			foreach (KeyValuePair<IMyMotorStator, float> rotor in solarRotors)
			{
				RotateTo(rotor.Key, rotor.Value, 2);
			}
		}

		void PanelExtend()
		{
			/*
			foreach (IMyMotorStator rotor in solarRotors.Keys)
			{
				rotor.TargetVelocityRad = 0;
				rotor.UpperLimitRad = 999;
				rotor.LowerLimitRad = -999;
			}
			*/
			foreach (IMyPistonBase piston in solarPistons) piston.Velocity = 1;
			solarProgram.Enabled = true;
		}

		void RotateTo(IMyMotorStator rotor, float targetAngle, float speed)
		{
			float angleDiff = NormalizeRad(targetAngle - rotor.Angle);

			if (angleDiff > 0)
			{
				rotor.UpperLimitRad = targetAngle;
				rotor.LowerLimitRad = targetAngle - angleDiff - 1;
				rotor.TargetVelocityRPM = speed;
			}
			else
			{
				rotor.LowerLimitRad = targetAngle;
				rotor.UpperLimitRad = targetAngle - angleDiff + 1;
				rotor.TargetVelocityRPM = -speed;
			}
		}

		float NormalizeRad(float rad)
		{
			if (rad > pi) rad -= (2 * pi);
			if (rad < -pi) rad += (2 * pi);
			return rad;
		}

		float ToRad(float deg)
		{
			float rad = deg * pi / 180;
			return NormalizeRad(rad);
		}

		//to here
	}
}