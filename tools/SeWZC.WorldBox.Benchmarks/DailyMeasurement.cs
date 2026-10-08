internal readonly record struct DailyMeasurement(long Tick, int StartPopulation, int EndPopulation,
    double WallMs, double ProcessCpuMs, long AllocatedBytes, int Gen0, int Gen1, int Gen2, double GcPauseMs);
