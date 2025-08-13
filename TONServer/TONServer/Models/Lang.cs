using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace TONServer
{
    public class Langs
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ru { get; set; }
        public string en { get; set; }
    }

    public class LangDesc
    {
        public int Id { get; set; }
        public string Lang { get; set; }
        public string Desc { get; set; }
    }

    public class Lang : Dictionary<string, string>
    {
        public new string this[string key]
        {
            get { return GetValue(key); }
            set { }
        }

        protected virtual string GetValue(string key)
        {
            foreach (var v in this)
            {
                if (v.Key.ToLower() == key.ToLower()) return v.Value;
            }
            try
            {
                var db = Rep.Db;
                var rec = db.Langs.FirstOrDefault(x => x.Name == key);
                if (rec == null) db.Execute($"insert into Langs (Name, ru, en) values ('{key}', '{key}', '{key}')").Wait();
                this.Add(key, key);
            }
            catch { }
            return key;
        }
    }

    public class LangRep
    {
        public static List<LangDesc> LangDescs()
        {
            var db = Rep.Db;
            var r = db.LangDescs.ToList();
            if (r.Count == 0)
            {
                db.LangDescs.Add(new LangDesc { Lang = "ru", Desc = "Русский" });
                db.LangDescs.Add(new LangDesc { Lang = "en", Desc = "English" });
                db.SaveChanges();
                r = db.LangDescs.ToList();
            }
            return r;
        }

        public static Dictionary<string, Lang> Langs()
        {
            var langs = new Dictionary<string, Lang>();
            var table = Rep.Db.Select("select * from Langs");
            foreach (var dc in table.Columns)
            {
                string name = dc.ToString();
                if (name != "Name") langs.Add(name, new Lang());
            }
            foreach (DataRow dr in table.Rows)
            {
                string name = dr["Name"].ToString();
                foreach (var key in langs.Keys)
                {
                    string value = dr[key].ToString();
                    langs[key].Add(name, value);
                }
            }
            return langs;
        }

        public static Lang Lang(User user, HttpRequest Request, out string langS)
        {
            var lang = new Lang();
            langS = "ru";
            if (user != null && !string.IsNullOrEmpty(user.Lang)) langS = user.Lang;
            else if (Request.Cookies.ContainsKey("lang") && Request.Cookies["lang"] != null) langS = Request.Cookies["lang"];
            else if (Request.Headers.ContainsKey("Accept-Language")) langS = Request.Headers["Accept-Language"].ToString().Substring(0, 2);
            var db = Rep.Db;
            var table = db.Select("select * from Langs");
            if (!table.Columns.Contains(langS)) langS = "ru";
            foreach (DataRow dr in table.Rows)
            {
                string name = dr["Name"].ToString();
                string value = dr.Table.Columns.Contains(langS) ? dr[langS].ToString() : dr["ru"].ToString();
                if (string.IsNullOrEmpty(value)) value = dr["ru"].ToString();
                if (lang.ContainsKey(name)) db.Execute($"delete from Langs where [Name] = '{name}'").Wait();
                else lang.Add(name, value);
            }
            return lang;
        }
    }
}
