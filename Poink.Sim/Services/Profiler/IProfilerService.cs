using System;
using System.Threading.Tasks;
using Poink.Sim.Models;

namespace Poink.Sim.Profiler;

internal interface IProfilerService
{
	Task<ProfilerChunk> LoadAsync(ProfilerConfig config, DateTime beforeUtc, int barCount);
}
