using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TONServer.Components
{
    public class Upload : ViewComponent
    {
        public IViewComponentResult Invoke(string action = "Index/AddFile", string caption = "Загрузить", string cl = "btn btn-green", string inputId = "files", string formId = "sendfiles", string frameId = "frame")
        {
            string r = "";
            r += $@"<a href=""javascript:;"" class=""{cl}"" onclick=""$('#{inputId}').trigger('click')"">{caption}</a>
<form action=""../../{action}"" method=""post"" enctype=""multipart/form-data"" id=""{formId}"" style=""display: none"" target=""{frameId}"">
    <input type=""file"" name=""files"" multiple id=""{inputId}"" onchange=""$('#{formId}').submit()"" /><br>
    <input type=""submit"" />
    <iframe width=""0"" height=""0"" border=""0"" name=""{frameId}"" id=""{frameId}""></iframe>
</form>";
            return new HtmlContentViewComponentResult(new HtmlString(r));
        }
    }
}
