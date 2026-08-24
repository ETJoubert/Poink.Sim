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

        public string StrategyName { get; set; } = string.Empty;

        public string RewardPolicyName { get; set; } = string.Empty;

        public string Dataset { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public string ParametersJson { get; set; } = string.Empty;

        /// <summary>The network's structure at the moment this training run started -- kept separate from the live <see cref="NeuralNetwork.StructureJson"/> since that can be edited later.</summary>
        [Column(TypeName = "jsonb")]
        public string? StructureSnapshotJson { get; set; }

        public byte[] Data { get; set; } = Array.Empty<byte>();

        public long WeightCount { get; set; }

        public double AvgReward { get; set; }

        public DateTime? TrainedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
