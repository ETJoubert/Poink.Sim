using System;

namespace Poink.ZTicks;

public readonly record struct TickRecord(DateTime TimestampUtc, double Bid, double Ask);
