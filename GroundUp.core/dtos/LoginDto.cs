using System.ComponentModel.DataAnnotations;

namespace GroundUp.core.dtos
{
    public class LoginDto
    {
        [Required]
        public string Identifier { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }
}