using ConvertApiDotNet;
using Ipfs.Engine;
using Libs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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

        [HttpPost]
        public async Task<IActionResult> GetTonConnectPayload(string session)
        {
            try
            {
                if (string.IsNullOrEmpty(session)) throw new Exception("Session required");
                CleanupExpiredPayloads();

                var origin = $"{Request.Scheme}://{Request.Host}";
                TonApiClient.EnsureClientId(origin);
                var manifestUrl = $"{origin}/tonconnect-manifest.json";
                var domain = string.IsNullOrWhiteSpace(Request.Host.Host) ? Request.Host.Value : Request.Host.Host;
                var payloadResult = await TonConnectService.GetPayloadAsync(manifestUrl);

                var info = new TonConnectPayloadInfo
                {
                    Payload = payloadResult.Payload,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = payloadResult.ExpiresAt,
                    Domain = domain
                };

                _Singleton.Payloads.AddOrUpdate(session, info, (key, existing) => info);

                return Json(new
                {
                    r = "ok",
                    payload = payloadResult.Payload,
                    expiresAt = payloadResult.ExpiresAt?.ToUnixTimeMilliseconds(),
                    links = payloadResult.Links == null ? null : new {
                        tonkeeperUniversal = payloadResult.Links.TonkeeperUniversal,
                        tonkeeperDeeplink = payloadResult.Links.TonkeeperDeeplink,
                        tonDeeplink = payloadResult.Links.TonDeeplink
                    }
                });
            }
            catch (Exception ex)
            {
                Helper.Log(ex);
                return Json(new { r = "error", m = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetAddress(string session, int x, int y, int z)
        {
            try
            {
                if (_Singleton.Sessions.TryRemove(session, out var address) && !string.IsNullOrEmpty(address))
                {
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
                TonApiClient.EnsureClientId($"{Request.Scheme}://{Request.Host}");
                var nftRequest = new HttpRequestMessage(HttpMethod.Get, $"/v2/accounts/{address}/nfts?limit=1000");
                var j = await TonApiClient.SendAsync(nftRequest);
                var items = j["nft_items"] ?? j["nfts"] ?? j["items"];
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        try
                        {
                            var metadata = item["metadata"] as JObject;
                            var previews = item["previews"] as JArray;
                            string url = metadata?["image"]?.ToString()
                                ?? metadata?["image_data"]?.ToString()
                                ?? item["content"]?["image"]?.ToString()
                                ?? previews?.First?["url"]?.ToString();
                            if (string.IsNullOrEmpty(url)) continue;

                            string name = metadata?["name"]?.ToString() ?? item["name"]?.ToString();
                            string description = metadata?["description"]?.ToString() ?? item["description"]?.ToString();
                            string external_url = metadata?["external_url"]?.ToString() ?? item["externalUrl"]?.ToString();

                            var image = new Image { RoomId = room.Id, Selected = true, Url = url.Replace("%20", ""), Name = name, Description = description, ExternalUrl = external_url };
                            if (url.StartsWith("ipfs:") || url.EndsWith(".mp4"))
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
                        catch (Exception ex) { Helper.Log(ex); }
                    }
                }
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

        private void CleanupExpiredPayloads()
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var pair in _Singleton.Payloads)
            {
                if (pair.Value == null || pair.Value.IsExpired(now))
                {
                    _Singleton.Payloads.TryRemove(pair.Key, out _);
                }
            }
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
