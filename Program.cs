using Bev.IO.BcrWriter;
using Bev.IO.NmmReader;
using Bev.IO.NmmReader.scan_mode;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nmm2Bcr
{
    class Program
    {
        static void Main(string[] args)
        {
            // parse command line arguments
            var options = new Options();
            if (!CommandLine.Parser.Default.ParseArgumentsStrict(args, options))
                Console.WriteLine("*** ParseArgumentsStrict returned false");
            if (options.BeQuiet == true)
                ConsoleUI.BeSilent();
            else
                ConsoleUI.BeVerbatim();
            ConsoleUI.Welcome();

            // get the filename(s)
            string[] fileNames = options.ListOfFileNames.ToArray();
            if (fileNames.Length == 0)
                ConsoleUI.ErrorExit("!Missing input file", 1);
            // read all relevant scan data
            ConsoleUI.StartOperation("Reading and evaluating files");
            NmmFileName nmmFileName = new NmmFileName(fileNames[0]);
            nmmFileName.SetScanIndex(options.ScanIndex);
            NmmScanData nmmScanData = new NmmScanData(nmmFileName);
            ConsoleUI.Done();

            if (options.DoHeydemann)
            {
                nmmScanData.ApplyNLcorrection();
                if (nmmScanData.NonlinearityCorrectionApplied)
                {
                    ConsoleUI.WriteLine($"Interferometric nonlinearity correction applied, span {nmmScanData.NonlinearityCorrectionSpan * 1e9:F1} nm");
                }
                else
                {
                    ConsoleUI.WriteLine($"Interferometric nonlinearity correction not successful.");
                }
            }

            // some checks of the provided CLA options
            if (options.ProfileIndex < 0)
                options.ProfileIndex = 0;
            if (options.ProfileIndex > nmmScanData.MetaData.NumberOfProfiles)
                options.ProfileIndex = nmmScanData.MetaData.NumberOfProfiles;

            TopographyProcessType topographyProcessType = TopographyProcessType.ForwardOnly;
            if (options.UseBack)
                topographyProcessType = TopographyProcessType.BackwardOnly;
            if (options.UseBoth)
                topographyProcessType = TopographyProcessType.Average;
            if (options.UseDiff)
                topographyProcessType = TopographyProcessType.Difference;
            if (nmmScanData.MetaData.ScanStatus == ScanDirectionStatus.ForwardOnly)
            {
                if (topographyProcessType != TopographyProcessType.ForwardOnly)
                    ConsoleUI.WriteLine("No backward scan data present, switching to forward only.");
                topographyProcessType = TopographyProcessType.ForwardOnly;
            }
            if (nmmScanData.MetaData.ScanStatus == ScanDirectionStatus.Unknown)
                ConsoleUI.ErrorExit("!Unknown scan type", 2);
            if (nmmScanData.MetaData.ScanStatus == ScanDirectionStatus.NoData)
                ConsoleUI.ErrorExit("!No scan data present", 3);

            // now we can start to sort and format everything we need
            BcrWriter bcr = new BcrWriter();
            bcr.Relaxed = !options.Strict; // overrules Relaxed
            ConsoleUI.WriteLine(bcr.Relaxed ? "Relaxed formatting" : "Strict formatting");
            bcr.ForceIsoFormat = options.IsoFormat;
            ConsoleUI.WriteLine(bcr.ForceIsoFormat ? "ISO 25178-71 format" : "Legacy format");

            // ISO 25178-71 file header
            bcr.CreationDate = nmmScanData.MetaData.CreationDate;
            bcr.ManufacurerId = nmmScanData.MetaData.InstrumentIdentifier;
            bcr.NumberOfPointsPerProfile = nmmScanData.MetaData.NumberOfDataPoints;
            if (options.ProfileIndex == 0)
            {
                bcr.NumberOfProfiles = nmmScanData.MetaData.NumberOfProfiles;
                bcr.YScale = nmmScanData.MetaData.ScanFieldDeltaY;
                ConsoleUI.WriteLine("Extract complete scanfield");
            }
            else
            {
                bcr.NumberOfProfiles = 1;
                bcr.YScale = 0;
                ConsoleUI.WriteLine($"Extract single profile {options.ProfileIndex} only");
            }
            bcr.XScale = nmmScanData.MetaData.ScanFieldDeltaX;
            bcr.ZScale = options.ZScale;

            // read actual topography data for given channel
            if (!nmmScanData.ColumnPresent(options.ChannelSymbol))
                ConsoleUI.ErrorExit($"!Channel {options.ChannelSymbol} not in scan data", 5);
            double[] rawData = nmmScanData.ExtractProfile(options.ChannelSymbol, options.ProfileIndex, topographyProcessType);

            // level data 
            DataLeveling levelObject;
            if (options.ProfileIndex == 0)
                levelObject = new DataLeveling(rawData, nmmScanData.MetaData.NumberOfDataPoints, nmmScanData.MetaData.NumberOfProfiles);
            else
                levelObject = new DataLeveling(rawData, nmmScanData.MetaData.NumberOfDataPoints);
            levelObject.BiasValue = options.Bias * 1.0e-6; //  bias is given in µm on the command line
            double[] leveledTopographyData = levelObject.LevelData(MapOptionToReference(options.ReferenceMode));

            // generate a dictionary with all relevant metadata for the ISO 25178-71 file trailer
            Dictionary<string, string> bcrMetaData = new Dictionary<string, string>();
            bcrMetaData.Add("InputFile", nmmScanData.MetaData.BaseFileName);
            bcrMetaData.Add("ConvertedBy", $"{ConsoleUI.Title} version {ConsoleUI.Version}");
            bcrMetaData.Add("UserComment", options.UserComment);
            bcrMetaData.Add("OperatorName", nmmScanData.MetaData.User);
            bcrMetaData.Add("Organisation", nmmScanData.MetaData.Organisation);
            bcrMetaData.Add("SampleIdentifier", nmmScanData.MetaData.SampleIdentifier);
            bcrMetaData.Add("SampleSpecies", nmmScanData.MetaData.SampleSpecies);
            bcrMetaData.Add("SampleSpecification", nmmScanData.MetaData.SampleSpecification);
            bcrMetaData.Add("SPMtechnique", nmmScanData.MetaData.SpmTechnique);
            bcrMetaData.Add("Probe", nmmScanData.MetaData.ProbeDesignation);
            bcrMetaData.Add("ZAxisSource", options.ChannelSymbol);
            bcrMetaData.Add("Trace", topographyProcessType.ToString());
            if (options.ProfileIndex != 0)
            {
                bcrMetaData.Add("NumProfiles", $"{nmmScanData.MetaData.NumberOfProfiles}");
                bcrMetaData.Add("ExtractedProfile", $"{options.ProfileIndex}");
            }
            if (nmmScanData.MetaData.NumberOfScans > 1)
            {
                bcrMetaData.Add("NumberOfScans", $"{nmmScanData.MetaData.NumberOfScans}");
                bcrMetaData.Add("Scan", $"{nmmScanData.MetaData.ScanIndex}");
            }
            bcrMetaData.Add("ReferenceDatum", levelObject.LevelModeDescription);
            if (nmmScanData.HeydemannCorrectionApplied)
            {
                bcrMetaData.Add("HeydemannCorrection", $"Span {nmmScanData.NonlinearityCorrectionSpan * 1e9:F1} nm");
            }
            bcrMetaData.Add("EnvironmentMode", nmmScanData.MetaData.EnvironmentMode);
            bcrMetaData.Add("SampleTemperature", $"{nmmScanData.MetaData.SampleTemperature:F3} oC");
            bcrMetaData.Add("AirTemperature", $"{nmmScanData.MetaData.AirTemperature:F3} oC");
            bcrMetaData.Add("AirPressure", $"{nmmScanData.MetaData.BarometricPressure:F0} Pa");
            bcrMetaData.Add("AirHumidity", $"{nmmScanData.MetaData.RelativeHumidity:F1} %");
            bcrMetaData.Add("TemperatureGradient", $"{nmmScanData.MetaData.AirTemperatureGradient:F3} oC");
            bcrMetaData.Add("TemperatureRange", $"{nmmScanData.MetaData.AirTemperatureDrift:F3} oC");
            bcrMetaData.Add("ScanSpeed", $"{nmmScanData.MetaData.ScanSpeed * 1e6} um/s");
            bcrMetaData.Add("AngularOrientation", $"{nmmScanData.MetaData.ScanFieldRotation:F3} grad");
            bcrMetaData.Add("ScanFieldCenterX", $"{nmmScanData.MetaData.ScanFieldCenterZ * 1000:F3} mm");
            bcrMetaData.Add("ScanFieldCenterY", $"{nmmScanData.MetaData.ScanFieldCenterY * 1000:F3} mm");
            bcrMetaData.Add("ScanFieldCenterZ", $"{nmmScanData.MetaData.ScanFieldCenterZ * 1000:F3} mm");
            bcrMetaData.Add("ScanFieldOriginX", $"{nmmScanData.MetaData.ScanFieldOriginX} m");
            bcrMetaData.Add("ScanFieldOriginY", $"{nmmScanData.MetaData.ScanFieldOriginY} m");
            bcrMetaData.Add("ScanFieldOriginZ", $"{nmmScanData.MetaData.ScanFieldOriginZ} m");
            bcrMetaData.Add("ScanDuration", $"{nmmScanData.MetaData.ScanDuration.TotalSeconds:F0} s");
            bcrMetaData.Add("GlitchedDataPoints", $"{nmmScanData.MetaData.NumberOfGlitchedDataPoints}");
            bcrMetaData.Add("SpuriousDataLines", $"{nmmScanData.MetaData.SpuriousDataLines}");
            for (int i = 0; i < nmmScanData.MetaData.ScanComments.Count; i++)
            {
                bcrMetaData.Add($"ScanComment{i + 1}", nmmScanData.MetaData.ScanComments[i]);
            }

            // ISO 25178-71 main section
            bcr.PrepareMainSection(leveledTopographyData);

            // ISO 25178-71 file trailer
            bcr.PrepareTrailerSection(bcrMetaData);

            // now generate output
            string outFileName;
            if (fileNames.Length >= 2)
                outFileName = fileNames[1];
            else
                outFileName = nmmFileName.GetFreeFileNameWithIndex("sdf");

            ConsoleUI.WritingFile(outFileName);
            if (!bcr.WriteToFile(outFileName))
            {
                ConsoleUI.Abort();
                ConsoleUI.ErrorExit("!could not write file", 4);
            }
            ConsoleUI.Done();
        }

        // this method is used to map the numerical option to the apropiate enumeration
        static ReferenceTo MapOptionToReference(int reference)
        {
            switch (reference)
            {
                case 1:
                    return ReferenceTo.Minimum;
                case 2:
                    return ReferenceTo.Maximum;
                case 3:
                    return ReferenceTo.Average;
                case 4:
                    return ReferenceTo.Central;
                case 5:
                    return ReferenceTo.Bias;
                case 6:
                    return ReferenceTo.First;
                case 7:
                    return ReferenceTo.Last;
                case 8:
                    return ReferenceTo.Center;
                case 9:
                    return ReferenceTo.Line;
                case 10:
                    return ReferenceTo.Lsq;
                case 11:
                    return ReferenceTo.LinePositive;
                case 12:
                    return ReferenceTo.LsqPositive;
                default:
                    return ReferenceTo.None;
            }
        }
    }
}
