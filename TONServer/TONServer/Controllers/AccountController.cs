using Libs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace TONServer
{
    public class AccountController : _Controller
    {
        public async Task<IActionResult> Login()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("index", "index");
            return View();
        }

        public async Task<IActionResult> Register()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("index", "index");
            return View();
        }

        public async Task<IActionResult> Remember()
        {
            return View();
        }

        public async Task<IActionResult> RememberConfirm(string id)
        {
            var rec = await db.Users.FirstOrDefaultAsync(x => x.ConfirmRemember == id);
            return View(rec);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            SetSession<User>("user", null);
            return RedirectToAction("index", "index");
        }

        [Authorize]
        public async Task<IActionResult> Personal()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(User m)
        {
            try
            {

                if (string.IsNullOrEmpty(m.Login) || string.IsNullOrEmpty(m.Pass)) throw new Exception(lang["Необходимо заполнить все данные"]);
                var rec = await db.Users.FirstOrDefaultAsync(x => x.Login == m.Login);
                if (rec == null)
                {
                    if (m.Login == "admin" && m.Pass == "8bufrxex9i")
                    {
                        m.Role = "admin";
                        m.Name = "admin";
                        m.Date = DateTime.Now;
                        m.Avatar = "../../img/avatar.png";
                        m.Lang = HttpContext.Session.GetString("langS");
                        await db.Users.AddAsync(m);
                        await db.SaveChangesAsync();
                        m.Pass = Helper.HMACSHA512(m.Pass, m.Id.ToString());
                        await db.SaveChangesAsync();
                        await Authenticate(m.Id, m.Role);
                        return Json(new { r = "ok", url = "../../admin/index" });
                    }
                    else throw new Exception(lang["Неверный логин или пароль"]);
                }
                string pass = Helper.HMACSHA512(m.Pass, rec.Id.ToString());
                if (pass != rec.Pass) throw new Exception(lang["Неверный логин или пароль"]);
                await Authenticate(rec.Id, rec.Role);
                string url = rec.Role == "admin" ? "../../admin/index" : Url.Action("personal", "account");
                return Json(new { r = "ok", url });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> Register(User m)
        {
            try
            {
                string recaptcha = Request.Form["g-recaptcha-response"];
                if (!HttpContext.Session.Keys.Contains("recaptcha") || GetSession<string>("recaptcha") != recaptcha) Rep.Recaptcha(recaptcha);
                SetSession("recaptcha", recaptcha);
                if (string.IsNullOrEmpty(m.Login) || string.IsNullOrEmpty(m.Pass) || string.IsNullOrEmpty(m.Name)) throw new Exception(lang["Необходимо заполнить все данные"]);
                if (!Rep.IsValid(m.Login)) throw new Exception(lang["Неверный e-mail"]);
                if (m.Pass.Length < 6 || m.Pass.Length > 24) throw new Exception("Длина пароля должна быть не менее 6 и не более 24 символов");
                var rec = await db.Users.FirstOrDefaultAsync(x => x.Login == m.Login);
                if (rec != null) throw new Exception(lang["Такой логин занят"]);
                m.Date = DateTime.Now;
                m.Role = "user";
                m.Avatar = "../../img/avatar.png";
                m.Lang = HttpContext.Session.GetString("langS");
                await db.Users.AddAsync(m);
                await db.SaveChangesAsync();
                m.Pass = Helper.HMACSHA512(m.Pass, m.Id.ToString());
                await db.SaveChangesAsync();
                await Authenticate(m.Id, m.Role);
                return Json(new { r = "ok", url = Url.Action("personal", "account") });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [NonAction]
        private async Task Authenticate(int id, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimsIdentity.DefaultNameClaimType, id.ToString()),
                new Claim(ClaimsIdentity.DefaultRoleClaimType, role)
            };
            ClaimsIdentity ci = new ClaimsIdentity(claims, "ApplicationCookie", ClaimsIdentity.DefaultNameClaimType, ClaimsIdentity.DefaultRoleClaimType);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(ci));
        }

        [HttpPost]
        public async Task<IActionResult> AddAvatar(IFormFileCollection files)
        {
            var list = await Rep.SaveFiles(files, env);
            var user = await db.Users.FindAsync(Helper.IntParse(User.Identity.Name));
            user.Avatar = "../../files/" + list[0];
            await db.SaveChangesAsync();
            await _Hub.SendSession("avatar", session, user.Avatar);
            return StatusCode(200);
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> ChangePass(string oldPass, string newPass, string newPass1)
        {
            try
            {
                if (newPass != newPass1) throw new Exception(lang["Новые пароли не совпадают"]);
                var rec = await db.Users.FindAsync(Helper.IntParse(User.Identity.Name));
                oldPass = Helper.HMACSHA512(oldPass, rec.Id.ToString());
                if (oldPass != rec.Pass) throw new Exception(lang["Неверный старый пароль"]);
                if (newPass.Length < 6 || newPass.Length > 24) throw new Exception(lang["Длина пароля должна быть не менее 6 и не более 24 символов"]);
                rec.Pass = Helper.HMACSHA512(newPass, rec.Id.ToString());
                await db.SaveChangesAsync();
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SetLang(string lang)
        {
            try
            {
                if (User.Identity.IsAuthenticated)
                {
                    var user = await db.Users.FindAsync(Helper.IntParse(User.Identity.Name));
                    user.Lang = lang;
                    await db.SaveChangesAsync();
                }
                else Response.Cookies.Append("lang", lang);
                HttpContext.Session.Remove("lang");
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> Remember(string email)
        {
            try
            {
                var rec = await db.Users.FirstOrDefaultAsync(x => x.Login == email);
                if (rec == null) throw new Exception(lang["Пользователь с таким email не найден"]);
                rec.ConfirmRemember = Helper.RandomString();
                await db.SaveChangesAsync();
                string url = $"{GetLeftPart(Request)}/account/rememberconfirm/{rec.ConfirmRemember}";
                await Rep.Mail(email, lang["Восстановление пароля"], $@"{lang["Для восстановления пароля перейдите по ссылке:"]}<br><br><a target=""_blank"" href=""{url}"">{url}</a>");
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RememberConfirm(string confirmRemember, string newPass, string newPass1)
        {
            try
            {
                if (newPass != newPass1) throw new Exception(lang["Новые пароли не совпадают"]);
                var rec = await db.Users.FirstOrDefaultAsync(x => x.ConfirmRemember == confirmRemember);
                if (rec == null) throw new Exception(lang["Пользователь не найден"]);
                if (newPass.Length < 6 || newPass.Length > 24) throw new Exception(lang["Длина пароля должна быть не менее 6 и не более 24 символов"]);
                rec.ConfirmRemember = null;
                rec.Pass = Helper.HMACSHA512(newPass, rec.Id.ToString());
                await db.SaveChangesAsync();
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [Authorize, HttpPost]
        public async Task<IActionResult> ChangeData(User o)
        {
            try
            {
                var rec = await db.Users.FindAsync(Helper.IntParse(User.Identity.Name));
                rec.Name = o.Name;
                rec.Login = o.Login;
                await db.SaveChangesAsync();
                SetSession("user", rec);
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }
    }
}
