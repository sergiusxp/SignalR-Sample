using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignalR_Sample.Data;

namespace SignalR_Sample.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _dbContext;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly IDataProtector _protector;

        public AccountController(
            SignInManager<IdentityUser> signInManager, 
            ApplicationDbContext dbContext,
            IHubContext<NotificationHub> hubContext,
            IDataProtectionProvider dataProtectionProvider)
        {
            _signInManager = signInManager;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _protector = dataProtectionProvider.CreateProtector("SignalRSample.SecretCookies");
        }

        [HttpGet("{controller}/Awaiting")]
        public IActionResult Index()
        {
            return View("Otp");
        }

        [HttpGet("{controller}/Otp/{requestId}/{timeStamp}")]
        public async Task<IActionResult> Otp(string requestId, long timeStamp)
        {
            if (timeStamp < 0)
            {
                TempData["ErrorMessage"] = "Otp code not valid.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // 3 minutes
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timeStamp > 1800)
            {
                TempData["ErrorMessage"] = "Otp code expired.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            var userId = _dbContext.Otps.Where(x => x.OtpRequestId.ToString() == requestId).FirstOrDefault();
            if (userId == null) 
            {
                TempData["ErrorMessage"] = "Otp code not valid.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }
            string userIdSession = userId.UserId;

            var user = await _signInManager.UserManager.FindByIdAsync(userIdSession);
            if(user == null)
            {
                TempData["ErrorMessage"] = "Authentication failed.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // 2nd security method: check in db
            var timeStampDate = DateTimeOffset.FromUnixTimeSeconds(timeStamp).UtcDateTime;
            var otp = await _dbContext.Otps.FirstOrDefaultAsync(o => o.UserId == user.Id && o.ExpirationDate == timeStampDate);
            if (otp == null)
            {
                TempData["ErrorMessage"] = "Otp code not valid.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            try
            {
                await _hubContext.Clients.Group(userIdSession).SendAsync("ReceiveMsg", $"Authenticated");
            }
            catch (Exception error)
            {
                await _hubContext.Clients.Group(userIdSession).SendAsync("ReceiveMsgError", error.Message);
            }

            TempData["ErrorMessage"] = "";
            return LocalRedirect("/Home");
        }

        [HttpGet("{controller}/CheckOtpAuthenticity/{userId}")]
        public async Task<IActionResult> CheckOtpAuthenticity(string userId)
        {
            string notValidOtp = "Otp not valid or expired. Please try again.";
            string realUserId = _protector.Unprotect(userId);
            var validOtp = _dbContext.Otps.Where(otp => otp.UserId == realUserId
                && otp.ExpirationDate > DateTime.UtcNow
                && DateTime.UtcNow < otp.ExpirationDate).FirstOrDefault();

            if (validOtp == null)
            {
                return Json(new { success = false, message = notValidOtp });
            }

            // Handle expired OTPs
            var opts = await _dbContext.Otps.Where(otp => DateTime.UtcNow > otp.ExpirationDate).ToListAsync();
            if (validOtp != null) 
            {
                _dbContext.Otps.RemoveRange(opts);
                await _dbContext.SaveChangesAsync();
            }

            var user = await _signInManager.UserManager.FindByIdAsync(realUserId);
            await _signInManager.SignInAsync(user, true);
            return Json(new { success = true, message = "<b>Valid OTP!</b> Redirect in 3 seconds..." });
        }

        [Authorize]
        [HttpGet("{controller}/Secret")]
        public IActionResult Secret()
        {
            return View();
        }
    }
}
