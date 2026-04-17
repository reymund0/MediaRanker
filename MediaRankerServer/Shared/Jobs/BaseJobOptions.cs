
public abstract class BaseJobOptions
{
    public abstract string JobName { get; }
    public abstract bool Enabled { get; set; }
    public abstract int ScheduleHourUtc { get; set; }
}
