﻿using Bev.IO.BcrWriter;
using Bev.IO.NmmReader;
using Bev.IO.NmmReader.scan_mode;
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Nmm2Bcr
{
    class Program
    {
        private static Options options = new Options(); // this must be set in Run()

        public static void Main(string[] args)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Parser parser = new Parser(with => with.HelpWriter = null);
            ParserResult<Options> parserResult = parser.ParseArguments<Options>(args);
            parserResult
                .WithParsed<Options>(options => Run(options))
                .WithNotParsed(errs => DisplayHelp(parserResult, errs));
        }

        private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            string appName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            HelpText helpText = HelpText.AutoBuild(result, h =>
            {
                h.AutoVersion = false;
                h.AdditionalNewLineAfterOption = false;
                h.AddPreOptionsLine("\nProgram to convert scanning files by SIOS NMM - 1 to BCR or ISO 25178 - 71:2012 raster data format. " +
                "The quadruple of files (dat ind dsc pos) are analyzed to obtain the required parameters. " +
                "A rudimentary data processing is implemented via the -r option.");
                h.AddPreOptionsLine("");
                h.AddPreOptionsLine($"Usage: {appName} InputPath [OutPath] [options]");
                h.AddPostOptionsLine("");
                h.AddPostOptionsLine("Supported values for --reference (-r):");
                h.AddPostOptionsLine("    0: nop");
                h.AddPostOptionsLine("    1: min");
                h.AddPostOptionsLine("    2: max");
                h.AddPostOptionsLine("    3: average");
                h.AddPostOptionsLine("    4: mid");
                h.AddPostOptionsLine("    5: bias");
                h.AddPostOptionsLine("    6: first");
                h.AddPostOptionsLine("    7: last");
                h.AddPostOptionsLine("    8: center");
                h.AddPostOptionsLine("    9: linear");
                h.AddPostOptionsLine("   10: LSQ");
                h.AddPostOptionsLine("   11: linear(positive)");
                h.AddPostOptionsLine("   12: LSQ(positive)");
                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e);
            Console.WriteLine(helpText);
        }

        private static void Run(Options ops)
        {
            options = ops;

            // parse command line arguments
            if (options.BeQuiet == true)
                ConsoleUI.BeSilent();
            else
                ConsoleUI.BeVerbatim();
            ConsoleUI.Welcome();

            ConsoleUI.StartOperation("Reading and evaluating files");
            NmmFileName nmmFileName = new NmmFileName(options.InputPath);
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
            }
            else
            {
                bcr.NumberOfProfiles = 1;
            }
            if (bcr.NumberOfProfiles == 1)
            {
                if (nmmScanData.MetaData.ScanFieldDeltaY == 0)
                    bcr.YScale = nmmScanData.MetaData.ScanFieldDeltaX; // quadratic pixels for single profile lines
                else
                    bcr.YScale = nmmScanData.MetaData.ScanFieldDeltaY;
                if (options.LineOnly)
                    bcr.YScale = 0; // but force 1D line if requested
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
            bcrMetaData.Add("SampleIdentifier", nmmScanData.MetaData.SampleIdentifier);
            bcrMetaData.Add("SampleSpecies", nmmScanData.MetaData.SampleSpecies);
            bcrMetaData.Add("SampleSpecification", nmmScanData.MetaData.SampleSpecification);
            bcrMetaData.Add("UserComment", options.UserComment);
            bcrMetaData.Add("InputFile", nmmScanData.MetaData.BaseFileName);
            bcrMetaData.Add("ConvertedBy", $"{HeadingInfo.Default}");
            bcrMetaData.Add("NMMReader", $"{typeof(NmmScanData).Assembly.GetName().Name} {typeof(NmmScanData).Assembly.GetName().Version}");
            bcrMetaData.Add("BcrWriter", $"{typeof(BcrWriter).Assembly.GetName().Name} {typeof(BcrWriter).Assembly.GetName().Version}");
            bcrMetaData.Add("OperatorName", nmmScanData.MetaData.User);
            bcrMetaData.Add("Organisation", nmmScanData.MetaData.Organisation);
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
            if (nmmScanData.NonlinearityCorrectionApplied)
            {
                bcrMetaData.Add("HeydemannCorrection", $"Span {nmmScanData.HeydemannCorrectionSpan * 1e9:F1} nm");
                bcrMetaData.Add("MatusDaiCorrection", $"Span {nmmScanData.DaiCorrectionSpan * 1e9:F1} nm");
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
            bcrMetaData.Add("ScanFieldCenterX", $"{nmmScanData.MetaData.ScanFieldCenterX:F9} m");
            bcrMetaData.Add("ScanFieldCenterY", $"{nmmScanData.MetaData.ScanFieldCenterY:F9} m");
            bcrMetaData.Add("ScanFieldCenterZ", $"{nmmScanData.MetaData.ScanFieldCenterZ:F9} m");
            bcrMetaData.Add("ScanFieldOriginX", $"{nmmScanData.MetaData.ScanFieldOriginX:F9} m");
            bcrMetaData.Add("ScanFieldOriginY", $"{nmmScanData.MetaData.ScanFieldOriginY:F9} m");
            bcrMetaData.Add("ScanFieldOriginZ", $"{nmmScanData.MetaData.ScanFieldOriginZ:F9} m");
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
            string outFileName = GetOutputFilename(nmmFileName, topographyProcessType);
            ConsoleUI.WritingFile(outFileName);
            if (!bcr.WriteToFile(outFileName))
            {
                ConsoleUI.Abort();
                ConsoleUI.ErrorExit("!could not write file", 4);
            }
            ConsoleUI.Done();
        }

        static string GetOutputFilename(NmmFileName nfm, TopographyProcessType topo)
        {
            string outFileName;
            string channelSuffix = string.Empty;
            string traceSuffix = string.Empty;
            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                outFileName = nfm.GetFreeFileNameWithIndex("sdf");
                if (options.AddPostfix)
                {
                    switch (topo)
                    {
                        case TopographyProcessType.None:
                            break;
                        case TopographyProcessType.ForwardOnly:
                            traceSuffix = "_f";
                            break;
                        case TopographyProcessType.BackwardOnly:
                            traceSuffix = "_b";
                            break;
                        case TopographyProcessType.Average:
                            traceSuffix = "_a";
                            break;
                        case TopographyProcessType.Difference:
                            traceSuffix = "_d";
                            break;
                        default:
                            break;
                    }
                    if (!string.Equals(options.ChannelSymbol, "-LZ+AZ", StringComparison.OrdinalIgnoreCase))
                    {
                        channelSuffix = $"_{options.ChannelSymbol.ToUpper()}";
                    }
                }
                outFileName = $"{Path.GetFileNameWithoutExtension(outFileName)}{traceSuffix}{channelSuffix}{Path.GetExtension(outFileName)}";
            }
            else
            {
                outFileName = options.OutputPath;
            }
            return outFileName;
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
