using Microsoft.EntityFrameworkCore;
using Poink.Sim.Data.Entities;

// Migrate dotnet ef migrations add AddCTraderAccountTokens --output-dir Data/Migrations/PoinkSim
namespace Poink.Sim.Data
{
    public class PoinkSimContext : DbContext
    {
        public PoinkSimContext(DbContextOptions<PoinkSimContext> options) : base(options) { }

        public DbSet<CTraderAccountToken> CTraderAccountTokens { get; set; }

        public DbSet<TrainingResult> TrainingResults { get; set; }

        public DbSet<NeuralNetwork> NeuralNetworks { get; set; }

        public DbSet<NeuralWeights> NeuralWeights { get; set; }
    }
}
