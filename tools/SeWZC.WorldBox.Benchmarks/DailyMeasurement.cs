/// <summary>一次完整模拟日的负载和性能测量结果。</summary>
/// <param name="Tick">本日结束时的日序。</param>
/// <param name="StartPopulation">计时开始时的存活人数。</param>
/// <param name="EndPopulation">计时结束时的存活人数。</param>
/// <param name="WallMs">推进本日的墙钟耗时，单位毫秒。</param>
/// <param name="ProcessCpuMs">本日的全进程 CPU 耗时，单位毫秒；平台未支持采集时为空。</param>
/// <param name="AllocatedBytes">全部托管线程在本日测量窗口的分配字节数。</param>
/// <param name="Gen0">本日零代回收次数。</param>
/// <param name="Gen1">本日一代回收次数。</param>
/// <param name="Gen2">本日二代回收次数。</param>
/// <param name="GcPauseMs">本日发生回收时最后一次回收记录的暂停总时长，单位毫秒。</param>
internal readonly record struct DailyMeasurement(long Tick, int StartPopulation, int EndPopulation,
    double WallMs, double? ProcessCpuMs, long AllocatedBytes, int Gen0, int Gen1, int Gen2, double GcPauseMs);
