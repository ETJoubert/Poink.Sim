using System;
using System.Linq;
using Poink.Sim.Simulation;
using Xunit;

namespace Poink.Sim.Tests.Simulation;

public class RingBufferTests
{
	[Fact]
	public void Add_BelowCapacity_KeepsInsertionOrder()
	{
		var buffer = new RingBuffer<int>(3);

		buffer.Add(1);
		buffer.Add(2);

		Assert.Equal(2, buffer.Count);
		Assert.Equal(new[] { 1, 2 }, buffer);
	}

	[Fact]
	public void Add_PastCapacity_DropsOldest()
	{
		var buffer = new RingBuffer<int>(3);

		foreach (var value in Enumerable.Range(1, 5))
			buffer.Add(value);

		Assert.Equal(3, buffer.Count);
		Assert.Equal(3, buffer[0]);
		Assert.Equal(5, buffer[2]);
		Assert.Equal(new[] { 3, 4, 5 }, buffer.ToList());
	}

	[Fact]
	public void ToList_AfterWrap_ReturnsOldestFirstWithRequestedHeadroom()
	{
		var buffer = new RingBuffer<int>(4);
		foreach (var value in Enumerable.Range(1, 6))
			buffer.Add(value);

		var list = buffer.ToList(extraCapacity: 10);

		Assert.Equal(new[] { 3, 4, 5, 6 }, list);
		Assert.True(list.Capacity >= 14);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(2)]
	public void Indexer_OutsideCount_Throws(int index)
	{
		var buffer = new RingBuffer<int>(5);
		buffer.Add(1);
		buffer.Add(2);

		Assert.Throws<ArgumentOutOfRangeException>(() => buffer[index]);
	}

	[Fact]
	public void Version_ChangesOnAddAndClear()
	{
		var buffer = new RingBuffer<int>(2);
		long initial = buffer.Version;

		buffer.Add(1);
		long afterAdd = buffer.Version;
		buffer.Clear();

		Assert.NotEqual(initial, afterAdd);
		Assert.NotEqual(afterAdd, buffer.Version);
		Assert.Empty(buffer);
	}

	[Fact]
	public void Clear_ThenAdd_StartsFresh()
	{
		var buffer = new RingBuffer<int>(2);
		buffer.Add(1);
		buffer.Add(2);
		buffer.Add(3);

		buffer.Clear();
		buffer.Add(9);

		Assert.Equal(new[] { 9 }, buffer.ToList());
	}
}
