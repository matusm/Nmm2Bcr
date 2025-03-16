using CommandLine;

namespace Nmm2Bcr
{
    class Options
    {
        [Option('c', "channel", Default = "-LZ+AZ", HelpText = "Channel to export.")]
        public string ChannelSymbol { get; set; }

        [Option('s', "scan", Default = 0, HelpText = "Scan index for multi-scan files.")]
        public int ScanIndex { get; set; }

        [Option('r', "reference", Default = 0, HelpText = "Height reference technique.")]
        public int ReferenceMode { get; set; }

        [Option('z', "zscale", Default = 1e-6, HelpText = "Scale factor for height axis.")]
        public double ZScale { get; set; }

        [Option('b', "bias", Default = 0.0, HelpText = "bias value [um] to be subtracted.")]
        public double Bias { get; set; }

        [Option('q', "quiet", HelpText = "Quiet mode. No screen output (except for errors).")]
        public bool BeQuiet { get; set; }

        [Option("comment", Default = "---", HelpText = "User supplied comment string.")]
        public string UserComment { get; set; }

        [Option("iso", HelpText = "Output file ISO 25178-71:2012 compliant.")]
        public bool IsoFormat { get; set; }

        [Option("heydemann", HelpText = "Perform Heydemann correction.")]
        public bool DoHeydemann { get; set; }

        [Option("back", HelpText = "Use backtrace profile (when present).")]
        public bool UseBack { get; set; }

        [Option("both", HelpText = "Mean of forward and backtrace profile (when present).")]
        public bool UseBoth { get; set; }

        [Option("diff", HelpText = "Difference (forward-backtrace) profile (when present).")]
        public bool UseDiff { get; set; }

        [Option("strict", HelpText = "Force standardized format.")]
        public bool Strict { get; set; }

        [Option('p', "profile", Default = 0, HelpText = "Extract single profile.")]
        public int ProfileIndex { get; set; }

        [Option("1Dprofile", HelpText = "Force single profiles to be of width 0.")]
        public bool LineOnly { get; set; }

        [Value(0, MetaName = "InputPath", Required = true, HelpText = "Input file-name including path")]
        public string InputPath { get; set; }

        [Value(1, MetaName = "OutputPath", HelpText = "Output file-name including path")]
        public string OutputPath { get; set; }

    }
}
