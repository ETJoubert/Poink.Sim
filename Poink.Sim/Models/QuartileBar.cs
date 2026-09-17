using System;

namespace Poink.Sim.Models;

internal readonly record struct QuartileBar(
	DateTime OpenTime,
	DateTime CloseTime,
	double Min,
	double Q1,
	double Median,
	double Q3,
	double Max,
	int Count
);
