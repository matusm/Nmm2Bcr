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
            // consume the verbosity option
            if (options.BeQuiet == true)
                ConsoleUI.BeSilent();
            else
                ConsoleUI.BeVerbatim();
            // print a welcome message
            ConsoleUI.Welcome();
            ConsoleUI.WriteLine();
            // get the filename(s)
            string[] fileNames = options.ListOfFileNames.ToArray();
            if (fileNames.Length == 0)
                ConsoleUI.ErrorExit("!Missing input file", 1);
            // read all relevant scan data
            ConsoleUI.StartOperation("Reading and evaluating files");
            NmmFileName nmmFileNameObject = new NmmFileName(fileNames[0]);
            nmmFileNameObject.SetScanIndex(options.ScanIndex);
            NmmScanData theData = new NmmScanData(nmmFileNameObject);
            ConsoleUI.Done();
            ConsoleUI.WriteLine();

            // some checks of the provided CLA options
            if (options.ProfileIndex < 0)
                options.ProfileIndex = 0;
            if (options.ProfileIndex > theData.MetaData.NumberOfProfiles)
                options.ProfileIndex = theData.MetaData.NumberOfProfiles;

            TopographyProcessType topographyProcessType = TopographyProcessType.ForwardOnly;
            if (options.UseBack)
                topographyProcessType = TopographyProcessType.BackwardOnly;
            if (options.UseBoth)
                topographyProcessType = TopographyProcessType.Average;
            if (options.UseDiff)
                topographyProcessType = TopographyProcessType.Difference;
            if (theData.MetaData.ScanStatus == ScanDirectionStatus.ForwardOnly)
            {
                if (topographyProcessType != TopographyProcessType.ForwardOnly)
                    ConsoleUI.WriteLine("No backward scan data present, switching to forward only.");
                topographyProcessType = TopographyProcessType.ForwardOnly;
            }
            if (theData.MetaData.ScanStatus == ScanDirectionStatus.Unknown)
                ConsoleUI.ErrorExit("!Unknown scan type", 2);
            if (theData.MetaData.ScanStatus == ScanDirectionStatus.NoData)
                ConsoleUI.ErrorExit("!No scan data present", 3);

            // now we can start to sort and format everything we need
            BcrWriter bcr = new BcrWriter();
            bcr.Relaxed = options.Relaxed;
            bcr.ForceIsoFormat = options.IsoFormat;
            // ISO 25178-71 file header
            bcr.CreationDate = theData.MetaData.CreationDate;
            bcr.ManufacurerId = theData.MetaData.InstrumentIdentifier;
            bcr.NumberOfPointsPerProfile = theData.MetaData.NumberOfDataPoints;
            if (options.ProfileIndex == 0)
                bcr.NumberOfProfiles = theData.MetaData.NumberOfProfiles;
            else
                bcr.NumberOfProfiles = 1;
            bcr.XScale = theData.MetaData.ScanFieldDeltaX;
            bcr.YScale = theData.MetaData.ScanFieldDeltaY;

            // read actual topography data for given channel
            if (!theData.ColumnPresent(options.ChannelSymbol))
                ConsoleUI.ErrorExit($"!Channel {options.ChannelSymbol} not in scan data", 5);
            double[] rawData = theData.ExtractProfile(options.ChannelSymbol, options.ProfileIndex, topographyProcessType);

            if (options.DoHeydemann)
            {
                theData.ApplyHeydemannCorrection();
                if(theData.HeydemannCorrectionApplied)
                {
                    ConsoleUI.WriteLine($"Heydemann correction applied, span {theData.HeydemannCorrectionSpan*1e9:F1} nm");
                }
                else
                {
                    ConsoleUI.WriteLine($"Heydemann correction not successful.");
                }
                ConsoleUI.WriteLine();
            }

            // level data 
            DataLeveling levelObject;
            if (options.ProfileIndex == 0)
                levelObject = new DataLeveling(rawData, theData.MetaData.NumberOfDataPoints, theData.MetaData.NumberOfProfiles);
            else
                levelObject = new DataLeveling(rawData, theData.MetaData.NumberOfDataPoints);
            levelObject.BiasValue = options.Bias * 1.0e-6; //  bias is given in µm on the command line
            double[] leveledTopographyData = levelObject.LevelData(MapOptionToReference(options.ReferenceMode));

            // ISO 25178-71 main section
            bcr.PrepareMainSection(leveledTopographyData);

            // ISO 25178-71 file trailer
            // generate a dictionary with all relevant metadata
            Dictionary<string, string> bcrMetaData = new Dictionary<string, string>();
            bcrMetaData.Add("InputFile", theData.MetaData.BaseFileName);
            bcrMetaData.Add("ConvertedBy", $"{ConsoleUI.Title} version {ConsoleUI.Version}");
            bcrMetaData.Add("UserComment", options.UserComment);
            bcrMetaData.Add("OperatorName", theData.MetaData.User);
            bcrMetaData.Add("Organisation", theData.MetaData.Organisation);
            bcrMetaData.Add("SampleIdentifier", theData.MetaData.SampleIdentifier);
            bcrMetaData.Add("SampleSpecies", theData.MetaData.SampleSpecies);
            bcrMetaData.Add("SampleSpecification", theData.MetaData.SampleSpecification);
            bcrMetaData.Add("SPMtechnique", theData.MetaData.SpmTechnique);
            bcrMetaData.Add("Probe", theData.MetaData.ProbeDesignation);
            bcrMetaData.Add("ZAxisSource", options.ChannelSymbol);
            bcrMetaData.Add("Trace", topographyProcessType.ToString());
            if (options.ProfileIndex != 0)
            {
                bcrMetaData.Add("NumProfiles", $"{theData.MetaData.NumberOfProfiles}");
                bcrMetaData.Add("ExtractedProfile", $"{options.ProfileIndex}");
            }
            if (theData.MetaData.NumberOfScans > 1)
            {
                bcrMetaData.Add("NumberOfScans", $"{theData.MetaData.NumberOfScans}");
                bcrMetaData.Add("Scan", $"{theData.MetaData.ScanIndex}");
            }
            bcrMetaData.Add("ReferenceDatum", levelObject.LevelModeDescription);
            if (theData.HeydemannCorrectionApplied)
            {
                bcrMetaData.Add("HeydemannCorrection", $"Span {theData.HeydemannCorrectionSpan * 1e9:F1} nm");
            }
            bcrMetaData.Add("EnvironmentMode", theData.MetaData.EnvironmentMode);
            bcrMetaData.Add("SampleTemperature", $"{theData.MetaData.SampleTemperature:F3} oC");
            bcrMetaData.Add("AirTemperature", $"{theData.MetaData.AirTemperature:F3} oC");
            bcrMetaData.Add("AirPressure", $"{theData.MetaData.BarometricPressure:F0} Pa");
            bcrMetaData.Add("AirHumidity", $"{theData.MetaData.RelativeHumidity:F1} %");
            bcrMetaData.Add("TemperatureGradient", $"{theData.MetaData.AirTemperatureGradient:F3} oC");
            bcrMetaData.Add("TemperatureRange", $"{theData.MetaData.AirTemperatureDrift:F3} oC");
            bcrMetaData.Add("ScanSpeed", $"{theData.MetaData.ScanSpeed} um/s");
            bcrMetaData.Add("AngularOrientation", $"{theData.MetaData.ScanFieldRotation:F3} grad");
            bcrMetaData.Add("ScanFieldCenterX", $"{theData.MetaData.ScanFieldCenterX*1000:F1} mm");
            bcrMetaData.Add("ScanFieldCenterY", $"{theData.MetaData.ScanFieldCenterY * 1000:F1} mm");
            bcrMetaData.Add("ScanFieldCenterZ", $"{theData.MetaData.ScanFieldCenterZ * 1000:F1} mm");
            bcrMetaData.Add("GlitchedDataPoints", $"{theData.MetaData.NumberOfGlitchedDataPoints}");
            bcrMetaData.Add("SpuriousDataLines", $"{theData.MetaData.SpuriousDataLines}");
            for (int i = 0; i < theData.MetaData.ScanComments.Count; i++)
            {
                bcrMetaData.Add($"ScanComment{i + 1}", theData.MetaData.ScanComments[i]);
            }

            bcr.PrepareTrailerSection(bcrMetaData);

            // now generate output
            string outFileName;
            if (fileNames.Length >= 2)
                outFileName = fileNames[1];
            else
                outFileName = nmmFileNameObject.GetFreeFileNameWithIndex("sdf");

            ConsoleUI.WritingFile(outFileName);
            if (!bcr.WriteToFile(outFileName))
            {
                ConsoleUI.Abort();
                ConsoleUI.ErrorExit("!could not write file", 4);
            }
            ConsoleUI.Done();
            ConsoleUI.WriteLine();
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
