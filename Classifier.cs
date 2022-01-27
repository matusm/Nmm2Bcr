namespace Nmm2Bcr
{
    class Classifier
    {

        public Classifier(int[] intensity)
        {
            this.intensity = intensity;
        }

        public int[] GetSegmentedProfile(double threshold, int lower, int upper)
        {
            int intThreshold = (int)((upper - lower) * threshold);
            int[] segmented = new int[intensity.Length];
            for (int i = 0; i < segmented.Length; i++)
            {
                if (intensity[i] > intThreshold)
                    segmented[i] = 1;
                else
                    segmented[i] = 0;
            }
            return segmented;
        }

        private readonly int[] intensity;

    }
}
