using System.ComponentModel.DataAnnotations;

namespace SCRM.Models.Identity
{
    public class Permission
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Module { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public string? Group { get; set; }

        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
    }
}