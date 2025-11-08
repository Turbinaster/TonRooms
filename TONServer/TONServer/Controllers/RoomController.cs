using ConvertApiDotNet;
using Libs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web;

namespace TONServer.Controllers
{
    public class RoomController : _Controller
    {
        [Route("{id}")]
        public IActionResult Index(string id)
        {
            if (id == "admin") return RedirectToAction("index", "admin");
            string address = "";
            var room = db.RoomWebs.FirstOrDefault(x => x.Address == id);
            if (room == null) room = db.RoomWebs.FirstOrDefault(x => x.Name == id);
            if (room != null) address = room.Address;
            ViewBag.address = address;
            return View();
        }

        [HttpGet]
        public IActionResult Auth(string session, string address)
        {
            if (string.IsNullOrWhiteSpace(session) || string.IsNullOrWhiteSpace(address))
            {
                return RedirectToAction("index", "index");
            }

            try
            {
                RegisterRoomSession(session, address);
            }
            catch (Exception ex)
            {
                Helper.Log(ex);
            }

            return RedirectToAction("index", "room", new { id = address });
        }

        [HttpPost]
        public IActionResult Connect(string session, string address)
        {
            try
            {
                RegisterRoomSession(session, address);
                return Json(new { r = "ok", redirect = Url.Action("index", "room", new { id = address }) });
            }
            catch (Exception ex)
            {
                Helper.Log(ex);
                return Json(new { r = "error", m = ex.Message });
            }
        }
        
        public IActionResult Logout(string session)
        {
            if (_Singleton.Sessions.ContainsKey(session)) _Singleton.Sessions.Remove(session);
            return RedirectToAction("index", "index");
        }

        [HttpPost]
        public async Task<IActionResult> GetAddress(string session, string address)
        {
            try
            {
                string address1 = Rep.SessionAddress(db, session, ref address, out var room, out var owner);
                bool result = address == address1;
                var items = db.ImageWebs.Where(x => x.Address == address && x.Selected).ToList();
                bool auth = owner != null;
                var incoming = auth ? db.RoomWebs.Where(x => owner.IncomingsList.Contains(x.Id)).Count() : 0;
                return Json(new { r = "ok", result, items, room, auth, owner, text = Rep.FriendsButtonText(owner, room), incoming });
            }
            catch (Exception ex) { Helper.Log(ex); return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> GetNft(string session)
        {
            try
            {
                var images = new List<ImageWeb>();
                string address = "";
                if (session == null && _Singleton.Development) address = "EQB0zy3wOR35FF1q2j3NsCxOyqzoRYioFroMqvsYEJ7mJ7-6";
                else address = _Singleton.Sessions.ContainsKey(session) ? _Singleton.Sessions[session] : "";
                var accountId = ResolveTonAccountId(address);
                var p = new Parser();
                //p.Fiddler = true;
                p.Timeout = 300000;
                p.AddHeader("Authorization", "Bearer " + _Singleton.Api);
                dynamic j = null;
                if (!string.IsNullOrWhiteSpace(accountId))
                {
                    p.Go($"https://tonapi.io/v2/accounts/{accountId}/nfts?limit=1000&indirect_ownership=true");
                    j = p.Json();
                }
                var list = new List<string>();
                if (j != null && j["nft_items"] != null)
                {
                    foreach (var item in j["nft_items"])
                    {
                        try
                        {
                            if (item["metadata"] != null && item["metadata"]["image"] != null)
                            {
                                string url = item["metadata"]["image"].ToString();
                                list.Add(url);
                                string name = item["metadata"]["name"]?.ToString();
                                string description = item["metadata"]["description"]?.ToString();
                                string external_url = item["metadata"]["external_url"]?.ToString();
                                var image = new ImageWeb { Address = address, Name = name, Description = description, ExternalUrl = external_url };
                                string file = url.Replace("/", "").Replace(":", "").Replace(".", "").Replace("?", "").Replace("%20", "") + ".png";
                                string path = System.IO.Path.Combine(_Singleton.WebRootPath, "files", file);
                                if (url.StartsWith("ipfs:") || url.EndsWith(".mp4"))
                                {
                                    continue;
                                }
                                else if (url.EndsWith(".svg"))
                                {
                                    if (!System.IO.File.Exists(path))
                                    {
                                        var convertApi = new ConvertApi("E9GpCfXSdIfFw81b");
                                        var convert = await convertApi.ConvertAsync("svg", "png", new ConvertApiFileParam("File", url));
                                        await convert.SaveFileAsync(path);
                                    }
                                }
                                else
                                {
                                    if (!System.IO.File.Exists(path))
                                    {
                                        p.DownloadFile(url, path);
                                    }
                                }
                                string result = $"{_Controller.GetLeftPart(Request)}/files/{file}";
                                image.Url = result;
                                images.Add(image);
                            }
                        }
                        catch (Exception ex) { Helper.Log(ex); }
                    }
                }
                else Helper.Log(p.Content);
                var recs = db.ImageWebs.Where(x => x.Address == address).ToList();
                foreach (var image in images) if (!recs.Any(x => x.Url == image.Url)) db.ImageWebs.Add(image);
                foreach (var rec in recs) if (!images.Any(x => x.Url == rec.Url)) db.ImageWebs.Remove(rec);
                db.SaveChanges();
                return Json(new { r = "ok", images = db.ImageWebs.Where(x => x.Address == address).ToList() });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> Select(int id)
        {
            try
            {
                var rec = db.ImageWebs.Find(id);
                rec.Selected = !rec.Selected;
                if (!rec.Selected)
                {
                    rec.X = 0;
                    rec.Y = 0;
                    rec.Wall = 0;
                    rec.Scale = 0;
                }
                db.SaveChanges();
                return Json(new { r = "ok", selected = rec.Selected });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        public async Task<IActionResult> SetPosition(int id, float x, float y, float scale, int index)
        {
            var rec = db.ImageWebs.Find(id);
            if (rec != null)
            {
                rec.X = x;
                rec.Y = y;
                rec.Scale = scale;
                rec.Wall = index;
                db.SaveChanges();
            }
            return Content("ok");
        }

        [HttpPost]
        public async Task<IActionResult> AddAvatar(IFormFileCollection files)
        {
            var list = await Rep.SaveFiles(files, env);
            await _Hub.SendSession("profile_edit_avatar", session, $"{_Controller.GetLeftPart(Request)}/files/{list[0]}");
            return StatusCode(200);
        }

        [HttpPost]
        public async Task<IActionResult> SaveProfile(string session, string profile_edit_avatar, string profile_edit_name, string profile_edit_desc, string profile_edit_link1, string profile_edit_link2)
        {
            try
            {
                if (session == null) session = "";
                string address = _Singleton.Sessions.ContainsKey(session) ? _Singleton.Sessions[session] : "";
                if (string.IsNullOrEmpty(address)) address = "EQDaVOscxs5EoL2X84KQMl0dKL0NhPhsZGd00dMTqWGl834b";
                var rec = db.RoomWebs.FirstOrDefault(x => x.Address == address);
                if (rec == null) { rec = new RoomWeb { Address = address }; db.RoomWebs.Add(rec); }
                rec.Avatar = profile_edit_avatar;
                rec.Name = profile_edit_name;
                if (string.IsNullOrEmpty(rec.Name)) rec.Name = $"{address.Substring(0, 4)}..{address.Substring(address.Length - 4, 4)}";
                rec.Desc = profile_edit_desc;
                rec.Link1 = "https://t.me/" + profile_edit_link1.Replace("https://t.me/", "");
                rec.Link2 = profile_edit_link2;
                db.SaveChanges();
                return Json(new { r = "ok", rec });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        private void RegisterRoomSession(string session, string address)
        {
            SetSessionAddress(session, address);
            if (!db.RoomWebs.Any(x => x.Address == address))
            {
                var shortName = address;
                if (!string.IsNullOrEmpty(address) && address.Length > 8)
                {
                    shortName = $"{address.Substring(0, 4)}..{address.Substring(address.Length - 4, 4)}";
                }

                db.RoomWebs.Add(new RoomWeb
                {
                    Address = address,
                    Name = shortName,
                    Avatar = $"{_Controller.GetLeftPart(Request)}/img/default.png"
                });
                db.SaveChanges();
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleFriends(string session, string address)
        {
            try
            {
                string address1 = Rep.SessionAddress(db, session, ref address, out var room, out var owner);
                if (owner.FriendsList.Contains(room.Id))
                {
                    owner.RemoveFriend(room.Id);
                    room.RemoveFriend(owner.Id);
                }
                else if (owner.OutcomingsList.Contains(room.Id))
                {
                    owner.RemoveOutcoming(room.Id);
                    room.RemoveIncoming(owner.Id);
                }
                else
                {
                    owner.AddOutcoming(room.Id);
                    room.AddIncoming(owner.Id);
                }
                db.SaveChanges();
                Rep.Notify(room, owner);
                return Json(new { r = "ok", text = Rep.FriendsButtonText(owner, room) });
            }
            catch (Exception ex) { Helper.Log(ex); return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> ShowFriends(string session)
        {
            try
            {
                var owner = Rep.SessionAddress(db, session);
                return PartialView("friends", owner);
            }
            catch (Exception ex) { Helper.Log(ex); return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFriend(string session, int id)
        {
            try
            {
                var owner = Rep.SessionAddress(db, session);
                var room = db.RoomWebs.Find(id);
                if (owner.FriendsList.Contains(room.Id))
                {
                    owner.RemoveFriend(room.Id);
                    room.RemoveFriend(owner.Id);
                }
                if (owner.OutcomingsList.Contains(room.Id))
                {
                    owner.RemoveOutcoming(room.Id);
                    room.RemoveIncoming(owner.Id);
                }
                if (owner.IncomingsList.Contains(room.Id))
                {
                    owner.RemoveIncoming(room.Id);
                    room.RemoveOutcoming(owner.Id);
                }
                db.SaveChanges();
                Rep.Notify(room, owner);
                return PartialView("friends", owner);
            }
            catch (Exception ex) { Helper.Log(ex); return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> AddFriend(string session, int id)
        {
            try
            {
                var owner = Rep.SessionAddress(db, session);
                var room = db.RoomWebs.Find(id);
                owner.RemoveIncoming(room.Id);
                room.RemoveOutcoming(owner.Id);
                owner.AddFriend(room.Id);
                room.AddFriend(owner.Id);
                db.SaveChanges();
                Rep.Notify(room, owner);
                return PartialView("friends", owner);
            }
            catch (Exception ex) { Helper.Log(ex); return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> ShowFriendsTeleport(string address)
        {
            try
            {
                var room = db.RoomWebs.FirstOrDefault(x => x.Address == address);
                return PartialView("friends_teleport", room);
            }
            catch (Exception ex) { Helper.Log(ex); return Json(new { r = "error", m = ex.Message }); }
        }
    }
}
