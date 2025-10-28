using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using Bloglytics.DTO;

namespace Bloglytics.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection");
        }

        // ================ LOGIN ================
        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("UserId") != null)
            {
                return RedirectToAction("Index", "Dashboard");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await ValidateUserAsync(model.Email, model.Password);

                if (user != null)
                {
                    await UpdateLastLoginAsync(user.UserId);

                    var token = GenerateJwtToken(user);

                    Response.Cookies.Append("AuthToken", token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = model.RememberMe
                            ? DateTimeOffset.UtcNow.AddDays(30)
                            : DateTimeOffset.UtcNow.AddHours(8)
                    });

                    HttpContext.Session.SetString("UserId", user.UserId.ToString());
                    HttpContext.Session.SetString("UserName", user.FullName);
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetString("UserRole", user.Role);

                    TempData["SuccessMessage"] = $"Welcome back, {user.FullName}!";

                    if (user.Role == "Admin")
                    {
                        return RedirectToAction("Index", "Admin");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Dashboard");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    TempData["ErrorMessage"] = "Invalid email or password. Please try again.";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An error occurred during login.");
                TempData["ErrorMessage"] = "An error occurred. Please try again later.";
                return View(model);
            }
        }

        // ================ REGISTRATION ================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // Check if email already exists
                if (await EmailExistsAsync(model.Email))
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    return View(model);
                }

                // Generate OTP
                string otp = GenerateOTP();

                // Store registration data and OTP in session temporarily
                HttpContext.Session.SetString("RegEmail", model.Email);
                HttpContext.Session.SetString("RegFullName", model.FullName);
                HttpContext.Session.SetString("RegPassword", model.Password); // Hash this in production
                HttpContext.Session.SetString("RegOTP", otp);
                HttpContext.Session.SetString("OTPExpiry", DateTime.Now.AddMinutes(10).ToString());

                // Send OTP email
                bool emailSent = await SendOTPEmailAsync(model.Email, model.FullName, otp);

                if (emailSent)
                {
                    TempData["SuccessMessage"] = "OTP has been sent to your email. Please verify to complete registration.";
                    return RedirectToAction("VerifyOTP", new { email = model.Email });
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to send OTP. Please try again.";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred during registration.";
                return View(model);
            }
        }

        // ================ VERIFY OTP ================
        [HttpGet]
        public IActionResult VerifyOTP(string email)
        {
            if (string.IsNullOrEmpty(email) || HttpContext.Session.GetString("RegEmail") != email)
            {
                return RedirectToAction("Register");
            }

            var model = new VerifyOTPViewModel { Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(VerifyOTPViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                string storedOTP = HttpContext.Session.GetString("RegOTP");
                string expiryStr = HttpContext.Session.GetString("OTPExpiry");

                if (string.IsNullOrEmpty(storedOTP) || string.IsNullOrEmpty(expiryStr))
                {
                    TempData["ErrorMessage"] = "OTP session expired. Please register again.";
                    return RedirectToAction("Register");
                }

                DateTime expiry = DateTime.Parse(expiryStr);
                if (DateTime.Now > expiry)
                {
                    TempData["ErrorMessage"] = "OTP has expired. Please request a new one.";
                    return View(model);
                }

                if (model.OTP == storedOTP)
                {
                    // OTP is correct, create user account
                    string email = HttpContext.Session.GetString("RegEmail");
                    string fullName = HttpContext.Session.GetString("RegFullName");
                    string password = HttpContext.Session.GetString("RegPassword");

                    await CreateUserAsync(email, fullName, password);

                    // Clear session
                    HttpContext.Session.Remove("RegEmail");
                    HttpContext.Session.Remove("RegFullName");
                    HttpContext.Session.Remove("RegPassword");
                    HttpContext.Session.Remove("RegOTP");
                    HttpContext.Session.Remove("OTPExpiry");

                    TempData["SuccessMessage"] = "Registration successful! Please login with your credentials.";
                    return RedirectToAction("Login");
                }
                else
                {
                    TempData["ErrorMessage"] = "Invalid OTP. Please try again.";
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred during verification.";
                return View(model);
            }
        }

        // ================ RESEND OTP ================
        [HttpPost]
        public async Task<JsonResult> ResendOTP(string email)
        {
            try
            {
                string fullName = HttpContext.Session.GetString("RegFullName");

                if (string.IsNullOrEmpty(fullName))
                {
                    return Json(new { success = false, message = "Session expired" });
                }

                string otp = GenerateOTP();
                HttpContext.Session.SetString("RegOTP", otp);
                HttpContext.Session.SetString("OTPExpiry", DateTime.Now.AddMinutes(10).ToString());

                bool sent = await SendOTPEmailAsync(email, fullName, otp);
                return Json(new { success = sent });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        // ================ FORGOT PASSWORD ================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var user = await GetUserByEmailAsync(model.Email);

                if (user != null)
                {
                    // Generate reset token
                    string token = Guid.NewGuid().ToString();

                    // Store token in database
                    await SavePasswordResetTokenAsync(user.UserId, token);

                    // Send reset email
                    bool emailSent = await SendPasswordResetEmailAsync(model.Email, user.FullName, token);

                    if (emailSent)
                    {
                        TempData["SuccessMessage"] = "Password reset link has been sent to your email.";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Failed to send email. Please try again.";
                    }
                }
                else
                {
                    // Don't reveal if email exists for security
                    TempData["SuccessMessage"] = "If the email exists, a password reset link has been sent.";
                }

                return View();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return View(model);
            }
        }

        // ================ RESET PASSWORD ================
        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token, string email)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid reset link.";
                return RedirectToAction("Login");
            }

            // Verify token
            bool isValid = await VerifyResetTokenAsync(email, token);
            if (!isValid)
            {
                TempData["ErrorMessage"] = "Reset link is invalid or has expired.";
                return RedirectToAction("Login");
            }

            var model = new ResetPasswordViewModel
            {
                Email = email,
                Token = token
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                bool isValid = await VerifyResetTokenAsync(model.Email, model.Token);
                if (!isValid)
                {
                    TempData["ErrorMessage"] = "Reset link is invalid or has expired.";
                    return RedirectToAction("Login");
                }

                // Update password
                await UpdatePasswordAsync(model.Email, model.NewPassword);

                // Invalidate token
                await InvalidateResetTokenAsync(model.Token);

                TempData["SuccessMessage"] = "Password reset successful! Please login with your new password.";
                return RedirectToAction("Login");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred. Please try again.";
                return View(model);
            }
        }

        // ================ LOGOUT ================
        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("AuthToken");
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        // ================ HELPER METHODS ================

        private async Task<UserDto> ValidateUserAsync(string email, string password)
        {
            UserDto user = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT UserId, Email, PasswordHash, FullName, Role, IsActive, EmailConfirmed 
                    FROM Users 
                    WHERE Email = @Email AND IsActive = 1 AND EmailConfirmed = 1";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string storedPassword = reader["PasswordHash"].ToString();

                            if (storedPassword == password)
                            {
                                user = new UserDto
                                {
                                    UserId = Convert.ToInt32(reader["UserId"]),
                                    Email = reader["Email"].ToString(),
                                    FullName = reader["FullName"].ToString(),
                                    Role = reader["Role"].ToString(),
                                    IsActive = Convert.ToBoolean(reader["IsActive"])
                                };
                            }
                        }
                    }
                }
            }

            return user;
        }

        private async Task UpdateLastLoginAsync(int userId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "UPDATE Users SET LastLoginAt = @LoginTime WHERE UserId = @UserId";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@LoginTime", DateTime.Now);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<bool> EmailExistsAsync(string email)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT COUNT(*) FROM Users WHERE Email = @Email";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task CreateUserAsync(string email, string fullName, string password)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    INSERT INTO Users (Email, PasswordHash, FullName, Role, IsActive, EmailConfirmed, CreatedAt)
                    VALUES (@Email, @Password, @FullName, 'Blogger', 1, 1, @CreatedAt)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Password", password); // Hash in production
                    cmd.Parameters.AddWithValue("@FullName", fullName);
                    cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<UserDto> GetUserByEmailAsync(string email)
        {
            UserDto user = null;

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                string query = "SELECT UserId, Email, FullName, Role FROM Users WHERE Email = @Email AND IsActive = 1";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);

                    using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            user = new UserDto
                            {
                                UserId = Convert.ToInt32(reader["UserId"]),
                                Email = reader["Email"].ToString(),
                                FullName = reader["FullName"].ToString(),
                                Role = reader["Role"].ToString()
                            };
                        }
                    }
                }
            }

            return user;
        }

        private async Task SavePasswordResetTokenAsync(int userId, string token)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                // First, create the table if it doesn't exist
                string createTable = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PasswordResetTokens')
                    CREATE TABLE PasswordResetTokens (
                        TokenId INT PRIMARY KEY IDENTITY(1,1),
                        UserId INT NOT NULL,
                        Token NVARCHAR(500) NOT NULL,
                        ExpiryDate DATETIME NOT NULL,
                        IsUsed BIT NOT NULL DEFAULT 0,
                        CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
                        FOREIGN KEY (UserId) REFERENCES Users(UserId)
                    )";

                using (SqlCommand cmd = new SqlCommand(createTable, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert token
                string query = @"
                    INSERT INTO PasswordResetTokens (UserId, Token, ExpiryDate)
                    VALUES (@UserId, @Token, @ExpiryDate)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@Token", token);
                    cmd.Parameters.AddWithValue("@ExpiryDate", DateTime.Now.AddHours(1));

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<bool> VerifyResetTokenAsync(string email, string token)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    SELECT COUNT(*) FROM PasswordResetTokens prt
                    INNER JOIN Users u ON prt.UserId = u.UserId
                    WHERE u.Email = @Email AND prt.Token = @Token 
                    AND prt.ExpiryDate > GETDATE() AND prt.IsUsed = 0";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Token", token);

                    int count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task UpdatePasswordAsync(string email, string newPassword)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = @"
                    UPDATE Users 
                    SET PasswordHash = @Password, UpdatedAt = @UpdatedAt
                    WHERE Email = @Email";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    cmd.Parameters.AddWithValue("@Password", newPassword); // Hash in production
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InvalidateResetTokenAsync(string token)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                string query = "UPDATE PasswordResetTokens SET IsUsed = 1 WHERE Token = @Token";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Token", token);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        private string GenerateOTP()
        {
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        private string GenerateJwtToken(UserDto user)
        {
            var securityKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ================ EMAIL SENDING ================

        private async Task<bool> SendOTPEmailAsync(string toEmail, string userName, string otp)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        _configuration["Email:Username"],
                        _configuration["Email:Password"]),
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromEmail"], "Bloglytics"),
                    Subject = "Email Verification - OTP Code",
                    Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #667eea;'>Welcome to Bloglytics!</h2>
                                <p>Hello {userName},</p>
                                <p>Thank you for registering with Bloglytics. Please use the following OTP to verify your email address:</p>
                                <div style='background: #f8f9fa; padding: 20px; text-align: center; margin: 20px 0;'>
                                    <h1 style='color: #667eea; font-size: 36px; letter-spacing: 10px; margin: 0;'>{otp}</h1>
                                </div>
                                <p>This OTP is valid for 10 minutes.</p>
                                <p>If you didn't request this, please ignore this email.</p>
                                <br/>
                                <p>Best regards,<br/>The Bloglytics Team</p>
                            </div>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Log error
                return false;
            }
        }

        private async Task<bool> SendPasswordResetEmailAsync(string toEmail, string userName, string token)
        {
            try
            {
                string resetLink = Url.Action("ResetPassword", "Account",
                    new { token = token, email = toEmail }, Request.Scheme);

                var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential(
                        _configuration["Email:Username"],
                        _configuration["Email:Password"]),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_configuration["Email:FromEmail"], "Bloglytics"),
                    Subject = "Password Reset Request",
                    Body = $@"
                        <html>
                        <body style='font-family: Arial, sans-serif;'>
                            <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <h2 style='color: #667eea;'>Password Reset Request</h2>
                                <p>Hello {userName},</p>
                                <p>We received a request to reset your password. Click the button below to reset it:</p>
                                <div style='text-align: center; margin: 30px 0;'>
                                    <a href='{resetLink}' style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 15px 40px; text-decoration: none; border-radius: 8px; display: inline-block; font-weight: 600;'>
                                        Reset Password
                                    </a>
                                </div>
                                <p>This link will expire in 1 hour.</p>
                                <p>If you didn't request a password reset, please ignore this email.</p>
                                <p style='color: #999; font-size: 12px; margin-top: 30px;'>
                                    If the button doesn't work, copy and paste this link into your browser:<br/>
                                    {resetLink}
                                </p>
                                <br/>
                                <p>Best regards,<br/>The Bloglytics Team</p>
                            </div>
                        </body>
                        </html>
                    ",
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(toEmail);

                await smtpClient.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                // Log error
                return false;
            }
        }
    }

    // ================ VIEW MODELS ================



}