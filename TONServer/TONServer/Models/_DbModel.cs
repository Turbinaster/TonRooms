using Libs;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TONServer
{
    public class Setting
    {
        public int Id { get; set; }
        public string Sitename { get; set; }
        public string RecaptchaKey { get; set; }
        public string RecaptchaSecret { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; }
        public string Pass { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public DateTime Date { get; set; }
        public string Avatar { get; set; }
        public string Lang { get; set; }
        public string ConfirmRemember { get; set; }
    }

    public class Room
    {
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public string Address { get; set; }
        public string Images { get; set; }
        public int Floors { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Link1 { get; set; }
        public string Link2 { get; set; }
    }

    public class Image
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Scale { get; set; }
        public int Wall { get; set; }
        public bool Selected { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ExternalUrl { get; set; }
        public int Floor { get; set; }
    }

    public class ImageWeb
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Scale { get; set; }
        public int Wall { get; set; }
        public bool Selected { get; set; }
        public string Url { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ExternalUrl { get; set; }
    }

    public class RoomWeb
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string Avatar { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Link1 { get; set; }
        public string Link2 { get; set; }
        public string Friends { get; set; }
        public string Incomings { get; set; }
        public string Outcomings { get; set; }
        [NotMapped]
        public List<int> FriendsList { get { return string.IsNullOrEmpty(Friends) ? new List<int>() : Friends.Split(',').Select(x => Helper.IntParse(x)).ToList(); } }
        [NotMapped]
        public List<int> IncomingsList { get { return string.IsNullOrEmpty(Incomings) ? new List<int>() : Incomings.Split(',').Select(x => Helper.IntParse(x)).ToList(); } }
        [NotMapped]
        public List<int> OutcomingsList { get { return string.IsNullOrEmpty(Outcomings) ? new List<int>() : Outcomings.Split(',').Select(x => Helper.IntParse(x)).ToList(); } }

        public void AddFriend(int id) { var list = FriendsList; list.Add(id); Friends = string.Join(",", list); }
        public void RemoveFriend(int id) { var list = FriendsList; list.Remove(id); Friends = string.Join(",", list); }
        public void AddIncoming(int id) { var list = IncomingsList; list.Add(id); Incomings = string.Join(",", list); }
        public void RemoveIncoming(int id) { var list = IncomingsList; list.Remove(id); Incomings = string.Join(",", list); }
        public void AddOutcoming(int id) { var list = OutcomingsList; list.Add(id); Outcomings = string.Join(",", list); }
        public void RemoveOutcoming(int id) { var list = OutcomingsList; list.Remove(id); Outcomings = string.Join(",", list); }
    }
}
