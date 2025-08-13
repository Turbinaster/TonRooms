using ConvertApiDotNet;
using Ipfs.Engine;
using Libs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Web;

namespace TONServer
{
    public class IndexController : _Controller
    {
        public async Task<IActionResult> Index()
        {
            return View();
        }

        public async Task<IActionResult> Auth()
        {
            /*var list = new List<string>();
            foreach (string key in Request.Query.Keys) list.Add($"{key}={Request.Query[key]}");
            Helper.Log(string.Join("&", list));*/
            
            string session = Request.Query["session"].ToString();
            var split = session.Split('?');
            session = split[0];
            string authToken = split[1].Replace("authToken=", "");

            var p = new Parser();
            p.AddHeader("Authorization", "Bearer " + _Singleton.Api);
            p.Go($"https://tonapi.io/v1/oauth/getToken?auth_token={authToken}&rate_limit=100&token_type=server");
            var j = p.Json();
            if (j != null && j["address"] != null)
            {
                string address = j["address"].ToString();
                if (!_Singleton.Sessions.ContainsKey(session)) _Singleton.Sessions.Add(session, address);
                else _Singleton.Sessions[session] = address;
            }
            else Helper.Log(p.Content);
            
            return Content("");
        }

        [HttpPost]
        public async Task<IActionResult> GetAddress(string session, int x, int y, int z)
        {
            try
            {
                string address = _Singleton.Sessions.ContainsKey(session) ? _Singleton.Sessions[session] : "";
                if (!string.IsNullOrEmpty(address))
                {
                    _Singleton.Sessions.Remove(session);
                    var rooms = db.Rooms.Where(x => x.Address == address).ToList();
                    foreach (var room in rooms)
                    {
                        var images = db.Images.Where(x => x.RoomId == room.Id).ToList();
                        if (images.Count > 0) db.Images.RemoveRange(images);
                    }
                    if (rooms.Count > 0) db.Rooms.RemoveRange(rooms);
                    db.Rooms.Add(new Room { Address = address, X = x, Y = y, Z = z });
                    db.SaveChanges();
                }
                return Json(new { r = "ok", address });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> GetNft(string address)
        {
            try
            {
                var images = new List<Image>();
                var room = db.Rooms.FirstOrDefault(r => r.Address == address);
                var p = new Parser();
                p.AddHeader("Authorization", "Bearer " + _Singleton.Api);
                p.Go($"https://tonapi.io/v1/nft/getItemsByOwnerAddress?account={address}");
                var j = p.Json();
                if (j != null && j["nft_items"] != null)
                {
                    foreach (var item in j["nft_items"])
                    {
                        try
                        {
                            if (item["metadata"] != null && item["metadata"]["image"] != null)
                            {
                                string url = item["metadata"]["image"].ToString();
                                string name = item["metadata"]["name"]?.ToString();
                                string description = item["metadata"]["description"]?.ToString();
                                string external_url = item["metadata"]["external_url"]?.ToString();
                                var image = new Image { RoomId = room.Id, Selected = true, Url = url.Replace("%20", ""), Name = name, Description = description, ExternalUrl = external_url };
                                if (url.StartsWith("ipfs:"))
                                {
                                    continue;
                                }
                                else if (url.EndsWith(".svg"))
                                {
                                    string file = url.Replace("/", "").Replace(":", "").Replace(".", "").Replace("?", "").Replace("%20", "") + ".png";
                                    string path = Path.Combine(_Singleton.WebRootPath, "files", file);
                                    if (!System.IO.File.Exists(path))
                                    {
                                        var convertApi = new ConvertApi("E9GpCfXSdIfFw81b");
                                        var convert = await convertApi.ConvertAsync("svg", "png", new ConvertApiFileParam("File", url));
                                        await convert.SaveFileAsync(path);
                                    }
                                    string result = $"{_Controller.GetLeftPart(Request)}/files/{file}";
                                    image.Url = result;
                                }
                                images.Add(image);
                            }
                        }
                        catch (Exception ex) { Helper.Log(ex); }
                    }
                }
                else Helper.Log(p.Content);
                var recs = db.Images.Where(x => x.RoomId == room.Id).ToList();
                foreach (var image in images) if (!recs.Any(x => x.Url == image.Url)) db.Images.Add(image);
                foreach (var rec in recs) if (!images.Any(x => x.Url == rec.Url)) db.Images.Remove(rec);
                db.SaveChanges();
                return Json(new { r = "ok", images = db.Images.Where(x => x.RoomId == room.Id).ToList() });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> GetRooms()
        {
            try
            {
                var rooms = db.Rooms.ToList();
                var images = db.Images.Where(x => x.Selected).ToList();
                return Json(new { r = "ok", rooms, images });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SaveSelection(string address, string images)
        {
            try
            {
                var room = db.Rooms.FirstOrDefault(r => r.Address == address);
                var recs = db.Images.Where(x => x.RoomId == room.Id).ToList();
                var split = images.Split('|');
                foreach (var rec in recs)
                {
                    if (split.Contains(rec.Url)) rec.Selected = true; else rec.Selected = false;
                }
                db.SaveChanges();
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SetSelection(string address, string image, bool selected, int floor = 0)
        {
            try
            {
                var room = db.Rooms.FirstOrDefault(r => r.Address == address);
                if (room != null)
                {
                    var rec = db.Images.FirstOrDefault(x => x.RoomId == room.Id && x.Url == image);
                    if (rec != null)
                    {
                        rec.Selected = selected;
                        rec.Floor = floor;
                        db.SaveChanges();
                    }
                }
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SetPosition(string address, string image, float x, float y, float scale, int index)
        {
            try
            {
                var room = db.Rooms.FirstOrDefault(r => r.Address == address);
                if (room != null)
                {
                    var rec = db.Images.FirstOrDefault(x => x.RoomId == room.Id && x.Url == image);
                    if (rec != null)
                    {
                        rec.X = x;
                        rec.Y = y;
                        rec.Scale = scale;
                        rec.Wall = index;
                        db.SaveChanges();
                    }
                }
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> AddFloor(string address)
        {
            try
            {
                var room = db.Rooms.FirstOrDefault(r => r.Address == address);
                if (room != null)
                {
                    room.Floors++;
                    db.SaveChanges();
                }
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFloor(string address)
        {
            try
            {
                var room = db.Rooms.FirstOrDefault(r => r.Address == address);
                if (room != null && room.Floors > 0)
                {
                    room.Floors--;
                    db.SaveChanges();
                }
                return Json(new { r = "ok" });
            }
            catch (Exception ex) { return Json(new { r = "error", m = ex.Message }); }
        }

        public async Task<IActionResult> Test()
        {
            string url = "ipfs://bafybeibwffjrngqzvmckaa36y5lc3aihzuepjkbsllktsedxc5wu3ctuvu/7454.svg";
            string file = url.Replace("/", "").Replace(":", "").Replace(".", "") + ".png";
            string result = $"{_Controller.GetLeftPart(Request)}/files/{file}";
            var ipfs = new IpfsEngine(new char[] { 'q' });
            string text = await ipfs.FileSystem.ReadAllTextAsync(url.Replace("ipfs://", ""));
            return Content(text);
        }
    }
}
