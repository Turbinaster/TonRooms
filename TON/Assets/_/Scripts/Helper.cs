using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public static class Helper
{
    static System.Random random = new System.Random();

    public static JObject Json(string s)
    {
        try
        {
            var d = (JObject)JsonConvert.DeserializeObject(s);
            return d;
        }
        catch { }
        return null;
    }

    public static string RandomString(int length = 20)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    public static async Task<JObject> Post(string url, string data)
    {
        /*var proxy = new WebProxy
        {
            Address = new System.Uri("http://46.8.157.159:1050"),
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(userName: "n2QJbp", password: "ky3Jrz0zIH")
        };
        var httpClientHandler = new HttpClientHandler { Proxy = proxy };
        var client = new HttpClient(handler: httpClientHandler, disposeHandler: true);*/
        var client = new HttpClient();
        var response = await client.PostAsync(url, new StringContent(data, Encoding.UTF8, "application/x-www-form-urlencoded"));
        string s = await response.Content.ReadAsStringAsync();
        return Json(s);
    }

    public static void ClearChildren(Transform t)
    {
        foreach (Transform child in t) GameObject.Destroy(child.gameObject);
    }

    public static Vector2 ImageRatio(Image image, float height)
    {
        var t = image.GetComponent<Image>().sprite.texture;
        float ratio = (float)t.width / (float)t.height;
        return new Vector2(height * ratio, height);
    }
}

public static class RectTransformExtensions
{
    public static void SetLeft(this RectTransform rt, float left)
    {
        rt.offsetMin = new Vector2(left, rt.offsetMin.y);
    }

    public static void SetRight(this RectTransform rt, float right)
    {
        rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
    }

    public static void SetTop(this RectTransform rt, float top)
    {
        rt.offsetMax = new Vector2(rt.offsetMax.x, -top);
    }

    public static void SetBottom(this RectTransform rt, float bottom)
    {
        rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
    }
}
