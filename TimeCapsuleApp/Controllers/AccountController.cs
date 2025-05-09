using System;
using System.Linq;
using System.Net.Mail;
using System.Web.Mvc;
using TimeCapsuleApp;

namespace TimeCapsuleApp.Controllers
{
    public class AccountController : Controller
    {
        private TimeCapsuleDBEntities db = new TimeCapsuleDBEntities();

        // GET: Account/Login
        public ActionResult Login()
        {
            return View();
        }

        // POST: Account/Login
        [HttpPost]
        public ActionResult Login(string username, string password)
        {
            var user = db.USERS.FirstOrDefault(u => u.Username == username && u.PasswordHash == password);
            if (user != null)
            {
                Session["UserId"] = user.UserId;
                Session["Username"] = user.Username;

                return RedirectToAction("Index", "Message");
            }
            else
            {
                ViewBag.Error = "Kullanıcı adı veya şifre hatalı.";
                return View();
            }
        }

        public ActionResult Logout()
        {
            Session.Clear();
            return RedirectToAction("Login");
        }

        // GET: Account/Register
        public ActionResult Register()
        {
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        public ActionResult Register(string username, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Kullanıcı adı, şifre ve e-posta boş bırakılamaz.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Şifreler uyuşmuyor.";
                return View();
            }

            var existingUser = db.USERS.FirstOrDefault(u => u.Username == username);
            if (existingUser != null)
            {
                ViewBag.Error = "Bu kullanıcı adı zaten mevcut.";
                return View();
            }

            // Yeni kullanıcı oluştur
            var newUser = new USERS
            {
                Username = username,
                PasswordHash = password,
                Email = email,
                IsEmailConfirmed = false // Email onaylanmadı
            };

            db.USERS.Add(newUser);
            db.SaveChanges();

            // >>>>>> Onay maili gönder <<<<<<
            string confirmationLink = Url.Action("ConfirmEmail", "Account", new { userId = newUser.UserId }, protocol: Request.Url.Scheme);

            var fromAddress = new MailAddress("zorrumeysa54@gmail.com", "Time Capsule"); // FROM EKLENDİ

            var message = new MailMessage();
            message.From = fromAddress; // EKLENDİ!
            message.To.Add(email);
            message.Subject = "Time Capsule - E-posta Onayı";

            // --- Şık HTML gövde ---
            string body = $@"
            <html>
            <head>
                <style>
                    .email-container {{
                        font-family: Arial, sans-serif;
                        background-color: #f4f4f4;
                        padding: 20px;
                        border-radius: 10px;
                        max-width: 600px;
                        margin: auto;
                    }}
                    .btn {{
                        background-color: #833bb6;
                        color: white;
                        padding: 10px 20px;
                        text-decoration: none;
                        border-radius: 5px;
                    }}
                </style>
            </head>
            <body>
                <div class='email-container'>
                    <h2>Time Capsule - E-posta Onayı</h2>
                    <p>Merhaba {username},</p>
                    <p>Hesabınızı doğrulamak için aşağıdaki butona tıklayın:</p>
                    <p><a class='btn' href='{confirmationLink}'>E-postamı Doğrula</a></p>
                    <br>
                    <p>Teşekkürler,<br>Time Capsule Ekibi</p>
                </div>
            </body>
            </html>
            ";

            message.Body = body;
            message.IsBodyHtml = true;

            using (SmtpClient smtp = new SmtpClient())
            {
                smtp.Send(message);
            }

            ViewBag.Success = "Kayıt başarılı! Lütfen e-posta adresinizi kontrol edip onaylayın.";
            return View();
        }

        // GET: Account/ConfirmEmail
        public ActionResult ConfirmEmail(int userId)
        {
            var user = db.USERS.FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                return Content("Kullanıcı bulunamadı.");
            }

            user.IsEmailConfirmed = true;
            db.SaveChanges();

            // Otomatik giriş
            Session["UserId"] = user.UserId;
            Session["Username"] = user.Username;

            return RedirectToAction("Index", "Message");
        }
        private void CheckAndSendCapsuleMails()
        {
            var now = DateTime.Now;
            var capsules = db.MESSAGES
                             .Where(m => m.LockedUntil <= now && m.IsSent == false)
                             .ToList();

            foreach (var message in capsules)
            {
                var user = db.USERS.FirstOrDefault(u => u.UserId == message.UserId);
                if (user != null && user.IsEmailConfirmed == true)
                {
                    // Maili gönder
                    var fromAddress = new MailAddress("zorrumeysa54@gmail.com", "Time Capsule");
                    var mail = new MailMessage();
                    mail.From = fromAddress;
                    mail.To.Add(user.Email);
                    mail.Subject = "Time Capsule Mesajınız Açıldı";

                    string body = $@"
            <html>
            <body>
                <h3>Merhaba {user.Username},</h3>
                <p>Zaman kapsülünüz açıldı! İşte mesajınız:</p>
                <blockquote>{message.MessageText}</blockquote>";

                    if (!string.IsNullOrEmpty(message.ImagePath))
                    {
                        var extension = System.IO.Path.GetExtension(message.ImagePath).ToLower();
                        if (extension == ".jpg" || extension == ".png" || extension == ".jpeg" || extension == ".gif")
                        {
                            body += $"<p><img src='{Request.Url.Scheme}://{Request.Url.Authority}{message.ImagePath}' style='max-width:300px;' /></p>";
                        }
                        else
                        {
                            body += $"<p><a href='{Request.Url.Scheme}://{Request.Url.Authority}{message.ImagePath}'>Dosyayı Görüntüle</a></p>";
                        }
                    }

                    body += "<p>Sevgiler,<br>Time Capsule Ekibi</p></body></html>";

                    mail.Body = body;
                    mail.IsBodyHtml = true;

                    using (SmtpClient smtp = new SmtpClient())
                    {
                        smtp.Send(mail);
                    }

                    // Mail gönderildi olarak işaretle
                    message.IsSent = true;
                    db.SaveChanges();
                }
            }
        }
    }
}
