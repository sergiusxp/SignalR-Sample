using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using SignalR_Sample.Data;

namespace SignalR_Sample
{
    public class NotificationHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly IDataProtector _protector;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public NotificationHub(
            ApplicationDbContext context,
            IDataProtectionProvider dataProtectionProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _protector = dataProtectionProvider.CreateProtector("SignalRSample.SecretCookies");
            _httpContextAccessor = httpContextAccessor;
        }

        public override async Task OnConnectedAsync()
        {
            string userIdProtected = _httpContextAccessor?.HttpContext?.Request.Cookies["UserId"] ?? "";
            string userId = _protector.Unprotect(userIdProtected);

            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            await base.OnConnectedAsync();
        }
    }
}
