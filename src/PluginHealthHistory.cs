namespace TrafficSense;

internal readonly record struct PluginHealthPoint(TimeSpan Timestamp,double? CpuPercent,long? WorkingSetBytes);

internal sealed class PluginHealthHistory
{
    internal static readonly TimeSpan Duration=TimeSpan.FromMinutes(2);
    internal const int MaximumSamples=128;
    private readonly Queue<PluginHealthPoint> _samples=[];
    internal IReadOnlyCollection<PluginHealthPoint> Samples=>_samples;
    internal void Clear()=>_samples.Clear();
    internal void Add(TimeSpan timestamp,double? cpuPercent,long? workingSetBytes)
    {
        if(timestamp<TimeSpan.Zero || (_samples.Count>0 && timestamp<=_samples.Last().Timestamp))return;
        if(cpuPercent is {} cpu && (!double.IsFinite(cpu) || cpu<0 || cpu>100))cpuPercent=null;
        if(workingSetBytes<0)workingSetBytes=null;
        _samples.Enqueue(new(timestamp,cpuPercent,workingSetBytes));
        while(_samples.Count>MaximumSamples || (timestamp-_samples.Peek().Timestamp)>Duration)_samples.Dequeue();
    }
}
