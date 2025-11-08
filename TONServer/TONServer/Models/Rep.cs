using Libs;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TONServer.Components;
using Web;

namespace TONServer
{
    public class Rep
    {
        public static Setting Settings
        {
            get
            {
                var db = Db;
                var set = db.Settings.FirstOrDefault();
                if (set == null)
                {
                    set = new Setting();
                    set.Sitename = Helper.AppName;
                    set.RecaptchaKey = "6LeVpAAVAAAAAK6VhpEKsBD3nFNsnYaUniMy5FfR";
                    set.RecaptchaSecret = "6LeVpAAVAAAAAMcFDCz3JnJ3bZsnV1LwkbcxDR06";
                    db.Settings.Add(set);
                    db.SaveChanges();
                }
                return set;
            }
        }

        public static _DbContext Db
        {
            get
            {
                var ob = new DbContextOptionsBuilder<_DbContext>();
                ob.UseSqlServer(_Singleton.ConnectionString);
                var db = new _DbContext(ob.Options);
                return db;
            }
        }

        public static async Task Mail(string email, string subject, string message, List<string> attachments = null)
        {
            try
            {
                var emailMessage = new MimeMessage();

                emailMessage.From.Add(new MailboxAddress(typeof(Rep).Namespace, "alexxx6233@yandex.ru"));
                emailMessage.To.Add(new MailboxAddress("", email));
                emailMessage.Subject = subject;

                string body = File.ReadAllText($"{_Singleton.WebRootPath}/email/template.html");
                body = string.Format(body, message, Settings.Sitename, DateTime.Now.Year);

                var builder = new BodyBuilder();
                builder.HtmlBody = message;
                if (attachments != null) foreach (var a in attachments) builder.Attachments.Add(a);
                emailMessage.Body = builder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync("smtp.yandex.ru", 587, false);
                    await client.AuthenticateAsync("alexxx6233@yandex.ru", "Aqwsxz24");
                    await client.SendAsync(emailMessage);

                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex) { ServerLog.Log(ex); }
        }

        public static async Task<List<string>> SaveFiles(IFormFileCollection files, IWebHostEnvironment env)
        {
            foreach (var file in files)
            {
                using (var fileStream = new FileStream($"{env.WebRootPath}/files/{file.FileName}", FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }
            }
            return files.Select(x => x.FileName).ToList();
        }

        public static void Recaptcha(string g)
        {
            var p = new Parser();
            p.Post("https://www.google.com/recaptcha/api/siteverify", $"secret={Settings.RecaptchaSecret}&response={g}");
            var j = p.Json();
            if (!(bool)j["success"]) throw new Exception("Wrong captcha");
        }

        public static string Quill(string delta)
        {
            string message = "";
            var j = Parser.Json(delta);
            foreach (var op in j["ops"])
            {
                string text = "";
                if (op["insert"].HasValues) text = "<img src=\"" + op["insert"]["image"] + "\" />";
                else text = op["insert"].ToString().Replace("\n\n", "\n").Replace("\n", "<br>");
                var attrs = op["attributes"];
                if (attrs != null)
                {
                    if (attrs["bold"] != null) text = "<b>" + text + "</b>";
                    if (attrs["italic"] != null) text = "<i>" + text + "</i>";
                    if (attrs["strike"] != null) text = "<s>" + text + "</s>";
                    if (attrs["underline"] != null) text = "<u>" + text + "</u>";
                    if (attrs["link"] != null) text = $@"<a target=""blank"" href=""{attrs["link"]}"">{text}</a>";
                }
                message += text;
            }
            return message;
        }

        public static async Task SendSms(string phone, string text)
        {
            var p = new Parser();
            //p.FiddlerAuth = true;
            var o = new
            {
                login = "z1591603549118",
                password = "921956",
                messages = new[]
                {
                        new
                        {
                            clientId = "1",
                            phone,
                            text
                        }
                    }
            };
            string q = JsonConvert.SerializeObject(o);
            await p.PostAsync("http://json.gate.iqsms.ru/send/", q, false);
            string s = p.Content;
        }

        public static bool IsValid(string email)
        {
            try
            {
                var m = new System.Net.Mail.MailAddress(email);
                return true;
            }
            catch (FormatException) { return false; }
        }

        public static string SessionAddress(_DbContext db, string session, ref string address, out RoomWeb room, out RoomWeb owner)
        {
            if (session == null) session = "";
            string address1 = _Singleton.Sessions.ContainsKey(session) ? _Singleton.Sessions[session] : "";
            //if (_Singleton.Development) address1 = "EQDaVOscxs5EoL2X84KQMl0dKL0NhPhsZGd00dMTqWGl834b";
            if (_Singleton.Development) address1 = "EQB0zy3wOR35FF1q2j3NsCxOyqzoRYioFroMqvsYEJ7mJ7-6";
            string _address = address;
            room = db.RoomWebs.FirstOrDefault(x => x.Address == _address);
            if (room == null) room = db.RoomWebs.FirstOrDefault(x => x.Name == _address);
            if (room != null) address = room.Address;
            owner = db.RoomWebs.FirstOrDefault(x => x.Address == address1);
            return address1;
        }

        public static RoomWeb SessionAddress(_DbContext db, string session)
        {
            if (session == null) session = "";
            string address1 = _Singleton.Sessions.ContainsKey(session) ? _Singleton.Sessions[session] : "";
            //if (_Singleton.Development) address1 = "EQDaVOscxs5EoL2X84KQMl0dKL0NhPhsZGd00dMTqWGl834b";
            if (_Singleton.Development) address1 = "EQB0zy3wOR35FF1q2j3NsCxOyqzoRYioFroMqvsYEJ7mJ7-6";
            var owner = db.RoomWebs.FirstOrDefault(x => x.Address == address1);
            return owner;
        }

        public static string FriendsButtonText(RoomWeb owner, RoomWeb room)
        {
            if (owner == null) return "";
            string buttonText = "Добавить в друзья";
            if (owner.OutcomingsList.Contains(room.Id)) buttonText = "Отменить заявку";
            if (owner.FriendsList.Contains(room.Id)) buttonText = "Убрать из друзей";
            return buttonText;
        }

        public static async void Notify(RoomWeb room, RoomWeb owner)
        {
            foreach (var session in _Singleton.Sessions)
            {
                if (session.Key == room.Address)
                {
                    if (room.IncomingsList.Count > 0) await _Hub.Send("showNotify", room.Address);
                    if (room.IncomingsList.Count == 0) await _Hub.Send("hideNotify", room.Address);
                }
                if (session.Key == owner.Address)
                {
                    if (owner.IncomingsList.Count > 0) await _Hub.Send("showNotify", owner.Address);
                    if (owner.IncomingsList.Count == 0) await _Hub.Send("hideNotify", owner.Address);
                }
            }
        }
    }
}
