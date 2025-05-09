using System;
using System.Linq;
using System.Web.Mvc;
using TimeCapsuleApp;

namespace TimeCapsuleApp.Controllers
{
    public class AdminController : Controller
    {
        private TimeCapsuleDBEntities db = new TimeCapsuleDBEntities();

        // GET: Admin/AllMessages
        public ActionResult AllMessages()
        {
            if (Session["UserId"] == null || (int)Session["UserId"] != 1)
            {
                return RedirectToAction("Login", "Account");
            }

            var allMessages = db.MESSAGES
                                .OrderByDescending(m => m.CreatedDate)
                                .ToList();

            return View(allMessages);
        }
    }
}
