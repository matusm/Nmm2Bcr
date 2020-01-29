using Bev.IO.BcrWriter;
using Bev.IO.NmmReader;
using Bev.IO.NmmReader.scan_mode;
using Bev.UI;
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
            {
                ConsoleUI.BeSilent();
            }
            else
            {
                ConsoleUI.BeVerbatim();
            }

            // print a welcome message
            ConsoleUI.Welcome();

            // get the filename(s)
            string[] fileNames = options.ListOfFileNames.ToArray();
            if (fileNames.Length == 0)
                ConsoleUI.ErrorExit("Missing input file!", 1);

            // here all the scan data is read
            ConsoleUI.StartOperation("Reading and evaluating files");
            NmmFileName nmmFileNameObject = new NmmFileName(fileNames[0]);
            int scanIndex = options.ScanIndex;
            nmmFileNameObject.SetScanIndex(scanIndex);
            NmmScanData theData = new NmmScanData(nmmFileNameObject);
            ConsoleUI.Done();

            // some checks of the provided CLA options
            if (options.ProfileIndex < 0)
            {
                options.ProfileIndex = 0;
            }
            if (options.ProfileIndex > theData.MetaData.NumberOfProfiles)
            {
                options.ProfileIndex = theData.MetaData.NumberOfProfiles;
            }

            TopographyProcessType topographyProcessType = TopographyProcessType.ForwardOnly;
            if (options.UseBack)
            {
                topographyProcessType = TopographyProcessType.BackwardOnly;
            }
            if (options.UseBoth)
            {
                topographyProcessType = TopographyProcessType.Average;
            }
            if (options.UseDiff)
            {
                topographyProcessType = TopographyProcessType.Difference;
            }


            // now we can start to sort and format everything we need

            BcrWriter bcr = new BcrWriter();
            bcr.Relaxed = options.Relaxed;
            // ISO 25178-71 file header
            bcr.CreationDate = theData.MetaData.CreationDate;
            bcr.ManufacurerId = theData.MetaData.InstrumentIdentifier;
            bcr.NumberOfPointsPerProfile = theData.MetaData.NumberOfDataPoints;
            bcr.NumberOfProfiles = theData.MetaData.NumberOfProfiles; // TODO option dependent
            bcr.XScale = theData.MetaData.ScanFieldDeltaX;
            bcr.YScale = theData.MetaData.ScanFieldDeltaY;

            // read actual topography data
            double[] rawData = theData.ExtractProfile(options.ChannelSymbol, options.ProfileIndex, topographyProcessType);

            // level data 
            DataLeveling levelObject; 
            if(options.ProfileIndex == 0)
                levelObject = new DataLeveling(rawData, theData.MetaData.NumberOfDataPoints, theData.MetaData.NumberOfProfiles);
            else
                levelObject = new DataLeveling(rawData, theData.MetaData.NumberOfDataPoints);
            double[] topographyData = levelObject.LevelData(ReferenceTo.Lsq);

            // ISO 25178-71 main section
            bcr.PrepareMainSection(topographyData);

            // ISO 25178-71 file trailer
            // generate a dictionary with all relevant metadata
            Dictionary<string, string> bcrMetaData = new Dictionary<string, string>();
            bcrMetaData.Add("InputFile", theData.MetaData.BaseFileName);
            bcrMetaData.Add("ConvertedBy", $"{ConsoleUI.Title} version {ConsoleUI.Version}");
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
            if (theData.MetaData.NumberOfScans>1)
            {
                bcrMetaData.Add("NumberOfScans", $"{theData.MetaData.NumberOfScans}");
                bcrMetaData.Add("Scan",$"{theData.MetaData.ScanIndex}");
            }
            bcrMetaData.Add("ReferenceDatum", levelObject.LevelModeDescription);
            bcrMetaData.Add("EnvironmentMode", theData.MetaData.EnvironmentMode);
            bcrMetaData.Add("SampleTemperature", $"{theData.MetaData.SampleTemperature:F3} oC");
            bcrMetaData.Add("AirTemperature", $"{theData.MetaData.AirTemperature:F3} oC");
            bcrMetaData.Add("AirPressure", $"{theData.MetaData.BarometricPressure:F0} Pa");
            bcrMetaData.Add("AirHumidity", $"{theData.MetaData.RelativeHumidity:F1} %");
            bcrMetaData.Add("TemperatureGradient", $"{theData.MetaData.AirTemperatureGradient:F3} oC");
            bcrMetaData.Add("TemperatureRange", $"{theData.MetaData.AirTemperatureDrift:F3} oC");
            bcrMetaData.Add("ScanSpeed", $"{theData.MetaData.ScanSpeed} um/s");
            bcrMetaData.Add("ScanAngle", $"{theData.MetaData.ScanFieldRotation:F3} grad");
            //bcrMetaData.Add("ScanXcenter", $" mm");
            //bcrMetaData.Add("ScanYcenter", $" mm");

            bcr.PrepareTrailerSection(bcrMetaData);

            // now generate output
            string outFileName;
            if(fileNames.Length >=2)
            {
                outFileName = fileNames[1];
            }
            else
            {
                outFileName = nmmFileNameObject.GetFreeFileNameWithIndex("sdf");
            }

            ConsoleUI.WritingFile(outFileName);
            if(!bcr.WriteToFile(outFileName))
            {
                ConsoleUI.Abort();
                ConsoleUI.ErrorExit("!could not write file", 1);
            }
            ConsoleUI.Done();

        }

    }
}
