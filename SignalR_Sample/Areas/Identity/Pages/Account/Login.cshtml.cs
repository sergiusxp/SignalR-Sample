// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SignalR_Sample.Data;
using SignalR_Sample.Models;
using SignalR_Sample.Services;
using System.ComponentModel.DataAnnotations;

namespace SignalR_Sample.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IEmailService _emailSender;
        private readonly ApplicationDbContext _dbContext;
        private readonly IDataProtector _protector;

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            ILogger<LoginModel> logger,
            IEmailService emailSender,
            ApplicationDbContext dbContext,
            IDataProtectionProvider dataProtectionProvider)
        {
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _dbContext = dbContext;
            _protector = dataProtectionProvider.CreateProtector("SignalRSample.SecretCookies");
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl = Url.Content("/Account/Awaiting");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true

                var user = await _signInManager.UserManager.FindByNameAsync(Input.Email);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }

                DateTime expiration = DateTime.UtcNow.AddMinutes(3);
                long ts = ((DateTimeOffset)expiration).ToUnixTimeSeconds();
                var hashedCode = Crypto.CalculateSHA1($"{user.Id}_{user.Email}_{ts}");

                var result = await _signInManager.CheckPasswordSignInAsync(user, Input.Password, Input.RememberMe);
                if (result.Succeeded)
                {
                    // Let's check if we already sent an OTP request for the user
                    bool alreadySent = await _dbContext.Otps.AnyAsync(x => x.UserId == user.Id && DateTime.UtcNow < x.ExpirationDate);

                    if (!alreadySent)
                    {
                        // create some cookies for client security
                        CookieOptions option = new CookieOptions();
                        option.Expires = expiration;
                        option.Secure = true;
                        option.HttpOnly = true;
                        Response.Cookies.Append("UserId", _protector.Protect(user.Id), option);
                        Response.Cookies.Append("UserEmail", _protector.Protect(user.Email), option);
                        Response.Cookies.Append("OtpActive", _protector.Protect("true"), option);

                        Otp otp = new Otp
                        {
                            OtpRequestId = Guid.NewGuid(),
                            UserId = user.Id,
                            ExpirationDate = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime // to keep consistency in db
                        };

                        var request = HttpContext.Request;
                        var host = $"{request.Scheme}://{request.Host}";
                        string otpMessage = $"Hello! Please <a href=\"{host}/Account/Otp/{otp.OtpRequestId}/{ts}\">click here</a> to login. This link has validy of 3 minutes.";
                        await _emailSender.SendEmailAsync(user.Email, "Your OTP link", otpMessage);
                        _logger.LogInformation("OTP Email sent.");

                        Response.Cookies.Append("ReqId", _protector.Protect(otp.OtpRequestId.ToString()), option);
                        await _dbContext.Otps.AddAsync(otp);
                        await _dbContext.SaveChangesAsync();
                    }

                    _logger.LogInformation("User logged in. Opening OTP waiting page.");
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
