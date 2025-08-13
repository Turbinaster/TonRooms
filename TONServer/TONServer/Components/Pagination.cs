using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TONServer.Components
{
    public class Pagination : ViewComponent
    {
        public IViewComponentResult Invoke()
        {
            string r = "";
            int pages = ViewBag.pages;
            int page = ViewBag.page;
            string url = Request.GetDisplayUrl();
            int pad = 5;
            url = Regex.Replace(url, @"[&\?]page=\d+", "");
            string s = url.Contains("?") ? "&" : "?";
            if (pages > 1)
            {
                r += @"<div class=""pagination"">";
                for (int i = 1; i <= pages; i++)
                {
                    if (i > 1 && i < page - pad)
                    {
                        i = page - pad;
                        r += @"<span>...</span>";
                        continue;
                    }
                    if (i < pages && i > page + pad)
                    {
                        i = pages - 1;
                        r += @"<span>...</span>";
                        continue;
                    }
                    r += $@"<a href=""{url}{s}page={i}"" class=""{(i == page ? "active" : "")}""><span>{i}</span></a>";
                }
                r += @"</div>";
            }
            return new HtmlContentViewComponentResult(new HtmlString(r));
        }
    }
}
