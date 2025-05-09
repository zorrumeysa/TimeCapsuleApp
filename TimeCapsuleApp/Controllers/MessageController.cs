using System;
using System.Linq;
using System.Net.Mail;
using System.Net.Mime;
using System.Web;
using System.Web.Mvc;
using TimeCapsuleApp;

namespace TimeCapsuleApp.Controllers
{
    public class MessageController : Controller
    {
        private TimeCapsuleDBEntities db = new TimeCapsuleDBEntities();

        public ActionResult Index()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = (int)Session["UserId"];

            CheckAndSendCapsuleMails(userId);

            var messages = db.MESSAGES
                             .Where(m => m.UserId == userId)
                             .OrderByDescending(m => m.CreatedDate)
                             .ToList();

            return View(messages);
        }

        public ActionResult Create()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        [HttpPost]
        public ActionResult Create(string MessageText, DateTime? LockedUntil, HttpPostedFileBase ImageFile, string ReceiverEmail)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(MessageText))
            {
                ViewBag.Error = "Mesaj alanı boş bırakılamaz.";
                return View();
            }

            if (LockedUntil == null)
            {
                ViewBag.Error = "Kilit tarihi seçmelisiniz.";
                return View();
            }

            string imagePath = null;

            if (ImageFile != null && ImageFile.ContentLength > 0)
            {
                var fileName = Guid.NewGuid().ToString() + System.IO.Path.GetExtension(ImageFile.FileName);
                var path = Server.MapPath("~/Uploads/" + fileName);
                ImageFile.SaveAs(path);
                imagePath = "/Uploads/" + fileName;
            }

            int userId = (int)Session["UserId"];

            var newMessage = new MESSAGES
            {
                UserId = userId,
                MessageText = MessageText,
                LockedUntil = LockedUntil.Value,
                ImagePath = imagePath,
                CreatedDate = DateTime.Now,
                ReceiverEmail = ReceiverEmail,
                IsSent = false
            };

            db.MESSAGES.Add(newMessage);
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        public ActionResult Delete(int id)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = (int)Session["UserId"];

            var message = db.MESSAGES.FirstOrDefault(m => m.MessageId == id && m.UserId == userId);

            if (message == null)
            {
                return HttpNotFound();
            }

            db.MESSAGES.Remove(message);
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        private void CheckAndSendCapsuleMails(int userId)
        {
            var now = DateTime.Now;

            var capsules = db.MESSAGES
                             .Where(m => m.UserId == userId &&
                                         m.LockedUntil <= now &&
                                         m.IsSent == false)
                             .ToList();

            foreach (var message in capsules)
            {
                var user = db.USERS.FirstOrDefault(u => u.UserId == message.UserId);

                if (user != null && user.IsEmailConfirmed == true)
                {
                    try
                    {
                        string recipient = string.IsNullOrEmpty(message.ReceiverEmail) ? user.Email : message.ReceiverEmail;

                        var fromAddress = new MailAddress("zorrumeysa54@gmail.com", "Time Capsule");
                        var mail = new MailMessage();
                        mail.From = fromAddress;
                        mail.To.Add(recipient);
                        mail.Subject = "Zaman Kapsülünüz Açıldı!";

                        // LOGIN URL
                        string loginUrl = $"{Request.Url.Scheme}://{Request.Url.Authority}/Account/Login";

                        string body = $@"
                        <html>
                        <body style='font-family:Arial,sans-serif;'>
                            <h3>Merhaba {user.Username},</h3>
                            <p>Zaman kapsülünüz açıldı! İşte mesajınız:</p>
                            <blockquote>{message.MessageText}</blockquote>";

                        // LinkedResource ve AlternateView kullanılacak mı?
                        AlternateView htmlView = null;

                        if (!string.IsNullOrEmpty(message.ImagePath))
                        {
                            var extension = System.IO.Path.GetExtension(message.ImagePath).ToLower();

                            if (extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif")
                            {
                                string filePath = Server.MapPath(message.ImagePath);

                                if (System.IO.File.Exists(filePath))
                                {
                                    string contentId = Guid.NewGuid().ToString();

                                    // HTML body
                                    body += $"<br><img src='cid:{contentId}' style='max-width:300px;'><br>";
                                    body += $@"<br><a href='{loginUrl}' style='display:inline-block;padding:10px 20px;background-color:#833bb6;color:white;border-radius:5px;text-decoration:none;'>Mesajlarımı Görüntüle</a><br>";

                                    body += "<p>Sevgiler,<br>Time Capsule Ekibi</p></body></html>";

                                    htmlView = AlternateView.CreateAlternateViewFromString(body, null, "text/html");

                                    LinkedResource inlineImage = new LinkedResource(filePath, MediaTypeNames.Image.Jpeg);
                                    inlineImage.ContentId = contentId;
                                    inlineImage.TransferEncoding = TransferEncoding.Base64;

                                    htmlView.LinkedResources.Add(inlineImage);

                                    mail.AlternateViews.Add(htmlView);
                                }
                            }
                            else
                            {
                                string fileUrl = $"{Request.Url.Scheme}://{Request.Url.Authority}{message.ImagePath}";
                                body += $"<p><a href='{fileUrl}'>Dosyayı Görüntüle</a></p>";
                                body += $@"<br><a href='{loginUrl}' style='display:inline-block;padding:10px 20px;background-color:#833bb6;color:white;border-radius:5px;text-decoration:none;'>Mesajlarımı Görüntüle</a><br>";
                                body += "<p>Sevgiler,<br>Time Capsule Ekibi</p></body></html>";

                                mail.Body = body;
                                mail.IsBodyHtml = true;
                            }
                        }
                        else
                        {
                            body += $@"<br><a href='{loginUrl}' style='display:inline-block;padding:10px 20px;background-color:#833bb6;color:white;border-radius:5px;text-decoration:none;'>Mesajlarımı Görüntüle</a><br>";
                            body += "<p>Sevgiler,<br>Time Capsule Ekibi</p></body></html>";

                            mail.Body = body;
                            mail.IsBodyHtml = true;
                        }

                        using (SmtpClient smtp = new SmtpClient())
                        {
                            smtp.Send(mail);
                        }

                        message.IsSent = true;
                        db.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Mail gönderilemedi: " + ex.Message);
                    }
                }
            }
        }
    }
}
