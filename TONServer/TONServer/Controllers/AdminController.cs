using Libs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace TONServer
{
    public class AdminController : _Controller
    {
        public async Task<IActionResult> Index()
        {
            return View(Rep.Settings);
        }

        public async Task<IActionResult> Langs()
        {
            return View();
        }

        public async Task<IActionResult> Users(int page = 1)
        {
            var users = db.Users.ToList();
            users = Helper.Pages(users, page, 50, ViewBag);
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> AddLang(string name)
        {
            try
            {
                await db.Execute($"insert into Langs (Name) values ({name})");
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> AddLangDesc(string name, string desc)
        {
            try
            {
                var rec = db.LangDescs.FirstOrDefault(x => x.Lang == name);
                if (rec == null)
                {
                    await db.Execute($"ALTER TABLE Langs ADD {name} nvarchar(MAX) NULL");
                    await db.LangDescs.AddAsync(new LangDesc { Lang = name, Desc = desc });
                }
                else rec.Desc = desc;
                await db.SaveChangesAsync();
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveLangDesc(int id)
        {
            try
            {
                var rec = db.LangDescs.Find(id);
                await db.Execute($"ALTER TABLE [Langs] DROP COLUMN [{rec.Lang}]");
                db.LangDescs.Remove(rec);
                await db.SaveChangesAsync();
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> ChangeLang(string name, string lang, string text)
        {
            try
            {
                if (text == null) text = "";
                var r = await db.Execute($"update [Langs] set [{lang}] = N'{text.Replace("'", "''")}' where Name = N'{name}'");
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveLang(string name)
        {
            try
            {
                await db.Execute($"delete from Langs where Name = {name}");
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveUser(int id)
        {
            try
            {
                var rec = await db.Users.FindAsync(id);
                db.Users.Remove(rec);
                await db.SaveChangesAsync();
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SaveSettings()
        {
            try
            {
                foreach (var prop in typeof(Setting).GetProperties())
                {
                    string name = prop.Name;
                    string value = HttpUtility.UrlDecode(Request.Form[name]);
                    if (value != null)
                    {
                        if (prop.PropertyType.Name == "String") value = "N'" + value + "'";
                        else if (value.ToLower() == "false") value = "'0'";
                        else if (value.ToLower() == "true") value = "'1'";
                        else value = value.Replace(",", ".");
                        var i = await db.Execute($"update Settings set [{name}] = {value}");
                    }
                }
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }
    }
}
