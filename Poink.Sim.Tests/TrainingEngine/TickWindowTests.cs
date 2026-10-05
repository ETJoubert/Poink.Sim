using System;
using System.Linq;
using Poink.Sim.Repository;
using Poink.Sim.Services.TrainingEngine;
using Poink.Sim.Tests.TestSupport;
using Xunit;

namespace Poink.Sim.Tests.TrainingEngine;

public class TickWindowTests
{
	private static readonly Tick[] Source = Enumerable.Range(0, 10).Select(i => TestTickHelper.AtMid(i, i)).ToArray();

	[Fact]
	public void Window_ExposesSliceOfSource()
	{
		var window = new TickWindow(Source, 2, 5);

		Assert.Equal(3, window.Count);
		Assert.Equal(Source[2], window[0]);
		Assert.Equal(Source[2..5], window);
	}

	[Fact]
	public void Window_OverWindow_IsRelativeToInnerWindow()
	{
		var inner = new TickWindow(Source, 2, 8);

		var outer = new TickWindow(inner, 1, 3);

		Assert.Equal(new[] { Source[3], Source[4] }, outer);
	}

	[Fact]
	public void Window_EndBeyondSource_IsClamped()
	{
		Assert.Equal(5, new TickWindow(Source, 5, 100).Count);
	}

	[Fact]
	public void Window_StartBeyondEnd_IsEmpty()
	{
		Assert.Empty(new TickWindow(Source, 8, 4));
	}

	[Fact]
	public void ExtendTo_GrowsEndButNotPastSource()
	{
		var window = new TickWindow(Source, 0, 0);

		window.ExtendTo(3);
		Assert.Equal(3, window.Count);

		window.ExtendTo(50);
		Assert.Equal(10, window.Count);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(3)]
	public void Indexer_OutsideWindow_Throws(int index)
	{
		var window = new TickWindow(Source, 2, 5);

		Assert.Throws<ArgumentOutOfRangeException>(() => window[index]);
	}
}
