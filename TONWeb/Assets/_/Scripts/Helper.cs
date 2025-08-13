using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public static class Helper
{
    static System.Random random = new System.Random();
    public static bool mobile, moveLock;
    public static Init init;

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

    public static void RotateToTarget(Transform origin, Transform target, float speed = 100)
    {
        // Determine which direction to rotate towards
        Vector3 targetDirection = target.position - origin.position;

        // The step size is equal to speed times frame time.
        float singleStep = 100 * Time.deltaTime;

        // Rotate the forward vector towards the target direction by one step
        Vector3 newDirection = Vector3.RotateTowards(origin.forward, targetDirection, singleStep, 0.0f);

        // Calculate a rotation a step closer to the target and applies rotation to this object
        origin.rotation = Quaternion.LookRotation(newDirection);
    }

    public static IEnumerator LoadFromServer(string url)
    {
        var request = new UnityWebRequest(url);
        yield return request.SendWebRequest();
        if (!string.IsNullOrEmpty(request.error)) Debug.Log(request.error);
        request.Dispose();
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
