using System;
using System.ComponentModel.DataAnnotations;

namespace Poink.Sim.Data.Entities
{
    public class CTraderAccountToken
    {
        [Key]
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ChangedAt { get; set; }

        public bool IsDeleted { get; set; }
    }
}
