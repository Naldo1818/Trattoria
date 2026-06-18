using System.ComponentModel.DataAnnotations;

namespace Trattoria.ViewModels
{
    public class IndexViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(20, MinimumLength = 5, ErrorMessage = "Password must be at least 10 characters long.")]
        [RegularExpression(@"^\S*$", ErrorMessage = "Password must not contain spaces.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}
