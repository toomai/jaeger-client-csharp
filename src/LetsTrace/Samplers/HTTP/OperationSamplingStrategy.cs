namespace LetsTrace.Samplers.HTTP
{
    public class OperationSamplingStrategy
    {
        public string Operation { get; set; }

        public ProbabilisticSamplingStrategy ProbabilisticSampling { get; set; }
    }
}