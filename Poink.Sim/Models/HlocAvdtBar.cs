

using System;

namespace Poink.Sim.Models;

internal readonly record struct HlocAvdtBar(
    DateTime OpenTime, DateTime CloseTime, DateTime HighTime, DateTime LowTime,
    double Open, double High, double Low, double Close,
    double Average, int Volume, double Distance, int UpVolume, int DownVolume);
