using Microsoft.EntityFrameworkCore;
using Poink.Sim.Data.Entities;

namespace Poink.Sim.Data
{
    public class PoinkSimContext : DbContext
    {
        public PoinkSimContext(DbContextOptions<PoinkSimContext> options) : base(options) { }

        public DbSet<CTraderAccountToken> CTraderAccountTokens { get; set; }
    }
}
