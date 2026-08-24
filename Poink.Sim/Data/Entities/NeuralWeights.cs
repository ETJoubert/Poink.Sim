using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Poink.Sim.Data.Entities
{
    /// <summary>One trained instance of a <see cref="NeuralNetwork"/>'s weights. A network accrues many of these as it undergoes separate training runs.</summary>
    public class NeuralWeights
    {
        [Key]
        public Guid Id { get; set; }

        public Guid NeuralNetworkId { get; set; }

        [ForeignKey(nameof(NeuralNetworkId))]
        public NeuralNetwork NeuralNetwork { get; set; } = null!;

        public string Name { get; set; } = string.Empty;

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public long WeightCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
