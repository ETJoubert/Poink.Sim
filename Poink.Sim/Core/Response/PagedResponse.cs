using System;
using System.Collections.Generic;

namespace Poink.Sim.Core
{
    public class PagedResponse<T>
    {
        public List<T> Items { get; init; } = [];
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
