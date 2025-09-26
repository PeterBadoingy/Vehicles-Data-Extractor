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
                        ExtractVehicleData();
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

            // Get primary and secondary paint indices
            int primaryColor = 0, secondaryColor = 0;
            NativeFunction.Natives.GET_VEHICLE_COLOURS<int>(vehicle, ref primaryColor, ref secondaryColor);

            // Get interior and dashboard colors
            int interiorColor = 0;
            NativeFunction.Natives.GET_VEHICLE_EXTRA_COLOUR_5<int>(vehicle, ref interiorColor);
            int dashboardColor = 0;
            NativeFunction.Natives.GET_VEHICLE_EXTRA_COLOUR_6<int>(vehicle, ref dashboardColor);

            // Get wheel and pearlescent colors
            int pearlescentColor = 0, wheelColor = 0;
            NativeFunction.Natives.GET_VEHICLE_EXTRA_COLOURS<int>(vehicle, ref pearlescentColor, ref wheelColor);

            // Map paint indices to VehicleColorLookup IDs
            int primaryColorID = MapPaintIndexToColorID(primaryColor);
            int secondaryColorID = MapPaintIndexToColorID(secondaryColor);
            int interiorColorID = MapPaintIndexToColorID(interiorColor);
            int dashboardColorID = MapPaintIndexToColorID(dashboardColor);
            int wheelColorID = MapPaintIndexToColorID(wheelColor);
            int pearlescentColorID = MapPaintIndexToColorID(pearlescentColor);

            var data = new DispatchableVehicle
            {
                DebugName = $"{vehicle.Model.Name}_PB",
                ModelName = vehicle.Model.Name.ToUpper(),
                RequiresDLC = IsDLCVehicle(vehicle.Model.Hash),
                RequiredPrimaryColorID = primaryColorID,
                RequiredSecondaryColorID = secondaryColorID,
                RequiredVariation = new VehicleVariation
                {
                    PrimaryColor = primaryColorID,
                    SecondaryColor = secondaryColorID,
                    InteriorColor = interiorColorID,
                    DashboardColor = dashboardColorID,
                    WheelColor = wheelColorID,
                    PearlescentColor = pearlescentColorID,
                    WheelType = NativeFunction.Natives.GET_VEHICLE_WHEEL_TYPE<int>(vehicle),
                    WindowTint = NativeFunction.Natives.GET_VEHICLE_WINDOW_TINT<int>(vehicle),
                    VehicleExtras = GetVehicleExtras(vehicle),
                    VehicleToggles = GetVehicleToggles(vehicle),
                    VehicleMods = GetVehicleMods(vehicle)
                }
            };

            string output = FormatVehicleData(data);
            File.AppendAllText(outputPath, output + Environment.NewLine);
            Game.DisplayNotification($"Vehicle data exported to {outputPath}.");
        }

        private static int MapPaintIndexToColorID(int paintIndex)
        {
            // Map GTA V paint indices to VehicleColorLookup IDs
            var colorMap = new Dictionary<int, int>
            {
                { 0, 0 }, { 1, 1 }, { 2, 2 }, { 3, 3 }, { 4, 4 }, { 5, 5 }, { 6, 6 }, { 7, 7 }, { 8, 8 }, { 9, 9 },
                { 10, 10 }, { 11, 11 }, { 12, 12 }, { 13, 13 }, { 14, 14 }, { 15, 15 }, { 16, 16 }, { 17, 17 },
                { 18, 18 }, { 19, 19 }, { 20, 20 }, { 21, 21 }, { 22, 22 }, { 23, 23 }, { 24, 24 }, { 25, 25 },
                { 26, 26 }, { 27, 27 }, { 28, 28 }, { 29, 29 }, { 30, 30 }, { 31, 31 }, { 32, 32 }, { 33, 33 },
                { 34, 34 }, { 35, 35 }, { 36, 36 }, { 37, 37 }, { 38, 38 }, { 39, 39 }, { 40, 40 }, { 41, 41 },
                { 42, 42 }, { 43, 43 }, { 44, 44 }, { 45, 45 }, { 46, 46 }, { 47, 47 }, { 48, 48 }, { 49, 49 },
                { 50, 50 }, { 51, 51 }, { 52, 52 }, { 53, 53 }, { 54, 54 }, { 55, 55 }, { 56, 56 }, { 57, 57 },
                { 58, 58 }, { 59, 59 }, { 60, 60 }, { 61, 61 }, { 62, 62 }, { 63, 63 }, { 64, 64 }, { 65, 65 },
                { 66, 66 }, { 67, 67 }, { 68, 68 }, { 69, 69 }, { 70, 70 }, { 71, 71 }, { 72, 72 }, { 73, 73 },
                { 74, 74 }, { 75, 75 }, { 76, 76 }, { 77, 77 }, { 78, 78 }, { 79, 79 }, { 80, 80 }, { 81, 81 },
                { 82, 82 }, { 83, 83 }, { 84, 84 }, { 85, 85 }, { 86, 86 }, { 87, 87 }, { 88, 88 }, { 89, 89 },
                { 90, 90 }, { 91, 91 }, { 92, 92 }, { 93, 93 }, { 94, 94 }, { 95, 95 }, { 96, 96 }, { 97, 97 },
                { 98, 98 }, { 99, 99 }, { 100, 100 }, { 101, 101 }, { 102, 102 }, { 103, 103 }, { 104, 104 },
                { 105, 105 }, { 106, 106 }, { 107, 107 }, { 108, 108 }, { 109, 109 }, { 110, 110 }, { 111, 111 },
                { 112, 112 }, { 113, 113 }, { 114, 114 }, { 115, 115 }, { 116, 116 }, { 117, 117 }, { 118, 118 },
                { 119, 119 }, { 120, 120 }, { 121, 121 }, { 122, 122 }, { 123, 123 }, { 124, 124 }, { 125, 125 },
                { 126, 126 }, { 127, 127 }, { 128, 128 }, { 129, 129 }, { 130, 130 }, { 131, 131 }, { 132, 132 },
                { 133, 133 }, { 134, 134 }, { 135, 135 }, { 136, 136 }, { 137, 137 }, { 138, 138 }, { 139, 139 },
                { 140, 140 }, { 141, 141 }, { 142, 142 }, { 143, 143 }, { 144, 144 }, { 145, 145 }, { 146, 146 },
                { 147, 147 }, { 148, 148 }, { 149, 149 }, { 150, 150 }, { 151, 151 }, { 152, 152 }, { 153, 153 },
                { 154, 154 }, { 155, 155 }, { 156, 156 }, { 157, 157 }, { 158, 158 }, { 159, 159 }, { 160, 160 }
            };

            return colorMap.ContainsKey(paintIndex) ? colorMap[paintIndex] : -1;
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
                if (NativeFunction.Natives.DOES_EXTRA_EXIST<bool>(vehicle, i) &&
                    NativeFunction.Natives.IS_VEHICLE_EXTRA_TURNED_ON<bool>(vehicle, i))
                {
                    extras.Add(new VehicleExtra(i, true));
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
                if (i >= 17 && i <= 22) continue; // Skip toggle mods
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
                {
                    sb.AppendLine($"            new VehicleExtra() {{ ID = {extra.ID}, IsTurnedOn = {extra.IsTurnedOn.ToString().ToLower()} }},");
                }
                sb.AppendLine($"        }},");
            }
            if (data.RequiredVariation.VehicleToggles.Any())
            {
                sb.AppendLine($"        VehicleToggles = new List<VehicleToggle>() {{");
                foreach (var toggle in data.RequiredVariation.VehicleToggles)
                {
                    sb.AppendLine($"            new VehicleToggle() {{ ID = {toggle.ID}, IsTurnedOn = {toggle.IsTurnedOn.ToString().ToLower()} }},");
                }
                sb.AppendLine($"        }},");
            }
            if (data.RequiredVariation.VehicleMods.Any())
            {
                sb.AppendLine($"        VehicleMods = new List<VehicleMod>() {{");
                foreach (var mod in data.RequiredVariation.VehicleMods)
                {
                    sb.AppendLine($"            new VehicleMod() {{ ID = {mod.ID}, Output = {mod.Output} }},");
                }
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