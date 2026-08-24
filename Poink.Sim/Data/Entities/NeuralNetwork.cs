using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Poink.Sim.Data.Entities
{
    /// <summary>A network's structure (encoders, architecture, output heads) as built from the frontend. Weights produced by training it are recorded separately in <see cref="NeuralWeights"/>, since one structure can be retrained many times.</summary>
    public class NeuralNetwork
    {
        [Key]
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public string StructureJson { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public ICollection<NeuralWeights> Weights { get; set; } = new List<NeuralWeights>();
    }
}
