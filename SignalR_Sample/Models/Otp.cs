using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace SignalR_Sample.Models
{
    public class Otp
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid OtpRequestId { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string Secret { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string UserId { get; set; }
        public IdentityUser User { get; set; }
    }
}
