using Rage;
using Rage.Attributes;
using Rage.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

[assembly: Plugin("Vehicle Data Extractor", Description = "Extracts vehicle data in GTA V", Author = "YourName")]

namespace VehicleDataExtractor
{
    public class DispatchableVehicle
    {
        public string DebugName { get; set; }
        public string ModelName { get; set; }
        public int AmbientSpawnChance { get; set; } = 75;
        public int WantedSpawnChance { get; set; } = 75;
        public int? RequiredPrimaryColorID { get; set; }
        public int? RequiredSecondaryColorID { get; set; }
        public VehicleVariation RequiredVariation { get; set; }
        public bool RequiresDLC { get; set; }

        public int MinOccupants { get; set; } = 1;
        public int MaxOccupants { get; set; }
    }

    public class VehicleVariation
    {
        public int PrimaryColor { get; set; }
        public int SecondaryColor { get; set; }
        public int InteriorColor { get; set; }
        public int DashboardColor { get; set; }
        public int WheelColor { get; set; }
        public int PearlescentColor { get; set; }
        public int WheelType { get; set; }
        public int WindowTint { get; set; }
        public List<VehicleExtra> VehicleExtras { get; set; } = new List<VehicleExtra>();
        public List<VehicleToggle> VehicleToggles { get; set; } = new List<VehicleToggle>();
        public List<VehicleMod> VehicleMods { get; set; } = new List<VehicleMod>();
    }

    public class VehicleExtra
    {
        public int ID { get; set; }
        public bool IsTurnedOn { get; set; }

        public VehicleExtra() { }
        public VehicleExtra(int id, bool isTurnedOn)
        {
            ID = id;
            IsTurnedOn = isTurnedOn;
        }
    }

    public class VehicleToggle
    {
        public int ID { get; set; }
        public bool IsTurnedOn { get; set; }

        public VehicleToggle() { }
        public VehicleToggle(int id, bool isTurnedOn)
        {
            ID = id;
            IsTurnedOn = isTurnedOn;
        }
    }

    public class VehicleMod
    {
        public int ID { get; set; }
        public int Output { get; set; }

        public VehicleMod() { }
        public VehicleMod(int id, int output)
        {
            ID = id;
            Output = output;
        }
    }

    public static class VehicleDataExtractor
    {
        private static bool isRunning = true;
        private static readonly string outputPath = "VehiclesDataOutput.cs";
        private static bool keyHeld = false;

        public static void Main()
        {
            Initialize();
        }

        public static void Initialize()
        {
            Game.DisplayNotification("Vehicle Data Extractor Loaded. Press F9 to extract data.");
            GameFiber.StartNew(() =>
            {
                while (isRunning)
                {
                    if (Game.IsKeyDown(System.Windows.Forms.Keys.F9))
                    {
                        if (!keyHeld) // debounce
                        {
                            ExtractVehicleData();
                            keyHeld = true;
                        }
                    }
                    else
                    {
                        keyHeld = false;
                    }
                    GameFiber.Yield();
                }
            }, "VehicleDataExtractor");
        }

        private static void ExtractVehicleData()
        {
            var player = Game.LocalPlayer.Character;
            if (!player.IsInAnyVehicle(false))
            {
                Game.DisplayNotification("You must be in a vehicle to extract data.");
                return;
            }

            var vehicle = player.CurrentVehicle;
            if (!vehicle.Exists())
            {
                Game.DisplayNotification("No valid vehicle found.");
                return;
            }

            string modelName;
            try
            {
                modelName = NativeFunction.Natives.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL<string>(vehicle.Model.Hash);
                if (string.IsNullOrEmpty(modelName) || modelName == "CARNOTFOUND")
                {
                    modelName = vehicle.Model.Name.ToUpper();
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"Failed to get vehicle model name: {ex.Message}");
                modelName = vehicle.Model.Name.ToUpper();
            }

            int seatCount = 0;
            try
            {
                seatCount = NativeFunction.Natives.GET_VEHICLE_MODEL_NUMBER_OF_SEATS<int>(vehicle.Model.Hash);
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"Failed to get seat count: {ex.Message}");
            }

            // Paint colors
            int primaryColor = 0, secondaryColor = 0;
            NativeFunction.Natives.GET_VEHICLE_COLOURS<int>(vehicle, ref primaryColor, ref secondaryColor);

            int interiorColor = 0;
            NativeFunction.Natives.GET_VEHICLE_EXTRA_COLOUR_5<int>(vehicle, ref interiorColor);

            int dashboardColor = 0;
            NativeFunction.Natives.GET_VEHICLE_EXTRA_COLOUR_6<int>(vehicle, ref dashboardColor);

            int pearlescentColor = 0, wheelColor = 0;
            NativeFunction.Natives.GET_VEHICLE_EXTRA_COLOURS<int>(vehicle, ref pearlescentColor, ref wheelColor);

            var data = new DispatchableVehicle
            {
                DebugName = $"{modelName}_PB",
                ModelName = modelName,
                MaxOccupants = seatCount,
                RequiresDLC = IsDLCVehicle(vehicle.Model.Hash),
                RequiredPrimaryColorID = primaryColor,
                RequiredSecondaryColorID = secondaryColor,
                RequiredVariation = new VehicleVariation
                {
                    PrimaryColor = primaryColor,
                    SecondaryColor = secondaryColor,
                    InteriorColor = interiorColor,
                    DashboardColor = dashboardColor,
                    WheelColor = wheelColor,
                    PearlescentColor = pearlescentColor,
                    WheelType = NativeFunction.Natives.GET_VEHICLE_WHEEL_TYPE<int>(vehicle),
                    WindowTint = NativeFunction.Natives.GET_VEHICLE_WINDOW_TINT<int>(vehicle),
                    VehicleExtras = GetVehicleExtras(vehicle),
                    VehicleToggles = GetVehicleToggles(vehicle),
                    VehicleMods = GetVehicleMods(vehicle)
                }
            };

            var sb = new StringBuilder();
            sb.AppendLine($"// Extracted at {DateTime.Now}");
            sb.AppendLine(FormatVehicleData(data));
            sb.AppendLine();

            File.AppendAllText(outputPath, sb.ToString());
            Game.DisplayNotification($"Vehicle data exported to {outputPath}.");
        }

        private static bool IsDLCVehicle(uint vehicleHash)
        {
            int numDLCVehicles = NativeFunction.Natives.GET_NUM_DLC_VEHICLES<int>();
            for (int i = 0; i < numDLCVehicles; i++)
            {
                IntPtr outData = Marshal.AllocHGlobal(24);
                try
                {
                    bool success = NativeFunction.Natives.GET_DLC_VEHICLE_DATA<bool>(i, outData);
                    if (success)
                    {
                        uint dlcHash = (uint)Marshal.ReadInt64(outData, 8);
                        if (dlcHash == vehicleHash)
                        {
                            return true;
                        }
                    }
                }
                finally
                {
                    if (outData != IntPtr.Zero)
                        Marshal.FreeHGlobal(outData);
                }
            }
            return false;
        }

        private static List<VehicleExtra> GetVehicleExtras(Vehicle vehicle)
        {
            var extras = new List<VehicleExtra>();
            for (int i = 1; i <= 12; i++)
            {
                if (NativeFunction.Natives.DOES_EXTRA_EXIST<bool>(vehicle, i))
                {
                    bool isOn = NativeFunction.Natives.IS_VEHICLE_EXTRA_TURNED_ON<bool>(vehicle, i);
                    extras.Add(new VehicleExtra(i, isOn));
                }
            }
            return extras;
        }

        private static List<VehicleToggle> GetVehicleToggles(Vehicle vehicle)
        {
            var toggles = new List<VehicleToggle>();
            for (int i = 17; i <= 22; i++)
            {
                bool isToggledOn = NativeFunction.Natives.IS_TOGGLE_MOD_ON<bool>(vehicle, i);
                if (isToggledOn)
                {
                    toggles.Add(new VehicleToggle(i, isToggledOn));
                }
            }
            return toggles;
        }

        private static List<VehicleMod> GetVehicleMods(Vehicle vehicle)
        {
            var mods = new List<VehicleMod>();
            for (int i = 0; i <= 48; i++)
            {
                if (i >= 17 && i <= 22) continue; // skip toggle mods
                int modIndex = NativeFunction.Natives.GET_VEHICLE_MOD<int>(vehicle, i);
                if (modIndex >= 0)
                {
                    mods.Add(new VehicleMod(i, modIndex));
                }
            }
            return mods;
        }

        private static string FormatVehicleData(DispatchableVehicle data)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"new DispatchableVehicle() {{");
            sb.AppendLine($"    DebugName = \"{data.DebugName}\",");
            sb.AppendLine($"    ModelName = \"{data.ModelName}\",");
            sb.AppendLine($"    AmbientSpawnChance = {data.AmbientSpawnChance},");
            sb.AppendLine($"    WantedSpawnChance = {data.WantedSpawnChance},");
            sb.AppendLine($"    MinOccupants = {data.MinOccupants},");
            sb.AppendLine($"    MaxOccupants = {data.MaxOccupants},");

            if (data.RequiredPrimaryColorID.HasValue)
                sb.AppendLine($"    RequiredPrimaryColorID = {data.RequiredPrimaryColorID},");
            if (data.RequiredSecondaryColorID.HasValue)
                sb.AppendLine($"    RequiredSecondaryColorID = {data.RequiredSecondaryColorID},");

            sb.AppendLine($"    RequiredVariation = new VehicleVariation() {{");
            sb.AppendLine($"        PrimaryColor = {data.RequiredVariation.PrimaryColor},");
            sb.AppendLine($"        SecondaryColor = {data.RequiredVariation.SecondaryColor},");
            sb.AppendLine($"        PearlescentColor = {data.RequiredVariation.PearlescentColor},");
            sb.AppendLine($"        InteriorColor = {data.RequiredVariation.InteriorColor},");
            sb.AppendLine($"        DashboardColor = {data.RequiredVariation.DashboardColor},");
            sb.AppendLine($"        WheelColor = {data.RequiredVariation.WheelColor},");
            sb.AppendLine($"        WheelType = {data.RequiredVariation.WheelType},");
            sb.AppendLine($"        WindowTint = {data.RequiredVariation.WindowTint},");

            if (data.RequiredVariation.VehicleExtras.Any())
            {
                sb.AppendLine($"        VehicleExtras = new List<VehicleExtra>() {{");
                foreach (var extra in data.RequiredVariation.VehicleExtras)
                    sb.AppendLine($"            new VehicleExtra() {{ ID = {extra.ID}, IsTurnedOn = {extra.IsTurnedOn.ToString().ToLower()} }},");
                sb.AppendLine($"        }},");
            }

            if (data.RequiredVariation.VehicleToggles.Any())
            {
                sb.AppendLine($"        VehicleToggles = new List<VehicleToggle>() {{");
                foreach (var toggle in data.RequiredVariation.VehicleToggles)
                    sb.AppendLine($"            new VehicleToggle() {{ ID = {toggle.ID}, IsTurnedOn = {toggle.IsTurnedOn.ToString().ToLower()} }},");
                sb.AppendLine($"        }},");
            }

            if (data.RequiredVariation.VehicleMods.Any())
            {
                sb.AppendLine($"        VehicleMods = new List<VehicleMod>() {{");
                foreach (var mod in data.RequiredVariation.VehicleMods)
                    sb.AppendLine($"            new VehicleMod() {{ ID = {mod.ID}, Output = {mod.Output} }},");
                sb.AppendLine($"        }},");
            }

            sb.AppendLine($"    }},");
            sb.AppendLine($"    RequiresDLC = {data.RequiresDLC.ToString().ToLower()},");
            sb.AppendLine($"}}");
            return sb.ToString();
        }

        public static void Stop()
        {
            isRunning = false;
        }
    }
}
