//*******************************************************************************************
//
// Class to analyze a intensity data to find the two plateaus in binary masks 
//
// The maxima in the upper and lower half of the intensity histogram
// are taken as the UpperBound and LowerBound, respectively
// The whole analysis is performed by the constructor
//
//*******************************************************************************************

using System.Linq;

namespace Nmm2Bcr
{
    public class IntensityEvaluator
    {

        public IntensityEvaluator(int[] intensities)
        {
            this.intensities = intensities;
            maxIntensity = intensities.Max();
            minIntensity = intensities.Min();
            intensityRange = maxIntensity - minIntensity;
            CreateHistogram();
            // CreateHistogram must be called in advance
            lowerBound = FindPeak(0.0, 0.5);
            upperBound = FindPeak(0.5, 1.0);
        }

        public int MaxIntensity => maxIntensity;
        public int MinIntensity => minIntensity;
        public int LowerBound => lowerBound;
        public int UpperBound => upperBound;
        public int[] Histogram { get; private set; }

        // local fields are necessary
        // using computed properties will make the class extremly slow
        private readonly int[] intensities;
        private readonly int intensityRange;
        private readonly int maxIntensity;
        private readonly int minIntensity;
        private readonly int lowerBound;
        private readonly int upperBound;

        private void CreateHistogram()
        {
            Histogram = new int[intensityRange + 1];
            foreach (var intensity in intensities)
            {
                Histogram[intensity - minIntensity]++;
            }
        }

        private int FindPeak(double relLower, double relUpper)
        {
            int lower = (int)((double)intensityRange * relLower);
            int upper = (int)((double)intensityRange * relUpper);
            // rudimentary error check
            if (lower < 0) lower = 0;
            if (upper > intensityRange) upper = intensityRange;
            if (upper < lower)
            {
                int temp = upper;
                upper = lower;
                lower = temp;
            }
            int histMaximum = 0;
            int peakPosition = 0;
            for (int i = lower; i < upper; i++)
            {
                if (Histogram[i] > histMaximum)
                {
                    histMaximum = Histogram[i];
                    peakPosition = i;
                }
            }
            return peakPosition + minIntensity;
        }
    }
}
