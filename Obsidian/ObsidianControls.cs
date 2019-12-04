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

		List<IMyTerminalBlock> solarBlocks = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> solarPistons = new List<IMyTerminalBlock>();
		List<IMyTerminalBlock> solarRotors = new List<IMyTerminalBlock>();
		Dictionary<string, float> solarHome = new System.Collections.Generic.Dictionary<string, float>();

		IMyProgrammableBlock solarProgram;
		IMyPistonBase solarArmPiston;
		IMyMotorStator solarArmRotor;

		string solarStatus = "retracted";

		public Program()
		{
			Runtime.UpdateFrequency = UpdateFrequency.Update10;

			IMyBlockGroup solarGroup = GridTerminalSystem.GetBlockGroupWithName("[Obsidian] Solar");
			solarGroup.GetBlocks(solarBlocks);

			solarProgram = GridTerminalSystem.GetBlockWithName("Solar Program") as IMyProgrammableBlock;
			solarArmPiston = GridTerminalSystem.GetBlockWithName("Solar Arm Piston") as IMyPistonBase;
			solarArmRotor = GridTerminalSystem.GetBlockWithName("Solar Arm Rotor") as IMyMotorStator;

			for (int i = 0; i < solarBlocks.Count; i++)
			{
				var block = solarBlocks[i] as IMyTerminalBlock;

				if (block is IMyPistonBase) { solarPistons.Add(block); }
				if (block is IMyMotorStator) { solarRotors.Add(block); }
			}

			solarHome["Azimuth"] = ToRad(-90);
			solarHome["Elevation 0"] = ToRad(90);
			solarHome["Elevation 1"] = ToRad(0);

			_commands["solar"] = SolarToggle;

		}

		public void Main(string argument, UpdateType updateSource)
		{

			if (solarStatus != "extended" && solarStatus != "retracted") { SolarToggle(); }

			if (_commandLine.TryParse(argument))
			{
				Action commandAction;

				string command = _commandLine.Argument(0);

				if (_commands.TryGetValue(_commandLine.Argument(0), out commandAction)) { commandAction(); }
				else { Echo($"Unknown command {command}"); }
			}
		}

		public void SolarToggle()
		{
			//retract solar
			if (solarStatus == "extended")
			{
				solarStatus = "retracting panels";
				PanelRetract();
			}
			else if (solarStatus == "retracting panels" && InPosition(solarRotors))
			{
				solarStatus = "retracting arm";
				solarArmPiston.Retract();
			}
			else if (solarStatus == "retracting arm" && InPosition(solarArmPiston))
			{
				solarStatus = "retracted";
				solarArmRotor.TargetVelocityRPM = -3;
			}

			//extend solar
			else if (solarStatus == "retracted")
			{
				solarStatus = "rotating arm";
				solarArmRotor.TargetVelocityRPM = 3;
			}
			else if (solarStatus == "rotating arm" && InPosition(solarRotors))
			{
				solarStatus = "extending arm";
				solarArmPiston.Extend();
			}
			else if (solarStatus == "extending arm" && InPosition(solarArmPiston))
			{
				solarStatus = "extended";
				PanelExtend();
			}
		}

		bool SolarMoving()
		{
			foreach (IMyMotorStator rotor in solarRotors)
			{
				if (rotor.Angle != rotor.UpperLimitRad && rotor.Angle != rotor.LowerLimitRad)
				{

				}
			}
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