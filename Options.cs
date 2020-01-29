using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace Nmm2Bcr
{
    class Options
    {
        [Option('c', "channel", DefaultValue = "-LZ+AZ", HelpText = "Channel to export.")]
        public string ChannelSymbol { get; set; }

        [Option('s', "scan", DefaultValue = 0, HelpText = "Scan index for multiscan files.")]
        public int ScanIndex { get; set; }

        [Option('r', "reference", DefaultValue = 1, HelpText = "Mode # of height referencing.")]
        public int ReferenceMode { get; set; }

        [Option('b', "bias", DefaultValue = 0.0, HelpText = "bias value [um] to be subtracted.")]
        public double Bias { get; set; }

        [Option('q', "quiet", HelpText = "Quiet mode. No screen output (except for errors).")]
        public bool BeQuiet { get; set; }

        [Option("iso", HelpText = "Output file ISO 25178-71:2012 compliant.")]
        public bool IsoFormat { get; set; }

        [Option("heydemann", HelpText = "Perform Heydemann correction.")]
        public bool Heydemann { get; set; }

        [Option("back", HelpText = "Use backtrace profile (when present).")]
        public bool UseBack { get; set; }

        [Option("both", HelpText = "Mean of forward and backtrace profile (when present).")]
        public bool UseBoth { get; set; }

        [Option("diff", HelpText = "Difference (forward-backtrace) profile (when present).")]
        public bool UseDiff { get; set; }

        [Option("relaxed", HelpText = "Allow large (>65535) field dimension")]
        public bool Relaxed { get; set; }

        [Option("profile", DefaultValue = 0, HelpText = "Extract single profile.")]
        public int ProfileIndex { get; set; }


        [ValueList(typeof(List<string>), MaximumElements = 2)]
        public IList<string> ListOfFileNames { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            string AppName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
            string AppVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            HelpText help = new HelpText
            {
                Heading = new HeadingInfo(AppName, "version " + AppVer),
                Copyright = new CopyrightInfo("Michael Matus", 2015),
                AdditionalNewLineAfterOption = false,
                AddDashesToOption = true
            };
            string sPre = "Program to convert scanning files by SIOS NMM-1 to BCR or ISO 25178-71:2012 raster data format. " +
                "The quadruple of files (dat ind dsc pos) are analyzed to obtain the required parameters. " +
                "A rudimentary data processing is possible via the -r option.";
            help.AddPreOptionsLine(sPre);
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine("Usage: " + AppName + " filename1 [filename2] [options]");
            help.AddPostOptionsLine("");
            help.AddPostOptionsLine("Supported values for -r: 1=min 2=max 3=avarage 4=mid 5=bias 6=first 7=last 8=center 9=linear 10=LSQ");

            help.AddOptions(this);

            return help;
        }


    }
}
